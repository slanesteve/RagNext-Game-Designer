using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GameTimersViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<GameTimer> _empty = new();

        public ObservableCollection<GameTimer> Timers => App.CurrentGame?.Timers ?? _empty;

        public ICommand AddTimerCommand { get; }
        public ICommand DeleteTimerCommand { get; }

        public GameTimersViewModel(IGameStorage storage)
        {
            _storage = storage;
            App.GameChanged += OnGameChanged;

            AddTimerCommand = new Command(async () =>
            {
                var newTimer = new GameTimer { Id = Guid.NewGuid(), Name = "New Timer", IntervalSeconds = 1 };

                if (App.CurrentGame?.Timers is not null)
                {
                    App.CurrentGame.Timers.Add(newTimer);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Timers));
                }
                else
                {
                    _empty.Add(newTimer);
                }
            });

            DeleteTimerCommand = new Command<GameTimer>(async (t) =>
            {
                if (t is null) return;
                if (App.CurrentGame?.Timers is not null)
                {
                    App.CurrentGame.Timers.Remove(t);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Timers));
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Timers)));
        }
    }
}
