#nullable enable
using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagsCore.Services;
using RagNext.Services;
using System.Globalization;
using Microsoft.Maui.Graphics.Converters;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using RagNext.Views.Popups;

namespace RagNext.Views
{
    public partial class PlayerEditPage : ContentPage
    {
        // Safely access named elements without relying on generated fields
        private CollectionView? InventoryListView => this.FindByName<CollectionView>("InventoryList");
        private CollectionView? AttributesListView => this.FindByName<CollectionView>("AttributesList");

        private readonly IAIChatService? _ai;

        private double _lastY;
        private string _lastUiEvent = "startup";
        private bool _suppressScroll;

        public PlayerEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
            AssignPlayerActions();
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
            App.GameChanged += (game) => OnGameLoaded(this, game); // ensure repopulate after load
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var player = App.CurrentGame?.Player;
            BindingContext = player;

            // Set image exclusively in code-behind to avoid binding race
            UpdatePortraitImage(player?.PortraitImagePath);

            await System.Threading.Tasks.Task.Delay(100);
            RagNext.Services.MenuHelper.PopulateMenuBar(this);

            if (DetailsScrollView.IsVisible)
            {
                DetailsTabBorder.BackgroundColor = RagNext.Services.ThemeService.GetPrimaryColor();
                ActionsTabBorder.BackgroundColor = Colors.Transparent;
            }
            else
            {
                ActionsTabBorder.BackgroundColor = RagNext.Services.ThemeService.GetPrimaryColor();
                DetailsTabBorder.BackgroundColor = Colors.Transparent;
            }

