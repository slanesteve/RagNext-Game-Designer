using Microsoft.Maui.Controls;

namespace RagNext.Views.Controls
{
    public partial class RightPaneLayout : ContentView
    {
        public RightPaneLayout() => InitializeComponent();

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