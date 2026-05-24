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
        private PackagingTarget _selectedTarget = PackagingTarget.Windows;

        // Maps target index (picker order) to enum
        private static readonly PackagingTarget[] _targets =
        [
            PackagingTarget.Windows,
            PackagingTarget.MacOS,
            PackagingTarget.Linux,
            PackagingTarget.WebGL
        ];

        public PublishGamePopup(Game game)
        {
            InitializeComponent();
            _game = game;

            // Pre-fill metadata
            TitleEntry.Text   = game.Title   ?? "My Adventure";
            AuthorEntry.Text  = game.Author  ?? Environment.UserName ?? "Unknown";
            VersionEntry.Text = game.Version ?? "1.0.0";

            // Default output folder
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DestinationEntry.Text = Path.Combine(docs, "RagNext_Published",
                PublishEngine.SanitizeName(game.Title ?? "MyAdventure"));

            // Show template availability on each platform card
            RefreshTemplateStatus();

            // Show game summary
            var s = PublishEngine.GetPublishSummary(game);
            SummaryLabel.Text =
                $"📖 {s.RoomCount} rooms  |  📦 {s.ObjectCount} objects  |  " +
                $"👤 {s.CharacterCount} characters  |  🔢 {s.VariableCount} variables  |  " +
                $"🖼️ {s.MediaCount} media assets";

            // Select Windows card by default
            SelectCard(PackagingTarget.Windows);

            // Wire progress event
            PublishEngine.OnProgress += OnPublishProgress;
        }

        // ── Template Status ───────────────────────────────────────────────────

        private void RefreshTemplateStatus()
        {
            SetCardStatus(WinStatus,    PackagingTarget.Windows);
            SetCardStatus(MacStatus,    PackagingTarget.MacOS);
            SetCardStatus(LinuxStatus,  PackagingTarget.Linux);
            SetCardStatus(WebGLStatus,  PackagingTarget.WebGL);
        }

        private void SetCardStatus(Label statusLabel, PackagingTarget target)
        {
            bool available = PublishEngine.IsTemplateAvailable(target);
            statusLabel.Text      = available ? "✅ Ready" : "⚠️ No template";
            statusLabel.TextColor = available
                ? Microsoft.Maui.Graphics.Color.FromArgb("#00FA9A")
                : Microsoft.Maui.Graphics.Color.FromArgb("#FF8C00");
        }

        // ── Platform Card Selection ───────────────────────────────────────────

        private void OnPlatformCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not string param || !int.TryParse(param, out int idx)) return;
            if (idx < 0 || idx >= _targets.Length) return;
            SelectCard(_targets[idx]);
        }

        private void SelectCard(PackagingTarget target)
        {
            _selectedTarget = target;

            // Reset all cards
            Border[] cards = [WinCard, MacCard, LinuxCard, WebGLCard];
            foreach (var card in cards)
            {
                card.Stroke          = Microsoft.Maui.Graphics.Color.FromArgb("#444444");
                card.StrokeThickness = 1;
                card.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#1E1E1E");
            }

            // Highlight selected
            var selected = _targets[(int)target] switch
            {
                _ when target == PackagingTarget.Windows => WinCard,
                _ when target == PackagingTarget.MacOS   => MacCard,
                _ when target == PackagingTarget.Linux   => LinuxCard,
                _                                        => WebGLCard
            };
            selected.Stroke          = Microsoft.Maui.Graphics.Color.FromArgb("#00BFFF");
            selected.StrokeThickness = 2;
            selected.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#252525");

            // Show/hide template missing warning
            TemplateMissingLabel.IsVisible = !PublishEngine.IsTemplateAvailable(target);
        }

        // ── Browse ────────────────────────────────────────────────────────────

        private async void OnBrowseClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
                if (result?.IsSuccessful == true && result.Folder is not null)
                    DestinationEntry.Text = Path.Combine(result.Folder.Path,
                        PublishEngine.SanitizeName(TitleEntry.Text?.Trim() ?? "MyAdventure"));
            }
            catch (Exception ex)
            {
                if (Shell.Current is not null)
                    await Shell.Current.DisplayAlert("Browse Error", ex.Message, "OK");
            }
        }

        // ── Publish ───────────────────────────────────────────────────────────

        private async void OnPublishClicked(object sender, EventArgs e)
        {
            var destination = DestinationEntry.Text?.Trim();
            var title       = TitleEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(destination))
            {
                await Shell.Current.DisplayAlert("Validation", "Please select an output folder.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                await Shell.Current.DisplayAlert("Validation", "Please enter a game title.", "OK");
                return;
            }
            if (!PublishEngine.IsTemplateAvailable(_selectedTarget))
            {
                await Shell.Current.DisplayAlert("Template Missing",
                    $"No shell template found for {_selectedTarget}.\n\n" +
                    $"Build the Unity player from Unity (File → Build Settings → Build), " +
                    $"then copy the output to:\n{PublishEngine.GetTemplateDir(_selectedTarget)}",
                    "OK");
                return;
            }

            // Update game metadata
            _game.Title   = title;
            _game.Author  = AuthorEntry.Text?.Trim() ?? string.Empty;
            _game.Version = VersionEntry.Text?.Trim() ?? "1.0.0";

            SetControlsEnabled(false);
            ProgressSection.IsVisible = true;
            PublishSpinner.IsRunning  = true;

            try
            {
                bool createZip = CreateZipCheck.IsChecked;

                await Task.Run(async () =>
                    await PublishEngine.PublishAsync(_game, _selectedTarget, destination, createZip));

                PublishSpinner.IsRunning = false;

                string zipNote = CreateZipCheck.IsChecked
                    ? $"\nA distribution ZIP has also been created next to the output folder."
                    : string.Empty;

                await Shell.Current.DisplayAlert("Published!",
                    $"✅ \"{title}\" is ready!\n\nOutput: {destination}{zipNote}\n\n" +
                    $"Your players can run it by double-clicking {title}" +
                    (_selectedTarget == PackagingTarget.Windows ? ".exe" :
                     _selectedTarget == PackagingTarget.MacOS   ? ".app" :
                     _selectedTarget == PackagingTarget.WebGL   ? "/index.html" : ""),
                    "Open Folder");

                // Open the output folder in Explorer/Finder
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = destination, UseShellExecute = true }); } catch { }

                await CloseAsync();
            }
            catch (Exception ex)
            {
                PublishSpinner.IsRunning  = false;
                ProgressSection.IsVisible = false;
                SetControlsEnabled(true);

                await Shell.Current.DisplayAlert("Publish Failed",
                    $"Could not publish: {ex.Message}", "OK");
            }
        }

        private void OnPublishProgress(string message)
        {
            // Progress arrives from a background Task — marshal to UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var label = new Label
                {
                    Text      = message,
                    FontSize  = 11,
                    TextColor = message.StartsWith("✅")
                        ? Microsoft.Maui.Graphics.Color.FromArgb("#00FA9A")
                        : Microsoft.Maui.Graphics.Color.FromArgb("#CCCCCC")
                };
                ProgressLog.Children.Add(label);

                // Auto-scroll to bottom
                await Task.Delay(50);
                await ProgressScroll.ScrollToAsync(0, double.MaxValue, animated: false);
            });
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            PublishEngine.OnProgress -= OnPublishProgress;
            await CloseAsync();
        }

        private void SetControlsEnabled(bool enabled)
        {
            TitleEntry.IsEnabled       = enabled;
            AuthorEntry.IsEnabled      = enabled;
            VersionEntry.IsEnabled     = enabled;
            DestinationEntry.IsEnabled = enabled;
            PublishBtn.IsEnabled       = enabled;
            WinCard.IsEnabled          = enabled;
            MacCard.IsEnabled          = enabled;
            LinuxCard.IsEnabled        = enabled;
            WebGLCard.IsEnabled        = enabled;
        }
    }
}
