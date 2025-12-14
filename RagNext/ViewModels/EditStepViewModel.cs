using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models; // for Game collections
using RagNext;
using System.Runtime.CompilerServices;         // to access App.CurrentGame

namespace RagNext.ViewModels
{
    // Add a flag to prevent duplicate saves and stop triggering full tree rebuilds that recreate nodes.
    public sealed class EditStepViewModel : BindableObject
    {
        private readonly StepDefinitionBase _target;
        private readonly Func<Task> _afterMutate;
        private bool _isSaving; // NEW

        public ObservableCollection<StepDefinitionBase> Definitions { get; } = new();
        public ObservableCollection<InputDefinition> EditableInputs { get; } = new();

        private StepDefinitionBase? _selectedDefinition;
        public StepDefinitionBase? SelectedDefinition
        {
            get => _selectedDefinition;
            set
            {
                if (_selectedDefinition == value) return;
                _selectedDefinition = value;
                OnPropertyChanged();
                if (value != null) LoadInputsFromDefinition(value);
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditStepViewModel(StepDefinitionBase target, Func<Task> afterMutate)
        {
            _target = target;
            _afterMutate = afterMutate;

            if (_target.Kind == StepKind.Command && Game.AvailableCommands != null)
                foreach (var c in Game.AvailableCommands) Definitions.Add(CloneDefinition(c));
            else if (_target.Kind == StepKind.Condition && Game.AvailableConditions != null)
                foreach (var c in Game.AvailableConditions) Definitions.Add(CloneDefinition(c));

            SelectedDefinition = Definitions.FirstOrDefault(d => d.Name == _target.Name) ?? Definitions.FirstOrDefault();
            if (EditableInputs.Count > 0) EditableInputs.Clear();
            int counter = 0;
            foreach (var i in _target.Inputs)
            {
                var clone = CloneInput(i);
                PreparePickerSource(clone);

                // Ensure current value is the same instance as an item in PickerSource (so Picker can select it)
                if (clone.PickerSource != null)
                {
                    var savedName = TryGetName(_target.Inputs[counter].Value);
                    var selected = clone.PickerSource.Cast<object?>().FirstOrDefault(v =>
                        ReferenceEquals(v, i.Value) ||
                        Equals(v, i.Value) ||
                        string.Equals(TryGetName(v), savedName, StringComparison.Ordinal) ||
                        string.Equals(v?.ToString(), i.Value?.ToString(), StringComparison.Ordinal));
                    if (selected != null) clone.Value = selected;
                }

                EditableInputs.Add(clone);
                counter++;
            }

            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await CancelAsync());
        }

        private async Task CancelAsync() => await _afterMutate();

        private void LoadInputsFromDefinition(StepDefinitionBase def)
        {
            EditableInputs.Clear();
            int count = 0;
            foreach (var i in def.Inputs)
            {
                var clone = CloneInput(i);
                PreparePickerSource(clone);
                if (clone.PickerSource != null)
                {
                    var savedName = TryGetName(_target.Inputs[count].Value);//i.Value);
                    var selected = clone.PickerSource.Cast<object?>().FirstOrDefault(v =>
                        ReferenceEquals(v, i.Value) ||
                        Equals(v, i.Value) ||
                        string.Equals(TryGetName(v), savedName, StringComparison.Ordinal) ||
                        string.Equals(v?.ToString(), i.Value?.ToString(), StringComparison.Ordinal));
                    if (selected != null) clone.Value = selected;
                }

                EditableInputs.Add(clone);
                count++;
            }
        }

        private static StepDefinitionBase CloneDefinition(StepDefinitionBase d) =>
            d.Kind == StepKind.Command
                ? new CommandDefinition { Name = d.Name, Category = d.Category, Inputs = d.Inputs.Select(CloneInput).ToList() }
                : new ConditionDefinition { Name = d.Name, Category = d.Category, Inputs = d.Inputs.Select(CloneInput).ToList() };

        private static InputDefinition CloneInput(InputDefinition i) => new()
        {
            Label = i.Label,
            ControlType = i.ControlType,
            DataType = i.DataType,
            Value = i.Value
        };

        private void PreparePickerSource(InputDefinition input)
        {
            var game = App.CurrentGame;
            if (game is null) return;

            if (input.ControlType != InputControlType.ComboBox)
                return;

            input.PickerSource = input.DataType switch
            {
                InputDataType.Room => game.Rooms.Cast<object>().ToList(),
                InputDataType.GameObject or InputDataType.Item => game.Objects.Cast<object>().ToList(),
                InputDataType.Character => game.Characters.Cast<object>().ToList(),
                InputDataType.Variable => game.Variables.Cast<object>().ToList(),
                _ => null
            };

            // Normalize Value to the actual object instance from PickerSource when possible
            if (input.PickerSource is not null && input.Value is not null)
            {
                var savedName = TryGetName(input.Value);
                var match = input.PickerSource.Cast<object?>().FirstOrDefault(v =>
                    ReferenceEquals(v, input.Value) ||
                    Equals(v, input.Value) ||
                    (!string.IsNullOrWhiteSpace(savedName) && string.Equals(TryGetName(v), savedName, StringComparison.Ordinal)));
                if (match is not null)
                    input.Value = match;
            }
        }

        private async Task SaveAsync()
        {
            if (_isSaving) return;           // PREVENT DOUBLE EXECUTION
            _isSaving = true;
            try
            {
                if (SelectedDefinition != null)
                {
                    _target.Name = SelectedDefinition.Name;
                    _target.Category = SelectedDefinition.Category;

                    // Replace inputs in-place (do not append).
                    _target.Inputs.Clear();
                    foreach (var i in EditableInputs)
                        _target.Inputs.Add(CloneInput(i));
                }

                // Just notify parent to refresh names, NOT to rebuild/append steps.
                await _afterMutate();
            }
            finally
            {
                _isSaving = false;
            }
        }

        private static string? TryGetName(object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;
            if (value is Enum e) return e.ToString();

            if (value is System.Text.Json.JsonElement el)
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.String) return el.GetString();
                if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (el.TryGetProperty("name", out var lower)) return lower.GetString();
                    if (el.TryGetProperty("Name", out var upper)) return upper.GetString();
                }
                return el.ToString();
            }

            if (value is System.Collections.IDictionary dict)
            {
                if (dict.Contains("name")) return dict["name"]?.ToString();
                if (dict.Contains("Name")) return dict["Name"]?.ToString();
            }

            var prop = value.GetType().GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.GetValue(value) is string pn && !string.IsNullOrWhiteSpace(pn)) return pn;

            return value.ToString();
        }
    }
}