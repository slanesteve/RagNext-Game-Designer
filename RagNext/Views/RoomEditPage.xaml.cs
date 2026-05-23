using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagsCore.Services;
using RagNext.Services;
using CommunityToolkit.Maui.Views;
using RagNext.Views.Popups;
using CommunityToolkit.Maui.Extensions;

namespace RagNext.Views
{
    [QueryProperty(nameof(RoomId), "roomId")]
    public partial class RoomEditPage : ContentPage
    {
        public string? RoomId { set { _ = SetRoomAsync(value); } }

        private CollectionView? AttributesListView => this.FindByName<CollectionView>("AttributesList");

        private readonly IAIChatService? _ai;

        public RoomEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
        }

        // ── Opposite direction map ─────────────────────────────────────────────
        private static readonly IReadOnlyDictionary<string, string> _opposites =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["North"] = "South",
                ["South"] = "North",
                ["East"]  = "West",
                ["West"]  = "East",
                ["Up"]    = "Down",
                ["Down"]  = "Up",
                ["In"]    = "Out",
                ["Out"]   = "In",
            };

        // One entry per direction: (picker, oneWayCheckbox, directionKey)
        private record ExitControl(Picker Picker, CheckBox OneWay, string Direction);
        private List<ExitControl>? _exitControls;
        private bool _suppressExitEvents;

        private async System.Threading.Tasks.Task SetRoomAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var room = game?.Rooms?.FirstOrDefault(r => r.Id == id);
            if (room is null)
            {
                await DisplayAlert("Not found", "Room not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = room;

            UpdatePortraitImage(room.PortraitImagePath);
            room.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Room.PortraitImagePath))
                    UpdatePortraitImage(room.PortraitImagePath);
            };

            LoadExits(room);
        }

        // ── Exits editor ───────────────────────────────────────────────────────

        private void LoadExits(Room room)
        {
            var game = App.CurrentGame;
            if (game is null) return;

            // Build a flat list of all rooms (except this one is still included so names display)
            var allRooms = game.Rooms.ToList();

            // Build control list once
            _exitControls ??= new List<ExitControl>
            {
                new(NorthPicker, NorthOneWay, "North"),
                new(SouthPicker, SouthOneWay, "South"),
                new(EastPicker,  EastOneWay,  "East"),
                new(WestPicker,  WestOneWay,  "West"),
                new(UpPicker,    UpOneWay,    "Up"),
                new(DownPicker,  DownOneWay,  "Down"),
                new(InPicker,    InOneWay,    "In"),
                new(OutPicker,   OutOneWay,   "Out"),
            };

            _suppressExitEvents = true;
            try
            {
                foreach (var ec in _exitControls)
                {
                    // Detach old handlers before re-populating
                    ec.Picker.SelectedIndexChanged -= OnExitPickerChanged;
                    ec.OneWay.CheckedChanged       -= OnExitOneWayChanged;

                    // Populate picker items
                    ec.Picker.ItemsSource = allRooms;

                    // Set selected room from exits dictionary
                    if (room.Exits.TryGetValue(ec.Direction, out var destId))
                    {
                        var destRoom = allRooms.FirstOrDefault(r => r.Id == destId);
                        ec.Picker.SelectedItem = destRoom;

                        // One-Way = true when the reverse exit does NOT point back to this room
                        if (_opposites.TryGetValue(ec.Direction, out var opposite))
                        {
                            var hasBackLink = destRoom is not null
                                && destRoom.Exits.TryGetValue(opposite, out var backId)
                                && backId == room.Id;
                            ec.OneWay.IsChecked = !hasBackLink;
                        }
                        else
                        {
                            ec.OneWay.IsChecked = false;
                        }
                    }
                    else
                    {
                        ec.Picker.SelectedItem = null;
                        ec.OneWay.IsChecked    = false;
                    }

                    // Re-attach handlers
                    ec.Picker.SelectedIndexChanged += OnExitPickerChanged;
                    ec.OneWay.CheckedChanged       += OnExitOneWayChanged;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnExitPickerChanged(object? sender, EventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not Picker picker) return;
            if (BindingContext is not Room room) return;

            var ec = _exitControls?.FirstOrDefault(x => x.Picker == picker);
            if (ec is null) return;

            var game = App.CurrentGame;
            if (game is null) return;

            var destRoom = picker.SelectedItem as Room;

            _suppressExitEvents = true;
            try
            {
                if (destRoom is null)
                {
                    // Clear the exit
                    room.Exits.Remove(ec.Direction);
                    ec.OneWay.IsChecked = false;

                    // Clear the back-link from any room that was pointing back at us via the opposite direction
                    if (_opposites.TryGetValue(ec.Direction, out var opp))
                    {
                        foreach (var r in game.Rooms)
                        {
                            if (r.Exits.TryGetValue(opp, out var backId) && backId == room.Id)
                                r.Exits.Remove(opp);
                        }
                    }
                }
                else
                {
                    // Set the forward exit
                    room.Exits[ec.Direction] = destRoom.Id;

                    // Bidirectional: if not one-way, set the reverse exit on the destination room
                    if (!ec.OneWay.IsChecked && _opposites.TryGetValue(ec.Direction, out var opposite))
                    {
                        destRoom.Exits[opposite] = room.Id;
                    }

                    // Compute one-way state: true if back link is missing
                    bool hasBack = _opposites.TryGetValue(ec.Direction, out var opp2)
                        && destRoom.Exits.TryGetValue(opp2, out var backId)
                        && backId == room.Id;
                    ec.OneWay.IsChecked = !hasBack;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnExitOneWayChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not CheckBox cb) return;
            if (BindingContext is not Room room) return;

            var ec = _exitControls?.FirstOrDefault(x => x.OneWay == cb);
            if (ec is null) return;

            var game = App.CurrentGame;
            if (game is null) return;

            var destRoom = ec.Picker.SelectedItem as Room;
            if (destRoom is null) return;
            if (!_opposites.TryGetValue(ec.Direction, out var opposite)) return;

            _suppressExitEvents = true;
            try
            {
                if (e.Value) // checked → one-way: remove the back-link
                {
                    destRoom.Exits.Remove(opposite);
                }
                else // unchecked → bidirectional: restore the back-link
                {
                    destRoom.Exits[opposite] = room.Id;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Save", "No game loaded to save.", "OK");
                return;
            }

            try
            {
                await GameStorage.SaveAsync(game, string.IsNullOrWhiteSpace(game.Title) ? $"save_{DateTime.Now:yyyyMMddHHmmss}" : game.Title);
                await DisplayAlert("Saved", "Game saved successfully.", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        }



        private sealed class DisposeAction : IDisposable
        {
            private readonly System.Action _a;
            public DisposeAction(System.Action a) => _a = a;
            public void Dispose() => _a();
        }

        private IDisposable StartSpinner(Button btn)
        {
            var original = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";
            var anim = new Animation(v => btn.Rotation = v, 0, 360);
            anim.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);
            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = 0;
                btn.Text = original;
                btn.IsEnabled = true;
            });
        }

        private async void OnAddAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Room room) return;

            var name = await DisplayPromptAsync("New Attribute", "Enter attribute name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var value = await DisplayPromptAsync("New Attribute", "Enter attribute value (optional):");
            room.Attributes.Add(new CustomAttribute { Name = name.Trim(), Value = string.IsNullOrWhiteSpace(value) ? null : value });
        }

        private async void OnRemoveAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Room room) return;

            var list = AttributesListView;
            var selected = list?.SelectedItem as CustomAttribute;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an attribute to remove.", "OK");
                return;
            }

            room.Attributes.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI", "AI service unavailable.", "OK");
                return;
            }
            if (sender is not Button btn) return;

            await AIAssistHelper.HandleAskAIAsync(this, btn, btn.CommandParameter, _ai);
        }

        private void OnPortraitDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void OnPortraitDrop(object sender, DropEventArgs e)
        {
            if (BindingContext is not Room room) return;

            string? localPath = null;

            // 1. Try parsing the custom MediaAssetId format from Text payload (cross-platform / WinUI safe)
            var text = await e.Data.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("MediaAssetId:"))
            {
                var guidStr = text.Substring("MediaAssetId:".Length);
                if (Guid.TryParse(guidStr, out var assetId) && App.CurrentGame is Game game)
                {
                    var asset = game.MediaAssets?.FirstOrDefault(a => a.Id == assetId);
                    if (asset is not null)
                    {
                        var lib = MauiProgram.Services.GetService(typeof(RagsCore.Services.IMediaLibrary)) as RagsCore.Services.IMediaLibrary;
                        if (lib is not null)
                        {
                            localPath = lib.GetLocalPath(game, asset);
                        }
                    }
                }
            }

            // 2. Fallback to DraggedItem properties
            if (string.IsNullOrWhiteSpace(localPath))
            {
                if (e.Data.Properties.TryGetValue("DraggedItem", out var item))
                {
                    var pathProp = item.GetType().GetProperty("Path")
                                 ?? item.GetType().GetProperty("FullPath")
                                 ?? item.GetType().GetProperty("FilePath");

                    if (pathProp?.GetValue(item) is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        localPath = path;
                    }
                    else
                    {
                        var assetProp = item.GetType().GetProperty("Asset");
                        if (App.CurrentGame is Game game && assetProp?.GetValue(item) is RagsCore.Models.MediaAsset asset)
                        {
                            var lib = MauiProgram.Services.GetService(typeof(RagsCore.Services.IMediaLibrary)) as RagsCore.Services.IMediaLibrary;
                            if (lib is not null)
                            {
                                localPath = lib.GetLocalPath(game, asset);
                            }
                        }
                    }
                }
            }

            // 3. Fallback to raw text if not already matching MediaAssetId
            if (string.IsNullOrWhiteSpace(localPath) && !string.IsNullOrWhiteSpace(text) && !text.StartsWith("MediaAssetId:"))
            {
                localPath = text;
            }

            if (!string.IsNullOrWhiteSpace(localPath))
            {
                room.PortraitImagePath = localPath;
                UpdatePortraitImage(localPath);
            }
        }

        private void OnClearPortraitClicked(object? sender, EventArgs e)
        {
            if (BindingContext is Room room)
            {
                room.PortraitImagePath = null;
                UpdatePortraitImage(null);
            }
        }

        private async void OnGeneratePortraitClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Room room)
            {
                await DisplayAlert("Portrait", "No room bound.", "OK");
                return;
            }

            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Portrait", "No active game loaded.", "OK");
                return;
            }

            var prompt = await DisplayPromptAsync("Generate Portrait", "Enter a prompt for the image:", "Generate", "Cancel", placeholder: "fantasy dungeon room or medieval castle hall");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            var sizeChoice = await MainThread.InvokeOnMainThreadAsync(async () =>
                await DisplayActionSheet("Image Size", "Cancel", null, "480 x 480", "720 x 720", "1024 x 1024"));
            if (string.IsNullOrWhiteSpace(sizeChoice) || sizeChoice == "Cancel") return;
            int? size = sizeChoice.StartsWith("480") ? 480 : sizeChoice.StartsWith("720") ? 720 : 1024;

            var imageService = MauiProgram.Services.GetService(typeof(IAIImageService)) as IAIImageService;
            if (imageService is null)
            {
                await DisplayAlert("Image AI", "Image AI service is not configured.", "OK");
                return;
            }

            using var spinner = StartSpinner(sender as Button ?? new Button { Text = "Generating..." });
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var path = await imageService.GenerateImageAsync(prompt.Trim(), size, cts.Token);
                if (string.IsNullOrWhiteSpace(path))
                {
                    await DisplayAlert("Image AI", "No image was generated.", "OK");
                    return;
                }

                var mediaLibrary = MauiProgram.Services.GetService(typeof(IMediaLibrary)) as IMediaLibrary;
                var treeStore = MauiProgram.Services.GetService(typeof(IMediaTreeStore)) as IMediaTreeStore;

                if (mediaLibrary is not null && treeStore is not null)
                {
                    // Copy to media library
                    var asset = await mediaLibrary.AddAsync(game, path, cts.Token);

                    // Add to "Rooms" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });

                    var rootFolder = doc.Roots.First();
                    var folderName = "Rooms";
                    var subfolder = rootFolder.Children.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    if (subfolder is null)
                    {
                        subfolder = new RagNext.Models.MediaFolder { Name = folderName };
                        rootFolder.Children.Add(subfolder);
                    }

                    if (!subfolder.AssetIds.Contains(asset.Id))
                    {
                        subfolder.AssetIds.Add(asset.Id);
                    }
                    await treeStore.SaveAsync(game, doc);

                    // Refresh Media Tree View
                    var mediaLibVm = MauiProgram.Services.GetService(typeof(RagNext.ViewModels.MediaLibraryViewModel)) as RagNext.ViewModels.MediaLibraryViewModel;
                    mediaLibVm?.Refresh();

                    // Resolve local path in game Assets folder and cleanup temp file
                    var localPath = mediaLibrary.GetLocalPath(game, asset);
                    try
                    {
                        if (System.IO.File.Exists(path) && !string.Equals(path, localPath, StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.File.Delete(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete temp portrait file: {ex.Message}");
                    }

                    room.PortraitImagePath = localPath;
                    UpdatePortraitImage(localPath);
                }
                else
                {
                    // Fallback to temp path if media library is unavailable
                    room.PortraitImagePath = path;
                    UpdatePortraitImage(path);
                }
            }
            catch (TaskCanceledException)
            {
                await DisplayAlert("Image AI", "Timed out generating image.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Image AI", ex.Message, "OK");
            }
        }

        private void UpdatePortraitImage(string? path)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    PortraitImage.IsVisible = false;
                    PortraitImage.Source = null;
                    PlaceholderLayout.IsVisible = true;
                    PortraitFileNameLabel.Text = "No image selected";
                    PortraitFileNameLabel.FontAttributes = FontAttributes.Italic;
                    ClearPortraitButton.IsVisible = false;
                }
                else
                {
                    try
                    {
                        PortraitImage.Source = ImageSource.FromFile(path);
                        PortraitImage.IsVisible = true;
                        PlaceholderLayout.IsVisible = false;
                        PortraitFileNameLabel.Text = System.IO.Path.GetFileName(path);
                        PortraitFileNameLabel.FontAttributes = FontAttributes.None;
                        ClearPortraitButton.IsVisible = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load portrait image: {ex.Message}");
                        PortraitImage.IsVisible = false;
                        PortraitImage.Source = null;
                        PlaceholderLayout.IsVisible = true;
                        PortraitFileNameLabel.Text = "Error loading image";
                        PortraitFileNameLabel.FontAttributes = FontAttributes.Italic;
                        ClearPortraitButton.IsVisible = false;
                    }
                }
            });
        }

        private async Task ImportPortraitFromFileAsync(string filePath)
        {
            if (BindingContext is not Room room) return;
            var game = App.CurrentGame;
            if (game is null) return;

            var mediaLibrary = MauiProgram.Services.GetService(typeof(IMediaLibrary)) as IMediaLibrary;
            var treeStore = MauiProgram.Services.GetService(typeof(IMediaTreeStore)) as IMediaTreeStore;

            if (mediaLibrary is not null && treeStore is not null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    // Copy to media library
                    var asset = await mediaLibrary.AddAsync(game, filePath, cts.Token);

                    // Add to "Rooms" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });

                    var rootFolder = doc.Roots.First();
                    var folderName = "Rooms";
                    var subfolder = rootFolder.Children.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    if (subfolder is null)
                    {
                        subfolder = new RagNext.Models.MediaFolder { Name = folderName };
                        rootFolder.Children.Add(subfolder);
                    }
                    if (!subfolder.AssetIds.Contains(asset.Id))
                    {
                        subfolder.AssetIds.Add(asset.Id);
                    }
                    await treeStore.SaveAsync(game, doc);

                    // Refresh Media Tree View
                    var mediaLibVm = MauiProgram.Services.GetService(typeof(RagNext.ViewModels.MediaLibraryViewModel)) as RagNext.ViewModels.MediaLibraryViewModel;
                    mediaLibVm?.Refresh();

                    // Resolve local path in game Assets folder
                    var localPath = mediaLibrary.GetLocalPath(game, asset);
                    room.PortraitImagePath = localPath;
                    UpdatePortraitImage(localPath);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Import Error", $"Failed to import image: {ex.Message}", "OK");
                }
            }
        }

        private void OnPortraitBorderLoaded(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is Border border)
            {
                if (border.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement platformView)
                {
                    platformView.AllowDrop = true;
                    platformView.DragOver += (s, args) =>
                    {
                        args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                        args.Handled = true;
                    };
                    platformView.Drop += async (s, args) =>
                    {
                        args.Handled = true;
                        var deferral = args.GetDeferral();
                        try
                        {
                            if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                            {
                                var items = await args.DataView.GetStorageItemsAsync();
                                var firstFile = items.FirstOrDefault() as Windows.Storage.StorageFile;
                                if (firstFile != null)
                                {
                                    var path = firstFile.Path;
                                    MainThread.BeginInvokeOnMainThread(async () =>
                                    {
                                        await ImportPortraitFromFileAsync(path);
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error dropping portrait: {ex.Message}");
                        }
                        finally
                        {
                            deferral.Complete();
                        }
                    };
                }
            }
#endif
        }
    }
}