using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class CharactersViewModel : BaseViewModel
    {
        private readonly ObservableCollection<Character> _empty = new();

        public ObservableCollection<Character> Characters => App.CurrentGame?.Characters ?? _empty;

        public CharactersViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Characters)));
        }
    }
}