using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class CharactersPage : ContentPage
    {
        public CharactersPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<CharactersViewModel>();
        }

        private async void OnAddClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Error", "No game loaded.", "OK");
                return;
            }

            var ch = new Character { Name = "New Character", Description = string.Empty };
            game.Characters.Add(ch);

            await Shell.Current.GoToAsync("CharacterEdit", new Dictionary<string, object>
            {
                ["characterId"] = ch.Id.ToString()
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
            if (e.CurrentSelection[0] is not Character ch) return;

            CharactersView.SelectedItem = null;
            await Shell.Current.GoToAsync("CharacterEdit", new Dictionary<string, object>
            {
                ["characterId"] = ch.Id.ToString()
            });
        }
    }
}