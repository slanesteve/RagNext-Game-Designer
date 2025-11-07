using System;
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

        public ObservableCollection<Node> Roots { get; private set; } = new();
        public Command AddFolderCommand { get; }
        public Command ImportFilesCommand { get; }
        public Command RenameCommand { get; }
        public Command DeleteCommand { get; }
        public Command SelectNodeCommand { get; }

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
            }
        }

        public sealed class Node : BindableObject
        {
            private bool _isSelected;
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

            App.GameChanged += OnGameChanged;
            _ = TryLoadAsync(App.CurrentGame);
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
                    var node = new Node { IsFolder = true, Name = f.Name, Folder = f, ParentFolder = parent, Level = level };
                    foreach (var child in f.Children.Select(c => BuildFolder(c, f, level + 1)))
                        node.Children.Add(child);
                    foreach (var id in f.AssetIds)
                    {
                        var asset = _game.MediaAssets.FirstOrDefault(a => a.Id == id);
                        if (asset is null) continue;
                        node.Children.Add(new Node
                        {
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

            foreach (var file in result)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var asset = await _library.AddAsync(_game, file.FullPath, cts.Token);
                if (!targetFolder.AssetIds.Contains(asset.Id))
                    targetFolder.AssetIds.Add(asset.Id);
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
    }
}