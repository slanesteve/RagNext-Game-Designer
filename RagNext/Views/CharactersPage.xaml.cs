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

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton b && b.CommandParameter is Character ch)
            {
                var game = App.CurrentGame;
                if (game is null) return;

                var refs = ValidationEngine.TraceReferences(game, ch.Id, ch.Name);
                string refWarning = refs.Count > 0
                    ? $"\n\n⚠️ WARNING: Active references to this character were found:\n• " + string.Join("\n• ", refs.Take(5)) + (refs.Count > 5 ? $"\n...and {refs.Count - 5} more." : "") + "\n\nDeleting this character will break these references!"
                    : "\n\nNo active references to this character were found.";

                var confirm = await DisplayAlert("Delete Character",
                    $"Are you sure you want to delete character '{ch.Name}'?{refWarning}",
                    "Delete", "Cancel");

                if (!confirm) return;

                if (game.Characters is not null)
                {
                    game.Characters.Remove(ch);
                    try
                    {
                        await GameStorage.SaveAsync(game);
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"Failed to auto-save after delete: {ex.Message}", "OK");
                    }
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await System.Threading.Tasks.Task.Delay(100);
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
        }
    }
}