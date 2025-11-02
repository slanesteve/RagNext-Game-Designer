using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    [QueryProperty(nameof(VariableId), "variableId")]
    public partial class GameVariableEditPage : ContentPage
    {
        public string? VariableId { set { _ = SetVariableAsync(value); } }

        public GameVariableEditPage()
        {
            InitializeComponent();
        }

        private async System.Threading.Tasks.Task SetVariableAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var variable = game?.Variables?.FirstOrDefault(v => v.Id == id);
            if (variable is null)
            {
                await DisplayAlert("Not found", "Variable not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = variable;
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