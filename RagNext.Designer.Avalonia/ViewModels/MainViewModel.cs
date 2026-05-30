using System.Windows.Input;
using RagsCore.Models;
using RagNext.Designer.Avalonia.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Game? _game;
        public Game? CurrentGame
        {
            get => _game;
            set
            {
                // Swap the instance and check if it changed
                if (SetProperty(ref _game, value))
                {
                    // CRITICAL: Explicitly broadcast that the flat and nested property paths have changed.
                    // This forces the XAML UI text entries to grab the new values upon loading a file!
                    OnPropertyChanged(nameof(CurrentGame));
                    OnPropertyChanged(nameof(GameTitle));
                    OnPropertyChanged(nameof(GameAuthor));
                    OnPropertyChanged(nameof(GameVersion));
                    OnPropertyChanged(nameof(GameIconPath));
                }
            }
        }

        public string GameIconPath
        {
            get => CurrentGame?.IconPath ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.IconPath != value)
                {
                    CurrentGame.IconPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GameTitle
        {
            get => CurrentGame?.Title ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Title != value)
                {
                    CurrentGame.Title = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GameAuthor
        {
            get => CurrentGame?.Author ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Author != value)
                {
                    CurrentGame.Author = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GameVersion
        {
            get => CurrentGame?.Version ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Version != value)
                {
                    CurrentGame.Version = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand NewGameCommand { get; }

        public MainViewModel()
        {
            // SAFE INITIALIZATION: Do NOT automatically construct a new Game() here at startup.
            // Let it pull the current application state safely, or remain null until the UI settles.
            CurrentGame = App.CurrentGame;

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
