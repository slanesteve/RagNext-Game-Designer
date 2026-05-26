using System.Linq;
using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views
{
    public partial class RoomsPage : ContentPage
    {
        public RoomsPage(RoomsViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            RagNext.Services.MenuHelper.PopulateMenuBar(this);
        }

        private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
        {
            var room = e.CurrentSelection.FirstOrDefault() as Room;
            if (room is null) return;
            await Shell.Current.GoToAsync($"RoomEdit?roomId={room.Id}");
        }

        private async void OnEditClicked(object sender, EventArgs e)
        {
            if (sender is Button b && b.CommandParameter is Room room)
            {
                await Shell.Current.GoToAsync($"RoomEdit?roomId={room.Id}");
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
                var fileName = string.IsNullOrWhiteSpace(game.Title) ? $"rooms_{System.DateTime.Now:yyyyMMddHHmmss}" : game.Title;
                await GameStorage.SaveAsync(game, fileName);
                await DisplayAlert("Saved", "Game saved.", "OK");
            }
            catch (System.Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        }
    }
}