using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using RagsCore.Actions;

namespace RagNext.Views.Controls
{
    public partial class ActionBranchEditorView : ContentView
    {
        public ActionBranchEditorView()
        {
            // Ensure this method exists in the generated partial class from XAML
            InitializeComponent();
        }

        // UI lists
        public ObservableCollection<string> AvailableConditionKeys { get; } =
            new(["Default", "var.equals", "player.inRoom", "room.hasObject"]);

        public string? SelectedConditionKey
        {
            get => _selectedConditionKey;
            set
            {
                _selectedConditionKey = value;
                ApplySelectedCondition();
            }
        }
        private string? _selectedConditionKey;

        void ApplySelectedCondition()
        {
            if (BindingContext is not ActionBranch branch) return;
            switch (SelectedConditionKey)
            {
                case null:
                case "Default":
                    branch.Condition = null;
                    break;
                case "var.equals":
                    branch.Condition = new VariableEqualsCondition();
                    break;
                case "player.inRoom":
                    branch.Condition = new PlayerInRoomCondition();
                    break;
                case "room.hasObject":
                    branch.Condition = new RoomHasObjectCondition();
                    break;
            }
        }

        void OnClearConditionClicked(object? sender, EventArgs e)
        {
            SelectedConditionKey = "Default";
        }

        void OnAddChildClicked(object? sender, EventArgs e)
        {
            if (BindingContext is not ActionBranch branch) return;
            branch.Children.Add(new ActionBranch());
        }
    }
}