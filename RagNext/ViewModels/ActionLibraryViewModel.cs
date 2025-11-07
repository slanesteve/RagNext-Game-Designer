using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using RagsCore.Models;
using RagsCore.Actions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RagNext.ViewModels
{
    public sealed class ActionLibraryViewModel : BindableObject
    {
        // Renamed Node -> Condition, Step -> Command
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
            public string Name { get; init; } = string.Empty;
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

            // Add this property
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
            }
        }

        public ObservableCollection<Node> Roots { get; } = new();

        public Command SelectNodeCommand { get; }
        public Command AddActionCommand { get; }
        public Command AddConditionCommand { get; }
        public Command AddCommandCommand { get; }
        public Command DeleteCommand { get; }

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
            Rebuild();
        }

        public ActionLibraryViewModel(Room room) : this(room.Actions) { }
        public ActionLibraryViewModel(GameObject obj) : this(obj.Actions) { }

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
                        childNode.Parent = actionNode; // Set parent
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
                    Parent = node // Set parent
                };
                node.Children.Add(inputNode);
            }

            if (step is ConditionDefinition cond)
            {
                foreach (var nested in cond.Steps)
                {
                    var childNode = BuildStepNode(nested, level + 1);
                    childNode.Parent = node; // Set parent
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

            // Find containing block
            foreach (var b in act.Nodes)
            {
                var idx = b.Steps.IndexOf(afterStep);
                if (idx >= 0)
                    return (b, idx + 1);
            }
            // Fallback to end
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

            // If a condition is selected, add as a child
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

            // If an action is selected, add at the top level
            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action actFromAction)
            {
                var (block, idx) = LocateInsertion(actFromAction, null);
                block.Steps.Insert(idx, new ConditionDefinition { Name = $"Condition {CountAllSteps(actFromAction) + 1}", Category = "Logic" });
                Rebuild();
                return;
            }

            // If a step is selected, add as a peer
            if (Selected.Model is StepDefinitionBase selectedStep)
            {
                // Peer inside a parent condition
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

                // Otherwise peer at top level within the action
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

            // If a condition is selected, add as a child
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

            // If an action is selected, add at the top level
            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action actFromAction)
            {
                var (block, idx) = LocateInsertion(actFromAction, null);
                block.Steps.Insert(idx, new CommandDefinition { Name = $"Command {CountAllSteps(actFromAction) + 1}", Category = "General" });
                Rebuild();
                return;
            }

            // If a step is selected, add as a peer
            if (Selected.Model is StepDefinitionBase selectedStep)
            {
                // Peer inside a parent condition
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

                // Otherwise peer at top level within the action
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
                    foreach (var block in action.Nodes.ToList())
                    {
                        if (block.Steps.Remove(step) && block.Steps.Count == 0 && action.Nodes.Count > 1)
                        {
                            action.Nodes.Remove(block);
                        }
                    }
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

        private int CountAllSteps(RagsCore.Models.Action act) => act.Nodes.Sum(n => n.Steps.Count);

        private RagsCore.Models.Action? FindParentAction(StepDefinitionBase step)
        {
            foreach (var act in _actions)
                if (act.Nodes.Any(b => b.Steps.Contains(step)))
                    return act;
            return null;
        }
    }
}