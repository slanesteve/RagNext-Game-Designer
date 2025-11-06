using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls;
using RagNext.Services;
using RagNext.Views.Popups;

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
                _ => null
            };

            var popup = new AIPromptPopup("Ask AI", current ?? "");
            await page.ShowPopupAsync(popup);

            var prompt = popup.PromptText;
            if (string.IsNullOrWhiteSpace(prompt) || popup.IsCancelled) return;

            using var spin = StartSpinner(btn);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await ai.AskAsync(prompt, cts.Token);
            if (string.IsNullOrWhiteSpace(result)) return;

            switch (target)
            {
                case Entry entry: entry.Text = result; break;
                case Editor editor: editor.Text = result; break;
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