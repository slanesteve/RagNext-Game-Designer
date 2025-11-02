using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.ViewModels
{
    public class RoomsViewModel : BaseViewModel
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<Room> _emptyRooms = new();

        public ObservableCollection<Room> Rooms => App.CurrentGame?.Rooms ?? _emptyRooms;

        public ICommand AddRoomCommand { get; }

        public RoomsViewModel(IGameStorage storage)
        {
            _storage = storage;

            App.GameChanged += OnGameChanged;

            AddRoomCommand = new Command(async () =>
            {
                var newRoom = new Room { Id = Guid.NewGuid(), Name = "New Room" };

                if (App.CurrentGame?.Rooms is not null)
                {
                    App.CurrentGame.Rooms.Add(newRoom);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                }
                else
                {
                    _emptyRooms.Add(newRoom);
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Rooms)));
        }
    }
}