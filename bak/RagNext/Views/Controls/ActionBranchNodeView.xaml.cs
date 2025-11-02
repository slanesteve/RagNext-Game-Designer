using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RagsCore.Actions;
using System.Threading.Tasks;

namespace RagNext.Views.Controls
{
    public partial class ActionBranchNodeView : ContentView
    {
        private ActionTreeView? _owner;

        public ActionBranchNodeView() { InitializeComponent(); }

        ActionTreeView? FindOwner()
        {
            Element? p = this.Parent;
            while (p is not null && p is not ActionTreeView) p = p.Parent;
            return p as ActionTreeView;
        }

        protected override void OnParentChanged()
        {
            base.OnParentChanged();
            if (_owner is not null) _owner.PropertyChanged -= OnOwnerPropertyChanged;
            _owner = FindOwner();
            if (_owner is not null) _owner.PropertyChanged += OnOwnerPropertyChanged;
            UpdateSelectionVisual();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            UpdateSelectionVisual();
        }

        void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActionTreeView.SelectedBranch))
                UpdateSelectionVisual();
        }

        void UpdateSelectionVisual()
        {
            var normal = TryGetColor("Gray400", Colors.LightGray);
            var selected = TryGetColor("Gray600", Colors.LightGray);

            if (BindingContext is ActionBranch b && _owner?.SelectedBranch == b)
                HeaderGrid.BackgroundColor = selected;
            else
                HeaderGrid.BackgroundColor = normal;
        }

        static Color TryGetColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c)
                return c;
            return fallback;
        }

        void OnSelectTapped(object? sender, TappedEventArgs e)
        {
            if (BindingContext is not ActionBranch b) return;
            FindOwner()?.SelectBranch(b);
        }

        void OnAddCommandClicked(object? sender, System.EventArgs e)
        {
            if (BindingContext is not ActionBranch b) return;
            _ = FindOwner()?.AddCommandToBranchAsync(b);
        }

        void OnAddChildClicked(object? sender, System.EventArgs e)
        {
            if (BindingContext is not ActionBranch b) return;
            _ = FindOwner()?.AddChildBranchAsync(b);
        }

        void OnAddTopLevelActionClicked(object? sender, System.EventArgs e)
        {
            _ = FindOwner()?.AddTopLevelActionAsync();
        }

        void OnRemoveClicked(object? sender, System.EventArgs e)
        {
            if (BindingContext is not ActionBranch b) return;
            FindOwner()?.RemoveBranch(b);
        }
    }
}