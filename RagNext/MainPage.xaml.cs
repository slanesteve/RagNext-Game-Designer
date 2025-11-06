using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext
{
    public partial class MainPage : ContentPage
    {
        private Game _game = Game.CreateNew("New Game", "Unknown");
        private bool _hasRunStartupFlow;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_hasRunStartupFlow)
                return;

            _hasRunStartupFlow = true;

            // give the UI a moment to attach, then show our reliable modal dialog
            await Task.Yield();

            try
            {
                var dialog = new StartupDialog();

                // ensure navigation is available; push modal and await the result
                await Navigation.PushModalAsync(dialog);
                var choice = await dialog.ResultTask.ConfigureAwait(false);

                // restore to UI thread to update UI/bindings
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (choice == "Create New Game")
                    {
                        await CreateNewGameFlowAsync();
                    }
                    else if (choice == "Load Saved Game")
                    {
                        await LoadGameFlowAsync();
                    }
                    else
                    {
                        var loaded = await GameStorage.LoadAsync();
                        if (loaded is not null)
                            _game = loaded;
                        EnsurePlayer();
                        BindingContext = _game;
                        App.CurrentGame = _game;
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup dialog error: {ex}");
                await DisplayAlert("Error", $"Failed during startup: {ex.Message}", "OK");
                var loaded = await GameStorage.LoadAsync();
                if (loaded is not null)
                    _game = loaded;
                EnsurePlayer();
                BindingContext = _game;
                App.CurrentGame = _game;
            }
        }

        private void EnsurePlayer()
        {
            if (_game.Player is null)
                _game.Player = new Player();
        }

        private async Task CreateNewGameFlowAsync()
        {
            while (true)
            {
                string? name = await DisplayPromptAsync("New Game", "Enter the game name", "OK", "Cancel", placeholder: "My Game");
                if (name is null)
                {
                    // user canceled - fall back to load or default
                    var loaded = await GameStorage.LoadAsync();
                    if (loaded is not null)
                        _game = loaded;
                    App.CurrentGame = _game;
                    break;
                }

                name = name.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    await DisplayAlert("Validation", "Name cannot be empty. Please enter a name.", "OK");
                    continue;
                }

                _game = Game.CreateNew(name, Environment.UserName ?? "Unknown");
                _game.Player = new Player { Name = "Player" };
                EnsurePlayer();
                BindingContext = _game;
                App.CurrentGame = _game;

                // Optionally save immediately - name the file with the game title
                try
                {
                    await GameStorage.SaveAsync(_game, name);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Save error", $"Failed to save new game: {ex.Message}", "OK");
                }

                break;
            }

            // Update UI summary button if present
            CounterBtn.Text = string.IsNullOrWhiteSpace(_game.Title) ? "Click me" : $"Game: {_game.Title}";
        }

        private async Task LoadGameFlowAsync()
        {
            try
            {
                var saves = await GameStorage.ListSavesAsync();
                if (saves == null || saves.Length == 0)
                {
                    var create = await DisplayAlert("No saved game", "No saved game found. Would you like to create a new one?", "Yes", "No");
                    if (create)
                    {
                        await CreateNewGameFlowAsync();
                        return;
                    }

                    // leave default/new game
                    _game = Game.CreateNew("New Game", "Unknown");
                }
                else
                {
                    // Let the user choose which save to load
                    var choice = await DisplayActionSheet("Choose saved game", "Cancel", null, saves);
                    if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                    {
                        // user cancelled - do nothing
                        return;
                    }

                    var loaded = await GameStorage.LoadAsync(choice);
                    if (loaded is null)
                    {
                        await DisplayAlert("Load error", "Failed to load the selected save.", "OK");
                        return;
                    }

                    _game = loaded;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load error", $"Failed to load saved game: {ex.Message}", "OK");
            }

            EnsurePlayer();
            BindingContext = _game;
            App.CurrentGame = _game;
            CounterBtn.Text = string.IsNullOrWhiteSpace(_game.Title) ? "Click me" : $"Game: {_game.Title}";
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            // existing counter logic remains
            // (you can keep the code you already had)
            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                EnsurePlayer();

                // ask for a name when saving (optional): use Title as default suggestion
                var defaultName = string.IsNullOrWhiteSpace(_game.Title) ? "save" : _game.Title;
                var name = await DisplayPromptAsync("Save Game", "Enter save name", "OK", "Cancel", placeholder: defaultName, initialValue: defaultName);
                if (name is null)
                {
                    // user cancelled save
                    return;
                }

                await GameStorage.SaveAsync(_game, name.Trim());
                await DisplayAlert("Saved", "Game saved successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save game: {ex.Message}", "OK");
            }
        }

        // Menu handlers
        private async void OnLoadMenuClicked(object? sender, EventArgs e)
        {
            await LoadGameFlowAsync();
        }

        private void OnExitMenuClicked(object? sender, EventArgs e)
        {
            // Immediately terminate the app process. On some platforms different APIs may be preferable.
            System.Environment.Exit(0);
        }

        private async void OnEditUndo(object? sender, EventArgs e)
        {
            await DisplayAlert("Edit", "Undo not implemented.", "OK");
        }

        private async void OnEditRedo(object? sender, EventArgs e)
        {
            await DisplayAlert("Edit", "Redo not implemented.", "OK");
        }
        private async void OnSettingsGeneral(object? sender, EventArgs e)
        {
            await DisplayAlert("Settings", "General not implemented.", "OK");
        }
        private async void OnSettingsAI(object? sender, EventArgs e)
        {
            // Show as modal to keep behavior consistent with other dialogs
            var page = new Views.AISettingsPage();
            await Navigation.PushModalAsync(page);
        }

        private async void OnHelpAbout(object? sender, EventArgs e)
        {
            await DisplayAlert("About", "RagNext - Game Editor\nVersion: 1.0", "OK");
        }

        private async void OnFileMenuClicked(object? sender, EventArgs e)
        {
            // Show a platform-friendly action sheet instead of constructing a MenuFlyout
            // (avoids APIs like MenuFlyout.Items and ShowAt which aren't available here)
            var options = new[] { "Save", "Load", "Exit"};
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                case "Save":
                    OnSaveClicked(this, EventArgs.Empty);
                    break;
                case "Load":
                    OnLoadMenuClicked(this, EventArgs.Empty);
                    break;
                case "Exit":
                    OnExitMenuClicked(this, EventArgs.Empty);
                    break;
                
            }
        }

        private async void OnEditMenuClicked(object sender, EventArgs e)
        {
            // Show a platform-friendly action sheet instead of constructing a MenuFlyout
            // (avoids APIs like MenuFlyout.Items and ShowAt which aren't available هنا)
            var options = new[] { "Undo", "Redo" };
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                case "Undo":
                    OnEditUndo(this, EventArgs.Empty);
                    break;
                case "Redo":
                    OnEditRedo(this, EventArgs.Empty);
                    break;
                
            }
        }
        private async void OnSettingsMenuClicked(object sender, EventArgs e)
        {
            // Show a platform-friendly action sheet instead of constructing a MenuFlyout
            // (avoids APIs like MenuFlyout.Items and ShowAt which aren't available هنا)
            var options = new[] { "General", "AI" };
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                case "General":
                    OnSettingsGeneral(this, EventArgs.Empty);
                    break;
                case "AI":
                    OnSettingsAI(this, EventArgs.Empty);
                    break;

            }
        }

        private async void OnHelpMenuClicked(object sender, EventArgs e)
        {
            // Show a platform-friendly action sheet instead of constructing a MenuFlyout
            // (avoids APIs like MenuFlyout.Items and ShowAt which aren't available هنا)
            var options = new[] {  "About" };
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                
                case "About":
                    OnHelpAbout(this, EventArgs.Empty);
                    break;
            }
        }

        // Switch to the Rooms tab
        private async void OpenRoomsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//rooms"); // absolute route to the "rooms" ShellContent
        }

        // Switch to the GameObjects tab
        private async void OpenGameObjectsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//gameobjects");
        }

        // Navigate to the RoomEdit page (route already registered in AppShell)
        private async void EditRoom(Guid roomId)
        {
            await Shell.Current.GoToAsync($"RoomEdit?roomId={roomId}");
        }

        private void ToolbarItem_Clicked(object sender, EventArgs e)
        {

        }
    }
}
