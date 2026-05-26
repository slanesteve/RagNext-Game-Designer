using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class GlobalFunctionsPage : ContentPage
    {
        public GlobalFunctionsPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<GlobalFunctionsViewModel>();
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

            var func = new GlobalFunction { Name = $"Function_{game.Functions.Count + 1}" };
            game.Functions.Add(func);

            await Shell.Current.GoToAsync("GlobalFunctionEdit", new Dictionary<string, object>
            {
                ["functionId"] = func.Id.ToString()
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
            if (e.CurrentSelection[0] is not GlobalFunction func) return;

            FunctionsView.SelectedItem = null;
            await Shell.Current.GoToAsync("GlobalFunctionEdit", new Dictionary<string, object>
            {
                ["functionId"] = func.Id.ToString()
            });
        }
    }
}
