using System;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    public partial class PlayerEditPage : ContentPage
    {
        public PlayerEditPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var game = App.CurrentGame;
            var player = game?.Player;
            if (game is null || player is null) return;

            BindingContext = player;
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