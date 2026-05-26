using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class GameTimersViewModel : BaseViewModel
    {
        private readonly ObservableCollection<GameTimer> _empty = new();

        public ObservableCollection<GameTimer> Timers => App.CurrentGame?.Timers ?? _empty;

        public GameTimersViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Timers)));
        }
    }
}
