using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using RagNext.Services;

namespace RagNext.Views.Controls
{
    public partial class SuggestiveEditor : ContentView
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(SuggestiveEditor), string.Empty, BindingMode.TwoWay);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SuggestiveEditor), string.Empty);

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public event EventHandler<TextChangedEventArgs>? TextChanged;
        public event EventHandler<FocusEventArgs>? UnfocusedEvent;

        private bool _isAutocompleteMode = false;
        private int _bracketIndex = -1;
        private List<SuggestionItem> _allSuggestions = new();

        private Thickness? _originalMargin;
        private bool _isMarginExpanded = false;

        public SuggestiveEditor()
        {
            InitializeComponent();
        }

        private void OnEditorFocused(object? sender, FocusEventArgs e)
        {
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;
            // Load fresh suggestions dynamically from the current game
            _allSuggestions = IntelligenceProvider.GetSuggestions(App.CurrentGame);
        }

        private void OnEditorUnfocused(object? sender, FocusEventArgs e)
        {
            // Forward unfocused event
            UnfocusedEvent?.Invoke(this, e);

            // Delayed hiding to let tap events on the list view process
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
            {
                HidePopup();
            });
        }

        private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Hide help popup card on typing
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;

            // Keep Text property in sync
            Text = e.NewTextValue ?? string.Empty;

            // Forward text changed event
            TextChanged?.Invoke(this, e);

            var newText = e.NewTextValue ?? string.Empty;
            var cursor = MainEditor.CursorPosition;

            // Trigger autocomplete if character before cursor is '{'
            if (cursor > 0 && newText.Length >= cursor && newText[cursor - 1] == '{')
            {
                _isAutocompleteMode = true;
                _bracketIndex = cursor - 1;
                UpdateSuggestions(string.Empty);
                return;
            }

            if (_isAutocompleteMode)
            {
                if (cursor <= _bracketIndex || cursor > newText.Length)
                {
                    HidePopup();
                    return;
                }

                var query = newText.Substring(_bracketIndex + 1, cursor - (_bracketIndex + 1));

                // Terminate autocomplete if query has spaces or closing bracket
                if (query.Contains(" ") || query.Contains("}"))
                {
                    HidePopup();
                    return;
                }

                UpdateSuggestions(query);
            }
        }

        private void UpdateSuggestions(string query)
        {
            var filtered = _allSuggestions
                .Where(s => s.Token.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Any())
            {
                SuggestionsList.ItemsSource = filtered;
                
                bool wasVisible = PopupCard.IsVisible;
                PopupCard.IsVisible = true;

                if (!wasVisible)
                {
                    ExpandScrollSpaceAndScroll();
                }
            }
            else
            {
                HidePopup();
            }
        }

        private void OnSuggestionTapped(object? sender, ItemTappedEventArgs e)
        {
            if (e.Item is SuggestionItem selected)
            {
                ApplyAutocomplete(selected);
            }
        }

        private void ApplyAutocomplete(SuggestionItem item)
        {
            var currentText = MainEditor.Text ?? string.Empty;
            var cursor = MainEditor.CursorPosition;

            if (_bracketIndex >= 0 && _bracketIndex < currentText.Length)
            {
                var before = currentText.Substring(0, _bracketIndex);
                var after = cursor < currentText.Length ? currentText.Substring(cursor) : string.Empty;

                var insertion = $"{{{item.Token}}}";
                MainEditor.Text = before + insertion + after;

                // Move cursor after the inserted bracket token
                MainEditor.CursorPosition = before.Length + insertion.Length;
            }

            HidePopup();
            
            // Re-focus the editor so editing is seamless
            MainEditor.Focus();
        }

        private void HidePopup()
        {
            PopupCard.IsVisible = false;
            _isAutocompleteMode = false;
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;
            RestoreScrollSpace();
        }

        private void ExpandScrollSpaceAndScroll()
        {
            var scrollView = FindParentScrollView();
            if (scrollView == null) return;

            // Calculate absolute position of the editor within the ScrollView content
            double absoluteY = 0;
            Element? curr = this;
            while (curr != null && curr != scrollView)
            {
                if (curr is VisualElement ve)
                {
                    absoluteY += ve.Y;
                }
                curr = curr.Parent;
            }

            var viewportHeight = scrollView.Height > 0 ? scrollView.Height : 500.0;
            var editorHeight = MainEditor.Height > 0 ? MainEditor.Height : (this.Height > 0 ? this.Height : 60.0);
            
            // Available space below the editor inside the ScrollView viewport
            var spaceBelowViewport = viewportHeight - (absoluteY - scrollView.ScrollY) - editorHeight;
            var spaceAboveViewport = absoluteY - scrollView.ScrollY;

            // Decide whether to show the popup above or below the editor.
            // We show it above if:
            // 1. Available space below the editor is tight (< 220px).
            // 2. There is enough space above the editor to fully show the popup card (>= 228px).
            bool showAbove = spaceBelowViewport < 220.0 && spaceAboveViewport >= 228.0;

            if (showAbove)
            {
                // Position above: popup is 220px tall, so we place its top at -228px (leaving a nice 8px gap)
                PopupCard.Margin = new Thickness(12, -228, 0, 0);

                // Since it's above, we don't need any bottom margin expansion!
                RestoreScrollSpace();
            }
            else
            {
                // Position below: standard position starting 80px down from the editor top
                PopupCard.Margin = new Thickness(12, 80, 0, 0);

                if (!_isMarginExpanded)
                {
                    _isMarginExpanded = true;
                    _originalMargin = this.Margin;

                    var contentHeight = (scrollView.Content as VisualElement)?.Height ?? 0;
                    var editorBottom = absoluteY + editorHeight;
                    var spaceBelow = Math.Max(0, contentHeight - editorBottom);

                    // The autocomplete popup card bottom starts 80px down from the editor's top (Margin="12, 80, 0, 0")
                    // and has a height of 220px. So its bottom is at 80 + 220 = 300px from the top of the editor.
                    // The space needed below the bottom of the editor is therefore: 300.0 - editorHeight.
                    // We add a 12px buffer for shadows/padding and a clean, tight look.
                    var neededSpace = Math.Max(0.0, 300.0 - editorHeight) + 12.0;
                    var marginToAdd = Math.Max(0, neededSpace - spaceBelow);

                    if (marginToAdd > 0)
                    {
                        // Only add the absolute minimum margin required to prevent clipping
                        var newMargin = new Thickness(
                            _originalMargin.Value.Left,
                            _originalMargin.Value.Top,
                            _originalMargin.Value.Right,
                            _originalMargin.Value.Bottom + marginToAdd
                        );
                        this.Margin = newMargin;
                    }
                    else
                    {
                        // There is already enough natural layout space below the editor, so no extra margin is needed!
                        _isMarginExpanded = false;
                    }
                }
            }

            // Smoothly scroll the editor control into view. A slight delay gives MAUI time to update the layout margin.
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), async () =>
            {
                await scrollView.ScrollToAsync(this, ScrollToPosition.MakeVisible, true);

                // If we are close to the bottom, force scroll all the way to the absolute bottom to clear the "scroll for more" warning.
                var contentHeight = (scrollView.Content as VisualElement)?.Height ?? 0;
                var viewport = scrollView.Height;
                var maxScroll = Math.Max(0, contentHeight - viewport);
                if (maxScroll > 0 && scrollView.ScrollY >= maxScroll - 40)
                {
                    await scrollView.ScrollToAsync(0, maxScroll, true);
                }
            });
        }

        private void RestoreScrollSpace()
        {
            if (_isMarginExpanded && _originalMargin.HasValue)
            {
                this.Margin = _originalMargin.Value;
                _isMarginExpanded = false;
                _originalMargin = null;
            }
            // Reset popup margin to default below position
            PopupCard.Margin = new Thickness(12, 80, 0, 0);
        }

        private void OnHelpIconTapped(object? sender, EventArgs e)
        {
            HidePopup();
            HelpPopupCard.IsVisible = !HelpPopupCard.IsVisible;
            HelpDismissOverlay.IsVisible = HelpPopupCard.IsVisible;
        }

        private void OnCloseHelpTapped(object? sender, EventArgs e)
        {
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;
        }

        private void OnHelpPopupTapped(object? sender, EventArgs e)
        {
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;
        }

        private void OnHelpDismissOverlayTapped(object? sender, EventArgs e)
        {
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;
            MainEditor.Focus();
        }

        private void OnHelpIconPointerEntered(object? sender, PointerEventArgs e)
        {
            HelpIcon.BackgroundColor = Color.FromArgb("#3a3a4c");
        }

        private void OnHelpIconPointerExited(object? sender, PointerEventArgs e)
        {
            HelpIcon.BackgroundColor = Color.FromArgb("#2a2a35");
        }

        private void OnMainEditorHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (MainEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox nativeTextBox)
            {
                var current = nativeTextBox.Padding;
                // Add a right-padding of 38px to keep text from running under the top-right ? icon (which extends up to 34px)
                nativeTextBox.Padding = new Microsoft.UI.Xaml.Thickness(current.Left, current.Top, 38, current.Bottom);
            }
#endif
        }

        private ScrollView? FindParentScrollView()
        {
            Element? parent = this.Parent;
            while (parent != null)
            {
                if (parent is ScrollView scrollView)
                {
                    return scrollView;
                }
                parent = parent.Parent;
            }
            return null;
        }
    }
}
