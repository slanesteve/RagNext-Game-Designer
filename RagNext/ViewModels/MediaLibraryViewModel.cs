using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using RagNext.Models;
using RagNext.Services;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.ViewModels
{
    public sealed class MediaLibraryViewModel : BindableObject
    {
        private readonly IMediaLibrary _library;
        private readonly IMediaTreeStore _store;
        private readonly Dictionary<Guid, bool> _folderExpandedStates = new();

        public ObservableCollection<Node> Roots { get; private set; } = new();
        public Command AddFolderCommand { get; }
        public Command ImportFilesCommand { get; }
        public Command RenameCommand { get; }
        public Command DeleteCommand { get; }
        public Command SelectNodeCommand { get; }
        public Command RemoveAssetCommand { get; }

        private MediaTreeDocument _doc = new();
        private Game? _game;
        private Node? _selected;

        public Node? Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                if (_selected != null) _selected.IsSelected = false;
                _selected = value;
                if (_selected != null) _selected.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedFile));
                OnPropertyChanged(nameof(IsSelectedImage));
                OnPropertyChanged(nameof(IsSelectedAudio));
                OnPropertyChanged(nameof(IsSelectedVideo));
                OnPropertyChanged(nameof(IsSelectedFallback));
                OnPropertyChanged(nameof(SelectedFilePath));
            }
        }

        public bool HasSelectedFile => Selected != null && !Selected.IsFolder && Selected.Asset != null;
        public bool IsSelectedImage => HasSelectedFile && Selected!.Asset!.Kind == MediaKind.Image;
        public bool IsSelectedAudio => HasSelectedFile && Selected!.Asset!.Kind == MediaKind.Audio;
        public bool IsSelectedVideo => HasSelectedFile && Selected!.Asset!.Kind == MediaKind.Video;
        public bool IsSelectedFallback => HasSelectedFile && !IsSelectedImage && !IsSelectedAudio && !IsSelectedVideo;

        public string SelectedFilePath
        {
            get
            {
                if (HasSelectedFile && _game != null && Selected!.Asset != null)
                {
                    return _library.GetLocalPath(_game, Selected.Asset);
                }
                return string.Empty;
            }
        }

        public sealed class Node : BindableObject
        {
            private bool _isSelected;
            private bool _isExpanded;

            public MediaLibraryViewModel? ViewModel { get; init; }
            public bool IsFolder { get; init; }
            public string Name { get; set; } = "";
            public ObservableCollection<Node> Children { get; } = new();
            public MediaFolder? Folder { get; init; }
            public MediaAsset? Asset { get; init; }
            public MediaFolder? ParentFolder { get; init; }

            // Hierarchy level (root = 0). Used for UI indentation.
            public int Level { get; init; }
            public Thickness Indent => new(Level * 16, 0, 0, 0);

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (_isExpanded == value) return;
                    _isExpanded = value;
                    OnPropertyChanged();
                    if (Folder != null && ViewModel != null)
                    {
                        ViewModel._folderExpandedStates[Folder.Id] = value;
                    }
                }
            }

            public string Icon => IsFolder ? "📁" :
                Asset?.Kind switch
                {
                    MediaKind.Image => "🖼️",
                    MediaKind.Audio => "🔊",
                    MediaKind.Video => "🎞️",
                    _ => "📦"
                };
        }

        public MediaLibraryViewModel(IMediaLibrary library, IMediaTreeStore store)
        {
            _library = library;
            _store = store;

            AddFolderCommand = new Command(async () => await AddFolderAsync());
            ImportFilesCommand = new Command(async () => await ImportFilesAsync());
            RenameCommand = new Command(async () => await RenameAsync());
            DeleteCommand = new Command(async () => await DeleteAsync());
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            RemoveAssetCommand = new Command<object?>(async o => { if (o is Node n) await RemoveAssetAsync(n); });

            App.GameChanged += OnGameChanged;
            _ = TryLoadAsync(App.CurrentGame);
        }

        public void Refresh()
        {
            MainThread.BeginInvokeOnMainThread(async () => await TryLoadAsync(_game));
        }

        private void OnGameChanged(Game? g) => MainThread.BeginInvokeOnMainThread(async () => await TryLoadAsync(g));

        private async Task TryLoadAsync(Game? g)
        {
            if (g is null) return;
            _game = g;
            _doc = await _store.LoadAsync(g);
            RebuildNodes();
        }

        private void RebuildNodes()
        {
            var prevSelectedFolder = Selected?.Folder ?? Selected?.ParentFolder;

            if (_game is null) return;

            ObservableCollection<Node> BuildRoots()
            {
                var newRoots = new ObservableCollection<Node>();

                Node BuildFolder(MediaFolder f, MediaFolder? parent, int level)
                {
                    bool isExpanded = _folderExpandedStates.TryGetValue(f.Id, out var state) ? state : (level == 0);
                    _folderExpandedStates[f.Id] = isExpanded;

                    var node = new Node
                    {
                        ViewModel = this,
                        IsFolder = true,
                        Name = f.Name,
                        Folder = f,
                        ParentFolder = parent,
                        Level = level,
                        IsExpanded = isExpanded
                    };
                    foreach (var child in f.Children.Select(c => BuildFolder(c, f, level + 1)))
                        node.Children.Add(child);
                    foreach (var id in f.AssetIds)
                    {
                        var asset = _game.MediaAssets.FirstOrDefault(a => a.Id == id);
                        if (asset is null) continue;
                        node.Children.Add(new Node
                        {
                            ViewModel = this,
                            IsFolder = false,
                            Name = string.IsNullOrWhiteSpace(asset.OriginalFileName) ? asset.RelativePath : asset.OriginalFileName,
                            Asset = asset,
                            ParentFolder = f,
                            Level = level + 1
                        });
                    }
                    return node;
                }

                if (_doc.Roots.Count == 0)
                    _doc.Roots.Add(new MediaFolder { Name = "Assets" });

                foreach (var f in _doc.Roots)
                    newRoots.Add(BuildFolder(f, null, 0));

                return newRoots;
            }

            var newRootsBuilt = BuildRoots();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Roots = newRootsBuilt;
                OnPropertyChanged(nameof(Roots));

                if (prevSelectedFolder != null)
                {
                    Node? FindMatch(Node n)
                    {
                        if (n.Folder == prevSelectedFolder || n.ParentFolder == prevSelectedFolder) return n;
                        foreach (var c in n.Children)
                        {
                            var m = FindMatch(c);
                            if (m != null) return m;
                        }
                        return null;
                    }
                    Selected = Roots.Select(FindMatch).FirstOrDefault(n => n != null);
                }
            });
        }

        private async Task AddFolderAsync()
        {
            if (_game is null) return;
            var parentFolder = Selected?.Folder ?? Selected?.ParentFolder ?? _doc.Roots.FirstOrDefault();
            if (parentFolder is null) return;

            var name = await Application.Current!.MainPage!.DisplayPromptAsync("New Folder", "Enter folder name", "Create", "Cancel", "Folder");
            if (string.IsNullOrWhiteSpace(name)) return;

            parentFolder.Children.Add(new MediaFolder { Name = name.Trim() });
            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private async Task<string> ComputeFileHashAsync(string filePath)
        {
            using var fs = File.OpenRead(filePath);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = await sha.ComputeHashAsync(fs);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private bool IsAssetInTree(Guid assetId, out string folderName)
        {
            folderName = "";
            bool SearchFolder(MediaFolder folder, out string foundName)
            {
                if (folder.AssetIds.Contains(assetId))
                {
                    foundName = folder.Name;
                    return true;
                }
                foreach (var child in folder.Children)
                {
                    if (SearchFolder(child, out foundName))
                        return true;
                }
                foundName = "";
                return false;
            }

            foreach (var root in _doc.Roots)
            {
                if (SearchFolder(root, out var name))
                {
                    folderName = name;
                    return true;
                }
            }
            return false;
        }

        private async Task ImportFilesAsync()
        {
            if (_game is null) return;

            var targetFolder =
                Selected is { IsFolder: true, Folder: not null } s1 ? s1.Folder :
                Selected is { ParentFolder: not null } s2 ? s2.ParentFolder :
                _doc.Roots.FirstOrDefault();

            if (targetFolder is null) return;

            var result = await FilePicker.PickMultipleAsync(new PickOptions { PickerTitle = "Select media files" });
            if (result is null) return;

            var duplicates = new List<string>();
            foreach (var file in result)
            {
                string hash;
                try
                {
                    hash = await ComputeFileHashAsync(file.FullPath);
                }
                catch
                {
                    continue;
                }

                var existingAsset = _game.MediaAssets.FirstOrDefault(a => a.Sha256 == hash);
                if (existingAsset != null && IsAssetInTree(existingAsset.Id, out var folderName))
                {
                    duplicates.Add($"{file.FileName} (already in '{folderName}')");
                    continue;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var asset = await _library.AddAsync(_game, file.FullPath, cts.Token);
                if (!targetFolder.AssetIds.Contains(asset.Id))
                    targetFolder.AssetIds.Add(asset.Id);
            }

            if (duplicates.Any())
            {
                await Application.Current!.MainPage!.DisplayAlert("Duplicate Files", 
                    "The following files were already in the library and were not imported:\n\n" + string.Join("\n", duplicates), "OK");
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private async Task RenameAsync()
        {
            if (_game is null || Selected is null) return;
            if (!Selected.IsFolder) return;
            var folder = Selected.Folder!;
            var name = await Application.Current!.MainPage!.DisplayPromptAsync("Rename Folder", "Enter new name", "OK", "Cancel", initialValue: folder.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            folder.Name = name.Trim();
            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private async Task DeleteAsync()
        {
            if (_game is null || Selected is null) return;
            if (!Selected.IsFolder) return;
            var confirm = await Application.Current!.MainPage!.DisplayAlert("Delete", $"Delete folder '{Selected.Name}' (assets stay in game)?", "Delete", "Cancel");
            if (!confirm) return;

            bool RemoveFolder(ObservableCollection<MediaFolder> list, MediaFolder f)
            {
                var idx = list.IndexOf(f);
                if (idx >= 0) { list.RemoveAt(idx); return true; }
                foreach (var x in list)
                    if (RemoveFolder(x.Children, f)) return true;
                return false;
            }

            if (RemoveFolder(_doc.Roots, Selected.Folder!))
            {
                await _store.SaveAsync(_game, _doc);
                RebuildNodes();
            }
        }

        public async Task<bool> MoveNodeAsync(Node sourceNode, MediaFolder targetFolder)
        {
            if (_game is null) return false;
            if (sourceNode.Folder == targetFolder) return false;

            // 1. Prevent circular moves: targetFolder cannot be sourceNode.Folder or one of its descendants.
            if (sourceNode.IsFolder && sourceNode.Folder != null)
            {
                if (IsDescendantOf(targetFolder, sourceNode.Folder))
                {
                    return false;
                }
            }

            // 2. Remove from original parent
            if (sourceNode.IsFolder && sourceNode.Folder != null)
            {
                bool removed = false;
                if (sourceNode.ParentFolder != null)
                {
                    removed = sourceNode.ParentFolder.Children.Remove(sourceNode.Folder);
                }
                if (!removed)
                {
                    bool RemoveFolder(ObservableCollection<MediaFolder> list, MediaFolder f)
                    {
                        var idx = list.IndexOf(f);
                        if (idx >= 0) { list.RemoveAt(idx); return true; }
                        foreach (var x in list)
                            if (RemoveFolder(x.Children, f)) return true;
                        return false;
                    }
                    RemoveFolder(_doc.Roots, sourceNode.Folder);
                }

                // Add to target folder
                targetFolder.Children.Add(sourceNode.Folder);
            }
            else if (!sourceNode.IsFolder && sourceNode.Asset != null)
            {
                bool removed = false;
                if (sourceNode.ParentFolder != null)
                {
                    removed = sourceNode.ParentFolder.AssetIds.Remove(sourceNode.Asset.Id);
                }
                if (!removed)
                {
                    bool RemoveAsset(ObservableCollection<MediaFolder> list, Guid assetId)
                    {
                        foreach (var folder in list)
                        {
                            if (folder.AssetIds.Contains(assetId))
                            {
                                folder.AssetIds.Remove(assetId);
                                return true;
                            }
                            if (RemoveAsset(folder.Children, assetId)) return true;
                        }
                        return false;
                    }
                    RemoveAsset(_doc.Roots, sourceNode.Asset.Id);
                }

                // Add to target folder
                if (!targetFolder.AssetIds.Contains(sourceNode.Asset.Id))
                {
                    targetFolder.AssetIds.Add(sourceNode.Asset.Id);
                }
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
            return true;
        }

        private bool IsDescendantOf(MediaFolder potentialDescendant, MediaFolder ancestor)
        {
            if (potentialDescendant == ancestor) return true;
            return ContainsFolderRecursive(ancestor, potentialDescendant);
        }

        private bool ContainsFolderRecursive(MediaFolder parent, MediaFolder target)
        {
            if (parent.Children.Contains(target)) return true;
            foreach (var child in parent.Children)
            {
                if (ContainsFolderRecursive(child, target)) return true;
            }
            return false;
        }

        public async Task ImportExternalFilesAsync(IEnumerable<string> filePaths, MediaFolder? targetFolder = null)
        {
            if (_game is null) return;

            if (targetFolder is null)
            {
                targetFolder =
                    Selected is { IsFolder: true, Folder: not null } s1 ? s1.Folder :
                    Selected is { ParentFolder: not null } s2 ? s2.ParentFolder :
                    _doc.Roots.FirstOrDefault();
            }

            if (targetFolder is null) return;

            var duplicates = new List<string>();
            foreach (var path in filePaths)
            {
                if (!System.IO.File.Exists(path)) continue;
                try
                {
                    string hash = await ComputeFileHashAsync(path);
                    var existingAsset = _game.MediaAssets.FirstOrDefault(a => a.Sha256 == hash);
                    if (existingAsset != null && IsAssetInTree(existingAsset.Id, out var folderName))
                    {
                        duplicates.Add($"{Path.GetFileName(path)} (already in '{folderName}')");
                        continue;
                    }

                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    var asset = await _library.AddAsync(_game, path, cts.Token);
                    if (!targetFolder.AssetIds.Contains(asset.Id))
                    {
                        targetFolder.AssetIds.Add(asset.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to import external file {path}: {ex.Message}");
                }
            }

            if (duplicates.Any())
            {
                await Application.Current!.MainPage!.DisplayAlert("Duplicate Files", 
                    "The following files were already in the library and were not imported:\n\n" + string.Join("\n", duplicates), "OK");
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private List<(string EntityType, string EntityName, System.Action ClearAction)> GetPortraitReferences(string localPath)
        {
            var refs = new List<(string, string, System.Action)>();

            if (_game is null) return refs;

            // Player
            if (string.Equals(_game.Player.PortraitImagePath, localPath, StringComparison.OrdinalIgnoreCase))
            {
                refs.Add(("Player", _game.Player.Name, () => _game.Player.PortraitImagePath = null));
            }

            // Rooms
            foreach (var r in _game.Rooms)
            {
                if (string.Equals(r.PortraitImagePath, localPath, StringComparison.OrdinalIgnoreCase))
                {
                    refs.Add(("Room", r.Name, () => r.PortraitImagePath = null));
                }
            }

            // Objects
            foreach (var o in _game.Objects)
            {
                if (string.Equals(o.PortraitImagePath, localPath, StringComparison.OrdinalIgnoreCase))
                {
                    refs.Add(("Object", o.Name, () => o.PortraitImagePath = null));
                }
            }

            // Characters
            foreach (var c in _game.Characters)
            {
                if (string.Equals(c.PortraitImagePath, localPath, StringComparison.OrdinalIgnoreCase))
                {
                    refs.Add(("Character", c.Name, () => c.PortraitImagePath = null));
                }
            }

            return refs;
        }

        private async Task RemoveAssetAsync(Node node)
        {
            if (_game is null || node.Asset is null) return;

            var asset = node.Asset;
            var confirm = await Application.Current!.MainPage!.DisplayAlert("Delete Asset", 
                $"Are you sure you want to permanently delete '{node.Name}' from this game's library?\n\nThis will delete the copy stored inside the game's folder, but your original file on your computer will not be touched.", 
                "Delete", "Cancel");
            if (!confirm) return;

            var localPath = _library.GetLocalPath(_game, asset);
            var refs = GetPortraitReferences(localPath);

            if (refs.Any())
            {
                var refList = string.Join("\n", refs.Select(r => $"- {r.EntityType}: {r.EntityName}"));
                var proceed = await Application.Current!.MainPage!.DisplayAlert("Asset in Use", 
                    $"This asset is currently in use as a portrait for:\n{refList}\n\nDeleting it will clear these references. Do you want to proceed?", 
                    "Yes", "No");
                if (!proceed) return;

                // Clear references
                foreach (var r in refs)
                {
                    r.ClearAction();
                }
            }

            // Remove from the tree doc
            bool RemoveFromTree(ObservableCollection<MediaFolder> list, Guid assetId)
            {
                foreach (var folder in list)
                {
                    if (folder.AssetIds.Contains(assetId))
                    {
                        folder.AssetIds.Remove(assetId);
                    }
                    RemoveFromTree(folder.Children, assetId);
                }
                return true;
            }
            RemoveFromTree(_doc.Roots, asset.Id);

            // Remove from game.MediaAssets
            _game.MediaAssets.Remove(asset);

            // If selected, clear selection
            if (Selected == node)
            {
                Selected = null;
            }

            // Save tree and game
            await _store.SaveAsync(_game, _doc);
            try
            {
                await GameStorage.SaveAsync(_game, string.IsNullOrWhiteSpace(_game.Title) ? $"save_{DateTime.Now:yyyyMMddHHmmss}" : _game.Title);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save game state: {ex.Message}");
            }

            // Physical deletion of file
            try
            {
                if (System.IO.File.Exists(localPath))
                {
                    System.IO.File.Delete(localPath);
                }
            }
            catch (Exception ex)
            {
                await Application.Current!.MainPage!.DisplayAlert("Error", 
                    $"Physical file deletion failed (file may be in use): {ex.Message}", "OK");
            }

            RebuildNodes();
        }
    }
}