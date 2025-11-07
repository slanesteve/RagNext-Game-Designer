using Microsoft.Maui.Controls;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public sealed class NodeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? AssetTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is MediaLibraryViewModel.Node n)
                return n.IsFolder ? (FolderTemplate ?? new DataTemplate()) : (AssetTemplate ?? new DataTemplate());

            return AssetTemplate ?? FolderTemplate ?? new DataTemplate();
        }
    }
}