using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public partial class ActionEditorView : ContentView
    {
        public ActionEditorView()
        {
            InitializeComponent();
        }

        // Game to edit against
        public static readonly BindableProperty GameProperty =
            BindableProperty.Create(nameof(Game), typeof(Game), typeof(ActionEditorView), null, propertyChanged: OnParamsChanged);
        public Game? Game
        {
            get => (Game?)GetValue(GameProperty);
            set => SetValue(GameProperty, value);
        }

        // Context room (optional)
        public static readonly BindableProperty RoomProperty =
            BindableProperty.Create(nameof(Room), typeof(Room), typeof(ActionEditorView), null, propertyChanged: OnParamsChanged);
        public Room? Room
        {
            get => (Room?)GetValue(RoomProperty);
            set => SetValue(RoomProperty, value);
        }

        // Focus object (optional)
        public static readonly BindableProperty FocusObjectProperty =
            BindableProperty.Create(nameof(FocusObject), typeof(GameObject), typeof(ActionEditorView), null, propertyChanged: OnParamsChanged);
        public GameObject? FocusObject
        {
            get => (GameObject?)GetValue(FocusObjectProperty);
            set => SetValue(FocusObjectProperty, value);
        }

        // The action being edited
        public static readonly BindableProperty ActionProperty =
            BindableProperty.Create(nameof(Action), typeof(GameAction), typeof(ActionEditorView), null, propertyChanged: OnParamsChanged);
        public GameAction? Action
        {
            get => (GameAction?)GetValue(ActionProperty);
            set => SetValue(ActionProperty, value);
        }

        private static void OnParamsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var view = (ActionEditorView)bindable;
            view.TryUpdateContext();
        }

        private void TryUpdateContext()
        {
            if (Game is null || Action is null)
                return;

            BindingContext = new ActionEditorViewModel(Game, Action, Room, FocusObject);
        }
    }
}