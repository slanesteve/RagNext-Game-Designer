using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Converters;
using CommunityToolkit.Maui.Core;
using System;

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

        public static readonly BindableProperty CharacterProperty =
            BindableProperty.Create(nameof(Character), typeof(Character), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty ActionsProperty =
            BindableProperty.Create(nameof(Actions), typeof(System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty HostElementTypeProperty =
            BindableProperty.Create(nameof(HostElementType), typeof(string), typeof(ActionTreeView), defaultValue: "GameObject");

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

        public Character? Character
        {
            get => (Character?)GetValue(CharacterProperty);
            set => SetValue(CharacterProperty, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>? Actions
        {
            get => (System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>?)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public string HostElementType
        {
            get => (string)GetValue(HostElementTypeProperty);
            set => SetValue(HostElementTypeProperty, value);
        }

        private DateTime _lastContextChangedTime = DateTime.UtcNow;

        private static void OnContextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var self = (ActionTreeView)bindable;
            self._lastContextChangedTime = DateTime.UtcNow;
            if (self.Actions != null)
                self.BindingContext = new ActionLibraryViewModel(self.Actions, self.HostElementType);
            else if (self.Player != null)
                self.BindingContext = new ActionLibraryViewModel(self.Player);
            else if (self.Room != null)
                self.BindingContext = new ActionLibraryViewModel(self.Room);
            else if (self.GameObject != null)
                self.BindingContext = new ActionLibraryViewModel(self.GameObject);
            else if (self.Character != null)
                self.BindingContext = new ActionLibraryViewModel(self.Character);
        }

        private void OnExpanderExpandedChanged(object? sender, ExpandedChangedEventArgs e)
        {
            if (sender is not View view || !e.IsExpanded)
                return;

            // Ignore expander events that fire during initial loading/context binding to prevent scrolling to the bottom on load
            if (DateTime.UtcNow - _lastContextChangedTime < TimeSpan.FromMilliseconds(800))
                return;

            if (BindingContext is ActionLibraryViewModel vm && vm.IsRebuilding)
                return;

            // Delayed execution to allow the expander expansion layout to propagate and update height
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), async () =>
            {
                var scrollView = FindParentScrollView(view);
                if (scrollView != null)
                {
                    // Scroll to position the newly expanded tree node perfectly in view (using MakeVisible to prevent massive layout shifts)
                    await scrollView.ScrollToAsync(view, ScrollToPosition.MakeVisible, true);

                    // If we are close to the bottom, force scroll all the way to the absolute bottom to clear the "scroll for more" warning.
                    var contentHeight = (scrollView.Content as VisualElement)?.Height ?? 0;
                    var viewport = scrollView.Height;
                    var maxScroll = Math.Max(0, contentHeight - viewport);
                    if (maxScroll > 0 && scrollView.ScrollY >= maxScroll - 40)
                    {
                        await scrollView.ScrollToAsync(0, maxScroll, true);
                    }
                }
            });
        }

        private ScrollView? FindParentScrollView(Element? element)
        {
            var parent = element?.Parent;
            while (parent != null)
            {
                if (parent is ScrollView scrollView)
                {
                    return scrollView;
                }
                parent = parent.Parent;
            }
            return null;
        }
        private double _startWidth = 280;

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startWidth = ParentGrid.ColumnDefinitions[0].Width.Value;
                    break;
                case GestureStatus.Running:
                    double newWidth = _startWidth + e.TotalX;
                    newWidth = System.Math.Clamp(newWidth, 180, 700);
                    ParentGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(newWidth));
                    break;
            }
        }

        private void OnPointerEntered(object sender, PointerEventArgs e)
        {
            HoverSplitterLine.FadeTo(1, 100);
            GripperBadge.ScaleTo(1.15, 100);
        }

        private void OnPointerExited(object sender, PointerEventArgs e)
        {
            HoverSplitterLine.FadeTo(0, 100);
            GripperBadge.ScaleTo(1.0, 100);
        }
    }
}