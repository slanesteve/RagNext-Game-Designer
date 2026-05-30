namespace RagNext.Designer.Avalonia.Services
{
    /// <summary>
    /// Target platform for a standalone game publish.
    /// Each value maps to a subfolder under Templates/ next to the Designer executable.
    /// </summary>
    public enum PackagingTarget
    {
        Windows,    // Templates/Windows/  → MyGame.exe + MyGame_Data/
        MacOS,      // Templates/MacOS/    → MyGame.app
        Linux,      // Templates/Linux/    → MyGame (ELF) + MyGame_Data/
        WebGL       // Templates/WebGL/    → index.html + StreamingAssets/
    }
}
