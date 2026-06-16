using Avalonia;
using System;

namespace RagNext.Designer.Avalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp(args)
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(Array.Empty<string>());

    public static AppBuilder BuildAvaloniaApp(string[] args)
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

        bool forceSoftware = false;
        bool forceGl = false;

        foreach (var arg in args)
        {
            if (arg.Equals("--software", StringComparison.OrdinalIgnoreCase))
                forceSoftware = true;
            else if (arg.Equals("--opengl", StringComparison.OrdinalIgnoreCase))
                forceGl = true;
        }

        if (forceSoftware)
        {
            builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = new[] { AvaloniaNativeRenderingMode.Software }
            });
            Console.WriteLine("[INFO] Forcing Avalonia Software rendering mode.");
        }
        else if (forceGl)
        {
            builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl }
            });
            Console.WriteLine("[INFO] Forcing Avalonia OpenGL rendering mode.");
        }

        return builder
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
    }
}
