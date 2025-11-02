using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

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

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}