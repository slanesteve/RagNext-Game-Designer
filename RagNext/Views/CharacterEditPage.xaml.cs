using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    [QueryProperty(nameof(CharacterId), "characterId")]
    public partial class CharacterEditPage : ContentPage
    {
        public string? CharacterId { set { _ = SetCharacterAsync(value); } }

        public CharacterEditPage()
        {
            InitializeComponent();
        }

        private async System.Threading.Tasks.Task SetCharacterAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var ch = game?.Characters?.FirstOrDefault(c => c.Id == id);
            if (ch is null)
            {
                await DisplayAlert("Not found", "Character not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = ch;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var game = App.CurrentGame;
            if (game is null)
            {
                await DisplayAlert("Save", "No game loaded to save.", "OK");
                return;
            }

            try
            {
                await GameStorage.SaveAsync(game, string.IsNullOrWhiteSpace(game.Title) ? $"save_{DateTime.Now:yyyyMMddHHmmss}" : game.Title);
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}