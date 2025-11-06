using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // MainThread

namespace RagNext
{
    public partial class SplashPage : ContentPage
    {
        public SplashPage()
        {
            InitializeComponent();
            _ = StartAsync();
        }

        async Task StartAsync()
        {
            await Task.Delay(1500);

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