using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using RagNext.ViewModels;
using RagNext.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using RagNext.Services;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using RagNext.Models;

namespace RagNext
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<RoomsPage>();
            builder.Services.AddTransient<RoomEditPage>();
            builder.Services.AddSingleton<GameObjectsPage>();
            builder.Services.AddTransient<GameObjectEditPage>();
            builder.Services.AddTransient<PlayerEditPage>();
            // Settings pages
            builder.Services.AddTransient<GeneralSettingsPage>();

            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<RoomsViewModel>();
            builder.Services.AddTransient<RoomEditViewModel>();
            builder.Services.AddSingleton<GameObjectsViewModel>();
            builder.Services.AddSingleton<GameVariablesViewModel>();
            builder.Services.AddSingleton<CharactersViewModel>();

            builder.Services.AddSingleton<RagsCore.Services.IGameStorage, GameStorageAdapter>();
            builder.Services.AddSingleton<RagsCore.Services.IMediaPathProvider, RagNext.Services.MauiMediaPathProvider>();
            builder.Services.AddSingleton<RagsCore.Services.IMediaLibrary, RagsCore.Services.MediaLibrary>();
            builder.Services.AddSingleton<IAISettingsService, AISettingsService>();
            builder.Services.AddSingleton<IAIChatService, AIChatService>();
            // General settings service
            builder.Services.AddSingleton<IGeneralSettingsService, GeneralSettingsService>();

            builder.Services.AddSingleton<IMediaTreeStore, MediaTreeStore>();
            builder.Services.AddSingleton<MediaLibraryViewModel>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Apply saved Designer Theme at startup
            var general = app.Services.GetService<IGeneralSettingsService>();
            if (general != null)
            {
                var s = general.Load();
                var theme = s is null ? AppTheme.Unspecified : s.DesignerTheme switch
                {
                    DesignerTheme.Light => AppTheme.Light,
                    DesignerTheme.Dark  => AppTheme.Dark,
                    _                   => AppTheme.Unspecified
                };

                // Avoid null ref if Application.Current isn't set yet
                if (Application.Current is Application currentApp)
                {
                    currentApp.UserAppTheme = theme;
                }
            }

            // Example: apply a custom palette on top of light/dark
            // Theme options you add: "Nord", "Dracula", "SolarizedDark", etc.
            RagNext.Services.ThemeService.ApplyThemeDictionary("Nord");

            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("RagNext.Services.GameStorage");
            RagNext.Services.GameStorage.ConfigureLogger(logger);
            Services = app.Services;
            return app;
        }
    }
}
