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
        public ICommand DeleteCharacterCommand { get; }

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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Characters));
                }
                else
                {
                    _empty.Add(newChar);
                }
            });

            DeleteCharacterCommand = new Command<Character>(async (c) =>
            {
                if (c is null) return;
                if (App.CurrentGame?.Characters is not null)
                {
                    App.CurrentGame.Characters.Remove(c);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Characters));
                }
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Characters)));
        }
    }
}
