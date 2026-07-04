using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RagNext.Models;
using RagNext.Designer.Avalonia.Services;
using RagsCore.Models;
using RagsCore.Services;
using Avalonia.Threading;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public sealed class MediaLibraryViewModel : ViewModelBase
    {
        private readonly IMediaLibrary _library;
        private readonly IMediaTreeStore _store;
        private readonly Dictionary<Guid, bool> _folderExpandedStates = new();

        public ObservableCollection<Node> Roots { get; private set; } = new();
        public ICommand AddFolderCommand { get; }
        public ICommand ImportFilesCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand SelectNodeCommand { get; }
        public ICommand RemoveAssetCommand { get; }
        public ICommand PlaySelectedCommand { get; }
        public ICommand StopSelectedCommand { get; }

        private MediaTreeDocument _doc = new();
        private Game? _game;
        private Node? _selected;

        // Decoupled hooks for view integration (dialogs)
        public static Func<Task<string[]>>? PickMultipleFilesAsync { get; set; }
        public static Func<string, string, Task<string>>? PromptInputAsync { get; set; }
        public static Func<string, string, Task<bool>>? ConfirmDialogAsync { get; set; }
        public static Func<string, string, Task<string>>? ConfirmPublishDialogAsync { get; set; }
        public static Action<string>? ShowNotification { get; set; }

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

        public sealed class Node : ViewModelBase
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

            public int Level { get; init; }
            public global::Avalonia.Thickness Indent => new(Level * 16, 0, 0, 0);

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

        public MediaLibraryViewModel() : this(new MediaLibrary(new AvaloniaMediaPathProvider()), new MediaTreeStore())
        {
        }

        public MediaLibraryViewModel(IMediaLibrary library, IMediaTreeStore store)
        {
            _library = library;
            _store = store;

            AddFolderCommand = new Command(async () => await AddFolderAsync());
            ImportFilesCommand = new Command(async () => await ImportFilesAsync());
            RenameCommand = new Command(async () => await RenameAsync());
            DeleteCommand = new Command(async () => await DeleteAsync());
            SortCommand = new Command(async () => await SortAlphabeticallyAsync());
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            RemoveAssetCommand = new Command<object?>(async o => { if (o is Node n) await RemoveAssetAsync(n); });
            PlaySelectedCommand = new Command(PlaySelected);
            StopSelectedCommand = new Command(StopSelected);

            App.GameChanged += OnGameChanged;
            _ = TryLoadAsync(App.CurrentGame);
        }

        public void Refresh()
        {
            Dispatcher.UIThread.Post(async () => await TryLoadAsync(_game));
        }

        private void OnGameChanged(Game? g) => Dispatcher.UIThread.Post(async () => await TryLoadAsync(g));

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

            Dispatcher.UIThread.Post(() =>
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

            string name = "New Folder";
            if (PromptInputAsync != null)
            {
                name = await PromptInputAsync("New Folder", "Enter folder name");
            }
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

            if (PickMultipleFilesAsync == null) return;
            var result = await PickMultipleFilesAsync();
            if (result is null || result.Length == 0) return;

            var duplicates = new List<string>();
            foreach (var path in result)
            {
                string hash;
                try
                {
                    hash = await ComputeFileHashAsync(path);
                }
                catch
                {
                    continue;
                }

                var existingAsset = _game.MediaAssets.FirstOrDefault(a => a.Sha256 == hash);
                if (existingAsset != null && IsAssetInTree(existingAsset.Id, out var folderName))
                {
                    duplicates.Add($"{Path.GetFileName(path)} (already in '{folderName}')");
                    continue;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var asset = await _library.AddAsync(_game, path, cts.Token);
                if (!targetFolder.AssetIds.Contains(asset.Id))
                    targetFolder.AssetIds.Add(asset.Id);
            }

            if (duplicates.Any() && ShowNotification != null)
            {
                ShowNotification("Duplicate files skipped:\n" + string.Join("\n", duplicates));
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        public async Task ImportFilesFromPathsAsync(string[] paths, MediaFolder? targetFolder = null)
        {
            if (_game is null) return;

            targetFolder ??=
                Selected is { IsFolder: true, Folder: not null } s1 ? s1.Folder :
                Selected is { ParentFolder: not null } s2 ? s2.ParentFolder :
                _doc.Roots.FirstOrDefault();

            if (targetFolder is null) return;

            var duplicates = new List<string>();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                string hash;
                try
                {
                    hash = await ComputeFileHashAsync(path);
                }
                catch
                {
                    continue;
                }

                var existingAsset = _game.MediaAssets.FirstOrDefault(a => a.Sha256 == hash);
                if (existingAsset != null && IsAssetInTree(existingAsset.Id, out var folderName))
                {
                    duplicates.Add($"{Path.GetFileName(path)} (already in '{folderName}')");
                    continue;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var asset = await _library.AddAsync(_game, path, cts.Token);
                if (!targetFolder.AssetIds.Contains(asset.Id))
                    targetFolder.AssetIds.Add(asset.Id);
            }

            if (duplicates.Any() && ShowNotification != null)
            {
                ShowNotification("Duplicate files skipped:\n" + string.Join("\n", duplicates));
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private async Task RenameAsync()
        {
            if (_game is null || Selected is null) return;
            
            if (Selected.IsFolder)
            {
                var folder = Selected.Folder!;
                string name = folder.Name;
                if (PromptInputAsync != null)
                {
                    name = await PromptInputAsync("Rename Folder", "Enter new name");
                }
                if (string.IsNullOrWhiteSpace(name)) return;
                folder.Name = name.Trim();
                await _store.SaveAsync(_game, _doc);
                RebuildNodes();
            }
            else if (Selected.Asset != null)
            {
                var asset = Selected.Asset;
                string name = asset.OriginalFileName;
                if (PromptInputAsync != null)
                {
                    name = await PromptInputAsync("Rename Asset", "Enter new name");
                }
                if (string.IsNullOrWhiteSpace(name)) return;

                var oldExt = Path.GetExtension(asset.OriginalFileName);
                var newExt = Path.GetExtension(name);
                if (string.IsNullOrEmpty(newExt) && !string.IsNullOrEmpty(oldExt))
                {
                    name += oldExt;
                }

                asset.OriginalFileName = name.Trim();
                try
                {
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save game after rename: {ex.Message}");
                }
                await _store.SaveAsync(_game, _doc);
                RebuildNodes();
            }
        }

        private async Task DeleteAsync()
        {
            if (_game is null || Selected is null) return;
            if (Selected.IsFolder)
            {
                bool confirm = true;
                if (ConfirmDialogAsync != null)
                {
                    confirm = await ConfirmDialogAsync("Delete Folder", $"Delete folder '{Selected.Name}'? (assets will remain in game database)");
                }
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
            else if (Selected.Asset != null)
            {
                await RemoveAssetAsync(Selected);
            }
        }

        public Task MoveNodeAsync(Node sourceNode, MediaFolder targetFolder)
        {
            return MoveNodesAsync(new[] { sourceNode }, targetFolder);
        }

        public async Task MoveNodesAsync(IEnumerable<Node> sourceNodes, MediaFolder targetFolder)
        {
            if (_game is null || sourceNodes is null || targetFolder is null) return;

            bool changed = false;
            foreach (var sourceNode in sourceNodes)
            {
                if (sourceNode.IsFolder)
                {
                    var folderToMove = sourceNode.Folder;
                    if (folderToMove == null || folderToMove == targetFolder) continue;

                    // Cyclic check
                    bool IsDescendant(MediaFolder parent, MediaFolder child)
                    {
                        if (parent.Children.Contains(child)) return true;
                        foreach (var sub in parent.Children)
                            if (IsDescendant(sub, child)) return true;
                        return false;
                    }
                    if (IsDescendant(folderToMove, targetFolder)) continue;

                    // Remove from old parent
                    bool RemoveFolderFromTree(ObservableCollection<MediaFolder> list, MediaFolder f)
                    {
                        var idx = list.IndexOf(f);
                        if (idx >= 0) { list.RemoveAt(idx); return true; }
                        foreach (var x in list)
                            if (RemoveFolderFromTree(x.Children, f)) return true;
                        return false;
                    }

                    if (RemoveFolderFromTree(_doc.Roots, folderToMove))
                    {
                        targetFolder.Children.Add(folderToMove);
                        changed = true;
                    }
                }
                else if (sourceNode.Asset != null)
                {
                    var assetToMove = sourceNode.Asset;
                    var oldFolder = sourceNode.ParentFolder;
                    if (oldFolder == targetFolder) continue;

                    if (oldFolder != null)
                    {
                        oldFolder.AssetIds.Remove(assetToMove.Id);
                    }

                    if (!targetFolder.AssetIds.Contains(assetToMove.Id))
                    {
                        targetFolder.AssetIds.Add(assetToMove.Id);
                    }
                    changed = true;
                }
            }

            if (changed)
            {
                await _store.SaveAsync(_game, _doc);
                RebuildNodes();
            }
        }

        private List<(string EntityType, string EntityName, global::System.Action ClearAction)> GetPortraitReferences(string localPath)
        {
            var refs = new List<(string, string, global::System.Action)>();

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
            bool confirm = true;
            if (ConfirmDialogAsync != null)
            {
                confirm = await ConfirmDialogAsync("Delete Asset", $"Are you sure you want to permanently delete '{node.Name}' from this game's library?");
            }
            if (!confirm) return;

            var localPath = _library.GetLocalPath(_game, asset);
            var refs = GetPortraitReferences(localPath);

            if (refs.Any())
            {
                var refList = string.Join(", ", refs.Select(r => $"{r.EntityType}: {r.EntityName}"));
                bool proceed = true;
                if (ConfirmDialogAsync != null)
                {
                    proceed = await ConfirmDialogAsync("Asset in Use", $"This asset is currently in use by:\n{refList}\n\nDeleting it will clear these references. Proceed?");
                }
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
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save game state: {ex.Message}");
            }

            // Physical deletion of file
            try
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete file physically: {ex.Message}");
            }

            RebuildNodes();
        }

        private bool _isSortedAscending = false;

        private async Task SortAlphabeticallyAsync()
        {
            if (_game is null) return;

            _isSortedAscending = !_isSortedAscending;

            // Helper to sort assets within a folder by their resolved names
            List<Guid> SortAssets(MediaFolder folder)
            {
                var query = folder.AssetIds
                    .Select(id => (Id: id, Asset: _game.MediaAssets.FirstOrDefault(a => a.Id == id)))
                    .Where(x => x.Asset != null);

                if (_isSortedAscending)
                {
                    query = query.OrderBy(x => string.IsNullOrWhiteSpace(x.Asset!.OriginalFileName) ? x.Asset.RelativePath : x.Asset.OriginalFileName, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = query.OrderByDescending(x => string.IsNullOrWhiteSpace(x.Asset!.OriginalFileName) ? x.Asset.RelativePath : x.Asset.OriginalFileName, StringComparer.OrdinalIgnoreCase);
                }

                return query.Select(x => x.Id).ToList();
            }

            void SortFolderRecursively(MediaFolder folder)
            {
                // 1. Sort folders (Children) alphabetically by name
                var sortedSubfolders = _isSortedAscending
                    ? folder.Children.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    : folder.Children.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

                folder.Children.Clear();
                foreach (var sf in sortedSubfolders)
                {
                    folder.Children.Add(sf);
                }

                // 2. Sort assets alphabetically
                var sortedAssets = SortAssets(folder);
                folder.AssetIds.Clear();
                foreach (var assetId in sortedAssets)
                {
                    folder.AssetIds.Add(assetId);
                }

                // 3. Recurse down
                foreach (var sf in folder.Children)
                {
                    SortFolderRecursively(sf);
                }
            }

            // Sort root folders alphabetically
            var sortedRoots = _isSortedAscending
                ? _doc.Roots.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList()
                : _doc.Roots.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

            _doc.Roots.Clear();
            foreach (var root in sortedRoots)
            {
                _doc.Roots.Add(root);
            }

            // Recurse into all folders starting from roots
            foreach (var root in _doc.Roots)
            {
                SortFolderRecursively(root);
            }

            await _store.SaveAsync(_game, _doc);
            RebuildNodes();
        }

        private System.Diagnostics.Process? _activePlayerProcess;

        public void PlaySelected()
        {
            var path = SelectedFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                StopSelected();
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                _activePlayerProcess = System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play media: {ex.Message}");
            }
        }

        public void StopSelected()
        {
            try
            {
                if (_activePlayerProcess != null && !_activePlayerProcess.HasExited)
                {
                    _activePlayerProcess.Kill();
                }
            }
            catch
            {
                // Ignore
            }
            _activePlayerProcess = null;
        }
    }
}
