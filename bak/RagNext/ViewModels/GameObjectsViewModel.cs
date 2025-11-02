using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class GameObjectsViewModel : BaseViewModel
    {
        private readonly ObservableCollection<GameObject> _emptyObjects = new();

        public ObservableCollection<GameObject> Objects => App.CurrentGame?.Objects ?? _emptyObjects;

        public GameObjectsViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Objects)));
        }
    }
}