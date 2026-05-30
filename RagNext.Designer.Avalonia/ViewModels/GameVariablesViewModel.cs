using System.Collections.ObjectModel;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GameVariablesViewModel : ViewModelBase
    {
        private readonly ObservableCollection<GameVariable> _empty = new();

        public ObservableCollection<GameVariable> Variables => App.CurrentGame?.Variables ?? _empty;

        public GameVariablesViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Variables)));
        }
    }
}
