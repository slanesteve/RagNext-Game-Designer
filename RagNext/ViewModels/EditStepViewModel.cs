using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models;
using RagNext;

namespace RagNext.ViewModels
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

    public sealed class EditStepViewModel : BindableObject
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
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
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
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
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
            if (p.PropertyType.IsEnum || p.Name == "StoreVariableName" || p.Name == "InputType" || p.PropertyType == typeof(Guid) || p.Name.Contains("Id")) return InputControlType.ComboBox;
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName" || p.Name == "SourceName") && 
                (p.DeclaringType != null && (p.DeclaringType.Name.Contains("Variable") || p.DeclaringType.Name.Contains("Random") || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType)))) 
                return InputControlType.ComboBox; 
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (p.Name.Equals("Gender", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (p.Name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (p.Name.Contains("Text", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Description", StringComparison.OrdinalIgnoreCase)) return InputControlType.TextArea;
            return InputControlType.Text;
        }

        private InputDataType GetDataType(PropertyInfo p)
        {
            if (p.Name == "StoreVariableName") return InputDataType.Variable;
            if (p.Name.Equals("RoomId", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("DestinationRoomId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Room;
            if (p.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("ContainerObjectId", StringComparison.OrdinalIgnoreCase)) return InputDataType.GameObject;
            if (p.Name.Equals("CharacterId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Character;
            if (p.Name.Equals("ItemId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Item;
            if (p.Name.Equals("SoundId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("PortraitId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("MediaId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Media;
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputDataType.Operator;
            if (p.Name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return InputDataType.Direction;
            if (p.Name.Equals("FunctionId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Function;
            if (p.Name.Equals("TimerId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Timer;
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName" || p.Name == "SourceName") && 
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

            input.PickerSource = input.DataType switch
            {
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
                                if (p.PropertyType == typeof(Guid) && valToSet is string strGuid && Guid.TryParse(strGuid, out var g))
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
                target is PlaySoundEffectCommand)
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
    }
}
