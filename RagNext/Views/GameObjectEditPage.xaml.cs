#nullable enable
using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagsCore.Services;
using RagNext.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using RagNext.Views.Popups;

namespace RagNext.Views
{
    [QueryProperty(nameof(ObjectId), "objectId")]
    public partial class GameObjectEditPage : ContentPage
    {
        public string? ObjectId { set { _ = SetObjectAsync(value); } }

        private CollectionView? AttributesListView => this.FindByName<CollectionView>("AttributesList");

        private readonly IAIChatService? _ai;

        public GameObjectEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
        }

        private async System.Threading.Tasks.Task SetObjectAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var obj = game?.Objects?.FirstOrDefault(o => o.Id == id);
            if (obj is null)
            {
                await DisplayAlert("Not found", "Object not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = obj;

            InitializeContainedItemsList(obj);

            UpdatePortraitImage(obj.PortraitImagePath);
            obj.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GameObject.PortraitImagePath))
                    UpdatePortraitImage(obj.PortraitImagePath);
            };
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
            var originalText = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";
            var anim = new Animation(v => btn.Rotation = v, 0, 360);
            anim.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);
            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = 0;
                btn.Text = originalText;
                btn.IsEnabled = true;
            });
        }

        private async void OnAddAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not GameObject obj) return;

            var name = await DisplayPromptAsync("New Attribute", "Enter attribute name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var value = await DisplayPromptAsync("New Attribute", "Enter attribute value (optional):");
            obj.Attributes.Add(new CustomAttribute { Name = name.Trim(), Value = string.IsNullOrWhiteSpace(value) ? null : value });
        }

        private async void OnRemoveAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not GameObject obj) return;

            var list = AttributesListView;
            var selected = list?.SelectedItem as CustomAttribute;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an attribute to remove.", "OK");
                return;
            }

            obj.Attributes.Remove(selected);
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
            if (BindingContext is not GameObject obj) return;

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
                obj.PortraitImagePath = localPath;
                UpdatePortraitImage(localPath);
            }
        }

        private void OnClearPortraitClicked(object? sender, EventArgs e)
        {
            if (BindingContext is GameObject obj)
            {
                obj.PortraitImagePath = null;
                UpdatePortraitImage(null);
            }
        }

        private async void OnGeneratePortraitClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not GameObject obj)
            {
                await DisplayAlert("Portrait", "No object bound.", "OK");
                return;
            }

            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Portrait", "No active game loaded.", "OK");
                return;
            }

            var popup = new AIImagePromptPopup("Generate Portrait", "cool fantasy weapon or ancient artifact item");
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

                    // Add to "Objects" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });

                    var rootFolder = doc.Roots.First();
                    var folderName = "Objects";
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

                    obj.PortraitImagePath = localPath;
                    UpdatePortraitImage(localPath);
                }
                else
                {
                    // Fallback to temp path if media library is unavailable
                    obj.PortraitImagePath = path;
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
            if (BindingContext is not GameObject obj) return;
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

                    // Add to "Objects" media folder in media tree
                    var doc = await treeStore.LoadAsync(game);
                    if (doc.Roots.Count == 0)
                        doc.Roots.Add(new RagNext.Models.MediaFolder { Name = "Assets" });

                    var rootFolder = doc.Roots.First();
                    var folderName = "Objects";
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
                    obj.PortraitImagePath = localPath;
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

        // ── Container Settings Handlers ─────────────────────────────────────

        public class ObjectCheckItem : BindableObject
        {
            public Guid Id { get; }
            public string Name { get; }

            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            public ObjectCheckItem(Guid id, string name, bool isChecked)
            {
                Id = id;
                Name = name;
                IsChecked = isChecked;
            }
        }

        private readonly System.Collections.ObjectModel.ObservableCollection<ObjectCheckItem> _availableContainedItems = new();

        private void InitializeContainedItemsList(GameObject currentObj)
        {
            _availableContainedItems.Clear();
            var game = App.CurrentGame;
            if (game?.Objects is null) return;

            foreach (var otherObj in game.Objects)
            {
                if (otherObj.Id == currentObj.Id) continue; // Exclude itself

                bool isChecked = currentObj.ContainedObjectIds.Contains(otherObj.Id);
                var checkItem = new ObjectCheckItem(otherObj.Id, otherObj.Name, isChecked);
                _availableContainedItems.Add(checkItem);
            }

            ContainedObjectsCheckList.ItemsSource = _availableContainedItems;
        }

        private void OnIsContainerCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            ContainedItemsSection.IsVisible = e.Value;
        }

        private void OnContainedItemCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (BindingContext is not GameObject obj) return;
            if (sender is CheckBox cb && cb.BindingContext is ObjectCheckItem item)
            {
                if (e.Value)
                {
                    if (!obj.ContainedObjectIds.Contains(item.Id))
                        obj.ContainedObjectIds.Add(item.Id);
                }
                else
                {
                    obj.ContainedObjectIds.Remove(item.Id);
                }
            }
        }
    }
}