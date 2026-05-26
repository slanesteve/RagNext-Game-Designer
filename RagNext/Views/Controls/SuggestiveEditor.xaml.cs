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
            BindableProperty.Create(nameof(Text), typeof(string), typeof(SuggestiveEditor), string.Empty, BindingMode.TwoWay,
                propertyChanged: OnTextChangedPropertyChanged);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly BindableProperty ShowToolbarProperty =
            BindableProperty.Create(nameof(ShowToolbar), typeof(bool), typeof(SuggestiveEditor), false);

        public bool ShowToolbar
        {
            get => (bool)GetValue(ShowToolbarProperty);
            set => SetValue(ShowToolbarProperty, value);
        }

        private int _bindingContextVersion = 0;
        private bool _isBindingContextChanging = false;

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            // Only take manual control of the editor text when the control is used in
            // "InputDefinition mode" (the action step editor). In all other cases
            // (Room, Player, Object, etc.) the XAML TwoWay binding on Text drives the
            // editor via OnTextChangedPropertyChanged, so we must NOT clear the editor here.
            if (BindingContext is RagsCore.Actions.InputDefinition input)
            {
                var currentVersion = ++_bindingContextVersion;
                _isBindingContextChanging = true;
                try
                {
                    var valStr = input.Value?.ToString() ?? string.Empty;
                    if (MainEditor.Text != valStr)
                    {
                        MainEditor.Text = valStr;
                    }
                    UpdateLivePreview(valStr);
                }
                finally
                {
                    Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                    {
                        if (currentVersion == _bindingContextVersion)
                            _isBindingContextChanging = false;
                    });
                }
            }
            // For all other binding contexts, do nothing — let {Binding Description, Mode=TwoWay}
            // flow through the Text BindableProperty → OnTextChangedPropertyChanged → MainEditor.Text.
        }

        private static void OnTextChangedPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SuggestiveEditor se)
            {
                if (se._isBindingContextChanging)
                    return;

                var newText = (string)newValue;
                if (se.MainEditor.Text != newText)
                {
                    se.MainEditor.Text = newText;
                }
                se.UpdateLivePreview(newText);
                se.MainEditor.InvalidateMeasure();
                se.InvalidateMeasure();
            }
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
        private bool _isSquareBracketMode = false;
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
            // Clear suggestion cache on focus; they are fetched dynamically on trigger character keypresses
            _allSuggestions.Clear();
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
            if (_isBindingContextChanging)
                return;

            // Hide help popup card on typing
            HelpPopupCard.IsVisible = false;
            HelpDismissOverlay.IsVisible = false;

            // Keep Text property in sync
            Text = e.NewTextValue ?? string.Empty;
            UpdateLivePreview(Text);

            // Forward text changed event
            TextChanged?.Invoke(this, e);

            var newText = e.NewTextValue ?? string.Empty;
            var cursor = MainEditor.CursorPosition;

            // Trigger autocomplete if character before cursor is '{'
            if (cursor > 0 && newText.Length >= cursor && newText[cursor - 1] == '{')
            {
                _isAutocompleteMode = true;
                _isSquareBracketMode = false;
                _bracketIndex = cursor - 1;
                _allSuggestions = IntelligenceProvider.GetSuggestions(App.CurrentGame);
                UpdateSuggestions(string.Empty);
                return;
            }

            // Trigger autocomplete if character before cursor is '['
            if (cursor > 0 && newText.Length >= cursor && newText[cursor - 1] == '[')
            {
                _isAutocompleteMode = true;
                _isSquareBracketMode = true;
                _bracketIndex = cursor - 1;
                _allSuggestions = IntelligenceProvider.GetEntitySuggestions(App.CurrentGame);
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
                var closingBracket = _isSquareBracketMode ? "]" : "}";

                // Terminate autocomplete if query has closing bracket or (for braces only) spaces
                if ((!_isSquareBracketMode && query.Contains(" ")) || query.Contains(closingBracket))
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

        private void OnSuggestionItemTapped(object? sender, EventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is SuggestionItem selected)
            {
                ApplyAutocomplete(selected);
            }
        }

        private void OnSuggestionPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Grid grid)
            {
                var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                grid.BackgroundColor = isDark ? Color.FromArgb("#252530") : Color.FromArgb("#F0F0F5");
            }
        }

        private void OnSuggestionPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.BackgroundColor = Colors.Transparent;
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

                var insertion = _isSquareBracketMode ? $"[{item.Token}]" : $"{{{item.Token}}}";
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
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            HelpIcon.BackgroundColor = isDark ? Color.FromArgb("#3a3a4c") : Color.FromArgb("#D0D0D5");
        }

        private void OnHelpIconPointerExited(object? sender, PointerEventArgs e)
        {
            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            HelpIcon.BackgroundColor = isDark ? Color.FromArgb("#2a2a35") : Color.FromArgb("#E0E0E5");
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

        public void WrapSelection(string startTag, string endTag)
        {
            var text = MainEditor.Text ?? string.Empty;
            var cursor = MainEditor.CursorPosition;
            var selectionLength = MainEditor.SelectionLength;

            if (selectionLength > 0 && cursor >= 0 && cursor + selectionLength <= text.Length)
            {
                var before = text.Substring(0, cursor);
                var selected = text.Substring(cursor, selectionLength);
                var after = text.Substring(cursor + selectionLength);

                // Case A: Selected text is already wrapped in the tags (e.g., "<b>hello</b>")
                if (selected.StartsWith(startTag) && selected.EndsWith(endTag))
                {
                    var unwrapped = selected.Substring(startTag.Length, selected.Length - startTag.Length - endTag.Length);
                    MainEditor.Text = before + unwrapped + after;
                    MainEditor.CursorPosition = cursor;
                    MainEditor.SelectionLength = unwrapped.Length;
                }
                // Case B: Selection is immediately bordered by the tags (e.g., "<b>" + "hello" + "</b>")
                else if (before.EndsWith(startTag) && after.StartsWith(endTag))
                {
                    var newBefore = before.Substring(0, before.Length - startTag.Length);
                    var newAfter = after.Substring(endTag.Length);
                    MainEditor.Text = newBefore + selected + newAfter;
                    MainEditor.CursorPosition = newBefore.Length;
                    MainEditor.SelectionLength = selected.Length;
                }
                else
                {
                    // Not wrapped, so wrap it
                    MainEditor.Text = before + startTag + selected + endTag + after;
                    MainEditor.CursorPosition = cursor + startTag.Length;
                    MainEditor.SelectionLength = selected.Length;
                }
            }
            else
            {
                if (cursor < 0 || cursor > text.Length)
                {
                    cursor = text.Length;
                }
                var before = text.Substring(0, cursor);
                var after = text.Substring(cursor);

                // If cursor is immediately between the tags, remove them (toggle empty)
                if (before.EndsWith(startTag) && after.StartsWith(endTag))
                {
                    var newBefore = before.Substring(0, before.Length - startTag.Length);
                    var newAfter = after.Substring(endTag.Length);
                    MainEditor.Text = newBefore + newAfter;
                    MainEditor.CursorPosition = newBefore.Length;
                }
                else
                {
                    MainEditor.Text = before + startTag + endTag + after;
                    MainEditor.CursorPosition = cursor + startTag.Length;
                }
            }
            MainEditor.Focus();
        }

        private void OnBoldClicked(object? sender, EventArgs e) => WrapSelection("<b>", "</b>");
        private void OnItalicClicked(object? sender, EventArgs e) => WrapSelection("<i>", "</i>");
        private void OnUnderlineClicked(object? sender, EventArgs e) => WrapSelection("<u>", "</u>");

        private async void OnColorClicked(object? sender, EventArgs e)
        {
            var colors = new[] { "Red (#FF0000)", "Green (#00FF00)", "Blue (#0000FF)", "Yellow (#FFFF00)", "Orange (#FFA500)", "Purple (#800080)", "Custom..." };
            var choice = await Application.Current?.MainPage?.DisplayActionSheet("Select Text Color", "Cancel", null, colors);
            if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

            string hex = "#FFFFFF";
            if (choice.Contains("#"))
            {
                int idx = choice.IndexOf('#');
                int closeIdx = choice.IndexOf(')');
                if (idx != -1 && closeIdx != -1)
                {
                    hex = choice.Substring(idx, closeIdx - idx);
                }
                else
                {
                    hex = choice.Substring(idx);
                }
            }
            else if (choice == "Custom...")
            {
                var customHex = await Application.Current?.MainPage?.DisplayPromptAsync("Custom Color", "Enter Hex Color (e.g. #FF5500):", "OK", "Cancel", "#FF5500");
                if (string.IsNullOrWhiteSpace(customHex)) return;
                hex = customHex.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
            }

            WrapSelection($"<color={hex}>", "</color>");
        }

        private async void OnHighlightClicked(object? sender, EventArgs e)
        {
            var colors = new[] { "Yellow (#FFFF00)", "Green (#00FF00)", "Cyan (#00FFFF)", "Magenta (#FF00FF)", "Red (#FF0000)", "Custom..." };
            var choice = await Application.Current?.MainPage?.DisplayActionSheet("Select Highlight Color", "Cancel", null, colors);
            if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

            string hex = "#FFFF00";
            if (choice.Contains("#"))
            {
                int idx = choice.IndexOf('#');
                int closeIdx = choice.IndexOf(')');
                if (idx != -1 && closeIdx != -1)
                {
                    hex = choice.Substring(idx, closeIdx - idx);
                }
                else
                {
                    hex = choice.Substring(idx);
                }
            }
            else if (choice == "Custom...")
            {
                var customHex = await Application.Current?.MainPage?.DisplayPromptAsync("Custom Highlight", "Enter Hex Color (e.g. #FFFF00):", "OK", "Cancel", "#FFFF00");
                if (string.IsNullOrWhiteSpace(customHex)) return;
                hex = customHex.Trim();
                if (!hex.StartsWith("#")) hex = "#" + hex;
            }

            if (hex.Length == 7)
            {
                hex += "55"; // Append 33% alpha transparency so text shines through TMPro mark highlights
            }

            WrapSelection($"<mark={hex}>", "</mark>");
        }

        private void OnClearFormattingClicked(object? sender, EventArgs e)
        {
            var text = MainEditor.Text ?? string.Empty;
            var cursor = MainEditor.CursorPosition;
            var selectionLength = MainEditor.SelectionLength;

            if (selectionLength > 0 && cursor >= 0 && cursor + selectionLength <= text.Length)
            {
                var before = text.Substring(0, cursor);
                var selected = text.Substring(cursor, selectionLength);
                var after = text.Substring(cursor + selectionLength);

                // Strip all tags inside the selection
                var cleaned = System.Text.RegularExpressions.Regex.Replace(selected, @"<[^>]+>", "");

                // Recursively strip any bordering tags (open/close tag pairs that immediately touch the selection boundaries)
                while (true)
                {
                    var openBeforeMatch = System.Text.RegularExpressions.Regex.Match(before, @"<[^>]+>$");
                    var closeAfterMatch = System.Text.RegularExpressions.Regex.Match(after, @"^</[^>]+>");
                    if (openBeforeMatch.Success && closeAfterMatch.Success)
                    {
                        before = before.Substring(0, before.Length - openBeforeMatch.Length);
                        after = after.Substring(closeAfterMatch.Length);
                    }
                    else
                    {
                        // Check for matching open/open or close/close border tags to strip nesting cleanly
                        var borderBefore = System.Text.RegularExpressions.Regex.Match(before, @"<[^>]+>$");
                        var borderAfter = System.Text.RegularExpressions.Regex.Match(after, @"^<[^>]+>");
                        if (borderBefore.Success && borderAfter.Success)
                        {
                            before = before.Substring(0, before.Length - borderBefore.Length);
                            after = after.Substring(borderAfter.Length);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                MainEditor.Text = before + cleaned + after;
                MainEditor.CursorPosition = before.Length;
                MainEditor.SelectionLength = cleaned.Length;
            }
            else
            {
                // If no selection, clean the entire field
                var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
                MainEditor.Text = cleaned;
                MainEditor.CursorPosition = Math.Min(cursor, cleaned.Length);
            }
            MainEditor.Focus();
        }

        private Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim().Trim('"', '\'');
            if (!hex.StartsWith("#"))
            {
                hex = "#" + hex;
            }

            try
            {
                if (hex.Length == 4) // #RGB
                {
                    char r = hex[1], g = hex[2], b = hex[3];
                    hex = $"#{r}{r}{g}{g}{b}{b}";
                }
                else if (hex.Length == 5) // #RGBA
                {
                    char r = hex[1], g = hex[2], b = hex[3], a = hex[4];
                    hex = $"#{a}{a}{r}{r}{g}{g}{b}{b}";
                }
                else if (hex.Length == 9) // #RRGGBBAA (Unity standard)
                {
                    // Unity is RGBA, MAUI is ARGB. Let's convert RRGGBBAA -> AARRGGBB
                    string rr = hex.Substring(1, 2);
                    string gg = hex.Substring(3, 2);
                    string bb = hex.Substring(5, 2);
                    string aa = hex.Substring(7, 2);
                    hex = $"#{aa}{rr}{gg}{bb}";
                }
                return Color.FromArgb(hex);
            }
            catch
            {
                return null;
            }
        }

        private FormattedString ParseUnityTagsToFormattedString(string text)
        {
            var formattedString = new FormattedString();
            if (string.IsNullOrEmpty(text)) return formattedString;

            // Simple tag parsing using Regex. Split string into tags <...> and text segments
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"(<[^>]+>|[^<]+)");

            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            Color textColor = null;
            Color bgColor = null;

            // Stacks to track color and background nesting
            var colorStack = new System.Collections.Generic.Stack<Color>();
            var bgStack = new System.Collections.Generic.Stack<Color>();

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var token = match.Value;
                if (token.StartsWith("<") && token.EndsWith(">"))
                {
                    var tag = token.ToLower().Trim('<', '>');
                    if (tag == "b") isBold = true;
                    else if (tag == "/b") isBold = false;
                    else if (tag == "i") isItalic = true;
                    else if (tag == "/i") isItalic = false;
                    else if (tag == "u") isUnderline = true;
                    else if (tag == "/u") isUnderline = false;
                    else if (tag.StartsWith("color="))
                    {
                        var val = tag.Substring("color=".Length);
                        var parsedColor = ParseHexColor(val);
                        if (parsedColor != null)
                        {
                            colorStack.Push(parsedColor);
                            textColor = parsedColor;
                        }
                    }
                    else if (tag == "/color")
                    {
                        if (colorStack.Count > 0) colorStack.Pop();
                        textColor = colorStack.Count > 0 ? colorStack.Peek() : null;
                    }
                    else if (tag.StartsWith("mark="))
                    {
                        var val = tag.Substring("mark=".Length);
                        // If Unity mark hex does not have alpha (length 7: #RRGGBB), append '55' (33% opacity)
                        // so that it renders beautifully translucent and doesn't obscure the text.
                        if (val.StartsWith("#") && val.Length == 7)
                        {
                            val += "55";
                        }
                        var parsedColor = ParseHexColor(val);
                        if (parsedColor != null)
                        {
                            bgStack.Push(parsedColor);
                            bgColor = parsedColor;
                        }
                    }
                    else if (tag == "/mark")
                    {
                        if (bgStack.Count > 0) bgStack.Pop();
                        bgColor = bgStack.Count > 0 ? bgStack.Peek() : null;
                    }
                }
                else
                {
                    // Text segment
                    var span = new Span
                    {
                        Text = token,
                        FontAttributes = (isBold ? FontAttributes.Bold : FontAttributes.None) | (isItalic ? FontAttributes.Italic : FontAttributes.None),
                        TextDecorations = isUnderline ? TextDecorations.Underline : TextDecorations.None
                    };
                    if (textColor != null)
                    {
                        span.TextColor = textColor;
                    }
                    if (bgColor != null)
                    {
                        span.BackgroundColor = bgColor;
                    }
                    formattedString.Spans.Add(span);
                }
            }

            return formattedString;
        }

        public void UpdateLivePreview(string text)
        {
            if (PreviewLabel != null)
            {
                PreviewLabel.FormattedText = ParseUnityTagsToFormattedString(text);
            }
        }
    }
}
