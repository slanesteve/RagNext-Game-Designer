using Microsoft.Maui.Controls;
using RagNext.ViewModels;

namespace RagNext.Views
{
    public partial class ActionLibraryPage : ContentPage
    {
        public ActionLibraryPage(RagsCore.Models.Player player)
        {
            InitializeComponent();
            BindingContext = new ActionLibraryViewModel(player);
        }
    }
}