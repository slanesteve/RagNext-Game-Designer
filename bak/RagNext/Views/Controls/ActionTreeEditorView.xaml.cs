using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagNext.Views.Controls
{
    public partial class ActionTreeEditorView : ContentView
    {
        public ActionTreeEditorView()
        {
            InitializeComponent();
            BindingContext = this;
        }

        public static readonly BindableProperty TreeProperty =
            BindableProperty.Create(nameof(Tree), typeof(ActionTree), typeof(ActionTreeEditorView));

        public ActionTree? Tree
        {
            get => (ActionTree?)GetValue(TreeProperty);
            set => SetValue(TreeProperty, value);
        }

        public static readonly BindableProperty SelectedBranchProperty =
            BindableProperty.Create(
                nameof(SelectedBranch),
                typeof(ActionBranch),
                typeof(ActionTreeEditorView),
                default(ActionBranch),
                BindingMode.TwoWay);

        public ActionBranch? SelectedBranch
        {
            get => (ActionBranch?)GetValue(SelectedBranchProperty);
            set => SetValue(SelectedBranchProperty, value);
        }

        public static readonly BindableProperty GameProperty =
            BindableProperty.Create(nameof(Game), typeof(object), typeof(ActionTreeEditorView), null);

        public object Game
        {
            get => GetValue(GameProperty);
            set => SetValue(GameProperty, value);
        }

        public static readonly BindableProperty RoomProperty =
            BindableProperty.Create(nameof(Room), typeof(Room), typeof(ActionTreeEditorView));

        public Room? Room
        {
            get => (Room?)GetValue(RoomProperty);
            set => SetValue(RoomProperty, value);
        }

        public static readonly BindableProperty FocusObjectProperty =
            BindableProperty.Create(nameof(FocusObject), typeof(GameObject), typeof(ActionTreeEditorView));

        public GameObject? FocusObject
        {
            get => (GameObject?)GetValue(FocusObjectProperty);
            set => SetValue(FocusObjectProperty, value);
        }

        private void OnAddBranchClicked(object sender, System.EventArgs e)
        {
            if (Tree is null) return;
            Tree.Branches.Add(new ActionBranch());
        }

        private void OnClearBranchesClicked(object sender, System.EventArgs e)
        {
            Tree?.Branches.Clear();
        }
    }
}