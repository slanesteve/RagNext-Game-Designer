using Microsoft.Maui.Controls;
using RagsCore.Models;
using System;
using Microsoft.Maui.ApplicationModel;

namespace RagNext
{
    public partial class App : Application
    {
        private static Game? _currentGame;
        public static event Action<Game?>? GameChanged;
        public static Game? CurrentGame
        {
            get => _currentGame;
            set
            {
                if (ReferenceEquals(_currentGame, value))
                    return;

                _currentGame = value;
                GameChanged?.Invoke(_currentGame);

                // Reset each tab to its root page (RoomsPage, GameObjectsPage, etc.)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var shell = Shell.Current;
                    if (shell is null) return;

                    var original = shell.CurrentItem;

                    try
                    {
                        await shell.GoToAsync("//rooms", false);
                        await shell.Navigation.PopToRootAsync(false);

                        await shell.GoToAsync("//gameobjects", false);
                        await shell.Navigation.PopToRootAsync(false);

                        await shell.GoToAsync("//main", false);
                        await shell.Navigation.PopToRootAsync(false);

                        // Restore previously selected tab (now reset), or stay on Rooms
                        if (original is not null)
                            shell.CurrentItem = original;
                        else
                            await shell.GoToAsync("//rooms", false);
                    }
                    catch
                    {
                        // Ignore navigation errors
                    }
                });
            }
        }

        public App()
        {
            InitializeComponent();
            MainPage = new SplashPage();
        }
    }
}