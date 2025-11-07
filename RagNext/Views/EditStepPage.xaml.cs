using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class EditStepPage : ContentPage
    {
        public EditStepPage(StepDefinitionBase step)
        {
            InitializeComponent();
            BindingContext = new EditStepViewModel(step, CloseAsync);
        }

        private async Task CloseAsync() => await Navigation.PopModalAsync();
    }
}