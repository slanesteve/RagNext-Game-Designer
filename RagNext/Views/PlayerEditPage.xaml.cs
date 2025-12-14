using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    public partial class PlayerEditPage : ContentPage
    {
        // Safely access named elements without relying on generated fields
        private CollectionView? InventoryListView => this.FindByName<CollectionView>("InventoryList");
        private CollectionView? AttributesListView => this.FindByName<CollectionView>("AttributesList");

        private readonly IAIChatService? _ai;

        public PlayerEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
            AssignPlayerActions();
            App.GameChanged += (game) => OnGameLoaded(this, game); // ensure repopulate after load
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            AssignPlayerActions(); // refresh when page appears
        }

        private void OnGameLoaded(object? sender, Game e)
        {
            // called when a new game is loaded
            MainThread.BeginInvokeOnMainThread(AssignPlayerActions);
        }

        private void AssignPlayerActions()
        {
            var game = App.CurrentGame;
            var player = game?.Player;
            var actions = player?.Actions;

            PlayerActionsView.Player = player;
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

        private async void OnAddInventoryClicked(object? sender, EventArgs e)
        {
            if (App.CurrentGame?.Player is not Player player || App.CurrentGame?.Objects is null) return;

            var ownedIds = player.Inventory.Select(o => o.Id).ToHashSet();
            var candidates = App.CurrentGame.Objects.Where(o => !ownedIds.Contains(o.Id)).ToList();
            if (candidates.Count == 0)
            {
                await DisplayAlert("Inventory", "No more objects to add.", "OK");
                return;
            }

            var choice = await DisplayActionSheet(
                "Add item to inventory",
                "Cancel", null,
                candidates.Select(o => o.Name).ToArray());

            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            var selected = candidates.FirstOrDefault(o => o.Name == choice);
            if (selected is not null)
                player.Inventory.Add(selected);
        }

        private async void OnRemoveInventoryClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var list = InventoryListView;
            var selected = list?.SelectedItem as GameObject;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an inventory item to remove.", "OK");
                return;
            }

            player.Inventory.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private async void OnAddAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var name = await DisplayPromptAsync("New Attribute", "Enter attribute name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var value = await DisplayPromptAsync("New Attribute", "Enter attribute value (optional):");
            player.Attributes.Add(new CustomAttribute { Name = name.Trim(), Value = string.IsNullOrWhiteSpace(value) ? null : value });
        }

        private async void OnRemoveAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Player player) return;

            var list = AttributesListView;
            var selected = list?.SelectedItem as CustomAttribute;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an attribute to remove.", "OK");
                return;
            }

            player.Attributes.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private sealed class DisposeAction : IDisposable
        {
            private readonly System.Action _action;
            public DisposeAction(System.Action action) => _action = action;
            public void Dispose() => _action();
        }

        private IDisposable StartSpinner(Button btn)
        {
            var originalText = btn.Text;
            var originalRotation = btn.Rotation;
            btn.IsEnabled = false;
            btn.Text = "⟳";

            var animation = new Animation(v => btn.Rotation = v, 0, 360);
            animation.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);

            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = originalRotation;
                btn.Text = originalText;
                btn.IsEnabled = true;
            });
        }

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI", "AI service is not available.", "OK");
                return;
            }

            if (sender is not Button btn)
                return;

            await AIAssistHelper.HandleAskAIAsync(this, btn, btn.CommandParameter, _ai);
        }
    }
}