using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Services;
using System.Globalization;
using Microsoft.Maui.Graphics.Converters;

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
            App.GameChanged += (game) => OnGameLoaded(this, game); // ensure repopulate after load
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var player = App.CurrentGame?.Player;
            BindingContext = player;

            // Set image exclusively in code-behind to avoid binding race
            UpdatePortraitImage(player?.PortraitImagePath);

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
                        await MainScroll.ScrollToAsync(0, _lastY, animated: false));
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
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
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
                player.Inventory.Add(selected);
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

            var prompt = await DisplayPromptAsync("Generate Portrait", "Enter a prompt for the image:", "Generate", "Cancel", placeholder: "heroic adventurer portrait");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            // Choose size
            // Wrap the action sheet on the UI thread and ensure you're calling it from an active page
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

                player.PortraitImagePath = path;
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
            // No equivalent to AcceptedOperation on DropEventArgs in .NET MAUI
            // You may set e.AcceptedOperation if you want to mark the event as handled
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void OnPortraitDrop(object sender, DropEventArgs e)
        {
            var player = BindingContext as Player ?? App.CurrentGame?.Player;
            if (player is null) return;

            // Retrieve the item set during drag start
            if (e.Data.Properties.TryGetValue("DraggedItem", out var item))
            {
                // Try common path properties
                var pathProp = item.GetType().GetProperty("Path")
                             ?? item.GetType().GetProperty("FullPath")
                             ?? item.GetType().GetProperty("FilePath");

                if (pathProp?.GetValue(item) is string path && !string.IsNullOrWhiteSpace(path))
                {
                    player.PortraitImagePath = path;
                    UpdatePortraitImage(path);
                    return;
                }

                // If a media asset was dragged from the media tree, resolve to a local file path
                var assetProp = item.GetType().GetProperty("Asset");
                var game = App.CurrentGame;
                if (game is not null && assetProp?.GetValue(item) is RagsCore.Models.MediaAsset asset)
                {
                    var lib = MauiProgram.Services.GetService(typeof(RagsCore.Services.IMediaLibrary)) as RagsCore.Services.IMediaLibrary;
                    if (lib is not null)
                    {
                        var localPath = lib.GetLocalPath(game, asset);
                        if (!string.IsNullOrWhiteSpace(localPath))
                        {
                            player.PortraitImagePath = localPath;
                            UpdatePortraitImage(localPath);
                            return;
                        }
                    }
                }
            }

            // Fallback: if text was provided during drag start
            var text = await e.Data.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                player.PortraitImagePath = text;
                UpdatePortraitImage(text);
            }
        }

        private void UpdatePortraitImage(string? path)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PortraitImage.ClearValue(Image.SourceProperty);
                PortraitImage.Source = string.IsNullOrWhiteSpace(path) ? null : ImageSource.FromFile(path);
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
