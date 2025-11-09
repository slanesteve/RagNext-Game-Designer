using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagsCore.Actions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;
using RagNext.Views;
using System.Windows.Input;

namespace RagNext.ViewModels
{
    public sealed class ActionLibraryViewModel : BindableObject
    {
        public enum NodeKind { Action, Condition, Command, Input }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public sealed class Node : BindableObject
        {
            public NodeKind Kind { get; init; }
            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    OnPropertyChanged();
                }
            }
            private string _name = string.Empty;
            public object? Model { get; init; }
            public ObservableCollection<Node> Children { get; } = new();
            public int Level { get; init; }
            private bool _isSelected;
            public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); } }
            private bool _isExpanded;
            public bool IsExpanded { get => _isExpanded; set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); } }
            public Thickness Indent => new(Level * 16, 0, 0, 0);
            public string Icon => Kind switch
            {
                NodeKind.Action => "⚙️",
                NodeKind.Condition => "🔀",
                NodeKind.Command => "➡️",
                NodeKind.Input => "🔧",
                _ => "❓"
            };
            public Node? Parent { get; set; }
        }

        private readonly Game? _game;
        private Node? _selected;
        public Node? Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                if (_selected != null) _selected.IsSelected = false;
                _selected = value;
                if (_selected != null) _selected.IsSelected = true;
                OnPropertyChanged();
                SetEditorForSelected(_selected);
            }
        }

        private EditStepViewModel? _editor;
        public EditStepViewModel? Editor
        {
            get => _editor;
            private set
            {
                if (_editor == value) return;
                _editor = value;
                OnPropertyChanged();
            }
        }

        private EditActionViewModel? _actionEditor;
        public EditActionViewModel? ActionEditor
        {
            get => _actionEditor;
            private set
            {
                if (_actionEditor == value) return;
                _actionEditor = value;
                OnPropertyChanged();
            }
        }

        private void SetEditorForSelected(Node? node)
        {
            Editor = null;
            ActionEditor = null;

            if (node?.Model is StepDefinitionBase step)
            {
                Editor = new EditStepViewModel(step, async () =>
                {
                    node.Name = step.Name;
                    node.Children.Where(c => c.Kind == NodeKind.Input).ToList().ForEach(c => node.Children.Remove(c));
                    foreach (var input in step.Inputs)
                    {
                        var inputNode = new Node
                        {
                            Kind = NodeKind.Input,
                            Name = string.IsNullOrWhiteSpace(input.Label) ? "Input" : input.Label,
                            Model = input,
                            Level = node.Level + 1,
                            Parent = node
                        };
                        node.Children.Add(inputNode);
                    }
                    node.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(node, new object[] { nameof(Node.Name) });
                    node.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(node, new object[] { nameof(Node.Children) });
                    OnPropertyChanged(nameof(Editor));
                });
                return;
            }

            if (node?.Kind == NodeKind.Action && node.Model is RagsCore.Models.Action act)
            {
                var vm = new EditActionViewModel(act);
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EditActionViewModel.Name))
                    {
                        node.Name = vm.Name;
                        node.GetType().GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?.Invoke(node, new object[] { nameof(Node.Name) });
                    }
                };
                ActionEditor = vm;
            }
        }

        private async Task RebuildAsync() => await MainThread.InvokeOnMainThreadAsync(Rebuild);

        public ObservableCollection<Node> Roots { get; } = new();

        public Command SelectNodeCommand { get; }
        public Command AddActionCommand { get; }
        public Command AddConditionCommand { get; }
        public Command AddCommandCommand { get; }
        public Command DeleteCommand { get; }
        public Command EditNodeCommand { get; }

        private readonly ObservableCollection<RagsCore.Models.Action> _actions;

        public ActionLibraryViewModel(Player player)
        {
            _game = App.CurrentGame;
            _actions = player.Actions;
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            AddActionCommand = new Command(AddAction);
            AddConditionCommand = new Command(AddCondition);
            AddCommandCommand = new Command(AddCommand);
            DeleteCommand = new Command(DeleteSelected);
            EditNodeCommand = new Command<object?>(o => { if (o is Node n) _ = EditNodeAsync(n); });
            _ = InitializeCatalogsAsync();
            Rebuild();
        }

        public ActionLibraryViewModel(ObservableCollection<RagsCore.Models.Action> actions)
        {
            _game = App.CurrentGame;
            _actions = actions;
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            AddActionCommand = new Command(AddAction);
            AddConditionCommand = new Command(AddCondition);
            AddCommandCommand = new Command(AddCommand);
            DeleteCommand = new Command(DeleteSelected);
            EditNodeCommand = new Command<object?>(o => { if (o is Node n) _ = EditNodeAsync(n); });
            _ = InitializeCatalogsAsync();
            Rebuild();
        }

        public ActionLibraryViewModel(Room room) : this(room.Actions) { }
        public ActionLibraryViewModel(GameObject obj) : this(obj.Actions) { }

        private static async Task InitializeCatalogsAsync()
        {
            try
            {
                await Task.WhenAll(
                    Game.EnsureAvailableCommandsAsync(() => FileSystem.OpenAppPackageFileAsync("Commands.json")),
                    Game.EnsureAvailableConditionsAsync(() => FileSystem.OpenAppPackageFileAsync("Conditions.json"))
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Catalog init failed: {ex}");
            }
        }

        private void Rebuild()
        {
            var prevSelectedModel = Selected?.Model;
            var prevSelectedName = Selected?.Name;

            var prevExpanded = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var n in Roots.SelectMany(r => Flatten(r)))
                if (n.IsExpanded && n.Model is not null)
                    prevExpanded.Add(n.Model);

            Roots.Clear();

            foreach (var action in _actions)
            {
                var actionNode = new Node { Kind = NodeKind.Action, Name = action.Name, Model = action, Level = 0, IsExpanded = true };
                foreach (var block in action.Nodes)
                {
                    foreach (var step in block.Steps)
                    {
                        var childNode = BuildStepNode(step, 1);
                        childNode.Parent = actionNode;
                        actionNode.Children.Add(childNode);
                    }
                }
                Roots.Add(actionNode);
            }

            foreach (var n in Roots.SelectMany(r => Flatten(r)))
                if (n.Model is not null && prevExpanded.Contains(n.Model))
                    n.IsExpanded = true;

            Selected = Roots
                .SelectMany(r => Flatten(r))
                .FirstOrDefault(n => (prevSelectedModel is not null && ReferenceEquals(n.Model, prevSelectedModel)) ||
                                     (!string.IsNullOrEmpty(prevSelectedName) && n.Name == prevSelectedName));

            static IEnumerable<Node> Flatten(Node n)
            {
                yield return n;
                foreach (var c in n.Children)
                    foreach (var d in Flatten(c))
                        yield return d;
            }
        }

        private Node BuildStepNode(StepDefinitionBase step, int level)
        {
            var kind = step.Kind == StepKind.Command ? NodeKind.Command : NodeKind.Condition;
            var node = new Node
            {
                Kind = kind,
                Name = string.IsNullOrWhiteSpace(step.Name) ? (kind == NodeKind.Command ? "Command" : "Condition") : step.Name,
                Model = step,
                Level = level
            };

            foreach (var input in step.Inputs)
            {
                var inputNode = new Node
                {
                    Kind = NodeKind.Input,
                    Name = string.IsNullOrWhiteSpace(input.Label) ? "Input" : input.Label,
                    Model = input,
                    Level = level + 1,
                    Parent = node
                };
                node.Children.Add(inputNode);
            }

            if (step is ConditionDefinition cond)
            {
                foreach (var nested in cond.Steps)
                {
                    var childNode = BuildStepNode(nested, level + 1);
                    childNode.Parent = node;
                    node.Children.Add(childNode);
                }
            }

            return node;
        }

        private void EnsureStepContainer(RagsCore.Models.Action act)
        {
            if (act.Nodes.Count == 0)
                act.Nodes.Add(new ActionNode { Title = "Steps" });
        }

        private (ActionNode block, int insertIndex) LocateInsertion(RagsCore.Models.Action act, StepDefinitionBase? afterStep)
        {
            EnsureStepContainer(act);
            if (afterStep is null)
            {
                var lastBlock = act.Nodes.Last();
                return (lastBlock, lastBlock.Steps.Count);
            }

            foreach (var b in act.Nodes)
            {
                var idx = b.Steps.IndexOf(afterStep);
                if (idx >= 0)
                    return (b, idx + 1);
            }
            var fallback = act.Nodes.Last();
            return (fallback, fallback.Steps.Count);
        }

        private void AddAction()
        {
            var a = new RagsCore.Models.Action { Name = $"Action {_actions.Count + 1}" };
            _actions.Add(a);
            Rebuild();
        }

        private void AddCondition()
        {
            if (Selected is null) return;

            if (Selected.Model is ConditionDefinition condDef)
            {
                condDef.Steps.Add(new ConditionDefinition
                {
                    Name = $"Condition {condDef.Steps.Count + 1}",
                    Category = "Logic"
                });
                Rebuild();
                return;
            }

            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action actFromAction)
            {
                var (block, idx) = LocateInsertion(actFromAction, null);
                block.Steps.Insert(idx, new ConditionDefinition { Name = $"Condition {CountAllSteps(actFromAction) + 1}", Category = "Logic" });
                Rebuild();
                return;
            }

            if (Selected.Model is StepDefinitionBase selectedStep)
            {
                if (Selected.Parent?.Model is ConditionDefinition parentCond)
                {
                    var idxInParent = parentCond.Steps.IndexOf(selectedStep);
                    if (idxInParent < 0) idxInParent = parentCond.Steps.Count - 1;
                    parentCond.Steps.Insert(idxInParent + 1, new ConditionDefinition
                    {
                        Name = $"Condition {parentCond.Steps.Count + 1}",
                        Category = "Logic"
                    });
                    Rebuild();
                    return;
                }

                if (FindParentAction(selectedStep) is RagsCore.Models.Action act)
                {
                    var (block, idx) = LocateInsertion(act, selectedStep);
                    block.Steps.Insert(idx, new ConditionDefinition { Name = $"Condition {CountAllSteps(act) + 1}", Category = "Logic" });
                    Rebuild();
                    return;
                }
            }
        }

        private void AddCommand()
        {
            if (Selected is null) return;

            if (Selected.Model is ConditionDefinition condDef)
            {
                condDef.Steps.Add(new CommandDefinition
                {
                    Name = $"Command {condDef.Steps.Count + 1}",
                    Category = "General"
                });
                Rebuild();
                return;
            }

            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action actFromAction)
            {
                var (block, idx) = LocateInsertion(actFromAction, null);
                block.Steps.Insert(idx, new CommandDefinition { Name = $"Command {CountAllSteps(actFromAction) + 1}", Category = "General" });
                Rebuild();
                return;
            }

            if (Selected.Model is StepDefinitionBase selectedStep)
            {
                if (Selected.Parent?.Model is ConditionDefinition parentCond)
                {
                    var idxInParent = parentCond.Steps.IndexOf(selectedStep);
                    if (idxInParent < 0) idxInParent = parentCond.Steps.Count - 1;
                    parentCond.Steps.Insert(idxInParent + 1, new CommandDefinition
                    {
                        Name = $"Command {parentCond.Steps.Count + 1}",
                        Category = "General"
                    });
                    Rebuild();
                    return;
                }

                if (FindParentAction(selectedStep) is RagsCore.Models.Action act)
                {
                    var (block, idx) = LocateInsertion(act, selectedStep);
                    block.Steps.Insert(idx, new CommandDefinition { Name = $"Command {CountAllSteps(act) + 1}", Category = "General" });
                    Rebuild();
                    return;
                }
            }
        }

        private void DeleteSelected()
        {
            if (Selected is null) return;

            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action a)
            {
                _actions.Remove(a);
                Rebuild();
                return;
            }

            if (Selected.Model is StepDefinitionBase step)
            {
                var action = FindParentAction(step);
                if (action != null)
                {
                    // Remove from nested structures (conditions or top-level blocks)
                    RemoveStepFrom(action, step);
                }
                Rebuild();
                return;
            }

            if (Selected.Model is InputDefinition input && Selected.Parent?.Model is ConditionDefinition parentCond)
            {
                parentCond.Inputs.Remove(input);
                Rebuild();
            }
        }

        private RagsCore.Models.Action? FindParentAction(StepDefinitionBase step)
        {
            foreach (var act in _actions)
            {
                foreach (var block in act.Nodes)
                {
                    if (ContainsStep(block.Steps, step))
                        return act;
                }
            }
            return null;
        }

        // Recursively checks if a step (possibly nested) exists in a list
        private static bool ContainsStep(IEnumerable<StepDefinitionBase> steps, StepDefinitionBase target)
        {
            foreach (var s in steps)
            {
                if (ReferenceEquals(s, target)) return true;
                if (s is ConditionDefinition cond && ContainsStep(cond.Steps, target)) return true;
            }
            return false;
        }

        // Recursively removes a step from an action's blocks and nested conditions
        private static bool RemoveStepFrom(RagsCore.Models.Action act, StepDefinitionBase target)
        {
            foreach (var block in act.Nodes.ToList())
            {
                if (RemoveFromList(block.Steps, target))
                {
                    if (block.Steps.Count == 0 && act.Nodes.Count > 1)
                        act.Nodes.Remove(block);
                    return true;
                }
            }
            return false;
        }

        private static bool RemoveFromList(List<StepDefinitionBase> list, StepDefinitionBase target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (ReferenceEquals(s, target))
                {
                    list.RemoveAt(i);
                    return true;
                }
                if (s is ConditionDefinition cond && RemoveFromList(cond.Steps, target))
                    return true;
            }
            return false;
        }

        private int CountAllSteps(RagsCore.Models.Action act) => act.Nodes.Sum(n => n.Steps.Count);

        private async Task EditNodeAsync(Node node)
        {
            if (node.Model is not StepDefinitionBase step) return;
            await InitializeCatalogsAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = new EditStepPage(step);
                void OnClosed(object? s, EventArgs e)
                {
                    page.Disappearing -= OnClosed;
                    Rebuild();
                }
                page.Disappearing += OnClosed;
                await (Application.Current.MainPage?.Navigation ?? Shell.Current.Navigation)
                    .PushModalAsync(page);
            });
        }

        public sealed class EditActionViewModel : BindableObject
        {
            private readonly RagsCore.Models.Action _action;
            private string _name;
            private bool _initiallyActive;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    _action.Name = _name;
                    OnPropertyChanged();
                }
            }

            public bool InitiallyActive
            {
                get => _initiallyActive;
                set
                {
                    if (_initiallyActive == value) return;
                    _initiallyActive = value;
                    _action.InitallyActive = _initiallyActive; // note: model property spelled InitallyActive
                    OnPropertyChanged();
                }
            }

            public ICommand RenameCommand { get; }

            public EditActionViewModel(RagsCore.Models.Action action)
            {
                _action = action;
                _name = action.Name;
                _initiallyActive = action.InitallyActive;
                RenameCommand = new Command(() =>
                {
                    _action.Name = _name;
                    _action.InitallyActive = _initiallyActive;
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(InitiallyActive));
                });
            }
        }
    }
}