using Microsoft.Maui.Controls;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public partial class MediaTreeView : ContentView
    {
        public MediaTreeView()
        {
            InitializeComponent();
            BindingContext = MauiProgram.Services.GetService(typeof(MediaLibraryViewModel)) as MediaLibraryViewModel;
        }
    }
}