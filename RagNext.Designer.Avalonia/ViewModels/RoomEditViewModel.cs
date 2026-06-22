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
                if (MainWindowViewModel.Instance != null)
                {
                    await MainWindowViewModel.Instance.SaveGameAsync();
                }
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
