using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public partial class MediaTreeView : ContentView
    {
        public MediaTreeView()
        {
            InitializeComponent();
            var vm = MauiProgram.Services.GetService(typeof(MediaLibraryViewModel)) as MediaLibraryViewModel;
            BindingContext = vm;
            if (vm != null)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaLibraryViewModel.Selected))
            {
                StopAudio();
                StopVideo();
                SetupVideoPlayer();
            }
        }

        // Add this event handler for DragStarting
        private void OnMediaDragStarting(object sender, DragStartingEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is object ctx)
            {
                e.Data.Properties["DraggedItem"] = ctx;

                var assetProp = ctx.GetType().GetProperty("Asset");
                if (assetProp?.GetValue(ctx) is RagsCore.Models.MediaAsset asset)
                {
                    e.Data.Text = $"MediaAssetId:{asset.Id}";
                }
                else
                {
                    var nameProp = ctx.GetType().GetProperty("Name");
                    if (nameProp?.GetValue(ctx) is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        e.Data.Text = name;
                    }
                }
            }
        }

        private void OnMediaFolderDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void OnMediaFolderDrop(object sender, DropEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is MediaLibraryViewModel.Node targetNode)
            {
                if (targetNode.IsFolder && targetNode.Folder != null)
                {
                    if (e.Data.Properties.TryGetValue("DraggedItem", out var item) && item is MediaLibraryViewModel.Node sourceNode)
                    {
                        var vm = BindingContext as MediaLibraryViewModel;
                        if (vm != null)
                        {
                            await vm.MoveNodeAsync(sourceNode, targetNode.Folder);
                        }
                    }
                }
            }
        }

        private void OnFolderBorderLoaded(object? sender, EventArgs e)
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
                                var filePaths = new System.Collections.Generic.List<string>();
                                foreach (var item in items)
                                {
                                    if (item is Windows.Storage.StorageFile file)
                                    {
                                        filePaths.Add(file.Path);
                                    }
                                }
                                if (filePaths.Count > 0 && border.BindingContext is MediaLibraryViewModel.Node node && node.Folder != null)
                                {
                                    var vm = BindingContext as MediaLibraryViewModel;
                                    if (vm != null)
                                    {
                                        MainThread.BeginInvokeOnMainThread(async () =>
                                        {
                                            await vm.ImportExternalFilesAsync(filePaths, node.Folder);
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error dropping files: {ex.Message}");
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

        private void OnLibraryRootLoaded(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is VisualElement element)
            {
                if (element.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement platformView)
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
                                if (args.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                                {
                                    var items = await args.DataView.GetStorageItemsAsync();
                                    var filePaths = new System.Collections.Generic.List<string>();
                                    foreach (var item in items)
                                    {
                                        if (item is Windows.Storage.StorageFile file)
                                        {
                                            filePaths.Add(file.Path);
                                        }
                                    }
                                    if (filePaths.Count > 0)
                                    {
                                        var vm = BindingContext as MediaLibraryViewModel;
                                        if (vm != null)
                                        {
                                            MainThread.BeginInvokeOnMainThread(async () =>
                                            {
                                                await vm.ImportExternalFilesAsync(filePaths, null);
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error dropping files: {ex.Message}");
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

        private double _startHeight = 150;

        private void OnSplitterPan(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startHeight = PreviewPanel.HeightRequest;
                    if (double.IsNaN(_startHeight) || _startHeight <= 0)
                    {
                        _startHeight = 150;
                    }
                    break;
                case GestureStatus.Running:
                    double newHeight = _startHeight - e.TotalY;
                    if (newHeight < 60) newHeight = 60;
                    if (newHeight > 300) newHeight = 300;
                    PreviewPanel.HeightRequest = newHeight;
                    break;
                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    break;
            }
        }

        private void OnPlayAudioClicked(object sender, EventArgs e)
        {
            var vm = BindingContext as MediaLibraryViewModel;
            if (vm != null && !string.IsNullOrEmpty(vm.SelectedFilePath))
            {
                PlayAudio(vm.SelectedFilePath);
            }
        }

        private void OnPauseAudioClicked(object sender, EventArgs e)
        {
            PauseAudio();
        }

        private void OnStopAudioClicked(object sender, EventArgs e)
        {
            StopAudio();
        }

        private void PlayAudio(string filePath)
        {
            try
            {
                MediaPlaybackEngine.Stop();
                MediaPlaybackEngine.Source = CommunityToolkit.Maui.Views.MediaSource.FromFile(filePath);
                MediaPlaybackEngine.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play audio: {ex.Message}");
            }
        }

        private void PauseAudio()
        {
            try
            {
                MediaPlaybackEngine.Pause();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to pause audio: {ex.Message}");
            }
        }

        private void StopAudio()
        {
            try
            {
                MediaPlaybackEngine.Stop();
                MediaPlaybackEngine.Source = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop audio: {ex.Message}");
            }
        }

        private void StopVideo()
        {
            try
            {
                VideoPlaybackEngine.Stop();
                VideoPlaybackEngine.Source = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop video: {ex.Message}");
            }
        }

        private void SetupVideoPlayer()
        {
            try
            {
                var vm = BindingContext as MediaLibraryViewModel;
                if (vm != null && vm.IsSelectedVideo && !string.IsNullOrEmpty(vm.SelectedFilePath))
                {
                    VideoPlaybackEngine.Source = CommunityToolkit.Maui.Views.MediaSource.FromFile(vm.SelectedFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to setup video player: {ex.Message}");
            }
        }
    }
}