using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class GameObjectsPage : ContentPage
    {
        public GameObjectsPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<GameObjectsViewModel>();
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
        }

        private async void OnAddClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Error", "No game loaded.", "OK");
                return;
            }

            var obj = new GameObject { Name = "New Object", Description = string.Empty };
            game.Objects.Add(obj);

            await Shell.Current.GoToAsync("GameObjectEdit", new Dictionary<string, object>
            {
                ["objectId"] = obj.Id.ToString()
            });
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Error", "No game loaded.", "OK");
                return;
            }

            try
            {
                await GameStorage.SaveAsync(game, string.IsNullOrWhiteSpace(game.Title) ? $"save_{DateTime.Now:yyyyMMddHHmmss}" : game.Title);
                await DisplayAlert("Saved", "Game saved.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        }

        private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0) return;
            if (e.CurrentSelection[0] is not GameObject obj) return;

            ObjectsView.SelectedItem = null; // allow reselecting same item
            await Shell.Current.GoToAsync("GameObjectEdit", new Dictionary<string, object>
            {
                ["objectId"] = obj.Id.ToString()
            });
        }
    }
}