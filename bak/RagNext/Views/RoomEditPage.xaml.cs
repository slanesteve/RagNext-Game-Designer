using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models;
using RagNext.Services;
using RagNext.Views.Controls;

namespace RagNext.Views
{
    [QueryProperty(nameof(RoomId), "roomId")]
    public partial class RoomEditPage : ContentPage
    {
        public string? RoomId { set { _ = SetRoomAsync(value); } }

        public RoomEditPage()
        {
            InitializeComponent();
        }

        private async System.Threading.Tasks.Task SetRoomAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!Guid.TryParse(value, out var id))
                return;

            var game = App.CurrentGame;
            var room = game?.Rooms?.FirstOrDefault(r => r.Id == id);
            if (room is null)
            {
                await DisplayAlert("Not found", "Room not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = room;

            // Legacy wiring only if editor exists
            var editor = this.FindByName<ActionEditorView>("ActionEditor");
            if (editor is not null)
            {
                editor.Game = game!;
                editor.Room = room;
                if (room.Actions.Count > 0)
                {
                    var actionsList = this.FindByName<CollectionView>("ActionsList");
                    actionsList?.ScrollTo(room.Actions[0], position: ScrollToPosition.Center, animate: false);
                    editor.Action = room.Actions[0];
                }
                else
                {
                    editor.Action = null;
                }
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
            if (BindingContext is not Room room) return;

            var action = new GameAction { Name = $"Action {room.Actions.Count + 1}" };
            room.Actions.Add(action);

            ActionsList.SelectedItem = action;
            ActionEditor.Action = action;
        }

        private void OnRemoveActionClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not Room room) return;
            if (ActionsList.SelectedItem is not GameAction action) return;

            room.Actions.Remove(action);
            ActionEditor.Action = ActionsList.SelectedItem as GameAction;
        }

        private void OnActionSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ActionEditor.Action = e.CurrentSelection.FirstOrDefault() as GameAction;
        }

        private Room? Model => BindingContext as Room;

        private void OnAddTreeClicked(object sender, EventArgs e)
        {
            if (Model is null) return;
            Model.ActionTrees.Add(new ActionTree { Name = "New Action" });
        }

        private void OnRemoveTreeClicked(object sender, EventArgs e)
        {
            if (Model is null) return;
            if (ActionTreesList.SelectedItem is ActionTree tree)
                Model.ActionTrees.Remove(tree);
        }

        private void OnTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No-op, editor binds via x:Reference to SelectedItem
        }

        private ActionEditorView ActionEditor => this.FindByName<ActionEditorView>("ActionEditor");
        private ListView ActionsList => this.FindByName<ListView>("ActionsList");
        private ListView ActionTreesListView => this.FindByName<ListView>("ActionTreesList");
    }
}