using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class StatusBarViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<StatusBarElement> _empty = new();

        public ObservableCollection<StatusBarElement> Elements => App.CurrentGame?.StatusBarElements ?? _empty;

        private StatusBarElement? _selectedElement;
        public StatusBarElement? SelectedElement
        {
            get => _selectedElement;
            set
            {
                if (SetProperty(ref _selectedElement, value))
                {
                    OnPropertyChanged(nameof(IsElementSelected));
                }
            }
        }

        public bool IsElementSelected => SelectedElement != null;

        public IEnumerable<MediaAsset> ImageMediaAssets => App.CurrentGame?.MediaAssets.Where(m => m.Kind == MediaKind.Image) ?? Enumerable.Empty<MediaAsset>();

        public ICommand AddElementCommand { get; }
        public ICommand DeleteElementCommand { get; }

        public StatusBarViewModel(IGameStorage storage)
        {
            _storage = storage;
            App.GameChanged += OnGameChanged;

            AddElementCommand = new Command(async () =>
            {
                var newElem = new StatusBarElement
                {
                    Id = Guid.NewGuid(),
                    Name = "health",
                    VisualOption = "TextOnly",
                    Text = "Health: 100",
                    TextColor = "#FF0000",
                    IsVisible = true
                };

                if (App.CurrentGame?.StatusBarElements is not null)
                {
                    App.CurrentGame.StatusBarElements.Add(newElem);
                    SelectedElement = newElem;
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Elements));
                }
            });

            DeleteElementCommand = new Command<StatusBarElement>(async (e) =>
            {
                if (e is null) return;
                if (App.CurrentGame?.StatusBarElements is not null)
                {
                    App.CurrentGame.StatusBarElements.Remove(e);
                    if (SelectedElement == e)
                    {
                        SelectedElement = null;
                    }
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Elements));
                }
            });
        }

        public async void SaveChanges()
        {
            if (App.CurrentGame != null)
            {
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
            }
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(Elements));
                OnPropertyChanged(nameof(ImageMediaAssets));
                if (SelectedElement != null && App.CurrentGame != null)
                {
                    // Update selection check
                    if (!App.CurrentGame.StatusBarElements.Contains(SelectedElement))
                    {
                        SelectedElement = null;
                    }
                }
            });
        }
    }
}
