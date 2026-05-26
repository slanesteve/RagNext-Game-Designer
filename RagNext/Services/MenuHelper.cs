using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Services
{
    public static class MenuHelper
    {
        public static void PopulateMenuBar(ContentPage page)
        {
            page.MenuBarItems.Clear();

            // File Menu
            var fileMenu = new MenuBarItem { Text = "File" };

            var newItem = new MenuFlyoutItem { Text = "New Game" };
            newItem.Clicked += async (s, e) => await CreateNewGameFlowAsync(page);

            var saveItem = new MenuFlyoutItem { Text = "Save Game" };
            saveItem.Clicked += async (s, e) => await SaveGameFlowAsync(page);

            var loadItem = new MenuFlyoutItem { Text = "Load Saved Game" };
            loadItem.Clicked += async (s, e) => await LoadGameFlowAsync(page);

            var publishItem = new MenuFlyoutItem { Text = "Publish Game" };
            publishItem.Clicked += async (s, e) => await PublishGameFlowAsync(page);

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Clicked += (s, e) => System.Environment.Exit(0);

            fileMenu.Add(newItem);
            fileMenu.Add(saveItem);
            fileMenu.Add(loadItem);
            fileMenu.Add(new MenuFlyoutSeparator());
            fileMenu.Add(publishItem);
            fileMenu.Add(new MenuFlyoutSeparator());
            fileMenu.Add(exitItem);

            // Edit Menu
            var editMenu = new MenuBarItem { Text = "Edit" };
            var undoItem = new MenuFlyoutItem { Text = "Undo" };
            undoItem.Clicked += async (s, e) => await page.DisplayAlert("Edit", "Undo not implemented.", "OK");
            var redoItem = new MenuFlyoutItem { Text = "Redo" };
            redoItem.Clicked += async (s, e) => await page.DisplayAlert("Edit", "Redo not implemented.", "OK");
            editMenu.Add(undoItem);
            editMenu.Add(redoItem);

            // Settings Menu
            var settingsMenu = new MenuBarItem { Text = "Settings" };
            var generalSettingsItem = new MenuFlyoutItem { Text = "General Settings" };
            generalSettingsItem.Clicked += async (s, e) => await page.Navigation.PushModalAsync(new Views.GeneralSettingsPage());
            var textAISettingsItem = new MenuFlyoutItem { Text = "Text AI Settings" };
            textAISettingsItem.Clicked += async (s, e) => await page.Navigation.PushModalAsync(new Views.AISettingsPage());
            var imageAISettingsItem = new MenuFlyoutItem { Text = "Image AI Settings" };
            imageAISettingsItem.Clicked += async (s, e) => await page.Navigation.PushModalAsync(new Views.ImageAISettingsPage());
            settingsMenu.Add(generalSettingsItem);
            settingsMenu.Add(textAISettingsItem);
            settingsMenu.Add(imageAISettingsItem);

            // Help Menu
            var helpMenu = new MenuBarItem { Text = "Help" };
            var aboutItem = new MenuFlyoutItem { Text = "About RagNext" };
            aboutItem.Clicked += async (s, e) => await page.DisplayAlert("About", "RagNext - Premium Story Engine & RPG Designer\nVersion 1.0\nPowered by Gemini API.", "OK");
            helpMenu.Add(aboutItem);

            page.MenuBarItems.Add(fileMenu);
            page.MenuBarItems.Add(editMenu);
            page.MenuBarItems.Add(settingsMenu);
            page.MenuBarItems.Add(helpMenu);
        }

        private static async Task CreateNewGameFlowAsync(ContentPage page)
        {
            string? name = await page.DisplayPromptAsync("New Game", "Enter the game name", "OK", "Cancel", placeholder: "My Game");
            if (name == null) return;

            name = name.Trim();
            if (string.IsNullOrEmpty(name))
            {
                await page.DisplayAlert("Validation", "Name cannot be empty.", "OK");
                return;
            }

            var game = Game.CreateNew(name, Environment.UserName ?? "Unknown");
            game.Player = new Player { Name = "Player" };
            App.CurrentGame = game;

            try
            {
                await GameStorage.SaveAsync(game, name);
                await page.DisplayAlert("Success", $"Created new game '{name}' successfully.", "OK");
                await Shell.Current.GoToAsync("//main");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Save Error", $"Failed to save game: {ex.Message}", "OK");
            }
        }

        private static async Task SaveGameFlowAsync(ContentPage page)
        {
            var game = App.CurrentGame;
            if (game == null)
            {
                await page.DisplayAlert("Save Error", "No active game to save.", "OK");
                return;
            }

            if (game.Player is null)
                game.Player = new Player { Name = "Player" };

            var defaultName = string.IsNullOrWhiteSpace(game.FileName) ? (string.IsNullOrWhiteSpace(game.Title) ? "save" : game.Title) : game.FileName;
            var name = await page.DisplayPromptAsync("Save Game", "Enter save name", "OK", "Cancel", placeholder: defaultName, initialValue: defaultName);
            if (name == null) return;

            string cleanName = name.Trim();
            if (string.IsNullOrWhiteSpace(cleanName)) return;

            if (string.IsNullOrWhiteSpace(game.Title))
            {
                game.Title = cleanName;
            }

            try
            {
                await GameStorage.SaveAsync(game, cleanName, isExplicitUserSave: true);
                await page.DisplayAlert("Saved", "Game saved successfully.", "OK");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Error", $"Failed to save game: {ex.Message}", "OK");
            }
        }

        private static async Task LoadGameFlowAsync(ContentPage page)
        {
            try
            {
                var saves = await GameStorage.ListSavesAsync();
                if (saves == null || saves.Length == 0)
                {
                    bool create = await page.DisplayAlert("No Saved Game", "No saved games found. Would you like to create a new one?", "Yes", "No");
                    if (create)
                    {
                        await CreateNewGameFlowAsync(page);
                    }
                    return;
                }

                string choice = await page.DisplayActionSheet("Select Saved Game to Load", "Cancel", null, saves);
                if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                    return;

                var loaded = await GameStorage.LoadAsync(choice);
                if (loaded is null)
                {
                    await page.DisplayAlert("Load Error", "Failed to parse the selected save file.", "OK");
                    return;
                }

                App.CurrentGame = loaded;
                await page.DisplayAlert("Loaded", $"Successfully loaded game '{choice}'.", "OK");
                await Shell.Current.GoToAsync("//main");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Load Error", $"Failed to list or load games: {ex.Message}", "OK");
            }
        }

        private static async Task PublishGameFlowAsync(ContentPage page)
        {
            var game = App.CurrentGame;
            if (game == null)
            {
                await page.DisplayAlert("Error", "No active game to publish.", "OK");
                return;
            }
            var popup = new Views.Popups.PublishGamePopup(game);
            await page.ShowPopupAsync(popup);
        }
    }
}
