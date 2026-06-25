using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GlobalFunctionsViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<GlobalFunction> _empty = new();

        public ObservableCollection<GlobalFunction> Functions => App.CurrentGame?.Functions ?? _empty;

        public ICommand AddFunctionCommand { get; }
        public ICommand DeleteFunctionCommand { get; }
        public ICommand SortCommand { get; }

        public GlobalFunctionsViewModel(IGameStorage storage)
        {
            _storage = storage;
            App.GameChanged += OnGameChanged;

            AddFunctionCommand = new Command(async () =>
            {
                var newFunc = new GlobalFunction { Id = Guid.NewGuid(), Name = "NewFunction" };

                if (App.CurrentGame?.Functions is not null)
                {
                    App.CurrentGame.Functions.Add(newFunc);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Functions));
                }
                else
                {
                    _empty.Add(newFunc);
                }
            });

            DeleteFunctionCommand = new Command<GlobalFunction>(async (f) =>
            {
                if (f is null) return;
                if (App.CurrentGame?.Functions is not null)
                {
                    App.CurrentGame.Functions.Remove(f);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Functions));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Functions is null) return;
                var sorted = global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.OrderBy(App.CurrentGame.Functions, f => f.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase));
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Functions.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Functions.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                OnPropertyChanged(nameof(Functions));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Functions)));
        }
    }
}
