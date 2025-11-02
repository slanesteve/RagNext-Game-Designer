namespace RagsCore.Services
{
    // App-specific path strategy (Designer, Player, tests, etc.)
    public interface IMediaPathProvider
    {
        // Return the per-game root folder (e.g., <AppData>/<gameId>)
        string GetGameRoot(RagsCore.Models.Game game);
    }
}