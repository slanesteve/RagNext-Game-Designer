using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using RagNext.Designer.Avalonia.ViewModels;
using RagNext.Designer.Avalonia.Views;
using RagsCore.Models;
using System;

namespace RagNext.Designer.Avalonia;

public partial class App : Application
{
    private static Game? _currentGame;
    public static event Action<Game?>? GameChanged;
    public static Game? CurrentGame
    {
        get => _currentGame;
        set
        {
            if (ReferenceEquals(_currentGame, value))
                return;

            _currentGame = value;
            GameChanged?.Invoke(_currentGame);
        }
    }

    private static object? _currentAISettings; // Keep as object? if AISettings isn't imported, or use dynamic
    public static event Action<object?>? AISettingsChanged;
    public static object? CurrentAISettings
    {
        get => _currentAISettings;
        set
        {
            if (ReferenceEquals(_currentAISettings, value))
                return;
            _currentAISettings = value;
            AISettingsChanged?.Invoke(_currentAISettings);
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}