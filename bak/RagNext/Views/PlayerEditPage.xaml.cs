using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Actions;
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

            ActionEditor.Game = game;

            if (player.Actions.Count > 0)
            {
                ActionsList.SelectedItem = player.Actions[0];
                ActionEditor.Action = player.Actions[0];
            }
            else
            {
                ActionEditor.Action = null;
            }
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

        private void OnAddActionClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var action = new GameAction { Name = $"Action {player.Actions.Count + 1}" };
            player.Actions.Add(action);

            ActionsList.SelectedItem = action;
            ActionEditor.Action = action;
        }

        private void OnRemoveActionClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;
            if (ActionsList.SelectedItem is not GameAction action) return;

            player.Actions.Remove(action);
            ActionEditor.Action = ActionsList.SelectedItem as GameAction;
        }

        private void OnActionSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ActionEditor.Action = e.CurrentSelection.FirstOrDefault() as GameAction;
        }
    }
}