            if (player is not null)
            {
                player.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(Player.PortraitImagePath))
                        UpdatePortraitImage(player.PortraitImagePath);
                };
            }

            // Ensure the page BindingContext is the Player
            if (BindingContext is not Player && App.CurrentGame?.Player is Player p)
                BindingContext = p;

            // Track ActionTreeView selection to correlate with jumps
            if (PlayerActionsView?.BindingContext is RagNext.ViewModels.ActionLibraryViewModel vm)
                HookActionTree(vm);

            PlayerActionsView.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PlayerActionsView.BindingContext) &&
                    PlayerActionsView.BindingContext is RagNext.ViewModels.ActionLibraryViewModel vm2)
                    HookActionTree(vm2);
            };
        }

        private void HookActionTree(RagNext.ViewModels.ActionLibraryViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.Selected))
                    _lastUiEvent = "ActionTreeView.Selected changed";
                // if (e.PropertyName == nameof(vm.Editor))
                    // _lastUiEvent = "Editor swapped (layout change)";
            };
        }

        private async void OnMainScrollScrolled(object? sender, ScrolledEventArgs e)
        {
            if (!_suppressScroll && e.ScrollY == 42 && _lastY > 42)
            {
                try
                {
                    _suppressScroll = true; // prevent recursion
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await DetailsScrollView.ScrollToAsync(0, _lastY, animated: false));
                }
                finally
                {
                    _suppressScroll = false;
                }
                return;
            }

            if (Math.Abs(e.ScrollY - _lastY) > 0.5)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Scroll] Y: {e.ScrollY:0.0} Δ:{e.ScrollY - _lastY:0.0} cause:{_lastUiEvent} @ {DateTime.Now:HH:mm:ss.fff}");
                _lastY = e.ScrollY;
            }
        }

        private void OnGameLoaded(object? sender, Game e)
        {
            // called when a new game is loaded
            MainThread.BeginInvokeOnMainThread(AssignPlayerActions);
        }

        private void AssignPlayerActions()
        {
            var game = App.CurrentGame;
            var player = game?.Player;
            var actions = player?.Actions;

            PlayerActionsView.Player = player;
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



        private string? GetInventoryOwnerName(GameObject item)
        {
            var game = App.CurrentGame;
            if (game is null) return null;

            // Check if player has it (exclude check if we are doing this for the player)
            // But since this helper is general, we can check all
            if (game.Player is not null && game.Player.Inventory.Any(o => o.Id == item.Id))
            {
                return "the Player";
            }

            // Check if any character has it
            if (game.Characters is not null)
            {
                foreach (var ch in game.Characters)
                {
                    if (ch.Inventory.Any(o => o.Id == item.Id))
                    {
                        return $"Character '{ch.Name}'";
                    }
                }
            }

            return null;
        }

        private async void OnAddInventoryClicked(object? sender, EventArgs e)
        {
            if (App.CurrentGame?.Player is not Player player || App.CurrentGame?.Objects is null) return;

            var ownedIds = player.Inventory.Select(o => o.Id).ToHashSet();
            var candidates = App.CurrentGame.Objects.Where(o => !ownedIds.Contains(o.Id)).ToList();
            if (candidates.Count == 0)
            {
                await DisplayAlert("Inventory", "No more objects to add.", "OK");
                return;
            }

            var choice = await DisplayActionSheet(
                "Add item to inventory",
                "Cancel", null,
                candidates.Select(o => o.Name).ToArray());

            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            var selected = candidates.FirstOrDefault(o => o.Name == choice);
            if (selected is not null)
            {
                var owner = GetInventoryOwnerName(selected);
                if (owner is not null)
                {
                    var confirm = await DisplayAlert(
                        "Warning",
                        $"This item is already assigned to {owner}.\n\nAre you sure you want to assign it here as well?",
                        "Yes", "No");
                    if (!confirm) return;
                }
                player.Inventory.Add(selected);
            }
        }

        private async void OnRemoveInventoryClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var list = InventoryListView;
            var selected = list?.SelectedItem as GameObject;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an inventory item to remove.", "OK");
                return;
            }

            player.Inventory.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private void OnRemoveIndividualInventoryClicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (BindingContext is not Player player) return;
            if (btn.BindingContext is not GameObject selected) return;

            player.Inventory.Remove(selected);
        }

        private async void OnAddAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var name = await DisplayPromptAsync("New Attribute", "Enter attribute name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var value = await DisplayPromptAsync("New Attribute", "Enter attribute value (optional):");
            player.Attributes.Add(new CustomAttribute { Name = name.Trim(), Value = string.IsNullOrWhiteSpace(value) ? null : value });
        }

        private async void OnRemoveAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var list = AttributesListView;
            var selected = list?.SelectedItem as CustomAttribute;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an attribute to remove.", "OK");
                return;
            }

            player.Attributes.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private sealed class DisposeAction : IDisposable
        {
            private readonly System.Action _action;
            public DisposeAction(System.Action action) => _action = action;
            public void Dispose() => _action();
        }

        private IDisposable StartSpinner(Button btn)
        {
            var originalText = btn.Text;
            var originalRotation = btn.Rotation;
            btn.IsEnabled = false;
            btn.Text = "⟳";

            var animation = new Animation(v => btn.Rotation = v, 0, 360);
            animation.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);

            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = originalRotation;
                btn.Text = originalText;
                btn.IsEnabled = true;
            });
        }

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI", "AI service is not available.", "OK");
                return;
            }

            if (sender is not Button btn)
                return;

            await AIAssistHelper.HandleAskAIAsync(this, btn, btn.CommandParameter, _ai);
        }

        private async void OnGeneratePortraitClicked(object? sender, EventArgs e)
        {
            var player = BindingContext as Player ?? App.CurrentGame?.Player;
            if (player is null)
            {
                await DisplayAlert("Portrait", "No player bound.", "OK");
                return;
            }

            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Portrait", "No active game loaded.", "OK");
                return;
            }

            var popup = new AIImagePromptPopup("Generate Portrait", "heroic adventurer portrait");
            await this.ShowPopupAsync(popup);
            if (popup.IsCancelled || string.IsNullOrWhiteSpace(popup.PromptText)) return;
            var prompt = popup.PromptText;
            int? size = popup.SelectedSize;

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

                    // Add to "Player" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });
                    
                    var rootFolder = doc.Roots.First();
                    var folderName = "Player";
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

                    player.PortraitImagePath = localPath;
                    UpdatePortraitImage(localPath);
                }
                else
                {
                    // Fallback to temp path if media library is unavailable
                    player.PortraitImagePath = path;
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

        private void OnPortraitDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void OnPortraitDrop(object sender, DropEventArgs e)
        {
            var player = BindingContext as Player ?? App.CurrentGame?.Player;
            if (player is null) return;

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
                player.PortraitImagePath = localPath;
                UpdatePortraitImage(localPath);
            }
        }

        private void OnClearPortraitClicked(object? sender, EventArgs e)
        {
            var player = BindingContext as Player ?? App.CurrentGame?.Player;
            if (player is not null)
            {
                player.PortraitImagePath = null;
                UpdatePortraitImage(null);
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

        private static async Task SetPortraitImageAsync(Image image, string path)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                image.ClearValue(Image.SourceProperty); // prevent binding from overwriting
                image.Source = ImageSource.FromFile(path);
            });
        }

        private Task SetPortraitImageAsync(string path) => SetPortraitImageAsync(PortraitImage, path);

        private async Task ImportPortraitFromFileAsync(string filePath)
        {
            if (BindingContext is not Player player) return;
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

                    // Add to "Player" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });

                    var rootFolder = doc.Roots.First();
                    var folderName = "Player";
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
                    player.PortraitImagePath = localPath;
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
                        if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                        {
                            args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                            args.Handled = true;
                        }
                    };
                    platformView.Drop += async (s, args) =>
                    {
                        if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                        {
                            args.Handled = true;
                            var deferral = args.GetDeferral();
                            try
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
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error dropping portrait: {ex.Message}");
                            }
                            finally
                            {
                                deferral.Complete();
                            }
                        }
                    };
                }
            }
#endif
        }

        private void OnDetailsTabClicked(object sender, EventArgs e)
        {
            DetailsTabBorder.BackgroundColor = RagNext.Services.ThemeService.GetPrimaryColor();
            DetailsTabLabel.TextColor = Colors.White;

            ActionsTabBorder.BackgroundColor = Colors.Transparent;
            ActionsTabLabel.TextColor = Colors.Gray;

            DetailsScrollView.IsVisible = true;
            ActionsContainer.IsVisible = false;
        }

        private void OnActionsTabClicked(object sender, EventArgs e)
        {
            ActionsTabBorder.BackgroundColor = RagNext.Services.ThemeService.GetPrimaryColor();
            ActionsTabLabel.TextColor = Colors.White;

            DetailsTabBorder.BackgroundColor = Colors.Transparent;
            DetailsTabLabel.TextColor = Colors.Gray;

            DetailsScrollView.IsVisible = false;
            ActionsContainer.IsVisible = true;
        }
    }

    // Option 1: keep absolute path (Windows only):
    //<Image Source="{Binding PortraitImagePath}" />

    // Option 2: use a converter to turn absolute paths into ImageSource:
    public class FilePathToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && !string.IsNullOrWhiteSpace(s)
                ? ImageSource.FromFile(s)
                : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null!;
    }

    // XAML usage:
    //<Image Source="{Binding PortraitImagePath, Converter={StaticResource FilePathToImageSourceConverter}}" />
}
