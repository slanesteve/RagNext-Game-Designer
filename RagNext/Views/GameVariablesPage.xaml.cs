using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class GameVariablesPage : ContentPage
    {
        public GameVariablesPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<GameVariablesViewModel>();
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

            var variable = new GameVariable { Name = "NewVariable", Type = "string", Value = string.Empty };
            game.Variables.Add(variable);

            await Shell.Current.GoToAsync("GameVariableEdit", new Dictionary<string, object>
            {
                ["variableId"] = variable.Id.ToString()
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
            if (e.CurrentSelection[0] is not GameVariable variable) return;

            VariablesView.SelectedItem = null;
            await Shell.Current.GoToAsync("GameVariableEdit", new Dictionary<string, object>
            {
                ["variableId"] = variable.Id.ToString()
            });
        }
    }
}