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

        public ObservableCollection<GameObject> Objects => App.CurrentGame?.Objects ?? _emptyObjects;

        public ICommand AddObjectCommand { get; }
        public ICommand DeleteObjectCommand { get; }

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
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Objects)));
        }
    }
}
