using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class GameObjectsPage : ContentPage
    {
        public GameObjectsPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<GameObjectsViewModel>();
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

            var obj = new GameObject { Name = "New Object", Description = string.Empty };
            game.Objects.Add(obj);

            await Shell.Current.GoToAsync("GameObjectEdit", new Dictionary<string, object>
            {
                ["objectId"] = obj.Id.ToString()
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
            if (e.CurrentSelection[0] is not GameObject obj) return;

            ObjectsView.SelectedItem = null; // allow reselecting same item
            await Shell.Current.GoToAsync("GameObjectEdit", new Dictionary<string, object>
            {
                ["objectId"] = obj.Id.ToString()
            });
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton b && b.CommandParameter is GameObject obj)
            {
                var game = App.CurrentGame;
                if (game is null) return;

                var refs = ValidationEngine.TraceReferences(game, obj.Id, obj.Name);
                string refWarning = refs.Count > 0
                    ? $"\n\n⚠️ WARNING: Active references to this object were found:\n• " + string.Join("\n• ", refs.Take(5)) + (refs.Count > 5 ? $"\n...and {refs.Count - 5} more." : "") + "\n\nDeleting this object will break these references!"
                    : "\n\nNo active references to this object were found.";

                var confirm = await DisplayAlert("Delete Object",
                    $"Are you sure you want to delete object '{obj.Name}'?{refWarning}",
                    "Delete", "Cancel");

                if (!confirm) return;

                if (game.Objects is not null)
                {
                    game.Objects.Remove(obj);
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
    }
}