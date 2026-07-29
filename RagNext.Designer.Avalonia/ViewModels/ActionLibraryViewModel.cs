using System;
using System.Collections.ObjectModel;
using System.Linq;
using RagsCore.Models;
using RagsCore.Actions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using RagNext.Designer.Avalonia.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public sealed class ActionLibraryViewModel : ViewModelBase
    {
        public enum NodeKind { Action, Condition, Command, Input }

        // Decoupled hooks for Avalonia UI views to hook into for dialogs
        public static Func<string, string, string[], Task<string>>? DisplayActionSheet { get; set; }
        public static Func<ActionStep, Func<ActionStep, Task>, Task<bool>>? EditStepDialog { get; set; }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public sealed class Node : ViewModelBase
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
            public object Indent => new global::Avalonia.Thickness(Level * 16, 0, 0, 0);
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
        private bool _isRebuilding;
        public bool IsRebuilding => _isRebuilding;
        private Node? _selected;
        public Node? Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                
                // Force save the currently active editor step parameters before detaching/replacing it.
                if (_editor != null)
                {
                    _ = _editor.SaveAsync();
                }
                
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

        public string HostElementType { get; } = "GameObject";

        public List<ActionTrigger> AvailableTriggers => HostElementType switch
        {
            "Player" => new List<ActionTrigger>
            {
                ActionTrigger.UserClicked,
                ActionTrigger.OnGameStart,
                ActionTrigger.OnGameLoad,
                ActionTrigger.OnTurnTick,
                ActionTrigger.OnPlayerEnter,
                ActionTrigger.OnPlayerExit,
                ActionTrigger.OnCharacterEnter,
                ActionTrigger.OnCharacterExit,
                ActionTrigger.OnCharacterKilled
            },
            "Room" => new List<ActionTrigger>
            {
                ActionTrigger.UserClicked,
                ActionTrigger.OnTurnTick,
                ActionTrigger.OnPlayerEnter,
                ActionTrigger.OnPlayerExit,
                ActionTrigger.OnCharacterEnter,
                ActionTrigger.OnCharacterExit,
                ActionTrigger.OnCharacterKilled
            },
            "Character" => new List<ActionTrigger>
            {
                ActionTrigger.UserClicked,
                ActionTrigger.OnTurnTick,
                ActionTrigger.OnPlayerEnter,
                ActionTrigger.OnPlayerExit,
                ActionTrigger.OnCharacterEnter,
                ActionTrigger.OnCharacterExit,
                ActionTrigger.OnCharacterKilled
            },
            "GameObject" => new List<ActionTrigger>
            {
                ActionTrigger.UserClicked,
                ActionTrigger.OnTurnTick,
                ActionTrigger.OnPlayerEnter,
                ActionTrigger.OnPlayerExit,
                ActionTrigger.OnCharacterEnter,
                ActionTrigger.OnCharacterExit,
                ActionTrigger.OnCharacterKilled,
                ActionTrigger.OnObjectExamined,
                ActionTrigger.OnObjectTaken,
                ActionTrigger.OnObjectDropped
            },
            _ => new List<ActionTrigger> { ActionTrigger.UserClicked }
        };

        public sealed class EditActionViewModel : ViewModelBase
        {
            private readonly RagsCore.Models.Action _action;
            public EditActionViewModel(RagsCore.Models.Action action)
            {
                _action = action;
            }

            public RagsCore.Models.Action GetUnderlyingAction() => _action;

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

            public ActionTrigger Trigger
            {
                get => _action.Trigger;
                set
                {
                    if (_action.Trigger == value) return;
                    _action.Trigger = value;
                    OnPropertyChanged();
                    
                    // Trigger dynamic auto-save to ensure designer instantly updates file state
                    if (MainWindowViewModel.Instance != null)
                    {
                        _ = MainWindowViewModel.Instance.SaveGameAsync();
                    }
                }
            }
        }

        private void SetEditorForSelected(Node? node)
        {
            if (node?.Model is ActionStep actionStep)
            {
                ActionEditor = null;
                Editor = new EditStepViewModel(actionStep, async (newTarget) => 
                {
                    if (newTarget != null)
                    {
                        if (newTarget != actionStep)
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

                        if (MainWindowViewModel.Instance != null)
                        {
                            await MainWindowViewModel.Instance.SaveGameAsync();
                        }
                    }
                });
                return;
            }

            if (node?.Kind == NodeKind.Action && node.Model is RagsCore.Models.Action act)
            {
                Editor = null;
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
                return;
            }

            Editor = null;
            ActionEditor = null;
        }

        private async Task RebuildAsync(object? selectModelOverride = null)
        {
            if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Rebuild(selectModelOverride);
                await Task.CompletedTask;
            }
            else
            {
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Rebuild(selectModelOverride));
            }
        }

        public ObservableCollection<Node> Roots { get; } = new();

        public ICommand SelectNodeCommand { get; }
        public ICommand AddActionCommand { get; }
        public ICommand AddConditionCommand { get; }
        public ICommand AddCommandCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand EditNodeCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }

        private readonly ObservableCollection<RagsCore.Models.Action> _actions;

        public ActionLibraryViewModel(Player player)
        {
            HostElementType = "Player";
            _game = App.CurrentGame;
            _actions = player.Actions;
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            AddActionCommand = new Command(async () => await AddActionAsync());
            AddConditionCommand = new Command(async () => await AddConditionAsync());
            AddCommandCommand = new Command(async () => await AddCommandAsync());
            DeleteCommand = new Command(async () => await DeleteSelectedAsync());
            EditNodeCommand = new Command<object?>(o => { if (o is Node n) _ = EditNodeAsync(n); });
            CopyCommand = new Command(async () => await CopySelectedAsync());
            PasteCommand = new Command(async () => await PasteSelectedAsync());
            _ = InitializeCatalogsAsync();
            Rebuild();
        }

        public ActionLibraryViewModel(ObservableCollection<RagsCore.Models.Action> actions, string hostElementType = "GameObject")
        {
            HostElementType = hostElementType;
            _game = App.CurrentGame;
            _actions = actions;
            SelectNodeCommand = new Command<object?>(o => { if (o is Node n) Selected = n; });
            AddActionCommand = new Command(async () => await AddActionAsync());
            AddConditionCommand = new Command(async () => await AddConditionAsync());
            AddCommandCommand = new Command(async () => await AddCommandAsync());
            DeleteCommand = new Command(async () => await DeleteSelectedAsync());
            EditNodeCommand = new Command<object?>(o => { if (o is Node n) _ = EditNodeAsync(n); });
            CopyCommand = new Command(async () => await CopySelectedAsync());
            PasteCommand = new Command(async () => await PasteSelectedAsync());
            _ = InitializeCatalogsAsync();
            Rebuild();
        }

        public ActionLibraryViewModel(Room room) : this(room.Actions, "Room") { }
        public ActionLibraryViewModel(GameObject obj) : this(obj.Actions, "GameObject") { }
        public ActionLibraryViewModel(Character character) : this(character.Actions, "Character") { }

        private static Task<Stream> OpenAppPackageFileAsync(string filename)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, filename);
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "WebAssets", filename);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "..", "Resources", "WebAssets", filename);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "Resources", "Raw", filename);
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "..", "..", "..", "..", "RagNext", "Resources", "Raw", filename);
            }
            if (File.Exists(path))
            {
                return Task.FromResult<Stream>(File.OpenRead(path));
            }
            throw new FileNotFoundException($"Could not find package file: {filename}");
        }

        private static async Task InitializeCatalogsAsync()
        {
            try
            {
                await Task.WhenAll(
                    Game.EnsureAvailableCommandsAsync(() => OpenAppPackageFileAsync("Commands.json")),
                    Game.EnsureAvailableConditionsAsync(() => OpenAppPackageFileAsync("Conditions.json"))
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Catalog init failed: {ex}");
            }
        }

        public bool CanAddAction => HostElementType != "Function" && HostElementType != "Timer";
        public bool IsInitiallyActiveVisible => HostElementType != "Function" && HostElementType != "Timer";

        private void OnActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RagsCore.Models.Action.Name) && sender is RagsCore.Models.Action action)
            {
                var node = Roots.FirstOrDefault(r => ReferenceEquals(r.Model, action));
                if (node != null)
                {
                    node.Name = action.Name;
                }
            }
        }

        public void RebuildTree()
        {
            Rebuild();
        }

        private void Rebuild(object? selectModelOverride = null)
        {
            if (_isRebuilding) return;
            _isRebuilding = true;
            try
            {
                var prevSelectedModel = selectModelOverride ?? Selected?.Model;
                var prevExpanded = new HashSet<object>(ReferenceEqualityComparer.Instance);
                foreach (var n in Roots.SelectMany(r => Flatten(r)))
                    if (n.IsExpanded && n.Model is not null)
                        prevExpanded.Add(n.Model);

                Roots.Clear();

                foreach (var action in _actions)
                {
                    action.PropertyChanged -= OnActionPropertyChanged;
                    action.PropertyChanged += OnActionPropertyChanged;

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
            }
            finally
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _isRebuilding = false);
            }

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
            else if (step is RagsCore.Actions.StartDialogueCommand dial)
            {
                foreach (var choice in dial.Choices)
                {
                    var choiceNode = new Node { Kind = NodeKind.Condition, Name = $"Choice: {choice.Text}", Model = choice.Commands, Level = level + 1, Parent = node, IsExpanded = true };
                    foreach (var nested in choice.Commands)
                    {
                        var childNode = BuildStepNode(nested, level + 2);
                        childNode.Parent = choiceNode;
                        choiceNode.Children.Add(childNode);
                    }
                    node.Children.Add(choiceNode);
                }
            }
            return node;
        }

        internal sealed class ActionTemplateCommand
        {
            [System.Text.Json.Serialization.JsonPropertyName("$type")]
            public string? Type { get; set; }
            public string? Text { get; set; }
            public string? ObjectId { get; set; }
            public string? RoomId { get; set; }
        }

        internal sealed class ActionTemplate
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string? Name { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("elementTypes")]
            public List<string>? ElementTypes { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("commands")]
            public List<ActionTemplateCommand>? Commands { get; set; }
        }

        private async Task AddActionAsync()
        {
            List<ActionTemplate> templates;
            try
            {
                using var stream = await OpenAppPackageFileAsync("ActionTemplates.json");
                templates = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.ListActionTemplate) ?? new();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load templates: {ex}");
                templates = new() { new ActionTemplate { Name = "New Action (Blank)", ElementTypes = new() { HostElementType } } };
            }

            var matching = templates.Where(t => t.ElementTypes?.Contains(HostElementType, StringComparer.OrdinalIgnoreCase) == true).ToList();
            if (matching.Count == 0)
            {
                matching.Add(new ActionTemplate { Name = "New Action (Blank)", ElementTypes = new() { HostElementType } });
            }

            var options = matching.Select(t => t.Name ?? "Unnamed Template").ToArray();
            
            string choice = "New Action (Blank)";
            if (DisplayActionSheet != null)
            {
                choice = await DisplayActionSheet("Select Action Template", "Cancel", options);
            }
            
            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
                return;

            var template = matching.FirstOrDefault(t => t.Name == choice) ?? matching[0];

            string actionName = template.Name == "New Action (Blank)" ? $"Action {_actions.Count + 1}" : template.Name ?? $"Action {_actions.Count + 1}";
            var newAction = new RagsCore.Models.Action { Name = actionName };

            if (template.Commands != null)
            {
                foreach (var cmdSpec in template.Commands)
                {
                    if (cmdSpec.Type == "general.displayText")
                    {
                        var step = new DisplayTextCommand { Text = cmdSpec.Text ?? "" };
                        newAction.Nodes.Add(step);
                    }
                    else if (cmdSpec.Type == "room.removeObject")
                    {
                        var step = new RemoveObjectFromRoomCommand { ObjectId = cmdSpec.ObjectId ?? "{this.Id}" };
                        newAction.Nodes.Add(step);
                    }
                    else if (cmdSpec.Type == "item.openContainer")
                    {
                        var step = new OpenContainerCommand { ObjectId = cmdSpec.ObjectId ?? "{this.Id}" };
                        newAction.Nodes.Add(step);
                    }
                    else if (cmdSpec.Type == "item.closeContainer")
                    {
                        var step = new CloseContainerCommand { ObjectId = cmdSpec.ObjectId ?? "{this.Id}" };
                        newAction.Nodes.Add(step);
                    }
                }
            }

            _actions.Add(newAction);
            Rebuild(newAction);
            if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
        }

        private async Task AddConditionAsync()
        {
            await InsertStepAsync(new VariableEqualsCondition());
        }

        private async Task AddCommandAsync()
        {
            await InsertStepAsync(new DisplayTextCommand { Text = "" });
        }
        
        private async Task InsertStepAsync(ActionStep step)
        {
            if (Selected?.Kind == NodeKind.Action && Selected?.Model is RagsCore.Models.Action act) act.Nodes.Add(step);
            else if (Selected?.Model is ObservableCollection<ActionStep> collection) collection.Add(step);
            else if (Selected?.Model is ActionStep selectedStep && Selected?.Parent?.Model is ObservableCollection<ActionStep> parentCollection) parentCollection.Insert(parentCollection.IndexOf(selectedStep) + 1, step);
            else if (Selected?.Model is ActionStep selectedStepAct && Selected?.Parent?.Model is RagsCore.Models.Action parentAct) parentAct.Nodes.Insert(parentAct.Nodes.IndexOf(selectedStepAct) + 1, step);
            Rebuild();
            if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
        }

        private async Task DeleteSelectedAsync()
        {
            if (Selected is null) return;
            if (Selected.Kind == NodeKind.Action && Selected.Model is RagsCore.Models.Action a)
            {
                if (HostElementType == "Function" || HostElementType == "Timer")
                {
                    return;
                }
                _actions.Remove(a);
                Rebuild();
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                return;
            }
            if (Selected.Model is ActionStep step)
            {
                if (Selected.Parent?.Model is ObservableCollection<ActionStep> parentCollection) parentCollection.Remove(step);
                else if (Selected.Parent?.Model is RagsCore.Models.Action parentAct) parentAct.Nodes.Remove(step);
                Rebuild();
                if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
                return;
            }
        }
        
        private async Task EditNodeAsync(Node node)
        {
            if (node.Model is not ActionStep step) return;
            if (EditStepDialog != null)
            {
                var saved = await EditStepDialog(step, async (newTarget) =>
                {
                    if (newTarget != null)
                    {
                        object? targetModel = null;
                        if (newTarget != step)
                        {
                            if (node.Parent?.Model is ObservableCollection<ActionStep> collection)
                            {
                                var ix = collection.IndexOf(step);
                                if (ix >= 0)
                                {
                                    collection.RemoveAt(ix);
                                    collection.Insert(ix, newTarget);
                                }
                            }
                            else if (node.Parent?.Model is RagsCore.Models.Action parAct)
                            {
                                var ix = parAct.Nodes.IndexOf(step);
                                if (ix >= 0)
                                {
                                    parAct.Nodes.RemoveAt(ix);
                                    parAct.Nodes.Insert(ix, newTarget);
                                }
                            }
                            targetModel = newTarget;
                        }
                        Rebuild(targetModel);
                        if (MainWindowViewModel.Instance != null)
                        {
                            await MainWindowViewModel.Instance.SaveGameAsync();
                        }
                    }
                });
            }
        }

        private async Task CopySelectedAsync()
        {
            if (Selected?.Model is null) return;
            ActionClipboardService.Copy(Selected.Model);
            OnPropertyChanged(nameof(CanPaste));
            await Task.CompletedTask;
        }

        private async Task PasteSelectedAsync()
        {
            var pasted = ActionClipboardService.Paste();
            if (pasted is null) return;

            if (pasted is RagsCore.Models.Action pastedAction)
            {
                if (HostElementType == "Function" || HostElementType == "Timer")
                {
                    return;
                }
                _actions.Add(pastedAction);
                Rebuild(pastedAction);
            }
            else if (pasted is ActionStep pastedStep)
            {
                await InsertStepAsync(pastedStep);
            }

            if (MainWindowViewModel.Instance != null) await MainWindowViewModel.Instance.SaveGameAsync();
        }

        public bool CanPaste => ActionClipboardService.CanPaste;
    }
}
