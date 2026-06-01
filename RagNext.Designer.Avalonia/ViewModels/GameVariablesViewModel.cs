using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GameVariablesViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<GameVariable> _empty = new();

        public ObservableCollection<GameVariable> Variables => App.CurrentGame?.Variables ?? _empty;

        public ICommand AddVariableCommand { get; }

        public GameVariablesViewModel(IGameStorage storage)
        {
            _storage = storage;
            App.GameChanged += OnGameChanged;

            AddVariableCommand = new Command(async () =>
            {
                var newVar = new GameVariable { Id = Guid.NewGuid(), Name = "New_Variable", Value = "0" };

                if (App.CurrentGame?.Variables is not null)
                {
                    App.CurrentGame.Variables.Add(newVar);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Variables));
                }
                else
                {
                    _empty.Add(newVar);
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Variables)));
        }
    }
}
