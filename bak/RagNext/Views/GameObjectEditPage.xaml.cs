using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;
using RagNext.Views.Controls;
using RagsCore.Actions;

namespace RagNext.Views
{
    [QueryProperty(nameof(ObjectId), "objectId")]
    public partial class GameObjectEditPage : ContentPage
    {
        public string? ObjectId { set { _ = SetObjectAsync(value); } }

        private CollectionView ActionTreesList;

        public GameObjectEditPage()
        {
            InitializeComponent();
            ActionTreesList = this.FindByName<CollectionView>("ActionTreesList");
        }

        private async System.Threading.Tasks.Task SetObjectAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var obj = game?.Objects?.FirstOrDefault(o => o.Id == id);
            if (obj is null)
            {
                await DisplayAlert("Not found", "Object not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = obj;

            // Provide context for command/condition pickers
            TreeEditor.Game = game!;
            TreeEditor.FocusObject = obj;
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

        private GameObject? Model => BindingContext as GameObject;

        private void OnAddTreeClicked(object sender, EventArgs e)
        {
            if (Model is null) return;
            var tree = new ActionTree { Name = "New Action" };
            Model.ActionTrees.Add(tree);

            // Ensure the editor has a target tree
            ActionTreesList.SelectedItem = tree;
        }

        private void OnRemoveTreeClicked(object sender, EventArgs e)
        {
            if (Model is null) return;
            if (ActionTreesList.SelectedItem is ActionTree tree)
                Model.ActionTrees.Remove(tree);
        }

        private void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Editor binds via x:Reference to SelectedItem
        }
    }
}