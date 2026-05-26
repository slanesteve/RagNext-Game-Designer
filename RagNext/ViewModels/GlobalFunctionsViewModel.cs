using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class GlobalFunctionsViewModel : BaseViewModel
    {
        private readonly ObservableCollection<GlobalFunction> _empty = new();

        public ObservableCollection<GlobalFunction> Functions => App.CurrentGame?.Functions ?? _empty;

        public GlobalFunctionsViewModel()
        {
            App.GameChanged += OnGameChanged;
        }

        private void OnGameChanged(Game? _)
        {
            MainThread.BeginInvokeOnMainThread(() => OnPropertyChanged(nameof(Functions)));
        }
    }
}
