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

        public sealed class EditActionViewModel : BindableObject
        {
            private readonly RagsCore.Models.Action _action;
            public EditActionViewModel(RagsCore.Models.Action action)
            {
                _action = action;
            }

            public string Name
            {
                get => _action.Name;
                set
                {
                    if (_action.Name == value) return;
                    _action.Name = value;
                    OnPropertyChanged();
                }
            }

            public bool InitiallyActive
            {
                get => _action.InitallyActive;
                set
                {
                    if (_action.InitallyActive == value) return;
                    _action.InitallyActive = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SetEditorForSelected(Node? node)
        {
            Editor = null;
            ActionEditor = null;

            if (node?.Model is ActionStep actionStep)
            {
                Editor = new EditStepViewModel(actionStep, async (newTarget) => 
                {
                    if (newTarget != null && newTarget != actionStep)
                    {
                        if (node.Parent?.Model is ObservableCollection<ActionStep> collection)
                        {
                            var ix = collection.IndexOf(actionStep);
                            if (ix >= 0)
                            {
                                collection.RemoveAt(ix);
                                collection.Insert(ix, newTarget);
                            }
                        }
                        else if (node.Parent?.Model is RagsCore.Models.Action parAct)
                        {
                            var ix = parAct.Nodes.IndexOf(actionStep);
                            if (ix >= 0)
                            {
                                parAct.Nodes.RemoveAt(ix);
                                parAct.Nodes.Insert(ix, newTarget);
                            }
                        }

                        await RebuildAsync(newTarget);
                    }
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

        private async Task RebuildAsync(object? selectModelOverride = null) => await MainThread.InvokeOnMainThreadAsync(() => Rebuild(selectModelOverride));

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

        private void Rebuild(object? selectModelOverride = null)
        {
            var prevSelectedModel = selectModelOverride ?? Selected?.Model;
            var prevExpanded = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var n in Roots.SelectMany(r => Flatten(r)))
                if (n.IsExpanded && n.Model is not null)
                    prevExpanded.Add(n.Model);

            Roots.Clear();

            foreach (var action in _actions)
            {
                var actionNode = new Node { Kind = NodeKind.Action, Name = action.Name, Model = action, Level = 0, IsExpanded = true };
                foreach (var step in action.Nodes)
                {
                    var childNode = BuildStepNode(step, 1);
                    childNode.Parent = actionNode;
                    actionNode.Children.Add(childNode);
                }
                Roots.Add(actionNode);
            }

            foreach (var n in Roots.SelectMany(r => Flatten(r)))
                if (n.Model is not null && prevExpanded.Contains(n.Model))
                    n.IsExpanded = true;

            Selected = Roots.SelectMany(r => Flatten(r)).FirstOrDefault(n => prevSelectedModel is not null && ReferenceEquals(n.Model, prevSelectedModel));

            static IEnumerable<Node> Flatten(Node n) { yield return n; foreach (var c in n.Children) foreach (var d in Flatten(c)) yield return d; }
        }

        private Node BuildStepNode(ActionStep step, int level)
        {
            var kind = step is RagsCore.Actions.Condition ? NodeKind.Condition : NodeKind.Command;
            var node = new Node { Kind = kind, Name = step.TypeName, Model = step, Level = level, IsExpanded = true };

            if (step is RagsCore.Actions.Condition cond)
            {
                var trueNode = new Node { Kind = NodeKind.Condition, Name = "True Branch", Model = cond.TrueBranch, Level = level + 1, Parent = node, IsExpanded = true };
                foreach (var nested in cond.TrueBranch) { var childNode = BuildStepNode(nested, level + 2); childNode.Parent = trueNode; trueNode.Children.Add(childNode); }
                node.Children.Add(trueNode);
                
                var falseNode = new Node { Kind = NodeKind.Condition, Name = "False Branch", Model = cond.FalseBranch, Level = level + 1, Parent = node, IsExpanded = true };
                foreach (var nested in cond.FalseBranch) { var childNode = BuildStepNode(nested, level + 2); childNode.Parent = falseNode; falseNode.Children.Add(childNode); }
                node.Children.Add(falseNode);
            }
            return node;
        }

        private void AddAction() { _actions.Add(new RagsCore.Models.Action { Name = $"Action {_actions.Count + 1}" }); Rebuild(); }
        private void AddCondition() { InsertStep(new VariableEqualsCondition()); }
        private void AddCommand() { InsertStep(new SetVariableCommand()); }
        
        private void InsertStep(ActionStep step)
        {
            if (Selected?.Kind == NodeKind.Action && Selected?.Model is RagsCore.Models.Action act) act.Nodes.Add(step);
            else if (Selected?.Model is ObservableCollection<ActionStep> collection) collection.Add(step);
            else if (Selected?.Model is ActionStep selectedStep && Selected?.Parent?.Model is ObservableCollection<ActionStep> parentCollection) parentCollection.Insert(parentCollection.IndexOf(selectedStep) + 1, step);
            else if (Selected?.Model is ActionStep selectedStepAct && Selected?.Parent?.Model is RagsCore.Models.Action parentAct) parentAct.Nodes.Insert(parentAct.Nodes.IndexOf(selectedStepAct) + 1, step);
            Rebuild();
        }

        private void DeleteSelected()
        {
            if (Selected is null) return;
            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action a) { _actions.Remove(a); Rebuild(); return; }
            if (Selected.Model is ActionStep step)
            {
                if (Selected.Parent?.Model is ObservableCollection<ActionStep> parentCollection) parentCollection.Remove(step);
                else if (Selected.Parent?.Model is RagsCore.Models.Action parentAct) parentAct.Nodes.Remove(step);
                Rebuild(); return;
            }
        }
        
        private async Task EditNodeAsync(Node node)
        {
            if (node.Model is not ActionStep step) return;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = new EditStepPage(step);
                void OnClosed(object? s, EventArgs e)
                {
                    page.Disappearing -= OnClosed;
                    
                    object? newTarget = null;
                    if (page.BindingContext is EditStepViewModel vm && vm.SelectedDefinition != null && vm.SelectedDefinition.Type != step.GetType())
                    {
                          if (node.Parent?.Model is ObservableCollection<ActionStep> collection) {
                              var ix = collection.IndexOf(step);
                              if (ix>=0) {
                                  collection.RemoveAt(ix);
                                  var fiProp = vm.GetType().GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                  newTarget = fiProp?.GetValue(vm) as ActionStep;
                                  if (newTarget is ActionStep nt) collection.Insert(ix, nt);
                              }
                          }
                          else if (node.Parent?.Model is RagsCore.Models.Action parAct) {
                              var ix = parAct.Nodes.IndexOf(step);
                              if (ix>=0) {
                                  parAct.Nodes.RemoveAt(ix);
                                  var fiProp = vm.GetType().GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                  newTarget = fiProp?.GetValue(vm) as ActionStep;
                                  if (newTarget is ActionStep nt) parAct.Nodes.Insert(ix, nt);
                              }
                          }
                    }
                    Rebuild(newTarget);
                }
                page.Disappearing += OnClosed;
                await (Application.Current.MainPage?.Navigation ?? Shell.Current.Navigation).PushModalAsync(page);
            });
        }
    }
}