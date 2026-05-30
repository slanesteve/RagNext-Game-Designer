using System.Windows.Input;
using RagsCore.Models;
using RagNext.Designer.Avalonia.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class RoomEditViewModel : ViewModelBase
    {
        private Room? _room;
        public Room? Room
        {
            get => _room;
            set => SetProperty(ref _room, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public RoomEditViewModel()
        {
            // default commands - callers/pages can set Room before use
            SaveCommand = new Command(async () =>
            {
                if (App.CurrentGame is null)
                    return;

                await GameStorage.SaveAsync(App.CurrentGame, string.IsNullOrWhiteSpace(App.CurrentGame.Title) ? $"save_{System.DateTime.Now:yyyyMMddHHmmss}" : App.CurrentGame.Title);
            });

            CancelCommand = new Command(() =>
            {
                // no-op here; pages can handle navigation when they wire this up
            });
        }

        public void Initialize(Room room)
        {
            Room = room;
        }
    }
}
