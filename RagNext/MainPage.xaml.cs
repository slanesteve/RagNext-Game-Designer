using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext
{
    public partial class MainPage : ContentPage
    {
        private Game _game = Game.CreateNew("New Game", "Unknown");
        private bool _hasRunStartupFlow;

        private MainViewModel? ViewModel => BindingContext as MainViewModel;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = new MainViewModel();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object? sender, EventArgs e)
        {
            this.Loaded -= MainPage_Loaded;

            if (_hasRunStartupFlow)
                return;

            _hasRunStartupFlow = true;

            // Give the desktop layout engine a moment to completely finish processing composition rules
            await Task.Delay(100);

            // Safely initialize default application context variables now that UI handles are verified
            if (App.CurrentGame == null)
            {
                App.CurrentGame = _game;
                if (ViewModel != null)
                {
                    ViewModel.CurrentGame = _game;
                }
            }
            else
            {
                _game = App.CurrentGame;
                SyncGameDataToUI();
            }

            // Toggle visibility of the internal layout overlay safely.
            StartupOverlayView.IsVisible = true;
        }

        private void CloseStartupOverlay()
        {
            StartupOverlayView.IsVisible = false;
        }

        private void OnBackgroundTapped(object? sender, EventArgs e)
        {
            CloseStartupOverlay();
        }

        private void OnDialogTapped(object? sender, EventArgs e)
        {
            // Swallow tap event on the dialog card to prevent background click-off
        }

        private void OnCancelOverlayClicked(object sender, EventArgs e)
        {
            CloseStartupOverlay();
        }

        private async void OnOverlayCreateClicked(object sender, EventArgs e)
        {
            CloseStartupOverlay();
            await CreateNewGameFlowAsync();
        }

        private async void OnOverlayLoadClicked(object sender, EventArgs e)
        {
            try
            {
                var saves = await GameStorage.ListSavesAsync();
                if (saves == null || saves.Length == 0)
                {
                    bool create = await DisplayAlert("No saved game", "No saved game found. Would you like to create a new one?", "Yes", "No");
                    if (create)
                    {
                        CloseStartupOverlay();
                        await CreateNewGameFlowAsync();
                    }
                    return;
                }

                WelcomeOptionsView.IsVisible = false;
                InlineSavePickerView.IsVisible = true;
                SavesCollectionView.ItemsSource = saves;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load error", $"Failed to scan local file directories: {ex.Message}", "OK");
            }
        }

        private async void OnSaveSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0)
                return;

            string? selectedSave = e.CurrentSelection[0] as string;

            ((CollectionView)sender).SelectedItem = null;

            if (string.IsNullOrEmpty(selectedSave))
                return;

            try
            {
                var loaded = await GameStorage.LoadAsync(selectedSave);
                if (loaded is null)
                {
                    await DisplayAlert("Load error", "Failed to parse the selected workspace profile save file.", "OK");
                    return;
                }

                _game = loaded;
                SyncGameDataToUI();
                CloseStartupOverlay();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load error", $"Failed to load project: {ex.Message}", "OK");
            }
        }

        private void OnCloseSavePickerClicked(object sender, EventArgs e)
        {
            InlineSavePickerView.IsVisible = false;
            WelcomeOptionsView.IsVisible = true;
        }

        private async void OnOverlayDefaultClicked(object sender, EventArgs e)
        {
            CloseStartupOverlay();
            var loaded = await GameStorage.LoadAsync();
            if (loaded is not null)
                _game = loaded;

            SyncGameDataToUI();
        }

        private void SyncGameDataToUI()
        {
            EnsurePlayer();
            App.CurrentGame = _game;

            if (ViewModel != null)
            {
                ViewModel.CurrentGame = _game;
            }

            CounterBtn.Text = string.IsNullOrWhiteSpace(_game.Title) ? "Click me" : $"Game: {_game.Title}";
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
                    var loaded = await GameStorage.LoadAsync();
                    if (loaded is not null)
                        _game = loaded;
                    SyncGameDataToUI();
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

                SyncGameDataToUI();

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
                    _game = Game.CreateNew("New Game", "Unknown");
                    SyncGameDataToUI();
                }
                else
                {
                    WelcomeOptionsView.IsVisible = false;
                    InlineSavePickerView.IsVisible = true;
                    StartupOverlayView.IsVisible = true;
                    SavesCollectionView.ItemsSource = saves;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Load error", $"Failed to load saved game: {ex.Message}", "OK");
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                EnsurePlayer();
                var defaultName = string.IsNullOrWhiteSpace(_game.FileName) ? (string.IsNullOrWhiteSpace(_game.Title) ? "save" : _game.Title) : _game.FileName;
                var name = await DisplayPromptAsync("Save Game", "Enter save name", "OK", "Cancel", placeholder: defaultName, initialValue: defaultName);
                if (name is null)
                    return;

                string cleanName = name.Trim();
                if (string.IsNullOrWhiteSpace(_game.Title))
                {
                    _game.Title = cleanName;
                }

                SyncGameDataToUI();

                await GameStorage.SaveAsync(_game, cleanName, isExplicitUserSave: true);
                await DisplayAlert("Saved", "Game saved successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save game: {ex.Message}", "OK");
            }
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void OnLoadMenuClicked(object? sender, EventArgs e)
        {
            await LoadGameFlowAsync();
        }

        private void OnExitMenuClicked(object? sender, EventArgs e)
        {
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
            var page = new Views.GeneralSettingsPage();
            await Navigation.PushModalAsync(page);
        }

        private async void OnSettingsAI(object? sender, EventArgs e)
        {
            var page = new Views.AISettingsPage();
            await Navigation.PushModalAsync(page);
        }

        private async void OnHelpAbout(object? sender, EventArgs e)
        {
            await DisplayAlert("About", "RagNext - Game Editor\nVersion: 1.0", "OK");
        }

        private async void OnFileMenuClicked(object? sender, EventArgs e)
        {
            var options = new[] { "New", "Save", "Load", "Exit" };
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                case "New":
                    await CreateNewGameFlowAsync();
                    break;
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
            var options = new[] { "General", "Text AI", "Image AI" };
            var choice = await DisplayActionSheet("Menu", "Cancel", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            switch (choice)
            {
                case "General":
                    OnSettingsGeneral(this, EventArgs.Empty);
                    break;
                case "Text AI":
                    OnSettingsAI(this, EventArgs.Empty);
                    break;
                case "Image AI":
                    var page = new Views.ImageAISettingsPage();
                    await Navigation.PushModalAsync(page);
                    break;
            }
        }

        private async void OnHelpMenuClicked(object sender, EventArgs e)
        {
            var options = new[] { "About" };
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

        private async void OpenRoomsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//rooms");
        }

        private async void OpenGameObjectsClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//gameobjects");
        }

        private async void EditRoom(Guid roomId)
        {
            await Shell.Current.GoToAsync($"RoomEdit?roomId={roomId}");
        }

        private void ToolbarItem_Clicked(object sender, EventArgs e) { }
    }
}