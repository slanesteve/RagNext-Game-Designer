using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GameObjectsViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<GameObject> _emptyObjects = new();
        private bool _isSortedAscending = false;

        public ObservableCollection<GameObject> Objects => App.CurrentGame?.Objects ?? _emptyObjects;

        public ICommand AddObjectCommand { get; }
        public ICommand DeleteObjectCommand { get; }
        public ICommand SortCommand { get; }

        public GameObjectsViewModel(IGameStorage storage)
        {
            _storage = storage;

            App.GameChanged += OnGameChanged;

            AddObjectCommand = new Command(async () =>
            {
                var newObj = new GameObject { Id = Guid.NewGuid(), Name = "New Object", Description = "A newly created object." };

                if (App.CurrentGame?.Objects is not null)
                {
                    App.CurrentGame.Objects.Add(newObj);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Objects));
                }
                else
                {
                    _emptyObjects.Add(newObj);
                }
            });

            DeleteObjectCommand = new Command<GameObject>(async (o) =>
            {
                if (o is null) return;
                if (App.CurrentGame?.Objects is not null)
                {
                    App.CurrentGame.Objects.Remove(o);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Objects));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Objects is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Objects);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, o => o.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, o => o.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Objects.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Objects.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                OnPropertyChanged(nameof(Objects));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Objects)));
        }
    }
}
