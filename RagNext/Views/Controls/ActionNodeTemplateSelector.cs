using Microsoft.Maui.Controls;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public sealed class ActionNodeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ActionTemplate { get; set; }
        public DataTemplate? NodeTemplate { get; set; }
        public DataTemplate? StepTemplate { get; set; }
        public DataTemplate? InputTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is ActionLibraryViewModel.Node n)
                return n.Kind switch
                {
                    ActionLibraryViewModel.NodeKind.Action => ActionTemplate!,
                    ActionLibraryViewModel.NodeKind.Condition => NodeTemplate!,
                    ActionLibraryViewModel.NodeKind.Command => StepTemplate!,
                    ActionLibraryViewModel.NodeKind.Input => InputTemplate!,
                    _ => InputTemplate!
                };
            return InputTemplate ?? new DataTemplate();
        }
    }
}