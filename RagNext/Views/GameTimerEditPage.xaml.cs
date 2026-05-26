#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagsCore.Services;
using RagNext.Services;

namespace RagNext.Views
{
    [QueryProperty(nameof(TimerId), "timerId")]
    public partial class GameTimerEditPage : ContentPage
    {
        public string? TimerId { set { _ = SetTimerAsync(value); } }

        // A collection containing only this GameTimer (which inherits from Action)
        // so we can bind it directly to the ActionTreeView's generic Actions property!
        public ObservableCollection<RagsCore.Models.Action> SingleTimerCollection { get; } = new();

        private readonly IAIChatService? _ai;

        public GameTimerEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
            RagNext.Services.MenuHelper.PopulateMenuBar(this);

            TimerTreeView.Actions = SingleTimerCollection;
        }

        private async System.Threading.Tasks.Task SetTimerAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var timer = game?.Timers?.FirstOrDefault(t => t.Id == id);
            if (timer is null)
            {
                await DisplayAlert("Not found", "Timer not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = timer;

            SingleTimerCollection.Clear();
            SingleTimerCollection.Add(timer);
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

        private async void OnAskAIClicked(object sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI Helper", "AI service not configured.", "OK");
                return;
            }

            if (sender is not Button btn || btn.CommandParameter is not View targetView) return;

            string prompt = string.Empty;
            if (targetView is Entry entry)
            {
                prompt = $"Suggest a short, clean, descriptive C#/script timer name for: {entry.Text}";
            }

            if (string.IsNullOrWhiteSpace(prompt)) return;

            var originalText = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";

            try
            {
                var response = await _ai.AskAsync(prompt);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    var cleanVal = response.Trim().Replace("\"", "").Replace("`", "");
                    if (targetView is Entry targetEntry) targetEntry.Text = cleanVal;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("AI Error", ex.Message, "OK");
            }
            finally
            {
                btn.Text = originalText;
                btn.IsEnabled = true;
            }
        }

        private void OnDetailsTabClicked(object sender, EventArgs e)
        {
            DetailsTabBorder.BackgroundColor = Color.FromArgb("#512BD4");
            DetailsTabLabel.TextColor = Colors.White;

            ActionsTabBorder.BackgroundColor = Colors.Transparent;
            ActionsTabLabel.TextColor = Colors.Gray;

            DetailsScrollView.IsVisible = true;
            ActionsContainer.IsVisible = false;
        }

        private void OnActionsTabClicked(object sender, EventArgs e)
        {
            ActionsTabBorder.BackgroundColor = Color.FromArgb("#512BD4");
            ActionsTabLabel.TextColor = Colors.White;

            DetailsTabBorder.BackgroundColor = Colors.Transparent;
            DetailsTabLabel.TextColor = Colors.Gray;

            DetailsScrollView.IsVisible = false;
            ActionsContainer.IsVisible = true;
        }
    }
}
