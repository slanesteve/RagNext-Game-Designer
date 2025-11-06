using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace RagNext.Views.Popups
{
    public partial class AIPromptPopup : Popup
    {
        public string PromptText => PromptEditor.Text;
        public bool IsCancelled { get; private set; } = false;

        public AIPromptPopup(string title, string? initialText = null)
        {
            InitializeComponent();
            TitleLabel.Text = title;
            PromptEditor.Text = initialText ?? string.Empty;
        }

        private async void OnOkClicked(object sender, EventArgs e) => await base.CloseAsync();

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            IsCancelled = true;
            await base.CloseAsync();
        }
    }
}