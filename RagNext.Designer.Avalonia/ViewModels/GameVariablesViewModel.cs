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
        private bool _isSortedAscending = false;

        public ObservableCollection<GameVariable> Variables => App.CurrentGame?.Variables ?? _empty;
        public ObservableCollection<string> VariableTypes { get; } = new() { "string", "number", "bool", "datetime", "array" };

        public ICommand AddVariableCommand { get; }
        public ICommand DeleteVariableCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand AddColumnCommand { get; }
        public ICommand RemoveColumnCommand { get; }
        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }

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
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Variables));
                }
                else
                {
                    _empty.Add(newVar);
                }
            });

            DeleteVariableCommand = new Command<GameVariable>(async (v) =>
            {
                if (v is null) return;
                if (App.CurrentGame?.Variables is not null)
                {
                    App.CurrentGame.Variables.Remove(v);
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                    OnPropertyChanged(nameof(Variables));
                }
            });

            SortCommand = new Command(async () =>
            {
                if (App.CurrentGame?.Variables is null) return;
                _isSortedAscending = !_isSortedAscending;
                var query = global::System.Linq.Enumerable.AsEnumerable(App.CurrentGame.Variables);
                if (_isSortedAscending)
                {
                    query = global::System.Linq.Enumerable.OrderBy(query, v => v.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    query = global::System.Linq.Enumerable.OrderByDescending(query, v => v.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }
                var sorted = global::System.Linq.Enumerable.ToList(query);
                for (int i = 0; i < sorted.Count; i++)
                {
                    int oldIndex = App.CurrentGame.Variables.IndexOf(sorted[i]);
                    if (oldIndex != i) App.CurrentGame.Variables.Move(oldIndex, i);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                OnPropertyChanged(nameof(Variables));
            });

            AddColumnCommand = new Command<GameVariable>(async (v) =>
            {
                if (v is null) return;
                string newCol = $"Col_{v.Columns.Count + 1}";
                v.Columns.Add(newCol);
                // Pad existing rows with an empty value for the new column
                foreach (var row in v.Rows)
                {
                    row.Add(string.Empty);
                }
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
            });

            RemoveColumnCommand = new Command<Tuple<GameVariable, string>>(async (t) =>
            {
                if (t == null) return;
                var v = t.Item1;
                var col = t.Item2;
                int idx = v.Columns.IndexOf(col);
                if (idx >= 0)
                {
                    v.Columns.RemoveAt(idx);
                    foreach (var row in v.Rows)
                    {
                        if (idx < row.Count) row.RemoveAt(idx);
                    }
                    if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                }
            });

            AddRowCommand = new Command<GameVariable>(async (v) =>
            {
                if (v is null) return;
                var newRow = new ObservableCollection<string>();
                for (int i = 0; i < v.Columns.Count; i++)
                {
                    newRow.Add(string.Empty);
                }
                v.Rows.Add(newRow);
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
            });

            RemoveRowCommand = new Command<Tuple<GameVariable, ObservableCollection<string>>>(async (t) =>
            {
                if (t == null) return;
                var v = t.Item1;
                var row = t.Item2;
                v.Rows.Remove(row);
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
            });
        }

        private void OnGameChanged(Game? _)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Variables)));
        }
    }
}
