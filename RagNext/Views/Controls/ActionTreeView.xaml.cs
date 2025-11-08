using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Converters;

namespace RagNext.Views.Controls
{
    public partial class ActionTreeView : ContentView
    {
        public ActionTreeView()
        {
            InitializeComponent();
        }

        public static readonly BindableProperty PlayerProperty =
            BindableProperty.Create(nameof(Player), typeof(Player), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty RoomProperty =
            BindableProperty.Create(nameof(Room), typeof(Room), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty GameObjectProperty =
            BindableProperty.Create(nameof(GameObject), typeof(GameObject), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public Player? Player
        {
            get => (Player?)GetValue(PlayerProperty);
            set => SetValue(PlayerProperty, value);
        }

        public Room? Room
        {
            get => (Room?)GetValue(RoomProperty);
            set => SetValue(RoomProperty, value);
        }

        public GameObject? GameObject
        {
            get => (GameObject?)GetValue(GameObjectProperty);
            set => SetValue(GameObjectProperty, value);
        }

        private static void OnContextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var self = (ActionTreeView)bindable;
            if (self.Player != null)
                self.BindingContext = new ActionLibraryViewModel(self.Player);
            else if (self.Room != null)
                self.BindingContext = new ActionLibraryViewModel(self.Room);
            else if (self.GameObject != null)
                self.BindingContext = new ActionLibraryViewModel(self.GameObject);
        }
    }
}