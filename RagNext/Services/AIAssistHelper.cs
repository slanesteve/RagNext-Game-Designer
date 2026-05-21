using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;
using RagNext.Services;
using RagNext.Views.Popups;
using RagNext.Views.Controls;

namespace RagNext.Services
{
    public static class AIAssistHelper
    {
        public static async Task HandleAskAIAsync(Page page, Button btn, object? target, IAIChatService ai)
        {
            string? current = target switch
            {
                Entry entry => entry.Text,
                Editor editor => editor.Text,
                SuggestiveEditor se => se.Text,
                _ => null
            };

            var popup = new AIPromptPopup("Ask AI", current ?? "");
            await page.ShowPopupAsync(popup);

            var prompt = popup.PromptText;
            if (string.IsNullOrWhiteSpace(prompt) || popup.IsCancelled) return;

            using var spin = StartSpinner(btn);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                var result = await ai.AskAsync(prompt, cts.Token);
                if (string.IsNullOrWhiteSpace(result)) return;

                switch (target)
                {
                    case Entry entry: entry.Text = result; break;
                    case Editor editor: editor.Text = result; break;
                    case SuggestiveEditor se: se.Text = result; break;
                }
            }
            catch (AITruncatedException ex)
            {
                if (!string.IsNullOrWhiteSpace(ex.PartialContent))
                {
                    switch (target)
                    {
                        case Entry entry: entry.Text = ex.PartialContent; break;
                        case Editor editor: editor.Text = ex.PartialContent; break;
                        case SuggestiveEditor se: se.Text = ex.PartialContent; break;
                    }
                    await page.DisplayAlert("AI Assist Truncated", $"{ex.Message}\n\nThe partial text generated so far has been filled in.", "OK");
                }
                else
                {
                    await page.DisplayAlert("AI Assist Error", ex.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("AI Assist Error", ex.Message, "OK");
            }
        }

        private static IDisposable StartSpinner(Button btn)
        {
            var original = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";
            var anim = new Animation(v => btn.Rotation = v, 0, 360);
            anim.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);
            return new SpinnerCleanup(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = 0;
                btn.Text = original;
                btn.IsEnabled = true;
            });
        }

        private sealed class SpinnerCleanup : IDisposable
        {
            private readonly Action _cleanup;
            public SpinnerCleanup(Action cleanup) => _cleanup = cleanup;
            public void Dispose() => _cleanup();
        }
    }
}