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
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Functions));
                }
                else
                {
                    _empty.Add(newFunc);
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Functions)));
        }
    }
}
