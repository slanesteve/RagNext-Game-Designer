using System.Windows.Input;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private Game? _game;
        public Game? CurrentGame
        {
            get => _game;
            set => SetProperty(ref _game, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand NewGameCommand { get; }

        public MainViewModel()
        {
            CurrentGame = App.CurrentGame ?? new Game { Id = System.Guid.NewGuid(), Title = "New Game" };
            App.CurrentGame = CurrentGame;

            SaveCommand = new Command(async () =>
            {
                if (CurrentGame is null)
                    return;

                await GameStorage.SaveAsync(CurrentGame, string.IsNullOrWhiteSpace(CurrentGame.Title) ? $"save_{System.DateTime.Now:yyyyMMddHHmmss}" : CurrentGame.Title);
            });

            NewGameCommand = new Command(() =>
            {
                CurrentGame = new Game { Id = System.Guid.NewGuid(), Title = "New Game" };
                App.CurrentGame = CurrentGame;
            });
        }
    }
}