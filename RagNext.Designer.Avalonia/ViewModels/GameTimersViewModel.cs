using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagNext.Designer.Avalonia.Services;
using RagNext.Models;
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
                    if (MainWindowViewModel.Instance != null)
                    {
                        var targetNode = MainWindowViewModel.Instance.SelectedTimerTreeNode;
                        EntityFolder? targetFolder = targetNode?.IsFolder == true 
                            ? targetNode.FolderModel 
                            : targetNode?.ParentNode?.FolderModel;
                        if (targetFolder != null)
                        {
                            EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Timers, newTimer.Id, targetFolder);
                            await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        }
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        var newNode = MainWindowViewModel.Instance.FindNodeByEntityId(MainWindowViewModel.Instance.TimerTreeRoots, newTimer.Id);
                        if (newNode != null)
                        {
                            MainWindowViewModel.Instance.ExpandParents(newNode);
                            MainWindowViewModel.Instance.SelectedTimerTreeNode = newNode;
                        }
                        else
                        {
                            MainWindowViewModel.Instance.SelectedTimer = newTimer;
                        }
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
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
                    if (MainWindowViewModel.Instance != null)
                    {
                        EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Timers, t.Id, null);
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        if (MainWindowViewModel.Instance.SelectedTimer == t)
                        {
                            MainWindowViewModel.Instance.SelectedTimer = App.CurrentGame.Timers.Count > 0 ? App.CurrentGame.Timers[0] : null;
                        }
                        await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
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
                if (MainWindowViewModel.Instance != null)
                {
                    MainWindowViewModel.Instance.RebuildEntityTrees();
                    await MainWindowViewModel.Instance.SaveGameAsync();
                }
                OnPropertyChanged(nameof(Timers));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Timers)));
        }
    }
}
