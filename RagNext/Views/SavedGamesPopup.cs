using System;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes; // Add this using directive
using RagNext.Services;

namespace RagNext.Views
{
    // Popup used by MainPage to select a saved game.
    public class SavedGamesPopup : Popup
    {
        private readonly TaskCompletionSource<string?> _tcs = new();
        public Task<string?> ResultTask => _tcs.Task;

        public SavedGamesPopup(string[] saves)
        {
            CanBeDismissedByTappingOutsideOfPopup = true;

            // Ensure ResultTask completes even when dismissed by tapping outside
            Closed += (s, e) =>
            {
                if (!_tcs.Task.IsCompleted)
                    _tcs.TrySetResult(null);
            };

            var stack = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 12
            };

            stack.Add(new Label
            {
                Text = "Select Saved Game",
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                HorizontalTextAlignment = TextAlignment.Center
            });

            foreach (var save in saves)
            {
                // Row container
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Padding = new Thickness(0, 0, 0, 4)
                };

                var selectBtn = new Button
                {
                    Text = save,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Center
                };
                selectBtn.Clicked += async (s, e) =>
                {
                    _tcs.TrySetResult(save);
                    await this.CloseAsync();
                };

                var deleteBtn = new ImageButton
                {
                    Source = "trashcan.png", // use PNG from Resources/Images
                    WidthRequest = 24,
                    HeightRequest = 24,
                    Padding = new Thickness(4),
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Aspect = Aspect.AspectFit
                };
                deleteBtn.SetValue(SemanticProperties.DescriptionProperty, $"Delete {save}");

                // Fallback if svg not found (e.g. build action not set yet)
                deleteBtn.Loaded += (s, e) =>
                {
                    if (deleteBtn.Source == null)
                    {
                        deleteBtn.Source = new FontImageSource
                        {
                            Glyph = "\ue74d",
                            FontFamily = "SegoeFluentIcons",
                            Color = Colors.Red,
                            Size = 20
                        };
                    }
                };

                deleteBtn.Clicked += async (s, e) =>
                {
                    var confirm = await Application.Current!.MainPage.DisplayAlert(
                        "Delete",
                        $"Delete save '{save}'?",
                        "Yes",
                        "No");

                    if (!confirm)
                        return;

                    try
                    {
                        await GameStorage.DeleteAsync(save);
                        stack.Remove(row);
                        if (stack.Children.Count <= 2) // header + cancel
                        {
                            if (!_tcs.Task.IsCompleted)
                                _tcs.TrySetResult(null);
                            await this.CloseAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await Application.Current!.MainPage.DisplayAlert(
                            "Error",
                            $"Failed to delete '{save}': {ex.Message}",
                            "OK");
                    }
                };

                row.Add(selectBtn, 0, 0);
                row.Add(deleteBtn, 1, 0);
                stack.Add(row);
            }

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Clicked += async (s, e) =>
            {
                _tcs.TrySetResult(null);
                await this.CloseAsync();
            };
            stack.Add(cancelBtn);

            Content = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
                Padding = 10,
                Content = stack
            };
        }

        protected virtual void OnClosed(object? result)
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.TrySetResult(result as string);
            // No base.OnClosed(result) call, as Popup does not define OnClosed.
        }
    }
}