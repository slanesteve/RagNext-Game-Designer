using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Storage;
using RagsCore.Models;
using RagNext.Services;

namespace RagNext.Views.Popups
{
    public partial class PublishGamePopup : Popup
    {
        private readonly Game _game;

        public PublishGamePopup(Game game)
        {
            InitializeComponent();
            _game = game;

            // Load initial metadata values
            TitleEntry.Text = game.Title ?? "My Adventure";
            AuthorEntry.Text = game.Author ?? Environment.UserName ?? "Unknown";
            VersionEntry.Text = game.Version ?? "1.0.0";

            // Set default destination path to user Documents folder
            string defaultDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DestinationEntry.Text = Path.Combine(defaultDocs, "RagNext_Published", SanitizeFolder(game.Title ?? "MyAdventure"));
        }

        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
                if (result != null && result.IsSuccessful && result.Folder != null)
                {
                    DestinationEntry.Text = result.Folder.Path;
                }
            }
            catch (Exception ex)
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Browse Error", $"Failed to browse directories: {ex.Message}", "OK");
                }
            }
        }

        private async void OnPublishClicked(object sender, EventArgs e)
        {
            var destination = DestinationEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(destination))
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Validation", "Please select an export destination directory.", "OK");
                return;
            }

            var title = TitleEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Validation", "Please enter a game title.", "OK");
                return;
            }

            // Lock form elements
            SetControlsEnabled(false);
            ProgressSection.IsVisible = true;
            PublishSpinner.IsRunning = true;
            ProgressStatusLabel.Text = "Publishing game, preparing files...";

            try
            {
                // Update game model
                _game.Title = title;
                _game.Author = AuthorEntry.Text?.Trim() ?? string.Empty;
                _game.Version = VersionEntry.Text?.Trim() ?? "1.0.0";

                int targetPlatform = PlatformPicker.SelectedIndex;

                // Execute the publication in a background thread to prevent UI freezing
                await Task.Run(async () =>
                {
                    await PublishEngine.PublishAsync(_game, targetPlatform, destination);
                });

                ProgressStatusLabel.Text = "Publish complete!";
                PublishSpinner.IsRunning = false;

                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Success", $"Game '{title}' has been successfully published to:\n{destination}", "OK");
                }
                
                await base.CloseAsync();
            }
            catch (Exception ex)
            {
                ProgressSection.IsVisible = false;
                PublishSpinner.IsRunning = false;
                SetControlsEnabled(true);
                
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Publish Error", $"Failed to compile standalone: {ex.Message}", "OK");
                }
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await base.CloseAsync();
        }

        private void SetControlsEnabled(bool enabled)
        {
            TitleEntry.IsEnabled = enabled;
            AuthorEntry.IsEnabled = enabled;
            VersionEntry.IsEnabled = enabled;
            PlatformPicker.IsEnabled = enabled;
            DestinationEntry.IsEnabled = enabled;
            PublishBtn.IsEnabled = enabled;
        }

        private string SanitizeFolder(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(name, c => !System.Linq.Enumerable.Contains(invalid, c)), c => c))).Trim();
            return string.IsNullOrEmpty(clean) ? "Adventure" : clean.Replace(" ", "_");
        }
    }
}
