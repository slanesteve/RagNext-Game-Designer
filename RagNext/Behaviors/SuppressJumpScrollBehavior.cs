using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

namespace RagNext.Behaviors
{
    public sealed class SuppressJumpScrollBehavior : Behavior<ScrollView>
    {
        private ScrollView? _scroll;
        private double _lastY;
        private bool _suppress;

        protected override void OnAttachedTo(ScrollView bindable)
        {
            _scroll = bindable;
            bindable.Scrolled += OnScrolled;
            base.OnAttachedTo(bindable);
        }

        protected override void OnDetachingFrom(ScrollView bindable)
        {
            bindable.Scrolled -= OnScrolled;
            _scroll = null;
            base.OnDetachingFrom(bindable);
        }

        private async void OnScrolled(object? sender, ScrolledEventArgs e)
        {
            // Example rule: prevent jump to top (≈0–50 region). Adjust threshold as needed.
            if (!_suppress && _lastY > 50 && e.ScrollY <= 50)
            {
                try
                {
                    _suppress = true;
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await _scroll!.ScrollToAsync(0, _lastY, animated: false));
                }
                finally
                {
                    _suppress = false;
                }
                return;
            }

            if (Math.Abs(e.ScrollY - _lastY) > 0.5)
            {
                Debug.WriteLine($"[SuppressJumpScroll] Y:{e.ScrollY:0.0} Δ:{e.ScrollY - _lastY:0.0}");
                _lastY = e.ScrollY;
            }
        }
    }
}