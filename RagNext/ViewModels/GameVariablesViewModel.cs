using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class GameVariablesViewModel : BaseViewModel
    {
        private readonly ObservableCollection<GameVariable> _empty = new();

        public ObservableCollection<GameVariable> Variables => App.CurrentGame?.Variables ?? _empty;

        public GameVariablesViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Variables)));
        }
    }
}