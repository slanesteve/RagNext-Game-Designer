using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using RagNext.Designer.Avalonia.Services;
using RagNext.Models;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class GlobalFunctionsViewModel : ViewModelBase
    {
        private readonly IGameStorage _storage;
        private readonly ObservableCollection<GlobalFunction> _empty = new();
        private bool _isSortedAscending = false;

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
                    if (MainWindowViewModel.Instance != null)
                    {
                        var targetNode = MainWindowViewModel.Instance.SelectedFunctionTreeNode;
                        EntityFolder? targetFolder = targetNode?.IsFolder == true 
                            ? targetNode.FolderModel 
                            : targetNode?.ParentNode?.FolderModel;
                        if (targetFolder != null)
                        {
                            EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Functions, newFunc.Id, targetFolder);
                            await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        }
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        var newNode = MainWindowViewModel.Instance.FindNodeByEntityId(MainWindowViewModel.Instance.FunctionTreeRoots, newFunc.Id);
                        if (newNode != null)
                        {
                            MainWindowViewModel.Instance.ExpandParents(newNode);
                            MainWindowViewModel.Instance.SelectedFunctionTreeNode = newNode;
                        }
                        else
                        {
                            MainWindowViewModel.Instance.SelectedFunction = newFunc;
                        }
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
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
                    if (MainWindowViewModel.Instance != null)
                    {
                        EntityTreeHelper.MoveEntityToFolder(MainWindowViewModel.Instance.EntityTreeDoc.Functions, f.Id, null);
                        MainWindowViewModel.Instance.RebuildEntityTrees();
                        if (MainWindowViewModel.Instance.SelectedFunction == f)
                        {
                            MainWindowViewModel.Instance.SelectedFunction = App.CurrentGame.Functions.Count > 0 ? App.CurrentGame.Functions[0] : null;
                        }
                        await MainWindowViewModel.Instance.SaveEntityTreeAsync();
                        await MainWindowViewModel.Instance.SaveGameAsync();
                    }
                    OnPropertyChanged(nameof(Functions));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Functions is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Functions);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, f => f.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, f => f.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Functions.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Functions.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null)
                {
                    MainWindowViewModel.Instance.RebuildEntityTrees();
                    await MainWindowViewModel.Instance.SaveGameAsync();
                }
                OnPropertyChanged(nameof(Functions));
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Functions)));
        }
    }
}
