using Microsoft.Maui.Controls;
using RagNext.Views;

namespace RagNext
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();
        }

        private static void RegisterRoutes()
        {
            Routing.RegisterRoute("RoomEdit", typeof(RagNext.Views.RoomEditPage));
            Routing.RegisterRoute("GameObjectEdit", typeof(RagNext.Views.GameObjectEditPage));
            Routing.RegisterRoute("GameVariableEdit", typeof(RagNext.Views.GameVariableEditPage));
            Routing.RegisterRoute("CharacterEdit", typeof(RagNext.Views.CharacterEditPage));
            Routing.RegisterRoute("PlayerEdit", typeof(RagNext.Views.PlayerEditPage));
            Routing.RegisterRoute(nameof(AISettingsPage), typeof(AISettingsPage));
        }
    }
}
