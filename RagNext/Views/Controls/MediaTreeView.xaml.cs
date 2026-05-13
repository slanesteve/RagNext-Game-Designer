using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
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

        // Add this event handler for DragStarting
        private void OnMediaDragStarting(object sender, DragStartingEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is object ctx)
            {
                e.Data.Properties["DraggedItem"] = ctx;

                var nameProp = ctx.GetType().GetProperty("Name");
                if (nameProp?.GetValue(ctx) is string name && !string.IsNullOrWhiteSpace(name))
                {
                    e.Data.Text = name;
                }
            }

            // Remove invalid property usage
            // e.AllowedOperations = DataPackageOperation.Copy;
        }
    }
}