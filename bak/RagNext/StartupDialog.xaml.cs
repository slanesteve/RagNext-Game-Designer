using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace RagNext
{
    public partial class StartupDialog : ContentPage
    {
        readonly TaskCompletionSource<string?> _tcs = new();

        public Task<string?> ResultTask => _tcs.Task;

        public StartupDialog()
        {
            InitializeComponent();

            CreateBtn.Clicked += async (_, _) =>
            {
                _tcs.TrySetResult("Create New Game");
                await DismissAsync();
            };

            LoadBtn.Clicked += async (_, _) =>
            {
                _tcs.TrySetResult("Load Saved Game");
                await DismissAsync();
            };

            CancelBtn.Clicked += async (_, _) =>
            {
                _tcs.TrySetResult(null);
                await DismissAsync();
            };
        }

        async Task DismissAsync()
        {
            // close the modal page
            if (Navigation.ModalStack.Contains(this))
                await Navigation.PopModalAsync().ConfigureAwait(false);
        }
    }
}
