using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using RagsCore.Models;
using RagNext.Services;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class GameTimersPage : ContentPage
    {
        public GameTimersPage()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetRequiredService<GameTimersViewModel>();
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

            var timer = new GameTimer { Name = $"Timer_{game.Timers.Count + 1}", IntervalSeconds = 60.0 };
            game.Timers.Add(timer);

            await Shell.Current.GoToAsync("GameTimerEdit", new Dictionary<string, object>
            {
                ["timerId"] = timer.Id.ToString()
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
            if (e.CurrentSelection[0] is not GameTimer timer) return;

            TimersView.SelectedItem = null;
            await Shell.Current.GoToAsync("GameTimerEdit", new Dictionary<string, object>
            {
                ["timerId"] = timer.Id.ToString()
            });
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (sender is ImageButton b && b.CommandParameter is GameTimer timer)
            {
                var game = App.CurrentGame;
                if (game is null) return;

                var refs = ValidationEngine.TraceReferences(game, timer.Id, timer.Name);
                string refWarning = refs.Count > 0
                    ? $"\n\n⚠️ WARNING: Active references to this timer were found:\n• " + string.Join("\n• ", refs.Take(5)) + (refs.Count > 5 ? $"\n...and {refs.Count - 5} more." : "") + "\n\nDeleting this timer will break these references!"
                    : "\n\nNo active references to this timer were found.";

                var confirm = await DisplayAlert("Delete Timer",
                    $"Are you sure you want to delete timer '{timer.Name}'?{refWarning}",
                    "Delete", "Cancel");

                if (!confirm) return;

                if (game.Timers is not null)
                {
                    game.Timers.Remove(timer);
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
