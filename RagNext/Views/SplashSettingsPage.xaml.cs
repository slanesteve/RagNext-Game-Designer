using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    public partial class SplashSettingsPage : ContentPage
    {
        private double _startX;
        private double _startY;
        private bool _isInitialized = false;

        public SplashSettingsPage()
        {
            InitializeComponent();
            SizeChanged += OnPageSizeChanged;
            PreviewCanvas.SizeChanged += OnCanvasSizeChanged;
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LoadConfiguration();
            await System.Threading.Tasks.Task.Delay(100);
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
        }

        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            UpdateCanvasAbsoluteTranslations();
        }

        private void OnCanvasSizeChanged(object? sender, EventArgs e)
        {
            UpdateCanvasAbsoluteTranslations();
        }

        private void LoadConfiguration()
        {
            var game = App.CurrentGame;
            if (game == null) return;

            // Self-healing initialization of SplashScreenSettings if null
            if (game.SplashScreen == null)
            {
                game.SplashScreen = new SplashScreenSettings();
            }

            var splash = game.SplashScreen;

            // 1. Populate Media Dropdown Pickers
            var images = game.MediaAssets.Where(m => m.Kind == MediaKind.Image).ToList();
            var audio = game.MediaAssets.Where(m => m.Kind == MediaKind.Audio).ToList();
            var video = game.MediaAssets.Where(m => m.Kind == MediaKind.Video || m.OriginalFileName.EndsWith(".webm") || m.OriginalFileName.EndsWith(".mp4")).ToList();

            BgImagePicker.ItemsSource = images;
            BgImagePicker.ItemDisplayBinding = new Binding("OriginalFileName");

            AudioPicker.ItemsSource = audio;
            AudioPicker.ItemDisplayBinding = new Binding("OriginalFileName");

            VideoPicker.ItemsSource = video;
            VideoPicker.ItemDisplayBinding = new Binding("OriginalFileName");

            // 2. Load settings to UI
            _isInitialized = false;

            EnabledSwitch.IsToggled = splash.Enabled;
            ModePicker.SelectedItem = splash.Mode;

            // Set Picker Selected Items securely
            if (!string.IsNullOrEmpty(splash.ImageAssetId))
            {
                var guid = Guid.TryParse(splash.ImageAssetId, out var g) ? g : Guid.Empty;
                BgImagePicker.SelectedItem = images.FirstOrDefault(i => i.Id == guid);
            }
            else BgImagePicker.SelectedItem = null;

            if (!string.IsNullOrEmpty(splash.SoundAssetId))
            {
                var guid = Guid.TryParse(splash.SoundAssetId, out var g) ? g : Guid.Empty;
                AudioPicker.SelectedItem = audio.FirstOrDefault(a => a.Id == guid);
            }
            else AudioPicker.SelectedItem = null;

            if (!string.IsNullOrEmpty(splash.VideoAssetId))
            {
                var guid = Guid.TryParse(splash.VideoAssetId, out var g) ? g : Guid.Empty;
                VideoPicker.SelectedItem = video.FirstOrDefault(v => v.Id == guid);
            }
            else VideoPicker.SelectedItem = null;

            TransitionPicker.SelectedItem = splash.TransitionStyle;

            TitleEntry.Text = splash.Text;
            FontSizeEntry.Text = splash.FontSize.ToString();
            FontColorEntry.Text = splash.FontColor;

            FadeInSlider.Value = splash.FadeInDuration;
            DisplaySlider.Value = splash.DisplayDuration;
            FadeOutSlider.Value = splash.FadeOutDuration;

            FadeInValLabel.Text = $"{splash.FadeInDuration:0.0}s";
            DisplayValLabel.Text = $"{splash.DisplayDuration:0.0}s";
            FadeOutValLabel.Text = $"{splash.FadeOutDuration:0.0}s";

            // Sync visual preview labels
            XCoordinateLabel.Text = $"X: {splash.TextX:0.0}%";
            YCoordinateLabel.Text = $"Y: {splash.TextY:0.0}%";
            PreviewLabel.Text = splash.Text;

            try
            {
                PreviewLabel.FontSize = splash.FontSize;
                PreviewLabel.TextColor = Color.FromArgb(splash.FontColor);
            }
            catch { }

            // Sync video vs image card visibility
            UpdateModeUIVisibility(splash.Mode);
            UpdatePreviewBackground();

            _isInitialized = true;
            UpdateCanvasAbsoluteTranslations();
        }

        private void UpdateCanvasAbsoluteTranslations()
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash == null || PreviewCanvas.Width <= 0 || PreviewCanvas.Height <= 0) return;

            // Translate percentage coordinates to canvas pixel space
            double xPos = (splash.TextX / 100) * PreviewCanvas.Width;
            double yPos = (splash.TextY / 100) * PreviewCanvas.Height;

            // Avoid overflowing off bottom/right edges
            double maxW = PreviewCanvas.Width - PreviewLabel.Width;
            double maxH = PreviewCanvas.Height - PreviewLabel.Height;

            PreviewLabel.TranslationX = Math.Clamp(xPos, 0, maxW > 0 ? maxW : PreviewCanvas.Width);
            PreviewLabel.TranslationY = Math.Clamp(yPos, 0, maxH > 0 ? maxH : PreviewCanvas.Height);
        }

        private void UpdateModeUIVisibility(string mode)
        {
            bool isImgMode = mode == "ImageAndText";
            ImageTextStylingCard.IsVisible = isImgMode;
            VideoStylingCard.IsVisible = !isImgMode;
            PreviewVideoMask.IsVisible = !isImgMode;
        }

        private void UpdatePreviewBackground()
        {
            var selectedAsset = BgImagePicker.SelectedItem as MediaAsset;
            if (selectedAsset != null && App.CurrentGame != null)
            {
                var lib = MauiProgram.Services.GetService(typeof(RagsCore.Services.IMediaLibrary)) as RagsCore.Services.IMediaLibrary;
                if (lib != null)
                {
                    string path = lib.GetLocalPath(App.CurrentGame, selectedAsset);
                    PreviewBgImage.Source = ImageSource.FromFile(path);
                    return;
                }
            }
            PreviewBgImage.Source = null;
        }

        private void OnTextPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash == null || !_isInitialized) return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = PreviewLabel.TranslationX;
                    _startY = PreviewLabel.TranslationY;
                    break;

                case GestureStatus.Running:
                    double newX = _startX + e.TotalX;
                    double newY = _startY + e.TotalY;

                    // Clamp values to canvas bounds securely
                    double maxW = PreviewCanvas.Width - PreviewLabel.Width;
                    double maxH = PreviewCanvas.Height - PreviewLabel.Height;

                    newX = Math.Clamp(newX, 0, maxW > 0 ? maxW : PreviewCanvas.Width);
                    newY = Math.Clamp(newY, 0, maxH > 0 ? maxH : PreviewCanvas.Height);

                    PreviewLabel.TranslationX = newX;
                    PreviewLabel.TranslationY = newY;

                    // Convert to percentage values
                    if (PreviewCanvas.Width > 0 && PreviewCanvas.Height > 0)
                    {
                        splash.TextX = Math.Clamp(Math.Round((newX / PreviewCanvas.Width) * 100, 1), 0, 100);
                        splash.TextY = Math.Clamp(Math.Round((newY / PreviewCanvas.Height) * 100, 1), 0, 100);

                        XCoordinateLabel.Text = $"X: {splash.TextX:0.0}%";
                        YCoordinateLabel.Text = $"Y: {splash.TextY:0.0}%";
                    }
                    break;
            }
        }

        private void OnEnabledToggled(object sender, ToggledEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized) splash.Enabled = e.Value;
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                string mode = ModePicker.SelectedItem as string ?? "ImageAndText";
                splash.Mode = mode;
                UpdateModeUIVisibility(mode);
            }
        }

        private void OnBgImageChanged(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                var asset = BgImagePicker.SelectedItem as MediaAsset;
                splash.ImageAssetId = asset?.Id.ToString() ?? string.Empty;
                UpdatePreviewBackground();
            }
        }

        private void OnAudioChanged(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                var asset = AudioPicker.SelectedItem as MediaAsset;
                splash.SoundAssetId = asset?.Id.ToString() ?? string.Empty;
            }
        }

        private void OnVideoChanged(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                var asset = VideoPicker.SelectedItem as MediaAsset;
                splash.VideoAssetId = asset?.Id.ToString() ?? string.Empty;
            }
        }

        private void OnTransitionChanged(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                splash.TransitionStyle = TransitionPicker.SelectedItem as string ?? "Fade";
            }
        }

        private void OnTitleTextChanged(object sender, TextChangedEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                splash.Text = e.NewTextValue;
                PreviewLabel.Text = e.NewTextValue;
            }
        }

        private void OnFontSizeChanged(object sender, TextChangedEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                if (double.TryParse(e.NewTextValue, out var val))
                {
                    splash.FontSize = val;
                    PreviewLabel.FontSize = val;
                }
            }
        }

        private void OnFontColorChanged(object sender, TextChangedEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash != null && _isInitialized)
            {
                try
                {
                    splash.FontColor = e.NewTextValue;
                    PreviewLabel.TextColor = Color.FromArgb(e.NewTextValue);
                }
                catch { }
            }
        }

        private void OnTimingChanged(object sender, ValueChangedEventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash == null || !_isInitialized) return;

            if (sender == FadeInSlider)
            {
                splash.FadeInDuration = Math.Round(e.NewValue, 1);
                FadeInValLabel.Text = $"{splash.FadeInDuration:0.0}s";
            }
            else if (sender == DisplaySlider)
            {
                splash.DisplayDuration = Math.Round(e.NewValue, 1);
                DisplayValLabel.Text = $"{splash.DisplayDuration:0.0}s";
            }
            else if (sender == FadeOutSlider)
            {
                splash.FadeOutDuration = Math.Round(e.NewValue, 1);
                FadeOutValLabel.Text = $"{splash.FadeOutDuration:0.0}s";
            }
        }

        private async void OnPreviewTransitionClicked(object sender, EventArgs e)
        {
            var splash = App.CurrentGame?.SplashScreen;
            if (splash == null) return;

            // Reset transitions state before simulation
            PreviewLabel.CancelAnimations();
            PreviewLabel.Opacity = 0;

            uint fadeInMs = (uint)(splash.FadeInDuration * 1000);
            uint holdMs = (uint)(splash.DisplayDuration * 1000);
            uint fadeOutMs = (uint)(splash.FadeOutDuration * 1000);

            // Record original coordinates
            double targetY = PreviewLabel.TranslationY;
            double targetScale = PreviewLabel.Scale;

            // Ensure simulation durations are within safe bounds
            if (fadeInMs < 100) fadeInMs = 100;
            if (fadeOutMs < 100) fadeOutMs = 100;

            // Perform Transition Playback dynamically based strictly on chosen style!
            switch (splash.TransitionStyle)
            {
                case "Rise":
                    // Text slides up smoothly from below
                    PreviewLabel.TranslationY = targetY + 30;
                    _ = PreviewLabel.FadeTo(1, fadeInMs, Easing.CubicOut);
                    await PreviewLabel.TranslateTo(PreviewLabel.TranslationX, targetY, fadeInMs, Easing.CubicOut);
                    break;

                case "Cinematic":
                    // Zoom & slow drift
                    PreviewLabel.Scale = 0.85;
                    _ = PreviewLabel.FadeTo(1, fadeInMs, Easing.CubicOut);
                    _ = PreviewLabel.ScaleTo(1.0, fadeInMs + holdMs, Easing.CubicOut);
                    await PreviewLabel.TranslateTo(PreviewLabel.TranslationX, targetY - 10, fadeInMs + holdMs, Easing.Linear);
                    break;

                case "Glitch":
                    // Rapid jitter offset jumps & fade-in
                    PreviewLabel.Opacity = 0.2;
                    PreviewLabel.TranslationX += 15;
                    await Task.Delay(60);
                    PreviewLabel.Opacity = 0.7;
                    PreviewLabel.TranslationX -= 30;
                    await Task.Delay(60);
                    PreviewLabel.TranslationX += 15;
                    PreviewLabel.Opacity = 1.0;
                    break;

                case "Exposure":
                    // Fast exposure glow simulation
                    PreviewLabel.Scale = 1.15;
                    _ = PreviewLabel.FadeTo(1, fadeInMs / 2, Easing.Linear);
                    await PreviewLabel.ScaleTo(1.0, fadeInMs, Easing.CubicOut);
                    break;

                default:
                    // Standard Fade Dissolve
                    await PreviewLabel.FadeTo(1, fadeInMs, Easing.Linear);
                    break;
            }

            // Hold phase
            await Task.Delay((int)holdMs);

            // Fade-out clean exit sequence
            await Task.WhenAll(
                PreviewLabel.FadeTo(0, fadeOutMs, Easing.Linear),
                PreviewLabel.ScaleTo(0.9, fadeOutMs, Easing.Linear)
            );

            // Restore elements back to draggable designer state
            PreviewLabel.Opacity = 1;
            PreviewLabel.Scale = targetScale;
            PreviewLabel.TranslationY = targetY;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game == null) return;

            try
            {
                await GameStorage.SaveAsync(game, string.IsNullOrWhiteSpace(game.Title) ? $"save_{DateTime.Now:yyyyMMddHHmmss}" : game.Title);
                await DisplayAlert("Splash Settings", "Configuration saved and synced successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save Error", ex.Message, "OK");
            }
        }
    }
}
