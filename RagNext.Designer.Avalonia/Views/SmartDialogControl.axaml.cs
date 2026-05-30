using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace RagNext.Designer.Avalonia.Views
{
    public partial class SmartDialogControl : UserControl
    {
        private TextBox _textBox = null!;
        private Popup _popup = null!;
        private ListBox _listBox = null!;
        private string _activeTrigger = "";

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<SmartDialogControl, string>(nameof(Text));

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public SmartDialogControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _textBox = this.FindControl<TextBox>("DialogTextBox")!;
            _popup = this.FindControl<Popup>("AutocompletePopup")!;
            _listBox = this.FindControl<ListBox>("AutocompleteList")!;

            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.PropertyChanged += TextBox_PropertyChanged;
            _listBox.DoubleTapped += ListBox_DoubleTapped;
        }

        private void TextBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.TextProperty)
            {
                var newText = _textBox.Text ?? string.Empty;
                if (Text != newText)
                {
                    Text = newText;
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty)
            {
                var val = change.NewValue as string ?? string.Empty;
                if (_textBox != null && _textBox.Text != val)
                {
                    _textBox.Text = val;
                }
            }
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_popup.IsOpen)
            {
                if (e.Key == Key.Escape)
                {
                    _popup.IsOpen = false;
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Down)
                {
                    var nextIdx = _listBox.SelectedIndex + 1;
                    if (nextIdx < _listBox.Items.Count)
                    {
                        _listBox.SelectedIndex = nextIdx;
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Up)
                {
                    var prevIdx = _listBox.SelectedIndex - 1;
                    if (prevIdx >= 0)
                    {
                        _listBox.SelectedIndex = prevIdx;
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    InsertSelectedToken();
                    e.Handled = true;
                    return;
                }
            }

            if (e.KeySymbol == "[")
            {
                // Entity Trigger '['
                _activeTrigger = "[";
                ShowAutocomplete(new List<string> { "Starting Chamber", "iron_pry_bar", "Mysterious Box", "Sword of Light" });
            }
            else if (e.KeySymbol == "{")
            {
                // Curly bracket '{'
                _activeTrigger = "{";
                ShowAutocomplete(new List<string> { "Player.Name", "Player.Gender", "Game.Title", "Story.TurnCount" });
            }
        }

        private void ShowAutocomplete(List<string> items)
        {
            _listBox.ItemsSource = items;
            if (items.Any())
            {
                _listBox.SelectedIndex = 0;
                _popup.IsOpen = true;
            }
            else
            {
                _popup.IsOpen = false;
            }
        }

        private void ListBox_DoubleTapped(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertSelectedToken();
        }

        private void InsertSelectedToken()
        {
            if (_listBox.SelectedItem is string selectedText)
            {
                var currentText = _textBox.Text ?? string.Empty;
                var caret = _textBox.CaretIndex;

                string insertText = "";
                if (_activeTrigger == "{")
                {
                    insertText = "{" + selectedText + "}";
                }
                else if (_activeTrigger == "[")
                {
                    insertText = "[" + selectedText + "]";
                }

                if (string.IsNullOrEmpty(insertText)) return;

                // Simple insert at caret
                var prefix = currentText.Substring(0, Math.Max(0, caret - 1));
                var suffix = currentText.Substring(caret);
                _textBox.Text = prefix + insertText + suffix;
                _textBox.CaretIndex = prefix.Length + insertText.Length;
            }
            _popup.IsOpen = false;
        }
    }
}
