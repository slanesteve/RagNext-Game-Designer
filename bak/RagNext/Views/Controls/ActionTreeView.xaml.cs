using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RagsCore.Actions;
using System.Threading.Tasks;

namespace RagNext.Views.Controls
{
    public partial class ActionTreeView : ContentView
    {
        public ActionTreeView() { InitializeComponent(); }

        public static readonly BindableProperty TreeProperty =
            BindableProperty.Create(nameof(Tree), typeof(ActionTree), typeof(ActionTreeView));

        public ActionTree? Tree
        {
            get => (ActionTree?)GetValue(TreeProperty);
            set => SetValue(TreeProperty, value);
        }

        public static readonly BindableProperty SelectedBranchProperty =
            BindableProperty.Create(nameof(SelectedBranch), typeof(ActionBranch), typeof(ActionTreeView), default(ActionBranch), BindingMode.TwoWay,
                propertyChanged: OnSelectedBranchChanged);

        public ActionBranch? SelectedBranch
        {
            get => (ActionBranch?)GetValue(SelectedBranchProperty);
            set => SetValue(SelectedBranchProperty, value);
        }

        static void OnSelectedBranchChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ActionTreeView atv) atv.UpdateDefaultHeaderVisual();
        }

        void UpdateDefaultHeaderVisual()
        {
            var selectedColor = TryGetColor("Gray600", Colors.LightGray);
            var normalColor = TryGetColor("Gray400", Colors.LightGray);
            DefaultHeader.BackgroundColor = SelectedBranch is null ? selectedColor : normalColor;
        }

        static Color TryGetColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c)
                return c;
            return fallback;
        }

        void OnSelectDefaultTapped(object? sender, TappedEventArgs e)
        {
            SelectedBranch = null;
            UpdateDefaultHeaderVisual();
        }

        async void OnAddDefaultCommandClicked(object? sender, System.EventArgs e)
        {
            if (Tree is null) return;
            var label = await PromptAsync("Add Command", "Enter command name (optional):", "e.g. Initialize state");
            var cmd = new SetVariableCommand { Name = "example", Value = "value", Label = string.IsNullOrWhiteSpace(label) ? null : label };
            Tree.DefaultCommands.Add(cmd);
        }

        async void OnAddTopLevelBranchClicked(object? sender, System.EventArgs e)
            => await AddTopLevelActionAsync();

        public async Task AddTopLevelActionAsync()
        {
            if (Tree is null) return;
            var name = await PromptAsync("Add Action", "Enter action name:", "New action");
            if (string.IsNullOrWhiteSpace(name)) return;
            Tree.Branches.Add(new ActionBranch { Name = name });
        }

        public async Task AddCommandToBranchAsync(ActionBranch branch)
        {
            var label = await PromptAsync("Add Command", "Enter command name (optional):", "e.g. Set variable");
            var cmd = new SetVariableCommand { Name = "example", Value = "value", Label = string.IsNullOrWhiteSpace(label) ? null : label };
            branch.Commands.Add(cmd);
        }

        public async Task AddChildBranchAsync(ActionBranch branch)
        {
            var name = await PromptAsync("Add Child Action", "Enter child action name:", "Child action");
            if (string.IsNullOrWhiteSpace(name)) return;
            branch.Children.Add(new ActionBranch { Name = name });
        }

        public void RemoveBranch(ActionBranch target)
        {
            if (Tree is null) return;
            if (Tree.Branches.Remove(target)) return;
            _ = RemoveFromChildren(Tree.Branches, target);
        }

        bool RemoveFromChildren(System.Collections.ObjectModel.ObservableCollection<ActionBranch> list, ActionBranch target)
        {
            foreach (var b in list)
            {
                if (b.Children.Remove(target)) return true;
                if (RemoveFromChildren(b.Children, target)) return true;
            }
            return false;
        }

        static Task<string?> PromptAsync(string title, string message, string placeholder = "", string accept = "OK", string cancel = "Cancel")
            => Application.Current?.MainPage?.DisplayPromptAsync(title, message, accept: accept, cancel: cancel, placeholder: placeholder)
               ?? Task.FromResult<string?>(null);

        public void SelectBranch(ActionBranch branch)
        {
            SelectedBranch = branch;
        }
    }
}