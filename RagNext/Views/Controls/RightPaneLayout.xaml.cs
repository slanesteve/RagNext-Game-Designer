using Microsoft.Maui.Controls;

namespace RagNext.Views.Controls
{
    public partial class RightPaneLayout : ContentView
    {
        private double _startWidth = 340;

        public RightPaneLayout() => InitializeComponent();

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startWidth = ParentGrid.ColumnDefinitions[2].Width.Value;
                    break;
                case GestureStatus.Running:
                    double newWidth = _startWidth - e.TotalX;
                    newWidth = System.Math.Clamp(newWidth, 200, 600);
                    ParentGrid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(newWidth));
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

        public static readonly BindableProperty MainProperty =
            BindableProperty.Create(nameof(Main), typeof(View), typeof(RightPaneLayout), propertyChanged: OnMainChanged);

        public View? Main
        {
            get => (View?)GetValue(MainProperty);
            set => SetValue(MainProperty, value);
        }

        private static void OnMainChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var self = (RightPaneLayout)bindable;
            var newView = (View?)newValue;
            self.MainPresenter.Content = newView;
            if (newView != null)
            {
                newView.BindingContext = self.BindingContext;
            }
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (Main != null)
            {
                Main.BindingContext = BindingContext;
            }
        }
    }
}