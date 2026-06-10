using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using RagsCore.Actions;
using RagsCore.Models;
using RagNext;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class StepTypeWrapper
    {
        public string Name { get; set; } = string.Empty;
        public Type Type { get; set; } = default!;
    }

    public sealed class NamedOption
    {
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public sealed class EditStepViewModel : ViewModelBase
    {
        private ActionStep _target;
        private readonly Func<ActionStep, Task> _afterMutate;
        private bool _isSaving;
        private bool _hasPendingSave;
        public bool IsSaved { get; private set; } = false;

        public ObservableCollection<StepTypeWrapper> Definitions { get; } = new();
        public ObservableCollection<InputDefinition> EditableInputs { get; } = new();

        private StepTypeWrapper? _selectedDefinition;
        public StepTypeWrapper? SelectedDefinition
        {
            get => _selectedDefinition;
            set
            {
                if (_selectedDefinition == value) return;
                // Avoid MAUI's two-way picker wipeout during BindingContext changes
                if (value == null && _target != null)
                {
                    OnPropertyChanged();
                    return;
                }

                _selectedDefinition = value;
                OnPropertyChanged();
                
                if (value != null && _target.GetType() != value.Type)
                {
                    var targetType = value.Type;
                    // Defer step mutation, parameters rebuild, and saving/rebuilding completely
                    // to allow the native Picker dropdown to fully close and layout to settle.
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        await Task.Delay(150);
                        
                        // Guard: ensure the selected definition hasn't changed while we were waiting
                        if (_selectedDefinition == null || _selectedDefinition.Type != targetType)
                            return;

                        var newTarget = (ActionStep)Activator.CreateInstance(targetType)!;
                        newTarget.Label = _target.Label;
                        if (newTarget is RagsCore.Actions.Condition newCond && _target is RagsCore.Actions.Condition oldCond)
                        {
                            newCond.TrueBranch = oldCond.TrueBranch;
                            newCond.FalseBranch = oldCond.FalseBranch;
                        }
                        _target = newTarget;
                        BuildInputsFromTarget();
                        
                        await SaveAsync();
                    });
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditStepViewModel(ActionStep target, Func<ActionStep, Task> afterMutate)
        {
            _target = target;
            _afterMutate = afterMutate;

            var baseType = _target.Kind == ActionStepKind.Command ? typeof(GameCommand) : typeof(RagsCore.Actions.Condition);
            var sortedWrappers = Assembly.GetAssembly(typeof(ActionStep))!.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(baseType))
                .Select(t => new StepTypeWrapper {
                    Name = ((ActionStep)Activator.CreateInstance(t)!).TypeName,
                    Type = t
                })
                .OrderBy(w => w.Name)
                .ToList();

            foreach (var w in sortedWrappers)
            {
                Definitions.Add(w);
            }

            _selectedDefinition = Definitions.FirstOrDefault(d => d.Type == _target.GetType());
            OnPropertyChanged(nameof(SelectedDefinition));

            BuildInputsFromTarget();

            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(() => _afterMutate(null));
        }

        public object? GetModelValue(string label)
        {
            if (_target == null) return null;
            return _target.GetType().GetProperty(label)?.GetValue(_target);
        }

        public void SetModelValue(string label, object? value)
        {
            if (_target == null) return;
            var p = _target.GetType().GetProperty(label);
            if (p != null && p.CanWrite)
            {
                object? valToSet = value;
                
                if (valToSet is GameVariable gv) { valToSet = gv.Name; }
                else if (valToSet is NamedOption no) { valToSet = no.Name; }
                else if (valToSet != null && valToSet.GetType() != typeof(Guid) && valToSet.GetType() != typeof(string))
                {
                    var idProp = valToSet.GetType().GetProperty("Id");
                    if (idProp != null) valToSet = idProp.GetValue(valToSet);
                }

                if (valToSet != null && p.PropertyType != valToSet.GetType())
                {
                    if (p.PropertyType.IsEnum)
                    {
                        try
                        {
                            valToSet = Enum.Parse(p.PropertyType, valToSet.ToString()!, true);
                        }
                        catch {}
                    }
                    else if (p.PropertyType == typeof(Guid) && valToSet is string strGuid && Guid.TryParse(strGuid, out var g))
                    {
                        valToSet = g;
                    }
                    else if (p.PropertyType == typeof(string) && valToSet is Guid guidVal)
                    {
                        valToSet = guidVal.ToString();
                    }
                    else if (p.PropertyType == typeof(double) || p.PropertyType == typeof(int) || p.PropertyType == typeof(float))
                    {
                        var strVal = valToSet?.ToString() ?? "0";
                        if (double.TryParse(strVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedDouble))
                        {
                            valToSet = Convert.ChangeType(parsedDouble, p.PropertyType);
                        }
                        else
                        {
                            valToSet = Convert.ChangeType(0, p.PropertyType);
                        }
                    }
                    else
                    {
                        try { valToSet = Convert.ChangeType(valToSet, p.PropertyType); } catch { }
                    }
                }

                p.SetValue(_target, valToSet);
            }
        }

        private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not InputDefinition input) return;

            if (e.PropertyName == nameof(InputDefinition.Value) || e.PropertyName == nameof(InputDefinition.IsManualMode))
            {
                SetModelValue(input.Label, input.Value);
                _ = SaveAsync();

                if (input.Label == "InputType" && _target is PromptPlayerInputCommand)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        BuildInputsFromTarget();
                    });
                }

                // Bug #5: When the entity picker changes, rebuild so ActionName picker refreshes.
                bool isEntityChange =
                    (input.Label == "CharacterId" && _target is CharacterSetActionActiveCommand) ||
                    (input.Label == "ItemId"      && _target is ItemSetActionActiveCommand)      ||
                    (input.Label == "RoomId"      && _target is RoomSetActionActiveCommand);

                if (isEntityChange)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        BuildInputsFromTarget();
                    });
                }
            }
        }

        private void BuildInputsFromTarget()
        {
            foreach (var input in EditableInputs)
            {
                input.PropertyChanged -= OnInputPropertyChanged;
            }

            EditableInputs.Clear();
            var props = _target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && 
                            p.Name != "Label" && 
                            p.Name != "DialogueId" && 
                            p.Name != "TrueBranch" && 
                            p.Name != "FalseBranch" && 
                            p.Name != "X" && 
                            p.Name != "Y" &&
                            p.Name != "Width" &&
                            p.Name != "Height" &&
                            !(p.PropertyType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)));

            foreach (var p in props)
            {
                if (p.Name == "CustomOptions" && _target is PromptPlayerInputCommand cmd && cmd.InputType != PlayerInputType.Custom)
                {
                    continue;
                }

                var input = new InputDefinition
                {
                    Label = p.Name,
                    Value = p.GetValue(_target),
                    ControlType = GetControlType(p),
                    DataType = GetDataType(p)
                };
                PreparePickerSource(input);
                input.PropertyChanged += OnInputPropertyChanged;
                EditableInputs.Add(input);
            }
        }

        private InputControlType GetControlType(PropertyInfo p)
        {
            if (p.PropertyType == typeof(bool)) return InputControlType.Checkbox;
            if (p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(float)) return InputControlType.Number;

            var name = p.Name;
            if (p.PropertyType.IsEnum || 
                name.Equals("StoreVariableName", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("InputType", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("PromptName", StringComparison.OrdinalIgnoreCase) || 
                (name.Equals("ChoiceText", StringComparison.OrdinalIgnoreCase) && _target is RemoveCustomChoiceCommand) || 
                p.PropertyType == typeof(Guid) || 
                name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Room", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Object", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Item", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Character", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Media", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Function", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Timer", StringComparison.OrdinalIgnoreCase)) 
                return InputControlType.ComboBox;

            if ((name.Equals("Name", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("NameA", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("NameB", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("VariableName", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("SourceName", StringComparison.OrdinalIgnoreCase)) && 
                (p.DeclaringType != null && (p.DeclaringType.Name.Contains("Variable") || p.DeclaringType.Name.Contains("Random") || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType)))) 
                return InputControlType.ComboBox; 

            if (name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (name.Equals("Gender", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            // Bug #5: ActionName on the 4 Set Action Active/Inactive commands uses a dynamic
            // entity-scoped ComboBox, not a plain Text field.
            if (name.Equals("ActionName", StringComparison.OrdinalIgnoreCase) &&
                (_target is CharacterSetActionActiveCommand ||
                 _target is ItemSetActionActiveCommand ||
                 _target is RoomSetActionActiveCommand ||
                 _target is PlayerSetActionActiveCommand))
                return InputControlType.ComboBox;

            if (name.Contains("Text", StringComparison.OrdinalIgnoreCase) || name.Contains("Description", StringComparison.OrdinalIgnoreCase)) return InputControlType.TextArea;
            return InputControlType.Text;
        }

        private InputDataType GetDataType(PropertyInfo p)
        {
            var name = p.Name;
            if (name.Equals("PromptName", StringComparison.OrdinalIgnoreCase)) return InputDataType.PromptName;
            if (name.Equals("StoreVariableName", StringComparison.OrdinalIgnoreCase)) return InputDataType.Variable;

            if (name.EndsWith("RoomId", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Room", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("DestinationRoomId", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.Room;

            if (name.EndsWith("ObjectId", StringComparison.OrdinalIgnoreCase) || 
                name.EndsWith("ItemId", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Object", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Item", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.GameObject;

            if (name.EndsWith("CharacterId", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Character", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.Character;

            if (name.EndsWith("SoundId", StringComparison.OrdinalIgnoreCase) || 
                name.EndsWith("PortraitId", StringComparison.OrdinalIgnoreCase) || 
                name.EndsWith("MediaId", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Media", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.Media;

            if (name.EndsWith("FunctionId", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Function", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.Function;

            if (name.EndsWith("TimerId", StringComparison.OrdinalIgnoreCase) || 
                name.Equals("Timer", StringComparison.OrdinalIgnoreCase)) 
                return InputDataType.Timer;

            if (name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputDataType.Operator;
            if (name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return InputDataType.Direction;

            if ((name.Equals("Name", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("NameA", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("NameB", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("VariableName", StringComparison.OrdinalIgnoreCase) || 
                 name.Equals("SourceName", StringComparison.OrdinalIgnoreCase)) && 
                (p.DeclaringType != null && (p.DeclaringType.Name.Contains("Variable") || p.DeclaringType.Name.Contains("Random") || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType)))) 
                return InputDataType.Variable;
            return InputDataType.String;
        }

        private void PreparePickerSource(InputDefinition input)
        {
            var game = App.CurrentGame;
            if (game is null || input.ControlType != InputControlType.ComboBox) return;

            if (input.Label == "InputType" && _target is PromptPlayerInputCommand)
            {
                input.PickerSource = Enum.GetNames(typeof(PlayerInputType))
                    .Select(name => new NamedOption { Name = name })
                    .Cast<object>()
                    .ToList();
                if (input.Value != null)
                {
                    var valStr = input.Value.ToString();
                    var match = input.PickerSource.Cast<NamedOption>().FirstOrDefault(x => string.Equals(x.Name, valStr, StringComparison.OrdinalIgnoreCase));
                    if (match != null) input.Value = match;
                }
                return;
            }

            if (input.Label.Equals("ChoiceText", StringComparison.OrdinalIgnoreCase) && _target is RemoveCustomChoiceCommand removeCmd)
            {
                input.PickerSource = GetCustomChoiceTexts(game, removeCmd.PromptName);
                if (input.Value != null)
                {
                    var valStr = input.Value.ToString();
                    var match = input.PickerSource.Cast<NamedOption>().FirstOrDefault(x => string.Equals(x.Name, valStr, StringComparison.OrdinalIgnoreCase));
                    if (match != null) input.Value = match;
                }
                return;
            }

            // Bug #5: Dynamic ActionName picker — list scoped to the selected entity.
            if (input.Label.Equals("ActionName", StringComparison.OrdinalIgnoreCase))
            {
                IEnumerable<RagsCore.Models.Action>? scopedActions = null;

                if (_target is CharacterSetActionActiveCommand charCmd &&
                    !string.IsNullOrEmpty(charCmd.CharacterId) &&
                    Guid.TryParse(charCmd.CharacterId, out var charGuid))
                {
                    scopedActions = game.Characters.FirstOrDefault(c => c.Id == charGuid)?.Actions;
                }
                else if (_target is ItemSetActionActiveCommand itemCmd &&
                    !string.IsNullOrEmpty(itemCmd.ItemId) &&
                    Guid.TryParse(itemCmd.ItemId, out var itemGuid))
                {
                    scopedActions = game.Objects.FirstOrDefault(o => o.Id == itemGuid)?.Actions;
                }
                else if (_target is RoomSetActionActiveCommand roomCmd &&
                    !string.IsNullOrEmpty(roomCmd.RoomId) &&
                    Guid.TryParse(roomCmd.RoomId, out var roomGuid))
                {
                    scopedActions = game.Rooms.FirstOrDefault(r => r.Id == roomGuid)?.Actions;
                }
                else if (_target is PlayerSetActionActiveCommand)
                {
                    scopedActions = game.Player.Actions;
                }

                if (scopedActions != null)
                {
                    input.PickerSource = scopedActions
                        .Select(a => new NamedOption { Name = a.Name })
                        .Cast<object>()
                        .ToList();

                    if (input.Value is string existingName && !string.IsNullOrEmpty(existingName))
                    {
                        var match = input.PickerSource.Cast<NamedOption>()
                            .FirstOrDefault(o => string.Equals(o.Name, existingName, StringComparison.OrdinalIgnoreCase));
                        if (match != null) input.Value = match;
                    }
                    return;
                }
                // No entity selected yet — leave picker source empty
                input.PickerSource = new List<object>();
                return;
            }

            input.PickerSource = input.DataType switch
            {
                InputDataType.PromptName => GetPromptNames(game),
                InputDataType.Room => game.Rooms.Cast<object>().ToList(),
                InputDataType.GameObject or InputDataType.Item => game.Objects.Cast<object>().ToList(),
                InputDataType.Character => game.Characters.Cast<object>().ToList(),
                InputDataType.Variable => game.Variables.Cast<object>().ToList(),
                InputDataType.Media => FilterMediaAssets(game, input.Label, _target),
                InputDataType.Function => game.Functions.Cast<object>().ToList(),
                InputDataType.Timer => game.Timers.Cast<object>().ToList(),
                InputDataType.Operator => new List<object>
                {
                    new NamedOption { Name = "=" },
                    new NamedOption { Name = "!=" },
                    new NamedOption { Name = ">" },
                    new NamedOption { Name = ">=" },
                    new NamedOption { Name = "<" },
                    new NamedOption { Name = "<=" }
                },
                InputDataType.Direction => new List<object>
                {
                    new NamedOption { Name = "North" },
                    new NamedOption { Name = "South" },
                    new NamedOption { Name = "East" },
                    new NamedOption { Name = "West" },
                    new NamedOption { Name = "Up" },
                    new NamedOption { Name = "Down" },
                    new NamedOption { Name = "In" },
                    new NamedOption { Name = "Out" }
                },
                _ => input.Label.Equals("Gender", StringComparison.OrdinalIgnoreCase)
                     ? new List<object>
                     {
                         new NamedOption { Name = "Male" },
                         new NamedOption { Name = "Female" },
                         new NamedOption { Name = "Non-binary" },
                         new NamedOption { Name = "Other" }
                     }
                     : null
            };

            if (input.PickerSource is not null && input.Value != null)
            {
                if (input.DataType == InputDataType.Variable && input.Value is string varName)
                {
                     var match = input.PickerSource.Cast<GameVariable>().FirstOrDefault(v => v.Name == varName);
                     if (match != null) input.Value = match;
                }
                else if (input.Value is Guid selectedId)
                {
                    var typeWithId = input.PickerSource.Cast<object>().FirstOrDefault();
                    if (typeWithId != null)
                    {
                        var prop = typeWithId.GetType().GetProperty("Id");
                        var match = input.PickerSource.Cast<object>().FirstOrDefault(m => 
                            prop != null && (Guid)prop.GetValue(m)! == selectedId);
                        if (match is not null)
                            input.Value = match;
                    }
                }
                else if (input.Value is string strVal)
                {
                    object? match = null;
                    if (Guid.TryParse(strVal, out var guidVal))
                    {
                        var firstObj = input.PickerSource.Cast<object>().FirstOrDefault();
                        if (firstObj != null)
                        {
                            var prop = firstObj.GetType().GetProperty("Id");
                            if (prop != null)
                            {
                                match = input.PickerSource.Cast<object>().FirstOrDefault(m =>
                                    prop.GetValue(m) is Guid g && g == guidVal);
                            }
                        }
                    }

                    if (match == null)
                    {
                        match = input.PickerSource.Cast<object>().FirstOrDefault(o => 
                            string.Equals(o.ToString(), strVal, StringComparison.OrdinalIgnoreCase));
                    }

                    if (match is not null)
                        input.Value = match;
                }
            }
        }

        public async Task SaveAsync()
        {
            if (_isSaving)
            {
                _hasPendingSave = true;
                return;
            }
            
            _isSaving = true;
            _hasPendingSave = false;
            
            try
            {
                do
                {
                    _hasPendingSave = false;

                    foreach (var src in EditableInputs)
                    {
                        var p = _target.GetType().GetProperty(src.Label);
                        if (p != null && p.CanWrite)
                        {
                            object? valToSet = src.Value;
                            
                            if (src.ControlType == InputControlType.ComboBox && valToSet != null)
                            {
                                if (valToSet is GameVariable gv) { valToSet = gv.Name; }
                                else if (valToSet is NamedOption no) { valToSet = no.Name; }
                                else { 
                                    var idProp = valToSet.GetType().GetProperty("Id");
                                    if (idProp != null) valToSet = idProp.GetValue(valToSet);
                                }
                            }

                            if (valToSet != null && p.PropertyType != valToSet.GetType())
                            {
                                if (p.PropertyType.IsEnum)
                                {
                                    try
                                    {
                                        valToSet = Enum.Parse(p.PropertyType, valToSet.ToString()!, true);
                                    }
                                    catch {}
                                }
                                else if (p.PropertyType == typeof(Guid) && valToSet is string strGuid && Guid.TryParse(strGuid, out var g))
                                {
                                    valToSet = g;
                                }
                                else if (p.PropertyType == typeof(string) && valToSet is Guid guidVal)
                                {
                                    valToSet = guidVal.ToString();
                                }
                                else if (p.PropertyType == typeof(double) || p.PropertyType == typeof(int) || p.PropertyType == typeof(float))
                                {
                                    var strVal = valToSet?.ToString() ?? "0";
                                    if (double.TryParse(strVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedDouble))
                                    {
                                        valToSet = Convert.ChangeType(parsedDouble, p.PropertyType);
                                    }
                                    else
                                    {
                                        valToSet = Convert.ChangeType(0, p.PropertyType);
                                    }
                                }
                                else
                                {
                                    try { valToSet = Convert.ChangeType(valToSet, p.PropertyType); } catch { }
                                }
                            }

                            p.SetValue(_target, valToSet);
                        }
                    }

                    await _afterMutate(_target);
                    IsSaved = true;
                } while (_hasPendingSave);
            }
            finally
            {
                _isSaving = false;
            }
        }
        private System.Collections.Generic.List<object> FilterMediaAssets(Game game, string label, ActionStep target)
        {
            var assets = game.MediaAssets;
            
            if (label.Equals("SoundId", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("SoundIdB", StringComparison.OrdinalIgnoreCase) ||
                label.Equals("SoundFile", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("MusicFile", StringComparison.OrdinalIgnoreCase) ||
                target is PlaySoundEffectCommand ||
                target is StopSoundEffectCommand)
            {
                return assets.Where(m => m.Kind == MediaKind.Audio).Cast<object>().ToList();
            }
            
            if (label.Equals("PortraitId", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("PortraitMedia", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("ImageFile", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("Picture", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("PictureFile", StringComparison.OrdinalIgnoreCase) ||
                target is CharacterSetPortraitMediaCommand ||
                target is PlayerSetPortraitMediaCommand ||
                target is CharacterDisplayPortraitCommand)
            {
                return assets.Where(m => m.Kind == MediaKind.Image).Cast<object>().ToList();
            }
            
            if (label.Equals("VideoFile", StringComparison.OrdinalIgnoreCase) || 
                label.Equals("VideoId", StringComparison.OrdinalIgnoreCase))
            {
                return assets.Where(m => m.Kind == MediaKind.Video).Cast<object>().ToList();
            }
            
            return assets.Cast<object>().ToList();
        }

        private System.Collections.Generic.List<object> GetPromptNames(Game game)
        {
            var prompts = new System.Collections.Generic.List<PromptPlayerInputCommand>();
            var choices = new System.Collections.Generic.List<AddCustomChoiceCommand>();
            CollectAllPromptPlayerInputCommands(prompts, choices);

            return prompts
                .Select(p => p.PromptName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new NamedOption { Name = name })
                .Cast<object>()
                .ToList();
        }

        private System.Collections.Generic.List<object> GetCustomChoiceTexts(Game game, string targetPromptName)
        {
            var prompts = new System.Collections.Generic.List<PromptPlayerInputCommand>();
            var choices = new System.Collections.Generic.List<AddCustomChoiceCommand>();
            CollectAllPromptPlayerInputCommands(prompts, choices);

            return choices
                .Where(c => string.Equals(c.PromptName, targetPromptName, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ChoiceText)
                .Where(txt => !string.IsNullOrWhiteSpace(txt))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(txt => new NamedOption { Name = txt })
                .Cast<object>()
                .ToList();
        }

        private void CollectAllPromptPlayerInputCommands(System.Collections.Generic.List<PromptPlayerInputCommand> prompts, System.Collections.Generic.List<AddCustomChoiceCommand> customChoices)
        {
            var game = App.CurrentGame;
            if (game == null) return;

            var allActions = new System.Collections.Generic.List<RagsCore.Models.Action>();
            
            if (game.Player?.Actions != null) allActions.AddRange(game.Player.Actions);
            foreach (var r in game.Rooms) { if (r.Actions != null) allActions.AddRange(r.Actions); }
            foreach (var o in game.Objects) { if (o.Actions != null) allActions.AddRange(o.Actions); }
            foreach (var c in game.Characters) { if (c.Actions != null) allActions.AddRange(c.Actions); }
            foreach (var f in game.Functions) { allActions.Add(f); }

            foreach (var action in allActions)
            {
                if (action.Nodes != null)
                {
                    foreach (var node in action.Nodes)
                    {
                        TraverseNode(node, prompts, customChoices);
                    }
                }
            }
        }

        private void TraverseNode(ActionStep node, System.Collections.Generic.List<PromptPlayerInputCommand> prompts, System.Collections.Generic.List<AddCustomChoiceCommand> customChoices)
        {
            if (node == null) return;

            if (node is PromptPlayerInputCommand prompt)
            {
                prompts.Add(prompt);
            }
            else if (node is AddCustomChoiceCommand choice)
            {
                customChoices.Add(choice);
            }
            else if (node is RagsCore.Actions.Condition cond)
            {
                if (cond.TrueBranch != null)
                {
                    foreach (var sub in cond.TrueBranch) TraverseNode(sub, prompts, customChoices);
                }
                if (cond.FalseBranch != null)
                {
                    foreach (var sub in cond.FalseBranch) TraverseNode(sub, prompts, customChoices);
                }
            }
        }
    }
}
