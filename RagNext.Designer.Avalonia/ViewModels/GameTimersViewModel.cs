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
        private bool _isSortedAscending = false;

        public ObservableCollection<GameTimer> Timers => App.CurrentGame?.Timers ?? _empty;

        public ICommand AddTimerCommand { get; }
        public ICommand DeleteTimerCommand { get; }
        public ICommand SortCommand { get; }

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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Timers));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Timers is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Timers);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Timers.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Timers.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                OnPropertyChanged(nameof(Timers));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Timers)));
        }
    }
}
