using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RagNext.Services;

namespace RagNext.Views
{
    public partial class SavedGamesPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _tcs = new();
        public Task<string?> Result => _tcs.Task;
        public ObservableCollection<string> Saves { get; } = new();

        public SavedGamesPage(string[] saves)
        {
            InitializeComponent();
            foreach (var s in saves)
                Saves.Add(s);
            BindingContext = this;
        }

        private async void OnItemTapped(object? sender, EventArgs e)
        {
            if (sender is Label lbl && lbl.BindingContext is string name)
            {
                _tcs.TrySetResult(name);
                await Navigation.PopModalAsync();
            }
        }

        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (sender is ImageButton btn && btn.BindingContext is string name)
            {
                var confirm = await DisplayAlert("Delete Save",
                    $"Delete saved game '{name}'?", "Delete", "Cancel");
                if (!confirm) return;

                try
                {
                    await GameStorage.DeleteAsync(name); // Ensure this method exists.
                    Saves.Remove(name);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete save: {ex.Message}", "OK");
                }
            }
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            _tcs.TrySetResult(null);
            await Navigation.PopModalAsync();
        }
    }
}