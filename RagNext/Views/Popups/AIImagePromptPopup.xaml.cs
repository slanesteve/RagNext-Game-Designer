using System;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace RagNext.Views.Popups
{
    public partial class AIImagePromptPopup : Popup
    {
        private const string LastSizePreferenceKey = "AIImagePromptPopup_LastSize";

        public string PromptText => PromptEditor.Text;
        public int SelectedSize { get; private set; } = 1024;
        public bool IsCancelled { get; private set; } = false;

        public AIImagePromptPopup(string title, string placeholder = "")
        {
            InitializeComponent();
            TitleLabel.Text = title;
            PromptEditor.Placeholder = placeholder;

            // Load last selected size from Preferences, default to "1024 x 1024"
            string lastSize = Preferences.Get(LastSizePreferenceKey, "1024 x 1024");
            SizePicker.SelectedItem = lastSize;
        }

        private async void OnOkClicked(object sender, EventArgs e)
        {
            // Save selected size to Preferences
            string selected = SizePicker.SelectedItem as string ?? "1024 x 1024";
            Preferences.Set(LastSizePreferenceKey, selected);

            SelectedSize = selected.StartsWith("480") ? 480 : selected.StartsWith("720") ? 720 : 1024;
            
            await base.CloseAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            IsCancelled = true;
            await base.CloseAsync();
        }

        private async void OnAskAIClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            var ai = MauiProgram.Services.GetService(typeof(RagNext.Services.IAIChatService)) as RagNext.Services.IAIChatService;
            if (ai == null)
            {
                await Application.Current.MainPage.DisplayAlert("AI Helper", "AI service is not configured.", "OK");
                return;
            }

            var parentPage = Application.Current?.Windows?[0]?.Page ?? Application.Current?.MainPage;
            if (parentPage != null)
            {
                await RagNext.Services.AIAssistHelper.HandleAskAIAsync(parentPage, btn, PromptEditor, ai);
            }
        }
    }
}
