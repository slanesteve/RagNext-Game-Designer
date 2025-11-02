using System.Threading.Tasks;
using Microsoft.Maui.Controls;

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
            // Short delay to show splash; adjust as desired
            await Task.Delay(1500);

            // Use the AppShell as the application's MainPage so Shell.Current is not null
            Application.Current!.MainPage = new AppShell();
        }
    }
}