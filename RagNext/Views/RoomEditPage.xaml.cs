using System;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;
using CommunityToolkit.Maui.Views;
using RagNext.Views.Popups;
using CommunityToolkit.Maui.Extensions;

namespace RagNext.Views
{
    [QueryProperty(nameof(RoomId), "roomId")]
    public partial class RoomEditPage : ContentPage
    {
        public string? RoomId { set { _ = SetRoomAsync(value); } }

        private readonly IAIChatService? _ai;

        public RoomEditPage()
        {
            InitializeComponent();
            _ai = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
        }

        private async System.Threading.Tasks.Task SetRoomAsync(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!Guid.TryParse(value, out var id)) return;

            var game = App.CurrentGame;
            var room = game?.Rooms?.FirstOrDefault(r => r.Id == id);
            if (room is null)
            {
                await DisplayAlert("Not found", "Room not found in current game.", "OK");
                await Navigation.PopAsync();
                return;
            }

            BindingContext = room;
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

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (_ai is null)
            {
                await DisplayAlert("AI", "AI service unavailable.", "OK");
                return;
            }
            if (sender is not Button btn) return;

            await AIAssistHelper.HandleAskAIAsync(this, btn, btn.CommandParameter, _ai);
            //test commits
        }
    }
}