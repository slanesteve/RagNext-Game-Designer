using System.Collections.ObjectModel;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GlobalFunctionsViewModel : ViewModelBase
    {
        private readonly ObservableCollection<GlobalFunction> _empty = new();

        public ObservableCollection<GlobalFunction> Functions => App.CurrentGame?.Functions ?? _empty;

        public GlobalFunctionsViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Functions)));
        }
    }
}
