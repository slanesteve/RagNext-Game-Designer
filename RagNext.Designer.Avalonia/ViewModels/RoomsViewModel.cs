using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class RoomsViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<Room> _emptyRooms = new();

        public ObservableCollection<Room> Rooms => App.CurrentGame?.Rooms ?? _emptyRooms;

        public ICommand AddRoomCommand { get; }
        public ICommand DeleteRoomCommand { get; }

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

            DeleteRoomCommand = new Command<Room>(async (room) =>
            {
                if (room is null) return;
                if (App.CurrentGame?.Rooms is not null)
                {
                    App.CurrentGame.Rooms.Remove(room);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Rooms));
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Rooms)));
        }
    }
}
