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
            self.MainPresenter.Content = (View?)newValue;
        }
    }
}