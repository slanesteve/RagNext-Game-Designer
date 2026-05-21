using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    [QueryProperty(nameof(CharacterId), "characterId")]
    public partial class CharacterEditPage : ContentPage
    {
        public string? CharacterId { set { _ = SetCharacterAsync(value); } }

        private CollectionView? AttributesListView => this.FindByName<CollectionView>("AttributesList");
        private CollectionView? InventoryListView => this.FindByName<CollectionView>("InventoryList");

        private readonly IAIChatService? _ai;

        public CharacterEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
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
                await DisplayAlert("Saved", "Game saved successfully.", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        }



        private sealed class DisposeAction : IDisposable
        {
            private readonly System.Action _a;
            public DisposeAction(System.Action a) => _a = a;
            public void Dispose() => _a();
        }

        private IDisposable StartSpinner(Button btn)
        {
            var original = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";
            var anim = new Animation(v => btn.Rotation = v, 0, 360);
            anim.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);
            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = 0;
                btn.Text = original;
                btn.IsEnabled = true;
            });
        }

        private async void OnAddAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Character ch) return;

            var name = await DisplayPromptAsync("New Attribute", "Enter attribute name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var value = await DisplayPromptAsync("New Attribute", "Enter attribute value (optional):");
            ch.Attributes.Add(new CustomAttribute { Name = name.Trim(), Value = string.IsNullOrWhiteSpace(value) ? null : value });
        }

        private async void OnRemoveAttributeClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Character ch) return;

            var list = AttributesListView;
            var selected = list?.SelectedItem as CustomAttribute;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an attribute to remove.", "OK");
                return;
            }

            ch.Attributes.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private string? GetInventoryOwnerName(GameObject item)
        {
            var game = App.CurrentGame;
            if (game is null) return null;

            // Check if player has it
            if (game.Player is not null && game.Player.Inventory.Any(o => o.Id == item.Id))
            {
                return "the Player";
            }

            // Check if any character has it
            if (game.Characters is not null)
            {
                foreach (var ch in game.Characters)
                {
                    if (ch.Inventory.Any(o => o.Id == item.Id))
                    {
                        return $"Character '{ch.Name}'";
                    }
                }
            }

            return null;
        }

        private async void OnAddInventoryClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Character ch || App.CurrentGame?.Objects is null) return;

            var ownedIds = ch.Inventory.Select(o => o.Id).ToHashSet();
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
            {
                var owner = GetInventoryOwnerName(selected);
                if (owner is not null)
                {
                    var confirm = await DisplayAlert(
                        "Warning",
                        $"This item is already assigned to {owner}.\n\nAre you sure you want to assign it here as well?",
                        "Yes", "No");
                    if (!confirm) return;
                }
                ch.Inventory.Add(selected);
            }
        }

        private async void OnRemoveInventoryClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Character ch) return;

            var list = InventoryListView;
            var selected = list?.SelectedItem as GameObject;
            if (selected is null)
            {
                await DisplayAlert("Remove", "Select an inventory item to remove.", "OK");
                return;
            }

            ch.Inventory.Remove(selected);
            if (list is not null) list.SelectedItem = null;
        }

        private void OnRemoveIndividualInventoryClicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (BindingContext is not Character ch) return;
            if (btn.BindingContext is not GameObject selected) return;

            ch.Inventory.Remove(selected);
        }

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI", "AI service unavailable.", "OK");
                return;
            }
            if (sender is not Button btn) return;

            await AIAssistHelper.HandleAskAIAsync(this, btn, btn.CommandParameter, _ai);
        }
    }
}