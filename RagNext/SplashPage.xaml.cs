using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // MainThread

namespace RagNext
{
    public partial class SplashPage : ContentPage
    {
        private bool _started = false;

        public SplashPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!_started)
            {
                _started = true;
                _ = StartAsync();
            }
        }

        async Task StartAsync()
        {
            // 1. Elastic spring-zoom and glow fade-in for the central logo badge
            _ = GlowingBadge.FadeTo(1, 800, Easing.CubicOut);
            await GlowingBadge.ScaleTo(1.0, 950, Easing.SpringOut);

            // 2. Smoothly slide up and fade in premium branding typography
            _ = TypographyStack.FadeTo(1, 600, Easing.CubicOut);
            await TypographyStack.TranslateTo(0, 0, 750, Easing.CubicOut);

            // 3. Reveal spinner loader and footer metadata
            _ = LoadingStack.FadeTo(1, 400, Easing.CubicOut);
            await VersionLabel.FadeTo(1, 400, Easing.CubicOut);

            // Maintain cinematic splash status briefly while initializing
            await Task.Delay(1800);

            // 4. Clean scale-down and overlay fade-out before opening designer shells
            await Task.WhenAll(
                GlowingBadge.ScaleTo(0.85, 450, Easing.CubicIn),
                this.FadeTo(0, 450, Easing.CubicIn)
            );

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Application.Current!.MainPage = new AppShell();
                });
            }
            catch (Exception ex)
            {
                // Show the actual startup error instead of a pink screen
                await DisplayAlert("Startup error", ex.ToString(), "OK");
            }
        }
    }
}