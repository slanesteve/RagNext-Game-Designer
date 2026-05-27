using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using RagNext.Services;

namespace RagNext.Views.Popups
{
    public partial class LoadGamePopup : Popup
    {
        private readonly TaskCompletionSource<string?> _tcs = new();
        public Task<string?> Result => _tcs.Task;
        public ObservableCollection<string> Saves { get; } = new();

        public LoadGamePopup(string[] saves)
        {
            InitializeComponent();
            foreach (var s in saves)
            {
                Saves.Add(s);
            }
            SavesCollectionView.ItemsSource = Saves;
        }

        private async void OnSaveTapped(object? sender, EventArgs e)
        {
            if (sender is TappedEventArgs tapped && tapped.Parameter is string name)
            {
                _tcs.TrySetResult(name);
                await CloseAsync();
            }
            else if (sender is Label lbl && lbl.BindingContext is string nameCtx)
            {
                _tcs.TrySetResult(nameCtx);
                await CloseAsync();
            }
        }

        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.CommandParameter is string name)
            {
                // Custom-styled popup confirmation alert
                var page = Shell.Current?.CurrentPage;
                if (page is null) return;

                var confirm = await page.DisplayAlert("Delete Saved Game",
                    $"Are you sure you want to permanently delete saved game '{name}'?", "Delete", "Cancel");
                
                if (!confirm) return;

                try
                {
                    await GameStorage.DeleteAsync(name);
                    Saves.Remove(name);
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", $"Failed to delete save file: {ex.Message}", "OK");
                }
            }
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            _tcs.TrySetResult(null);
            await CloseAsync();
        }
    }
}
