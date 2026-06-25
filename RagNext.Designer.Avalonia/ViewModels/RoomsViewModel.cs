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
        private bool _isSortedAscending = false;

        public ObservableCollection<Room> Rooms => App.CurrentGame?.Rooms ?? _emptyRooms;

        public ICommand AddRoomCommand { get; }
        public ICommand DeleteRoomCommand { get; }
        public ICommand SortCommand { get; }

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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Rooms));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Rooms is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Rooms);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Rooms.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Rooms.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                OnPropertyChanged(nameof(Rooms));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Rooms)));
        }
    }
}
