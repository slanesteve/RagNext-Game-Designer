using System;
using System.Collections.ObjectModel;
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

    public sealed class EditStepViewModel : BindableObject
    {
        private ActionStep _target;
        private readonly Func<ActionStep, Task> _afterMutate;
        private bool _isSaving;

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
                    var newTarget = (ActionStep)Activator.CreateInstance(value.Type)!;
                    newTarget.Label = _target.Label;
                    if (newTarget is RagsCore.Actions.Condition newCond && _target is RagsCore.Actions.Condition oldCond)
                    {
                        newCond.TrueBranch = oldCond.TrueBranch;
                        newCond.FalseBranch = oldCond.FalseBranch;
                    }
                    _target = newTarget;
                    BuildInputsFromTarget();
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
            var types = Assembly.GetAssembly(typeof(ActionStep))!.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(baseType));

            foreach (var t in types)
            {
                var instance = (ActionStep)Activator.CreateInstance(t)!;
                Definitions.Add(new StepTypeWrapper { Name = instance.TypeName, Type = t });
            }

            _selectedDefinition = Definitions.FirstOrDefault(d => d.Type == _target.GetType());
            OnPropertyChanged(nameof(SelectedDefinition));

            BuildInputsFromTarget();

            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(() => _afterMutate(null));
        }

        private void BuildInputsFromTarget()
        {
            EditableInputs.Clear();
            var props = _target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.Name != "Label" && p.Name != "TrueBranch" && p.Name != "FalseBranch");

            foreach (var p in props)
            {
                var input = new InputDefinition
                {
                    Label = p.Name,
                    Value = p.GetValue(_target),
                    ControlType = GetControlType(p),
                    DataType = GetDataType(p)
                };
                PreparePickerSource(input);
                EditableInputs.Add(input);
            }
        }

        private InputControlType GetControlType(PropertyInfo p)
        {
            if (p.PropertyType == typeof(bool)) return InputControlType.Checkbox;
            if (p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(float)) return InputControlType.Number;
            if (p.PropertyType == typeof(Guid) || p.Name.Contains("Id")) return InputControlType.ComboBox;
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName") && 
                (p.DeclaringType == typeof(SetVariableCommand) || p.DeclaringType == typeof(SetNumericRandomlyCommand) || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType!))) 
                return InputControlType.ComboBox; 
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (p.Name.Equals("Gender", StringComparison.OrdinalIgnoreCase)) return InputControlType.ComboBox;
            if (p.Name.Contains("Text", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Description", StringComparison.OrdinalIgnoreCase)) return InputControlType.TextArea;
            return InputControlType.Text;
        }

        private InputDataType GetDataType(PropertyInfo p)
        {
            if (p.Name.Equals("RoomId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Room;
            if (p.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("ContainerObjectId", StringComparison.OrdinalIgnoreCase)) return InputDataType.GameObject;
            if (p.Name.Equals("CharacterId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Character;
            if (p.Name.Equals("ItemId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Item;
            if (p.Name.Equals("SoundId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("PortraitId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("MediaId", StringComparison.OrdinalIgnoreCase)) return InputDataType.Media;
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return InputDataType.Operator;
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName") && 
                (p.DeclaringType == typeof(SetVariableCommand) || p.DeclaringType == typeof(SetNumericRandomlyCommand) || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType!))) 
                return InputDataType.Variable;
            return InputDataType.String;
        }

        private void PreparePickerSource(InputDefinition input)
        {
            var game = App.CurrentGame;
            if (game is null || input.ControlType != InputControlType.ComboBox) return;

            input.PickerSource = input.DataType switch
            {
                InputDataType.Room => game.Rooms.Cast<object>().ToList(),
                InputDataType.GameObject or InputDataType.Item => game.Objects.Cast<object>().ToList(),
                InputDataType.Character => game.Characters.Cast<object>().ToList(),
                InputDataType.Variable => game.Variables.Cast<object>().ToList(),
                InputDataType.Media => game.MediaAssets.Cast<object>().ToList(),
                InputDataType.Operator => new List<object> { "=", "!=", ">", ">=", "<", "<=" },
                _ => input.Label.Equals("Gender", StringComparison.OrdinalIgnoreCase)
                     ? new List<object> { "Male", "Female", "Non-binary", "Other" }
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
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                foreach (var src in EditableInputs)
                {
                    var p = _target.GetType().GetProperty(src.Label);
                    if (p != null && p.CanWrite)
                    {
                        object? valToSet = src.Value;
                        
                        if (src.ControlType == InputControlType.ComboBox && valToSet != null)
                        {
                            if (valToSet is GameVariable gv) { valToSet = gv.Name; }
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
                            else
                            {
                                try { valToSet = Convert.ChangeType(valToSet, p.PropertyType); } catch { }
                            }
                        }

                        p.SetValue(_target, valToSet);
                    }
                }

                await _afterMutate(_target);
            }
            finally
            {
                _isSaving = false;
            }
            await Task.CompletedTask;
        }
    }
}
