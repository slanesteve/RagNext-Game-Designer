using System.Collections.ObjectModel;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GameTimersViewModel : ViewModelBase
    {
        private readonly ObservableCollection<GameTimer> _empty = new();

        public ObservableCollection<GameTimer> Timers => App.CurrentGame?.Timers ?? _empty;

        public GameTimersViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Timers)));
        }
    }
}
