using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagNext.Designer.Avalonia.Services;
using RagNext.Models;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class CharactersViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<Character> _empty = new();
        private bool _isSortedAscending = false;

        public ObservableCollection<Character> Characters => App.CurrentGame?.Characters ?? _empty;

        public ICommand AddCharacterCommand { get; }
        public ICommand DeleteCharacterCommand { get; }
        public ICommand SortCommand { get; }

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
                    if (MainWindowViewModel.Instance != null)
                    {
                        var targetNode = MainWindowViewModel.Instance.SelectedCharacterTreeNode;
                        EntityFolder? targetFolder = targetNode?.IsFolder == true 
                            ? targetNode.FolderModel 
                            : targetNode?.ParentNode?.FolderModel;
                        if (targetFolder != null)
                        {
                            EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Characters, newChar.Id, targetFolder);
                            await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        }
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        var newNode = MainWindowViewModel.Instance.FindNodeByEntityId(MainWindowViewModel.Instance.CharacterTreeRoots, newChar.Id);
                        if (newNode != null)
                        {
                            MainWindowViewModel.Instance.ExpandParents(newNode);
                            MainWindowViewModel.Instance.SelectedCharacterTreeNode = newNode;
                        }
                        else
                        {
                            MainWindowViewModel.Instance.SelectedCharacter = newChar;
                        }
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
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
                    if (MainWindowViewModel.Instance != null)
                    {
                        EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Characters, c.Id, null);
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        if (MainWindowViewModel.Instance.SelectedCharacter == c)
                        {
                            MainWindowViewModel.Instance.SelectedCharacter = App.CurrentGame.Characters.Count > 0 ? App.CurrentGame.Characters[0] : null;
                        }
                        await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
                    OnPropertyChanged(nameof(Characters));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Characters is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Characters);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, c => c.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, c => c.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Characters.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Characters.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null)
                {
                    MainWindowViewModel.Instance.RebuildEntityTrees();
                    await MainWindowViewModel.Instance.SaveGameAsync();
                }
                OnPropertyChanged(nameof(Characters));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Characters)));
        }
    }
}
