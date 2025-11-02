using Microsoft.Maui.Controls;
using RagNext.Views;

namespace RagNext
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Routes for editors
            Routing.RegisterRoute("RoomEdit", typeof(RoomEditPage));
            Routing.RegisterRoute("GameObjectEdit", typeof(RagNext.Views.GameObjectEditPage));
            Routing.RegisterRoute("PlayerEdit", typeof(RagNext.Views.PlayerEditPage));
        }
    }
}
