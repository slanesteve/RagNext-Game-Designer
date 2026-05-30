using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class CharactersViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<Character> _empty = new();

        public ObservableCollection<Character> Characters => App.CurrentGame?.Characters ?? _empty;

        public ICommand AddCharacterCommand { get; }

        public CharactersViewModel(IGameStorage storage)
        {
            _storage = storage;

            App.GameChanged += OnGameChanged;

            AddCharacterCommand = new Command(async () =>
            {
                var newChar = new Character { Id = Guid.NewGuid(), Name = "New Character", Description = "A newly created character." };

                if (App.CurrentGame?.Characters is not null)
                {
                    App.CurrentGame.Characters.Add(newChar);
                    await _storage.SaveAsync(App.CurrentGame, "autosave");
                    OnPropertyChanged(nameof(Characters));
                }
                else
                {
                    _empty.Add(newChar);
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Characters)));
        }
    }
}
