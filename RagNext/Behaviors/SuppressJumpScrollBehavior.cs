using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RagNext.Behaviors
{
    public sealed class SuppressJumpScrollBehavior : Behavior<ScrollView>
    {
        private ScrollView? _scroll;
        private double _lastY;
        private bool _suppress;

        // Size-tracking and scroll restoration variables
        private double _preJumpScrollY;
        private bool _hasPreJump;
        private double _lastContentHeight;
        private double _savedScrollYBeforeShrink;
        private bool _isShrunk;
        private CancellationTokenSource? _shrinkTimeoutCts;

        protected override void OnAttachedTo(ScrollView bindable)
        {
            _scroll = bindable;
            bindable.Scrolled += OnScrolled;
            bindable.PropertyChanged += OnScrollPropertyChanged;
            if (bindable.Content != null)
            {
                bindable.Content.SizeChanged += OnContentSizeChanged;
                _lastContentHeight = bindable.Content.Height;
            }
            base.OnAttachedTo(bindable);
        }

        protected override void OnDetachingFrom(ScrollView bindable)
        {
            bindable.Scrolled -= OnScrolled;
            bindable.PropertyChanged -= OnScrollPropertyChanged;
            if (bindable.Content != null)
            {
                bindable.Content.SizeChanged -= OnContentSizeChanged;
            }
            _shrinkTimeoutCts?.Cancel();
            _scroll = null;
            base.OnDetachingFrom(bindable);
        }

        private void OnScrollPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScrollView.Content) && _scroll != null)
            {
                if (_scroll.Content != null)
                {
                    _scroll.Content.SizeChanged -= OnContentSizeChanged;
                    _scroll.Content.SizeChanged += OnContentSizeChanged;
                    _lastContentHeight = _scroll.Content.Height;
                }
            }
        }

        private void OnContentSizeChanged(object? sender, EventArgs e)
        {
            if (_scroll?.Content is not VisualElement content) return;

            double newHeight = content.Height;
            if (newHeight <= 0) return;

            if (_lastContentHeight > 0)
            {
                double delta = newHeight - _lastContentHeight;
                if (delta < -10) // Significant shrink
                {
                    if (!_isShrunk)
                    {
                        _savedScrollYBeforeShrink = _hasPreJump ? _preJumpScrollY : _lastY;
                        _isShrunk = true;
                        Debug.WriteLine($"[SuppressJumpScroll] Shrunk from {_lastContentHeight:0.0} to {newHeight:0.0}. Saved pre-clamp ScrollY: {_savedScrollYBeforeShrink:0.0}");

                        // Setup a 1-second safety timeout in case the layout doesn't expand back (e.g. permanent deletion)
                        _shrinkTimeoutCts?.Cancel();
                        _shrinkTimeoutCts = new CancellationTokenSource();
                        var token = _shrinkTimeoutCts.Token;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(1000, token);
                                if (!token.IsCancellationRequested)
                                {
                                    _isShrunk = false;
                                    _savedScrollYBeforeShrink = 0;
                                    _hasPreJump = false;
                                    Debug.WriteLine("[SuppressJumpScroll] Shrunk state safety timeout cleared.");
                                }
                            }
                            catch (TaskCanceledException) { }
                        });
                    }
                }
                else if (delta > 10) // Layout expanded back!
                {
                    if (_isShrunk && _savedScrollYBeforeShrink > 0)
                    {
                        double targetY = _savedScrollYBeforeShrink;

                        _shrinkTimeoutCts?.Cancel();
                        _shrinkTimeoutCts = null;
                        _isShrunk = false;
                        _hasPreJump = false;
                        _savedScrollYBeforeShrink = 0;

                        Debug.WriteLine($"[SuppressJumpScroll] Expanded from {_lastContentHeight:0.0} to {newHeight:0.0}. Restoring ScrollY to {targetY:0.0}");

                        _ = MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (_scroll == null) return;
                            try
                            {
                                _suppress = true;
                                await Task.Delay(50); // Give MAUI a frame to arrange the visual tree
                                await _scroll.ScrollToAsync(0, targetY, animated: false);
                                _lastY = targetY;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[SuppressJumpScroll] Failed to restore ScrollY: {ex.Message}");
                            }
                            finally
                            {
                                _suppress = false;
                            }
                        });
                    }
                }
            }

            _lastContentHeight = newHeight;
        }

        private void OnScrolled(object? sender, ScrolledEventArgs e)
        {
            // Prevent sudden jump to top (region <= 50)
            if (!_suppress && _lastY > 50 && e.ScrollY <= 50)
            {
                _suppress = true;
                var targetY = _lastY;
                _ = MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        // Pass 1: Quick restore after 150ms
                        await Task.Delay(150);
                        if (_scroll != null)
                        {
                            await _scroll.ScrollToAsync(0, targetY, animated: false);
                        }

                        // Pass 2: Safety restore after 400ms to catch slower layout settling
                        await Task.Delay(250);
                        if (_scroll != null)
                        {
                            await _scroll.ScrollToAsync(0, targetY, animated: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SuppressJumpScroll] Delayed restoration failed: {ex.Message}");
                    }
                    finally
                    {
                        _suppress = false;
                    }
                });
                return;
            }

            if (Math.Abs(e.ScrollY - _lastY) > 0.5)
            {
                if (!_suppress && e.ScrollY < _lastY - 10)
                {
                    // Sudden scroll decrease: likely layout-induced scroll clamping
                    _preJumpScrollY = _lastY;
                    _hasPreJump = true;
                    Debug.WriteLine($"[SuppressJumpScroll] Sudden scroll decrease: {_lastY:0.0} -> {e.ScrollY:0.0}. Saved pre-jump ScrollY.");
                }
                else if (e.ScrollY >= _lastY)
                {
                    // Normal scrolling down or staying same resets pre-jump flag
                    _hasPreJump = false;
                }

                _lastY = e.ScrollY;
            }
        }
    }
}