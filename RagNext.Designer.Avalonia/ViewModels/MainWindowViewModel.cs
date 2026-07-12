using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using RagsCore.Models;
using RagsCore.Services;
using RagNext.Designer.Avalonia.Services;
using Avalonia.Threading;


namespace RagNext.Designer.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public static MainWindowViewModel? Instance { get; private set; }

        private bool _isProjectLoading;
        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        private string _saveStatusText = "Saved";
        public string SaveStatusText
        {
            get => _saveStatusText;
            set => SetProperty(ref _saveStatusText, value);
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern long mciSendString(string command, System.Text.StringBuilder? returnValue, int returnLength, IntPtr winHandle);

        private readonly IGameStorage _storage;

        private Game? _game;
        public Game? CurrentGame
        {
            get => _game;
            set
            {
                if (value?.Variables != null)
                {
                    void EnsureSystemThemeVar(string varName, string defVal)
                    {
                        if (!global::System.Linq.Enumerable.Any(value.Variables, v => string.Equals(v.Name, varName, StringComparison.OrdinalIgnoreCase)))
                        {
                            value.Variables.Add(new RagsCore.Models.GameVariable { Id = Guid.NewGuid(), Name = varName, Value = defVal, Type = "string" });
                        }
                    }
                    EnsureSystemThemeVar("theme.preset", "default");
                    EnsureSystemThemeVar("theme.primaryBgColor", "");
                    EnsureSystemThemeVar("theme.textMainColor", "");
                    EnsureSystemThemeVar("theme.borderAccentColor", "");
                }

                if (SetProperty(ref _game, value))
                {
                    App.CurrentGame = value;
                    OnPropertyChanged(nameof(HasGame));
                    OnPropertyChanged(nameof(GameTitle));
                    OnPropertyChanged(nameof(GameAuthor));
                    OnPropertyChanged(nameof(GameVersion));
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        OnPropertyChanged(nameof(Player));
                        CurrentGame?.Player?.GetType().GetMethod("OnPropertyChanged")?.Invoke(CurrentGame.Player, new object[] { "StartingRoom" });
                        if (CurrentGame?.Characters != null)
                        {
                            foreach (var c in CurrentGame.Characters)
                            {
                                c.GetType().GetMethod("OnPropertyChanged")?.Invoke(c, new object[] { "StartingRoom" });
                            }
                        }
                    }, global::Avalonia.Threading.DispatcherPriority.Background);
                    OnPropertyChanged(nameof(SplashScreen));

                    PublishTitle = value?.Title ?? "My Adventure";
                    PublishAuthor = value?.Author ?? Environment.UserName ?? "Unknown";
                    PublishVersion = value?.Version ?? "1.0.0";
                    if (value != null)
                    {
                        // Self-healing: Ensure Kind is correctly resolved for legacy project files
                        foreach (var asset in value.MediaAssets)
                        {
                            if (asset.Kind == MediaKind.Other)
                            {
                                var ext = Path.GetExtension(asset.RelativePath ?? asset.OriginalFileName)?.ToLowerInvariant();
                                if (!string.IsNullOrEmpty(ext))
                                {
                                    if (ext == ".mp4" || ext == ".mov" || ext == ".avi")
                                        asset.Kind = MediaKind.Video;
                                    else if (ext == ".mp3" || ext == ".wav")
                                        asset.Kind = MediaKind.Audio;
                                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".webp")
                                        asset.Kind = MediaKind.Image;
                                }
                            }
                        }

                        string baseDir;
                        if (!string.IsNullOrEmpty(Preferences?.LastPublishDirectory))
                        {
                            baseDir = Preferences.LastPublishDirectory;
                        }
                        else
                        {
                            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            baseDir = Path.Combine(docs, "RagNext_Published");
                        }

                        string safeGameName = GetSafeDirectoryName(value.Title);
                        string? parentDir = Path.GetDirectoryName(baseDir);
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            PublishDestination = Path.Combine(parentDir, safeGameName);
                        }
                        else
                        {
                            PublishDestination = Path.Combine(baseDir, safeGameName);
                        }

                        value.MediaAssets.CollectionChanged += (sender, args) =>
                        {
                            RefreshMediaAssetFilters();
                        };
                        RefreshMediaAssetFilters();

                        if (value.StatusBarElements != null)
                        {
                            void Sub(RagsCore.Models.StatusBarElement s)
                            {
                                s.PropertyChanged += (sender, args) =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG] StatusBarElement PropertyChanged: {args.PropertyName}");
                                    _ = SaveGameAsync();
                                };
                            }
                            foreach (var s in value.StatusBarElements) Sub(s);
                            value.StatusBarElements.CollectionChanged += (sender, args) =>
                            {
                                if (args.NewItems != null)
                                {
                                    foreach (RagsCore.Models.StatusBarElement s in args.NewItems) Sub(s);
                                }
                                _ = SaveGameAsync();
                            };
                        }

                        if (value.Player != null)
                        {
                            value.Player.PropertyChanged += (sender, args) =>
                            {
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] Player PropertyChanged: {args.PropertyName}");
                                Console.WriteLine($"[DEBUG] Player PropertyChanged: {args.PropertyName}");
                                _ = SaveGameAsync();
                            };
                        }

                        if (value.Characters != null)
                        {
                            void Sub(RagsCore.Models.Character c)
                            {
                                c.PropertyChanged += (sender, args) =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Character PropertyChanged: {args.PropertyName}");
                                    Console.WriteLine($"[DEBUG] Character PropertyChanged: {args.PropertyName}");
                                    _ = SaveGameAsync();
                                };
                            }
                            foreach (var c in value.Characters) Sub(c);
                            value.Characters.CollectionChanged += (sender, args) =>
                            {
                                if (args.NewItems != null)
                                {
                                    foreach (RagsCore.Models.Character c in args.NewItems) Sub(c);
                                }
                                _ = SaveGameAsync();
                            };
                        }

                        if (value.SplashScreens != null)
                        {
                            if (value.SplashScreens.Count == 0 && value.SplashScreen != null)
                            {
                                value.SplashScreens.Add(value.SplashScreen);
                            }
                            foreach (var s in value.SplashScreens) HookSplashScreen(s);
                            value.SplashScreens.CollectionChanged += (sender, args) =>
                            {
                                if (args.NewItems != null)
                                {
                                    foreach (SplashScreenSettings s in args.NewItems) HookSplashScreen(s);
                                }
                                _ = SaveGameAsync();
                            };
                        }
                        SelectedSplashScreen = value.SplashScreen;
                    }
                    OnPropertyChanged(nameof(SplashBackgroundPath));
                    OnPropertyChanged(nameof(IsSplashVideoMode));
                    OnPropertyChanged(nameof(IsSplashVideoPreviewVisible));
                    // Fire media collections FIRST so ComboBox ItemsSources are ready,
                    // then post the selected-item notifications on background priority so
                    // they resolve after the ItemsSource update has fully rendered.
                    OnPropertyChanged(nameof(VideoMediaAssets));
                    OnPropertyChanged(nameof(ImageMediaAssets));
                    OnPropertyChanged(nameof(AudioMediaAssets));
                    Dispatcher.UIThread.Post(() =>
                    {
                        OnPropertyChanged(nameof(SelectedSplashImageAsset));
                        OnPropertyChanged(nameof(SelectedSplashVideoAsset));
                        OnPropertyChanged(nameof(SelectedSplashSoundAsset));
                    }, DispatcherPriority.Background);
                    OnPropertyChanged(nameof(SplashPreviewTextLeft));
                    OnPropertyChanged(nameof(SplashPreviewTextLeftWithOffset));
                    OnPropertyChanged(nameof(SplashPreviewTextTop));
                    OnPropertyChanged(nameof(SplashPreviewTextTopWithOffset));
                    OnPropertyChanged(nameof(SplashPreviewFontSize));
                    OnPropertyChanged(nameof(PublishSummaryText));
                    OnPropertyChanged(nameof(WinStatusText));
                    OnPropertyChanged(nameof(MacStatusText));
                    OnPropertyChanged(nameof(LinuxStatusText));
                    OnPropertyChanged(nameof(WebGLStatusText));
                    OnPropertyChanged(nameof(WinStatusColor));
                    OnPropertyChanged(nameof(MacStatusColor));
                    OnPropertyChanged(nameof(LinuxStatusColor));
                    OnPropertyChanged(nameof(WebGLStatusColor));
                }
            }
        }

        public bool HasGame => CurrentGame != null;

        public string GameTitle
        {
            get => CurrentGame?.Title ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Title != value)
                {
                    CurrentGame.Title = value;
                    OnPropertyChanged();
                    PublishTitle = value;
                }
            }
        }

        public string GameAuthor
        {
            get => CurrentGame?.Author ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Author != value)
                {
                    CurrentGame.Author = value;
                    OnPropertyChanged();
                    PublishAuthor = value;
                }
            }
        }

        private void HookSplashScreen(SplashScreenSettings s)
        {
            s.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SplashScreenSettings.ImageAssetId) ||
                    args.PropertyName == nameof(SplashScreenSettings.VideoAssetId) ||
                    args.PropertyName == nameof(SplashScreenSettings.SoundAssetId) ||
                    args.PropertyName == nameof(SplashScreenSettings.Mode) ||
                    args.PropertyName == nameof(SplashScreenSettings.Text) ||
                    args.PropertyName == nameof(SplashScreenSettings.FontColor) ||
                    args.PropertyName == nameof(SplashScreenSettings.FontSize) ||
                    args.PropertyName == nameof(SplashScreenSettings.TextX) ||
                    args.PropertyName == nameof(SplashScreenSettings.TextY) ||
                    args.PropertyName == nameof(SplashScreenSettings.Name))
                {
                    OnPropertyChanged(nameof(SplashBackgroundPath));
                    OnPropertyChanged(nameof(IsSplashVideoMode));
                    OnPropertyChanged(nameof(IsSplashVideoPreviewVisible));
                    OnPropertyChanged(nameof(SelectedSplashImageAsset));
                    OnPropertyChanged(nameof(SelectedSplashVideoAsset));
                    OnPropertyChanged(nameof(SelectedSplashSoundAsset));
                    
                    // Auto-save changes immediately!
                    _ = SaveGameAsync();
                }
                
                if (args.PropertyName == nameof(SplashScreenSettings.TextX))
                {
                    OnPropertyChanged(nameof(SplashPreviewTextLeft));
                    OnPropertyChanged(nameof(SplashPreviewTextLeftWithOffset));
                }
                else if (args.PropertyName == nameof(SplashScreenSettings.TextY))
                {
                    OnPropertyChanged(nameof(SplashPreviewTextTop));
                    OnPropertyChanged(nameof(SplashPreviewTextTopWithOffset));
                }
                else if (args.PropertyName == nameof(SplashScreenSettings.FontSize))
                {
                    OnPropertyChanged(nameof(SplashPreviewFontSize));
                }
                else if (args.PropertyName == nameof(SplashScreenSettings.Name))
                {
                    if (CurrentGame != null && !System.Linq.Enumerable.Any(CurrentGame.SplashScreens, x => x.Name == CurrentGame.DefaultSplashScreenName))
                    {
                        CurrentGame.DefaultSplashScreenName = s.Name;
                    }
                    OnPropertyChanged(nameof(IsSelectedSplashDefault));
                }
            };
        }

        public string GameVersion
        {
            get => CurrentGame?.Version ?? string.Empty;
            set
            {
                if (CurrentGame != null && CurrentGame.Version != value)
                {
                    CurrentGame.Version = value;
                    OnPropertyChanged();
                    PublishVersion = value;
                }
            }
        }

        private string _activeView = "Dashboard";
        public string ActiveView
        {
            get => _activeView;
            set
            {
                if (SetProperty(ref _activeView, value))
                {
                    if (value == "Validation")
                    {
                        RunValidation();
                    }
                    if (value == "Theme")
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            OnPropertyChanged(nameof(ImageMediaAssets));
                            OnPropertyChanged(nameof(SelectedThemeBackground));
                            OnPropertyChanged(nameof(SelectedThemeFrame));
                            OnPropertyChanged(nameof(SelectedBuiltInFont));
                        }, DispatcherPriority.Background);
                    }
                    // Reset editing state on navigation change
                    IsVisualEditing = false;
                    ActiveAction = null;
                    OnPropertyChanged(nameof(IsSplashVideoPreviewVisible));
                }
            }
        }

        // Sub-ViewModels
        public RoomsViewModel Rooms { get; }
        public CharactersViewModel Characters { get; }
        public GameObjectsViewModel Objects { get; }
        public GameVariablesViewModel Variables { get; }
        public GameTimersViewModel Timers { get; }
        public GlobalFunctionsViewModel Functions { get; }
        public MediaLibraryViewModel Media { get; }
        public PreferencesViewModel Preferences { get; }
        public StatusBarViewModel StatusBar { get; }

        // Active properties
        public Player? Player => CurrentGame?.Player;

        private SplashScreenSettings? _selectedSplashScreen;
        public SplashScreenSettings? SelectedSplashScreen
        {
            get => _selectedSplashScreen ?? CurrentGame?.SplashScreen;
            set
            {
                if (SetProperty(ref _selectedSplashScreen, value))
                {
                    OnPropertyChanged(nameof(SplashScreen));
                    OnPropertyChanged(nameof(SplashPreviewTextLeft));
                    OnPropertyChanged(nameof(SplashPreviewTextTop));
                    OnPropertyChanged(nameof(SplashPreviewFontSize));
                    OnPropertyChanged(nameof(SplashBackgroundPath));
                    OnPropertyChanged(nameof(IsSplashVideoMode));
                    OnPropertyChanged(nameof(IsSplashVideoPreviewVisible));
                    OnPropertyChanged(nameof(SelectedSplashImageAsset));
                    OnPropertyChanged(nameof(SelectedSplashVideoAsset));
                    OnPropertyChanged(nameof(SelectedSplashSoundAsset));
                    OnPropertyChanged(nameof(IsSelectedSplashDefault));
                    OnPropertyChanged(nameof(CanToggleDefaultSplash));
                }
            }
        }

        public bool IsSelectedSplashDefault
        {
            get
            {
                if (CurrentGame == null || SelectedSplashScreen == null) return false;
                if (CurrentGame.SplashScreens.Count <= 1) return true;
                return CurrentGame.DefaultSplashScreenName == SelectedSplashScreen.Name;
            }
            set
            {
                if (CurrentGame == null || SelectedSplashScreen == null) return;
                if (CurrentGame.SplashScreens.Count <= 1) return;
                if (value)
                {
                    CurrentGame.DefaultSplashScreenName = SelectedSplashScreen.Name;
                    OnPropertyChanged();
                    _ = SaveGameAsync();
                }
            }
        }

        public bool CanToggleDefaultSplash => CurrentGame != null && CurrentGame.SplashScreens.Count > 1;

        public SplashScreenSettings? SplashScreen => SelectedSplashScreen;

        public double SplashPreviewTextLeft => (SplashScreen?.TextX ?? 50) * 19.2;
        public double SplashPreviewTextTop => (SplashScreen?.TextY ?? 50) * 10.8;
        public double SplashPreviewFontSize => (SplashScreen?.FontSize ?? 32) * 2.4;

        private double _splashPreviewTextLeftOffset = 0.0;
        public double SplashPreviewTextLeftOffset
        {
            get => _splashPreviewTextLeftOffset;
            set
            {
                if (SetProperty(ref _splashPreviewTextLeftOffset, value))
                {
                    OnPropertyChanged(nameof(SplashPreviewTextLeftWithOffset));
                }
            }
        }

        private double _splashPreviewTextTopOffset = 0.0;
        public double SplashPreviewTextTopOffset
        {
            get => _splashPreviewTextTopOffset;
            set
            {
                if (SetProperty(ref _splashPreviewTextTopOffset, value))
                {
                    OnPropertyChanged(nameof(SplashPreviewTextTopWithOffset));
                }
            }
        }

        public double SplashPreviewTextLeftWithOffset => SplashPreviewTextLeft + SplashPreviewTextLeftOffset;
        public double SplashPreviewTextTopWithOffset => SplashPreviewTextTop + SplashPreviewTextTopOffset;

        public string SplashBackgroundPath
        {
            get
            {
                var game = CurrentGame;
                var splash = SplashScreen;
                if (splash != null)
                {
                    string assetId = splash.Mode == "Video" ? splash.VideoAssetId : splash.ImageAssetId;
                    if (!string.IsNullOrEmpty(assetId))
                    {
                        if (Guid.TryParse(assetId, out var id))
                        {
                            var asset = game.MediaAssets.FirstOrDefault(a => a.Id == id);
                            if (asset != null)
                            {
                                return new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, asset);
                            }
                        }
                    }
                }
                return string.Empty;
            }
        }

        public bool IsSplashVideoMode => SplashScreen?.Mode == "Video";
        public bool IsSplashVideoPreviewVisible => IsSplashVideoMode && (ActiveView == "SplashScreen");

        public MediaAsset? SelectedSplashImageAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == SplashScreen?.ImageAssetId);
            set
            {
                if (CurrentGame == null || SplashScreen == null) return;
                var newId = value?.IdString ?? string.Empty;
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(SplashScreen.ImageAssetId)) return;
                if (newId == SplashScreen.ImageAssetId) return;
                SplashScreen.ImageAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashImageAsset));
                OnPropertyChanged(nameof(SplashBackgroundPath));
                _ = SaveGameAsync();
            }
        }

        public MediaAsset? SelectedSplashVideoAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == SplashScreen?.VideoAssetId);
            set
            {
                if (CurrentGame == null || SplashScreen == null) return;
                var newId = value?.IdString ?? string.Empty;
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(SplashScreen.VideoAssetId)) return;
                if (newId == SplashScreen.VideoAssetId) return;
                SplashScreen.VideoAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashVideoAsset));
                OnPropertyChanged(nameof(SplashBackgroundPath));
                _ = SaveGameAsync();
            }
        }

        public MediaAsset? SelectedSplashSoundAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == SplashScreen?.SoundAssetId);
            set
            {
                if (CurrentGame == null || SplashScreen == null) return;
                var newId = value?.IdString ?? string.Empty;
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(SplashScreen.SoundAssetId)) return;
                if (newId == SplashScreen.SoundAssetId) return;
                SplashScreen.SoundAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashSoundAsset));
                _ = SaveGameAsync();
            }
        }

        private readonly System.Collections.ObjectModel.ObservableCollection<MediaAsset> _imageMediaAssets = new();
        public IEnumerable<MediaAsset> ImageMediaAssets => _imageMediaAssets;

        private readonly System.Collections.ObjectModel.ObservableCollection<MediaAsset> _videoMediaAssets = new();
        public IEnumerable<MediaAsset> VideoMediaAssets => _videoMediaAssets;

        private readonly System.Collections.ObjectModel.ObservableCollection<MediaAsset> _audioMediaAssets = new();
        public IEnumerable<MediaAsset> AudioMediaAssets => _audioMediaAssets;

        public void RefreshMediaAssetFilters()
        {
            _imageMediaAssets.Clear();
            _videoMediaAssets.Clear();
            _audioMediaAssets.Clear();
            if (CurrentGame?.MediaAssets != null)
            {
                foreach (var a in CurrentGame.MediaAssets)
                {
                    if (a.Kind == MediaKind.Image)
                        _imageMediaAssets.Add(a);
                    else if (a.Kind == MediaKind.Video)
                        _videoMediaAssets.Add(a);
                    else if (a.Kind == MediaKind.Audio)
                        _audioMediaAssets.Add(a);
                }
            }
            OnPropertyChanged(nameof(ImageMediaAssets));
            OnPropertyChanged(nameof(VideoMediaAssets));
            OnPropertyChanged(nameof(AudioMediaAssets));
            CurrentGame?.Theme?.NotifyThemeProperties();
            OnPropertyChanged(nameof(SelectedThemeBackground));
            OnPropertyChanged(nameof(SelectedThemeFrame));
        }

        public Func<string, double, double, double, Task>? PlaySplashVideoPreviewTransition { get; set; }
        public System.Action? StopSplashVideoPreview { get; set; }

        private double _splashPreviewImageOpacity = 1.0;
        public double SplashPreviewImageOpacity
        {
            get => _splashPreviewImageOpacity;
            set => SetProperty(ref _splashPreviewImageOpacity, value);
        }

        private double _splashPreviewTextOpacity = 1.0;
        public double SplashPreviewTextOpacity
        {
            get => _splashPreviewTextOpacity;
            set => SetProperty(ref _splashPreviewTextOpacity, value);
        }

        private double _splashPreviewImageScale = 1.0;
        public double SplashPreviewImageScale
        {
            get => _splashPreviewImageScale;
            set => SetProperty(ref _splashPreviewImageScale, value);
        }

        private double _splashPreviewTextScale = 1.0;
        public double SplashPreviewTextScale
        {
            get => _splashPreviewTextScale;
            set => SetProperty(ref _splashPreviewTextScale, value);
        }

        private bool _isPlayingSplashPreview = false;
        public bool IsPlayingSplashPreview
        {
            get => _isPlayingSplashPreview;
            set
            {
                if (SetProperty(ref _isPlayingSplashPreview, value))
                {
                    OnPropertyChanged(nameof(PreviewButtonText));
                }
            }
        }

        public string PreviewButtonText => IsPlayingSplashPreview ? "Previewing..." : "Preview Transition Sequence";

        public ICommand PreviewTransitionCommand { get; }

        // Visual Graph Scripting Overlay State
        private bool _isVisualEditing = false;
        public bool IsVisualEditing
        {
            get => _isVisualEditing;
            set => SetProperty(ref _isVisualEditing, value);
        }

        private bool _isAiGenerating = false;
        public bool IsAiGenerating
        {
            get => _isAiGenerating;
            set
            {
                if (SetProperty(ref _isAiGenerating, value))
                {
                    OnPropertyChanged(nameof(IsNotAiGenerating));
                }
            }
        }

        public bool IsNotAiGenerating => !_isAiGenerating;

        private RagsCore.Models.Action? _activeAction;
        public RagsCore.Models.Action? ActiveAction
        {
            get => _activeAction;
            set => SetProperty(ref _activeAction, value);
        }

        // Project lifecycle
        private bool _showWelcomeOverlay = true;
        public bool ShowWelcomeOverlay
        {
            get => _showWelcomeOverlay;
            set => SetProperty(ref _showWelcomeOverlay, value);
        }

        private bool _isAssetsSidebarOpen = false;
        public bool IsAssetsSidebarOpen
        {
            get => _isAssetsSidebarOpen;
            set => SetProperty(ref _isAssetsSidebarOpen, value);
        }

        public ICommand ToggleAssetsSidebarCommand { get; }

        private bool _showSplashOverlay = true;
        public bool ShowSplashOverlay
        {
            get => _showSplashOverlay;
            set => SetProperty(ref _showSplashOverlay, value);
        }

        private bool _showSavesOverlay = false;
        public bool ShowSavesOverlay
        {
            get => _showSavesOverlay;
            set => SetProperty(ref _showSavesOverlay, value);
        }

        public ObservableCollection<string> AvailableSaves { get; } = new();

        private string _selectedSave = string.Empty;
        public string SelectedSave
        {
            get => _selectedSave;
            set => SetProperty(ref _selectedSave, value);
        }

        // Validation Results
        public ObservableCollection<string> ValidationErrors { get; } = new();

        private string _publishStatus = "Ready";
        public string PublishStatus
        {
            get => _publishStatus;
            set => SetProperty(ref _publishStatus, value);
        }

        private bool _isPublishing = false;
        public bool IsPublishing
        {
            get => _isPublishing;
            set => SetProperty(ref _isPublishing, value);
        }

        private string _publishLogs = string.Empty;
        public string PublishLogs
        {
            get => _publishLogs;
            set => SetProperty(ref _publishLogs, value);
        }

        private string _publishTitle = string.Empty;
        public string PublishTitle
        {
            get => _publishTitle;
            set => SetProperty(ref _publishTitle, value);
        }

        private string _publishAuthor = string.Empty;
        public string PublishAuthor
        {
            get => _publishAuthor;
            set => SetProperty(ref _publishAuthor, value);
        }

        private string _publishVersion = "1.0.0";
        public string PublishVersion
        {
            get => _publishVersion;
            set => SetProperty(ref _publishVersion, value);
        }

        private string _publishDestination = string.Empty;
        public string PublishDestination
        {
            get => _publishDestination;
            set => SetProperty(ref _publishDestination, value);
        }

        private bool _publishCreateZip = true;
        public bool PublishCreateZip
        {
            get => _publishCreateZip;
            set => SetProperty(ref _publishCreateZip, value);
        }

        private PackagingTarget _selectedPublishTarget = PackagingTarget.Windows;
        public PackagingTarget SelectedPublishTarget
        {
            get => _selectedPublishTarget;
            set
            {
                if (SetProperty(ref _selectedPublishTarget, value))
                {
                    OnPropertyChanged(nameof(IsWindowsSelected));
                    OnPropertyChanged(nameof(IsMacSelected));
                    OnPropertyChanged(nameof(IsLinuxSelected));
                    OnPropertyChanged(nameof(IsWebGLSelected));
                    OnPropertyChanged(nameof(WinCardBorder));
                    OnPropertyChanged(nameof(MacCardBorder));
                    OnPropertyChanged(nameof(LinuxCardBorder));
                    OnPropertyChanged(nameof(WebGLCardBorder));
                    OnPropertyChanged(nameof(WinCardBg));
                    OnPropertyChanged(nameof(MacCardBg));
                    OnPropertyChanged(nameof(LinuxCardBg));
                    OnPropertyChanged(nameof(WebGLCardBg));
                    OnPropertyChanged(nameof(TemplateMissingWarningVisible));
                }
            }
        }

        public bool IsWindowsSelected => SelectedPublishTarget == PackagingTarget.Windows;
        public bool IsMacSelected => SelectedPublishTarget == PackagingTarget.MacOS;
        public bool IsLinuxSelected => SelectedPublishTarget == PackagingTarget.Linux;
        public bool IsWebGLSelected => SelectedPublishTarget == PackagingTarget.WebGL;

        public string WinCardBorder => IsWindowsSelected ? "#00BFFF" : "#444444";
        public string MacCardBorder => IsMacSelected ? "#00BFFF" : "#444444";
        public string LinuxCardBorder => IsLinuxSelected ? "#00BFFF" : "#444444";
        public string WebGLCardBorder => IsWebGLSelected ? "#00BFFF" : "#444444";

        public string WinCardBg => IsWindowsSelected ? "#252525" : "#1E1E1E";
        public string MacCardBg => IsMacSelected ? "#252525" : "#1E1E1E";
        public string LinuxCardBg => IsLinuxSelected ? "#252525" : "#1E1E1E";
        public string WebGLCardBg => IsWebGLSelected ? "#252525" : "#1E1E1E";

        public string WinStatusText => PublishEngine.IsTemplateAvailable(PackagingTarget.Windows) ? "✅ Ready" : "⚠️ No template";
        public string MacStatusText => PublishEngine.IsTemplateAvailable(PackagingTarget.MacOS) ? "✅ Ready" : "⚠️ No template";
        public string LinuxStatusText => PublishEngine.IsTemplateAvailable(PackagingTarget.Linux) ? "✅ Ready" : "⚠️ No template";
        public string WebGLStatusText => PublishEngine.IsTemplateAvailable(PackagingTarget.WebGL) ? "✅ Ready" : "⚠️ No template";

        private bool IsLightTheme => global::Avalonia.Application.Current?.ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Light;
        public string WinStatusColor => PublishEngine.IsTemplateAvailable(PackagingTarget.Windows) ? (IsLightTheme ? "#1B5E20" : "#00FA9A") : (IsLightTheme ? "#B23B00" : "#FF8C00");
        public string MacStatusColor => PublishEngine.IsTemplateAvailable(PackagingTarget.MacOS) ? (IsLightTheme ? "#1B5E20" : "#00FA9A") : (IsLightTheme ? "#B23B00" : "#FF8C00");
        public string LinuxStatusColor => PublishEngine.IsTemplateAvailable(PackagingTarget.Linux) ? (IsLightTheme ? "#1B5E20" : "#00FA9A") : (IsLightTheme ? "#B23B00" : "#FF8C00");
        public string WebGLStatusColor => PublishEngine.IsTemplateAvailable(PackagingTarget.WebGL) ? (IsLightTheme ? "#1B5E20" : "#00FA9A") : (IsLightTheme ? "#B23B00" : "#FF8C00");

        public bool TemplateMissingWarningVisible => !PublishEngine.IsTemplateAvailable(SelectedPublishTarget);

        public string PublishSummaryText
        {
            get
            {
                if (CurrentGame == null) return string.Empty;
                var s = PublishEngine.GetPublishSummary(CurrentGame);
                return $"📖 {s.RoomCount} rooms  |  📦 {s.ObjectCount} objects  |  👥 {s.CharacterCount} characters  |  📊 {s.VariableCount} variables  |  🖼️ {s.MediaCount} media assets";
            }
        }

        public ICommand SelectPlatformCommand { get; set; }

        // Dynamic Overlays for Inventory and Attributes
        private bool _showInventorySelectorOverlay = false;
        public bool ShowInventorySelectorOverlay
        {
            get => _showInventorySelectorOverlay;
            set => SetProperty(ref _showInventorySelectorOverlay, value);
        }

        private object? _inventoryTarget; // Can be Player or Character
        public object? InventoryTarget
        {
            get => _inventoryTarget;
            set => SetProperty(ref _inventoryTarget, value);
        }

        private bool _showAttributeDialogOverlay = false;
        public bool ShowAttributeDialogOverlay
        {
            get => _showAttributeDialogOverlay;
            set => SetProperty(ref _showAttributeDialogOverlay, value);
        }

        private bool _showComposeOverlay = false;
        public bool ShowComposeOverlay
        {
            get => _showComposeOverlay;
            set => SetProperty(ref _showComposeOverlay, value);
        }

        private string _composeTitle = "Compose Description";
        public string ComposeTitle
        {
            get => _composeTitle;
            set => SetProperty(ref _composeTitle, value);
        }

        private object? _composeTarget;
        public object? ComposeTarget
        {
            get => _composeTarget;
            set => SetProperty(ref _composeTarget, value);
        }

        private string _composeText = string.Empty;
        public string ComposeText
        {
            get => _composeText;
            set
            {
                if (SetProperty(ref _composeText, value))
                {
                    if (_composeTarget != null)
                    {
                        var prop = _composeTarget.GetType().GetProperty("Description");
                        if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(_composeTarget, value);
                        }
                        else
                        {
                            var textProp = _composeTarget.GetType().GetProperty("Text");
                            if (textProp != null && textProp.CanWrite)
                            {
                                textProp.SetValue(_composeTarget, value);
                            }
                        }
                    }
                }
            }
        }

        private string? _composeNodeId;
        public string? ComposeNodeId
        {
            get => _composeNodeId;
            set => SetProperty(ref _composeNodeId, value);
        }

        private string? _composeFieldName;
        public string? ComposeFieldName
        {
            get => _composeFieldName;
            set => SetProperty(ref _composeFieldName, value);
        }

        private string? _composeLoopSource;
        public string? ComposeLoopSource
        {
            get => _composeLoopSource;
            set => SetProperty(ref _composeLoopSource, value);
        }

        private string? _composeLoopArrayVar;
        public string? ComposeLoopArrayVar
        {
            get => _composeLoopArrayVar;
            set => SetProperty(ref _composeLoopArrayVar, value);
        }

        public event Func<string, string, string, Task>? ComposeApplied;

        private object? _attributeTarget; // Can be Player, Room, Character, or GameObject
        public object? AttributeTarget
        {
            get => _attributeTarget;
            set => SetProperty(ref _attributeTarget, value);
        }

        private string _newAttributeName = string.Empty;
        public string NewAttributeName
        {
            get => _newAttributeName;
            set => SetProperty(ref _newAttributeName, value);
        }

        private string _newAttributeValue = string.Empty;
        public string NewAttributeValue
        {
            get => _newAttributeValue;
            set => SetProperty(ref _newAttributeValue, value);
        }

        private CustomAttribute? _editingAttribute;

        // Theme Customizer properties
        public List<string> BuiltInFonts { get; } = new List<string>
        {
            "Outfit", "Inter", "Roboto", "Cinzel", "PlayFairDisplay",
            "Lora", "Orbitron", "PressStart2P", "VT323", "Caveat",
            "Pacifico", "Creepster", "SpecialElite", "Montserrat", "Merriweather"
        };

        public string SelectedBuiltInFont
        {
            get => CurrentGame?.Theme?.FontName ?? "Outfit";
            set
            {
                if (value == null)
                {
                    OnPropertyChanged(nameof(SelectedBuiltInFont));
                    OnPropertyChanged(nameof(FontPreviewFamilyName));
                    return;
                }
                if (CurrentGame != null && CurrentGame.Theme != null)
                {
                    CurrentGame.Theme.FontName = value;
                    CurrentGame.Theme.FontAssetId = string.Empty; // Not using imported assets
                    OnPropertyChanged(nameof(SelectedBuiltInFont));
                    OnPropertyChanged(nameof(FontPreviewFamilyName));
                    _ = SaveGameAsync();
                }
            }
        }

        public global::Avalonia.Media.FontFamily FontPreviewFamilyName
        {
            get
            {
                var name = SelectedBuiltInFont;
                try
                {
                    var familyName = name switch
                    {
                        "PlayFairDisplay" => "Playfair Display",
                        "PressStart2P" => "Press Start 2P",
                        "SpecialElite" => "Special Elite",
                        _ => name
                    };
                    return new global::Avalonia.Media.FontFamily($"avares://RagNext/Assets/Fonts/{name}.ttf#{familyName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FontPreview] Failed to load custom font preview: {ex.Message}");
                }
                return new global::Avalonia.Media.FontFamily(name);
            }
        }

        public MediaAsset? SelectedThemeBackground
        {
            get
            {
                var id = CurrentGame?.Theme?.BackgroundAssetId;
                var list = CurrentGame?.MediaAssets;
                var found = list?.FirstOrDefault(a => string.Equals(a.Id.ToString(), id, StringComparison.OrdinalIgnoreCase));
                System.Diagnostics.Debug.WriteLine($"[DEBUG-THEME-GET] SelectedThemeBackground get. ID: '{id}', Found: '{found?.Name}' (List Count: {list?.Count})");
                return found;
            }
            set
            {
                if (value == null)
                {
                    OnPropertyChanged(nameof(SelectedThemeBackground));
                    return;
                }
                if (CurrentGame != null && CurrentGame.Theme != null)
                {
                    CurrentGame.Theme.BackgroundAssetId = value.Id.ToString();
                    OnPropertyChanged(nameof(SelectedThemeBackground));
                    _ = SaveGameAsync();
                }
            }
        }

        public MediaAsset? SelectedThemeFrame
        {
            get
            {
                var id = CurrentGame?.Theme?.FrameAssetId;
                var list = CurrentGame?.MediaAssets;
                var found = list?.FirstOrDefault(a => string.Equals(a.Id.ToString(), id, StringComparison.OrdinalIgnoreCase));
                System.Diagnostics.Debug.WriteLine($"[DEBUG-THEME-GET] SelectedThemeFrame get. ID: '{id}', Found: '{found?.Name}' (List Count: {list?.Count})");
                return found;
            }
            set
            {
                if (value == null)
                {
                    OnPropertyChanged(nameof(SelectedThemeFrame));
                    return;
                }
                if (CurrentGame != null && CurrentGame.Theme != null)
                {
                    CurrentGame.Theme.FrameAssetId = value.Id.ToString();
                    OnPropertyChanged(nameof(SelectedThemeFrame));
                    _ = SaveGameAsync();
                }
            }
        }

        private ObservableCollection<string> _themePresets = new();
        public ObservableCollection<string> ThemePresets { get => _themePresets; set => SetProperty(ref _themePresets, value); }

        private string? _selectedThemePreset;
        public string? SelectedThemePreset
        {
            get => _selectedThemePreset;
            set
            {
                if (SetProperty(ref _selectedThemePreset, value) && !string.IsNullOrEmpty(value))
                {
                    if (CurrentGame?.Theme != null)
                    {
                        CurrentGame.Theme.ActivePreset = value;
                    }
                    if (!_isProjectLoading)
                    {
                        LoadThemePreset(value);
                        _ = SaveGameAsync();
                    }
                }
            }
        }

        private string _newPresetName = string.Empty;
        public string NewPresetName { get => _newPresetName; set => SetProperty(ref _newPresetName, value); }

        public void InitializePresets()
        {
            try
            {
                var path = GetPresetsDirectory();
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                
                void EnsurePresetFile(string name, string bgColor, string textColor, string borderColor)
                {
                    var presetPath = Path.Combine(path, $"{name}.json");
                    if (!File.Exists(presetPath))
                    {
                        var theme = new ThemeSettings
                        {
                            PrimaryBgColor = bgColor,
                            TextMainColor = textColor,
                            BorderAccentColor = borderColor,
                            FontName = "Outfit",
                            InventoryDockPosition = "Right",
                            RoomItemsDockPosition = "Right",
                            NavigationDockPosition = "Right",
                            PanelPadding = 12,
                            BorderRadius = 8,
                            AspectRatio = 1.333,
                            TextBoxAlignment = "Left",
                            TextBoxWidth = 780,
                            TextBoxHeight = 320,
                            PortraitAlignment = "TopLeft",
                            SidebarWidth = 360,
                            BottomBarHeight = 220,
                            FontSize = 18,
                            FrameApplyToGameScreen = true,
                            FrameApplyToMainText = false,
                            FrameApplyToPopups = false,
                            FrameApplyToSidebars = false
                        };
                        var json = System.Text.Json.JsonSerializer.Serialize(theme, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(presetPath, json);
                    }
                }

                EnsurePresetFile("Default", "#1e1e24", "#ffffff", "#4a4a5a");
                EnsurePresetFile("Pink", "#2d121c", "#fca5a5", "#f43f5e");
                EnsurePresetFile("Blue", "#0f172a", "#93c5fd", "#3b82f6");
                EnsurePresetFile("Dark", "#121212", "#e0e0e0", "#333333");
                EnsurePresetFile("Glass", "rgba(40,40,40,0.55)", "#ffffff", "rgba(255,255,255,0.2)");

                ThemePresets.Clear();
                foreach (var file in Directory.GetFiles(path, "*.json"))
                {
                    ThemePresets.Add(Path.GetFileNameWithoutExtension(file));
                }

                string activePreset = CurrentGame?.Theme?.ActivePreset;
                if (!string.IsNullOrEmpty(activePreset) && ThemePresets.Contains(activePreset))
                {
                    _selectedThemePreset = activePreset;
                    OnPropertyChanged(nameof(SelectedThemePreset));
                }
                else if (SelectedThemePreset == null && ThemePresets.Contains("Default"))
                {
                    _selectedThemePreset = "Default";
                    OnPropertyChanged(nameof(SelectedThemePreset));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Presets] Failed to list presets: {ex.Message}");
            }
        }

        private string GetPresetsDirectory() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", "Themes");

        private void SaveThemePreset()
        {
            if (string.IsNullOrWhiteSpace(NewPresetName) || CurrentGame?.Theme == null) return;
            try
            {
                var dir = GetPresetsDirectory();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var presetPath = Path.Combine(dir, $"{NewPresetName.Trim()}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(CurrentGame.Theme, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetPath, json);

                if (!ThemePresets.Contains(NewPresetName.Trim()))
                {
                    ThemePresets.Add(NewPresetName.Trim());
                }
                NewPresetName = string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Presets] Failed to save preset: {ex.Message}");
            }
        }

        private void LoadThemePreset(string presetName)
        {
            if (CurrentGame?.Theme == null) return;
            try
            {
                var file = Path.Combine(GetPresetsDirectory(), $"{presetName}.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var loadedTheme = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(json);
                    if (loadedTheme != null)
                    {
                        CurrentGame.Theme.PrimaryBgColor = loadedTheme.PrimaryBgColor;
                        CurrentGame.Theme.TextMainColor = loadedTheme.TextMainColor;
                        CurrentGame.Theme.BorderAccentColor = loadedTheme.BorderAccentColor;
                        CurrentGame.Theme.FontName = loadedTheme.FontName;
                        CurrentGame.Theme.FontAssetId = loadedTheme.FontAssetId;
                        if (!string.IsNullOrEmpty(loadedTheme.BackgroundAssetId))
                        {
                            CurrentGame.Theme.BackgroundAssetId = loadedTheme.BackgroundAssetId;
                        }
                        if (!string.IsNullOrEmpty(loadedTheme.FrameAssetId))
                        {
                            CurrentGame.Theme.FrameAssetId = loadedTheme.FrameAssetId;
                        }
                        CurrentGame.Theme.InventoryDockPosition = loadedTheme.InventoryDockPosition;
                        CurrentGame.Theme.RoomItemsDockPosition = loadedTheme.RoomItemsDockPosition ?? "Right";
                        CurrentGame.Theme.NavigationDockPosition = loadedTheme.NavigationDockPosition;
                        CurrentGame.Theme.PanelPadding = loadedTheme.PanelPadding;
                        CurrentGame.Theme.BorderRadius = loadedTheme.BorderRadius;
                        CurrentGame.Theme.AspectRatio = loadedTheme.AspectRatio;
                        CurrentGame.Theme.TextBoxAlignment = loadedTheme.TextBoxAlignment ?? "Left";
                        CurrentGame.Theme.TextBoxWidth = loadedTheme.TextBoxWidth > 0 ? loadedTheme.TextBoxWidth : 780;
                        CurrentGame.Theme.TextBoxHeight = loadedTheme.TextBoxHeight > 0 ? loadedTheme.TextBoxHeight : 320;
                        CurrentGame.Theme.PortraitAlignment = loadedTheme.PortraitAlignment ?? "TopLeft";
                        CurrentGame.Theme.SidebarWidth = loadedTheme.SidebarWidth > 0 ? loadedTheme.SidebarWidth : 360;
                        CurrentGame.Theme.BottomBarHeight = loadedTheme.BottomBarHeight > 0 ? loadedTheme.BottomBarHeight : 220;
                        CurrentGame.Theme.FontSize = loadedTheme.FontSize > 0 ? loadedTheme.FontSize : 18;
                        CurrentGame.Theme.FrameApplyToGameScreen = loadedTheme.FrameApplyToGameScreen;
                        CurrentGame.Theme.FrameApplyToMainText = loadedTheme.FrameApplyToMainText;
                        CurrentGame.Theme.FrameApplyToPopups = loadedTheme.FrameApplyToPopups;
                        CurrentGame.Theme.FrameApplyToSidebars = loadedTheme.FrameApplyToSidebars;
                        CurrentGame.Theme.ActivePreset = presetName;

                        OnPropertyChanged(nameof(SelectedThemeBackground));
                        OnPropertyChanged(nameof(SelectedThemeFrame));
                        OnPropertyChanged(nameof(SelectedBuiltInFont));
                        OnPropertyChanged(nameof(FontPreviewFamilyName));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Presets] Failed to load preset: {ex.Message}");
            }
        }

        private void DeleteThemePreset()
        {
            if (string.IsNullOrEmpty(SelectedThemePreset)) return;
            try
            {
                var file = Path.Combine(GetPresetsDirectory(), $"{SelectedThemePreset}.json");
                if (File.Exists(file)) File.Delete(file);
                ThemePresets.Remove(SelectedThemePreset);
                SelectedThemePreset = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Presets] Failed to delete preset: {ex.Message}");
            }
        }

        // Commands
        public ICommand NavigateCommand { get; }
        public ICommand AddSplashScreenCommand { get; }
        public ICommand DeleteSplashScreenCommand { get; }
        public ICommand SaveThemePresetCommand { get; }
        public ICommand DeleteThemePresetCommand { get; }
        public ICommand ClearThemeFontCommand { get; }
        public ICommand ClearThemeBackgroundCommand { get; }
        public ICommand ClearThemeFrameCommand { get; }
        public ICommand NewGameCommand { get; }
        public ICommand ShowLoadGameCommand { get; }
        public ICommand LoadSelectedGameCommand { get; }
        public ICommand DeleteSelectedGameCommand { get; }
        public ICommand SaveGameCommand { get; }
        public ICommand CloseWelcomeCommand { get; }
        public ICommand PublishCommand { get; }

        public static Func<Task<string>> PickFolderAsync { get; set; }
        public static Func<Task<string?>>? PickImportPackageFileAsync { get; set; }
        public static Func<string, Task<string?>>? PickExportPackageFileAsync { get; set; }
        public static Func<string, string, Task>? ShowAlertDialogAsync { get; set; }
        public static Func<string, string, Task<bool>>? ShowConfirmDialogAsync { get; set; }
        public ICommand BrowsePublishDestinationCommand { get; }
        public ICommand ImportPackageCommand { get; }
        public ICommand ExportPackageCommand { get; }

        public ICommand StartEditingActionCommand { get; }
        public ICommand StopEditingActionCommand { get; }
        public ICommand AddActionCommand { get; }
        public ICommand DeleteActionCommand { get; }
        public ICommand CopyActionCommand { get; }
        public ICommand PasteActionCommand { get; }
        public bool CanPasteAction => RagNext.Designer.Avalonia.Services.ActionClipboardService.CanPaste;

        private static readonly ActionTrigger[] _allTriggers = (ActionTrigger[])Enum.GetValues(typeof(ActionTrigger));
        public ActionTrigger[] AllTriggers => _allTriggers;

        private static readonly ActionTrigger[] _playerTriggers = new[]
        {
            ActionTrigger.UserClicked,
            ActionTrigger.OnGameStart,
            ActionTrigger.OnGameLoad,
            ActionTrigger.OnTurnTick,
            ActionTrigger.OnPlayerEnter,
            ActionTrigger.OnPlayerExit,
            ActionTrigger.OnCharacterEnter,
            ActionTrigger.OnCharacterExit,
            ActionTrigger.OnCharacterKilled
        };
        public ActionTrigger[] PlayerTriggers => _playerTriggers;

        private static readonly ActionTrigger[] _roomTriggers = new[]
        {
            ActionTrigger.UserClicked,
            ActionTrigger.OnTurnTick,
            ActionTrigger.OnPlayerEnter,
            ActionTrigger.OnPlayerExit,
            ActionTrigger.OnCharacterEnter,
            ActionTrigger.OnCharacterExit,
            ActionTrigger.OnCharacterKilled
        };
        public ActionTrigger[] RoomTriggers => _roomTriggers;

        private static readonly ActionTrigger[] _characterTriggers = new[]
        {
            ActionTrigger.UserClicked,
            ActionTrigger.OnTurnTick,
            ActionTrigger.OnPlayerEnter,
            ActionTrigger.OnPlayerExit,
            ActionTrigger.OnCharacterEnter,
            ActionTrigger.OnCharacterExit,
            ActionTrigger.OnCharacterKilled
        };
        public ActionTrigger[] CharacterTriggers => _characterTriggers;

        private static readonly ActionTrigger[] _objectTriggers = new[]
        {
            ActionTrigger.UserClicked,
            ActionTrigger.OnTurnTick,
            ActionTrigger.OnPlayerEnter,
            ActionTrigger.OnPlayerExit,
            ActionTrigger.OnCharacterEnter,
            ActionTrigger.OnCharacterExit,
            ActionTrigger.OnCharacterKilled,
            ActionTrigger.OnObjectExamined,
            ActionTrigger.OnObjectTaken,
            ActionTrigger.OnObjectDropped
        };
        public ActionTrigger[] ObjectTriggers => _objectTriggers;

        private static readonly string[] _directionFilters = new[] 
        { 
            "All", "N", "S", "E", "W", "NW", "NE", "SW", "SE", "Up", "Down", "In", "Out" 
        };
        public string[] DirectionFilters => _directionFilters;
        public ICommand OpenComposeCommand { get; }
        public ICommand CloseComposeCommand { get; }
        public ICommand LoadLastWorkspaceCommand { get; }
        public ICommand LoadRecentProjectCommand { get; }
        public ICommand RemoveRecentProjectCommand { get; }

        public ICommand TriggerAddInventoryCommand { get; }
        public ICommand CloseInventorySelectorCommand { get; }
        public ICommand SelectInventoryItemCommand { get; }
        public ICommand RemoveInventoryItemCommand { get; }
        public ICommand ClearPortraitCommand { get; }

        public ICommand TriggerAddAttributeCommand { get; }
        public ICommand TriggerEditAttributeCommand { get; }
        public ICommand CloseAttributeDialogCommand { get; }
        public ICommand SaveAttributeCommand { get; }
        public ICommand RemoveAttributeCommand { get; }

        public ObservableCollection<RagsCore.Models.Action> GlobalActions { get; } = new();
        public ObservableCollection<RagsCore.Models.Action> MatchActionTemplates { get; } = new();

        private bool _showActionSelectorOverlay;
        public bool ShowActionSelectorOverlay { get => _showActionSelectorOverlay; set => SetProperty(ref _showActionSelectorOverlay, value); }

        internal object? _actionTargetEntity;

        private RagsCore.Models.Action? _lastAddedAction;
        public RagsCore.Models.Action? LastAddedAction { get => _lastAddedAction; set => SetProperty(ref _lastAddedAction, value); }

        public ICommand AddGlobalActionCommand { get; }
        public ICommand DeleteGlobalActionCommand { get; }
        public ICommand PasteGlobalActionCommand { get; }
        public ICommand SelectActionTemplateCommand { get; }
        public ICommand CloseActionSelectorCommand { get; }

        // Items Creators Command delegation
        public ICommand AddRoomCommand => Rooms.AddRoomCommand;
        public ICommand AddCharacterCommand => Characters.AddCharacterCommand;
        public ICommand AddObjectCommand => Objects.AddObjectCommand;

        // Items Deletion Command delegation
        public ICommand DeleteRoomCommand => Rooms.DeleteRoomCommand;
        public ICommand DeleteCharacterCommand => Characters.DeleteCharacterCommand;
        public ICommand DeleteObjectCommand => Objects.DeleteObjectCommand;
        public ICommand DeleteVariableCommand => Variables.DeleteVariableCommand;
        public ICommand DeleteTimerCommand => Timers.DeleteTimerCommand;
        public ICommand DeleteFunctionCommand => Functions.DeleteFunctionCommand;

        public MainWindowViewModel()
        {
            Instance = this;
            _storage = new AvaloniaGameStorage();

            Rooms = new RoomsViewModel(_storage);
            Characters = new CharactersViewModel(_storage);
            Objects = new GameObjectsViewModel(_storage);
            Variables = new GameVariablesViewModel(_storage);
            Timers = new GameTimersViewModel(_storage);
            Functions = new GlobalFunctionsViewModel(_storage);
            Media = new MediaLibraryViewModel();
            Preferences = new PreferencesViewModel();
            StatusBar = new StatusBarViewModel(_storage);

            App.GameChanged += (g) => 
            { 
                System.Diagnostics.Debug.WriteLine($"[DEBUG-THEME] Loaded BackgroundAssetId: '{g?.Theme?.BackgroundAssetId}', FrameAssetId: '{g?.Theme?.FrameAssetId}'");
                System.Diagnostics.Debug.WriteLine($"[DEBUG-THEME] MediaAssets Count: {g?.MediaAssets?.Count}");
                _isProjectLoading = true;
                CurrentGame = g; 
                OnPropertyChanged(nameof(Player));
                OnPropertyChanged(nameof(SplashScreen));
                OnPropertyChanged(nameof(SelectedBuiltInFont));
                OnPropertyChanged(nameof(FontPreviewFamilyName));
                OnPropertyChanged(nameof(SelectedThemeBackground));
                OnPropertyChanged(nameof(SelectedThemeFrame));
                InitializePresets();
                Media.Refresh();
                
                Dispatcher.UIThread.Post(() => {
                    _isProjectLoading = false;
                }, DispatcherPriority.Background);
            };

            NavigateCommand = new Command<string>(view => ActiveView = view ?? "Dashboard");
            ToggleAssetsSidebarCommand = new Command(() => IsAssetsSidebarOpen = !IsAssetsSidebarOpen);

            SaveThemePresetCommand = new Command(() => SaveThemePreset());
            DeleteThemePresetCommand = new Command(() => DeleteThemePreset());
            ClearThemeFontCommand = new Command(() => { SelectedBuiltInFont = "Outfit"; });
            ClearThemeBackgroundCommand = new Command(() => 
            {
                if (CurrentGame?.Theme != null)
                {
                    CurrentGame.Theme.BackgroundAssetId = string.Empty;
                    OnPropertyChanged(nameof(SelectedThemeBackground));
                    _ = SaveGameAsync();
                }
            });
            ClearThemeFrameCommand = new Command(() => 
            {
                if (CurrentGame?.Theme != null)
                {
                    CurrentGame.Theme.FrameAssetId = string.Empty;
                    OnPropertyChanged(nameof(SelectedThemeFrame));
                    _ = SaveGameAsync();
                }
            });

            AddSplashScreenCommand = new Command(() =>
            {
                if (CurrentGame == null) return;
                var count = CurrentGame.SplashScreens.Count;
                var newSplash = new SplashScreenSettings
                {
                    Name = $"Splash Screen {count + 1}"
                };
                HookSplashScreen(newSplash);
                CurrentGame.SplashScreens.Add(newSplash);
                SelectedSplashScreen = newSplash;
                OnPropertyChanged(nameof(CanToggleDefaultSplash));
                OnPropertyChanged(nameof(IsSelectedSplashDefault));
                _ = SaveGameAsync();
            });

            DeleteSplashScreenCommand = new Command(() =>
            {
                if (CurrentGame == null || SelectedSplashScreen == null) return;
                if (CurrentGame.SplashScreens.Count <= 1) return;
                
                var toDelete = SelectedSplashScreen;
                CurrentGame.SplashScreens.Remove(toDelete);
                
                SelectedSplashScreen = CurrentGame.SplashScreens.FirstOrDefault();
                OnPropertyChanged(nameof(CanToggleDefaultSplash));
                OnPropertyChanged(nameof(IsSelectedSplashDefault));
                _ = SaveGameAsync();
            });

            PreviewTransitionCommand = new Command(async () =>
            {
                if (IsPlayingSplashPreview || SplashScreen == null) return;
                IsPlayingSplashPreview = true;

                var splash = SplashScreen;
                double fadeIn = Math.Max(0.1, splash.FadeInDuration);
                double hold = Math.Max(0.1, splash.DisplayDuration);
                double fadeOut = Math.Max(0.1, splash.FadeOutDuration);
                string style = splash.TransitionStyle ?? "Fade";

                if (IsSplashVideoMode && PlaySplashVideoPreviewTransition != null)
                {
                    await PlaySplashVideoPreviewTransition(style, fadeIn, hold, fadeOut);
                    // Wait for the duration of the transition to complete
                    await Task.Delay((int)((fadeIn + hold + fadeOut) * 1000));
                    StopSplashVideoPreview?.Invoke();
                    IsPlayingSplashPreview = false;
                    return;
                }

                var rnd = new Random();

                // Resolve Selected Audio File Path
                string? soundPath = null;
                if (splash.Mode != "Video" && !string.IsNullOrEmpty(splash.SoundAssetId) && Guid.TryParse(splash.SoundAssetId, out var sGuid))
                {
                    var asset = CurrentGame.MediaAssets.FirstOrDefault(a => a.Id == sGuid);
                    if (asset != null)
                    {
                        soundPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(CurrentGame, asset);
                    }
                }

                // Play Audio natively on Windows during preview
                bool audioStarted = false;
                if (OperatingSystem.IsWindows() && !string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    try
                    {
                        mciSendString("close splashAudio", null, 0, IntPtr.Zero);
                        mciSendString($"open \"{soundPath}\" type mpegvideo alias splashAudio", null, 0, IntPtr.Zero);
                        mciSendString("play splashAudio", null, 0, IntPtr.Zero);
                        audioStarted = true;
                    }
                    catch
                    {
                        // Ignore audio playback failures in preview
                    }
                }

                try
                {
                    // 1. Initial State: Hidden
                    SplashPreviewImageOpacity = 0.0;
                    SplashPreviewTextOpacity = 0.0;
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;
                    SplashPreviewImageScale = 1.0;
                    SplashPreviewTextScale = 1.0;
                    await Task.Delay(200);

                    // 2. Fade In Sequence (frequent updates for 1:1 smooth rendering matching Unity)
                    int stepsFadeIn = (int)(fadeIn * 60.0); // 60 FPS target
                    int stepDelayFadeIn = (int)((fadeIn * 1000) / stepsFadeIn);
                    for (int i = 0; i <= stepsFadeIn; i++)
                    {
                        double progress = (double)i / stepsFadeIn;
                        
                        double imgOpacity = progress;
                        double txtOpacity = progress;

                        if (style.Equals("Rise", StringComparison.OrdinalIgnoreCase))
                        {
                            // Text slides up from 60px below to mimic player exactly
                            SplashPreviewTextTopOffset = 60.0 * (1.0 - progress);
                        }
                        else if (style.Equals("Glitch", StringComparison.OrdinalIgnoreCase))
                        {
                            // Glitch shake title text with random translation shifts
                            if (rnd.NextDouble() < 0.15)
                            {
                                txtOpacity = rnd.NextDouble() * 0.5 + 0.2; // 0.2 to 0.7
                                SplashPreviewTextLeftOffset = rnd.NextDouble() * 20.0 - 10.0; // -10 to 10
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 10.0 - 5.0; // -5 to 5
                            }
                            else
                            {
                                txtOpacity = progress;
                                SplashPreviewTextLeftOffset = 0.0;
                                SplashPreviewTextTopOffset = 0.0;
                            }
                        }
                        else if (style.Equals("Exposure", StringComparison.OrdinalIgnoreCase))
                        {
                            // Flash overexposure: raise opacity quickly using t^0.4
                            double expT = Math.Pow(progress, 0.4);
                            imgOpacity = expT;
                            txtOpacity = progress;
                            
                            // Pop text scale from 1.5 down to 1.0
                            SplashPreviewTextScale = 1.5 - 0.5 * progress;
                        }
                        else if (style.Equals("Cinematic", StringComparison.OrdinalIgnoreCase))
                        {
                            // Slow zoom-in on both background and text
                            SplashPreviewImageScale = 1.0 + 0.08 * progress;
                            SplashPreviewTextScale = 1.0 + 0.08 * progress;
                        }
                        else if (style.Equals("CRT", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rnd.NextDouble() < 0.1)
                            {
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 8.0 - 4.0;
                            }
                            else
                            {
                                SplashPreviewTextTopOffset = 0.0;
                            }
                        }
                        else if (style.Equals("RGBSplit", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextLeftOffset = 4.0 * Math.Sin(progress * Math.PI * 4);
                        }
                        else if (style.Equals("ParticleSmoke", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSand", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleEmbers", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleRain", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSnow", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextScale = 0.8 + 0.2 * progress;
                        }
                        else if (style.Equals("SoundReactive", StringComparison.OrdinalIgnoreCase))
                        {
                            double pulse = 1.0 + 0.05 * Math.Sin(progress * Math.PI * 6);
                            SplashPreviewImageScale = pulse;
                            SplashPreviewTextScale = pulse;
                        }

                        SplashPreviewImageOpacity = imgOpacity;
                        SplashPreviewTextOpacity = txtOpacity;
                        await Task.Delay(stepDelayFadeIn);
                    }

                    // Reset offsets for Hold State
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;
                    SplashPreviewImageOpacity = 1.0;
                    SplashPreviewTextOpacity = 1.0;

                    // 3. Hold State (with live running animations like Glitch shaking/flicker)
                    int stepsHold = (int)(hold * 60.0);
                    int stepDelayHold = (int)((hold * 1000) / stepsHold);
                    for (int i = 0; i < stepsHold; i++)
                    {
                        double progress = (double)i / stepsHold;
                        if (style.Equals("Glitch", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rnd.NextDouble() < 0.08)
                            {
                                SplashPreviewTextOpacity = rnd.NextDouble() * 0.6 + 0.3; // 0.3 to 0.9
                                SplashPreviewTextLeftOffset = rnd.NextDouble() * 30.0 - 15.0; // -15 to 15
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 16.0 - 8.0; // -8 to 8
                            }
                            else
                            {
                                SplashPreviewTextOpacity = 1.0;
                                SplashPreviewTextLeftOffset = 0.0;
                                SplashPreviewTextTopOffset = 0.0;
                            }
                        }
                        else if (style.Equals("Cinematic", StringComparison.OrdinalIgnoreCase))
                        {
                            // Continue zooming in both
                            SplashPreviewImageScale = 1.08 + 0.12 * progress;
                            SplashPreviewTextScale = 1.08 + 0.12 * progress;
                        }
                        else if (style.Equals("CRT", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rnd.NextDouble() < 0.08)
                            {
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 6.0 - 3.0;
                                SplashPreviewTextOpacity = rnd.NextDouble() * 0.4 + 0.6;
                            }
                            else
                            {
                                SplashPreviewTextTopOffset = 0.0;
                                SplashPreviewTextOpacity = 1.0;
                            }
                        }
                        else if (style.Equals("RGBSplit", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextLeftOffset = 3.0 * Math.Sin(progress * Math.PI * 10);
                        }
                        else if (style.Equals("ParticleSmoke", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSand", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleEmbers", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleRain", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSnow", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextScale = 1.0 + 0.02 * Math.Sin(progress * Math.PI * 2);
                        }
                        else if (style.Equals("SoundReactive", StringComparison.OrdinalIgnoreCase))
                        {
                            double pulse = 1.0 + 0.04 * Math.Abs(Math.Sin(progress * Math.PI * 8));
                            SplashPreviewImageScale = pulse;
                            SplashPreviewTextScale = pulse;
                        }
                        await Task.Delay(stepDelayHold);
                    }

                    // Reset offsets for Fade Out State
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;

                    // 4. Fade Out Sequence
                    int stepsFadeOut = (int)(fadeOut * 60.0);
                    int stepDelayFadeOut = (int)((fadeOut * 1000) / stepsFadeOut);
                    for (int i = stepsFadeOut; i >= 0; i--)
                    {
                        double progress = (double)i / stepsFadeOut;
                        double imgOpacity = progress;
                        double txtOpacity = progress;

                        if (style.Equals("Glitch", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rnd.NextDouble() < 0.15)
                            {
                                txtOpacity = (rnd.NextDouble() * 0.5 + 0.2) * progress;
                                SplashPreviewTextLeftOffset = rnd.NextDouble() * 16.0 - 8.0;
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 8.0 - 4.0;
                            }
                            else
                            {
                                txtOpacity = progress;
                                SplashPreviewTextLeftOffset = 0.0;
                                SplashPreviewTextTopOffset = 0.0;
                            }
                        }
                        else if (style.Equals("Cinematic", StringComparison.OrdinalIgnoreCase))
                        {
                            // Finish zoom-in to 1.25
                            SplashPreviewImageScale = 1.20 + 0.05 * (1.0 - progress);
                            SplashPreviewTextScale = 1.20 + 0.05 * (1.0 - progress);
                        }
                        else if (style.Equals("CRT", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rnd.NextDouble() < 0.1)
                            {
                                SplashPreviewTextTopOffset = rnd.NextDouble() * 8.0 - 4.0;
                            }
                        }
                        else if (style.Equals("RGBSplit", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextLeftOffset = 5.0 * Math.Sin(progress * Math.PI * 4);
                        }
                        else if (style.Equals("ParticleSmoke", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSand", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleEmbers", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleRain", StringComparison.OrdinalIgnoreCase) || style.Equals("ParticleSnow", StringComparison.OrdinalIgnoreCase))
                        {
                            SplashPreviewTextScale = 1.0 - 0.2 * (1.0 - progress);
                        }
                        else if (style.Equals("SoundReactive", StringComparison.OrdinalIgnoreCase))
                        {
                            double pulse = 1.0 + 0.05 * Math.Sin(progress * Math.PI * 4);
                            SplashPreviewImageScale = pulse * progress;
                            SplashPreviewTextScale = pulse * progress;
                        }

                        SplashPreviewImageOpacity = imgOpacity;
                        SplashPreviewTextOpacity = txtOpacity;
                        await Task.Delay(stepDelayFadeOut);
                    }

                    // Done
                    SplashPreviewImageOpacity = 0.0;
                    SplashPreviewTextOpacity = 0.0;
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;
                    SplashPreviewImageScale = 1.0;
                    SplashPreviewTextScale = 1.0;
                    await Task.Delay(300);
                }
                catch
                {
                    // Ignore errors during delay
                }
                finally
                {
                    // Stop Audio natively
                    if (audioStarted && OperatingSystem.IsWindows())
                    {
                        try
                        {
                            mciSendString("stop splashAudio", null, 0, IntPtr.Zero);
                            mciSendString("close splashAudio", null, 0, IntPtr.Zero);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    // Restore to normal static state
                    SplashPreviewImageOpacity = 1.0;
                    SplashPreviewTextOpacity = 1.0;
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;
                    SplashPreviewImageScale = 1.0;
                    SplashPreviewTextScale = 1.0;
                    IsPlayingSplashPreview = false;
                }
            });

            BrowsePublishDestinationCommand = new Command(async () =>
            {
                if (PickFolderAsync != null)
                {
                    var folder = await PickFolderAsync();
                    if (!string.IsNullOrEmpty(folder))
                    {
                        PublishDestination = folder;
                    }
                }
            });

            ImportPackageCommand = new Command(async () => await ImportPackageAsync());
            ExportPackageCommand = new Command(async () => await ExportPackageAsync());
            LoadSampleGameCommand = new Command<SampleGameItem>(async sample => await LoadSampleGameAsync(sample));
            LoadSampleGames();

            NewGameCommand = new Command(() =>
            {
                CurrentGame = new Game
                {
                    Id = Guid.NewGuid(),
                    Title = "My New Adventure",
                    Author = "Game Designer",
                    Version = "1.0.0"
                };
                // Automatically add one initial default Room to prevent empty-validation failure!
                CurrentGame.Rooms.Add(new Room { Id = Guid.NewGuid(), Name = "Starting Chamber", Description = "A mysterious dark chamber." });
                CurrentGame.Player.StartingRoom = CurrentGame.Rooms[0];

                ShowWelcomeOverlay = false;
                ShowSavesOverlay = false;
                ActiveView = "Dashboard";
            });

            ShowLoadGameCommand = new Command(async () =>
            {
                var saves = await _storage.ListSavesAsync();
                AvailableSaves.Clear();
                foreach (var save in saves)
                {
                    AvailableSaves.Add(save);
                }
                ShowSavesOverlay = true;
            });

            LoadSelectedGameCommand = new Command(async () =>
            {
                if (string.IsNullOrEmpty(SelectedSave)) return;
                var game = await _storage.LoadAsync(SelectedSave);
                if (game != null)
                {
                    CurrentGame = game;
                    ShowWelcomeOverlay = false;
                    ShowSplashOverlay = false;
                    ShowSavesOverlay = false;
                    ActiveView = "Dashboard";
                    SaveRecentProject(SelectedSave);
                }
            });

            DeleteSelectedGameCommand = new Command<string>(async saveName =>
            {
                if (string.IsNullOrEmpty(saveName)) return;
                
                bool confirm = true;
                if (ShowConfirmDialogAsync != null)
                {
                    confirm = await ShowConfirmDialogAsync("Delete Project", $"Are you sure you want to permanently delete \"{saveName}\"? This cannot be undone.");
                }

                if (confirm)
                {
                    await _storage.DeleteSaveAsync(saveName);
                    AvailableSaves.Remove(saveName);
                    RemoveRecentProject(saveName);
                }
            });

            SaveGameCommand = new Command(async () =>
            {
                await SaveGameAsync();
            });

            CloseWelcomeCommand = new Command(() => ShowWelcomeOverlay = false);

            PublishCommand = new Command(async () => await PublishProjectAsync());

            SelectPlatformCommand = new Command<string>(platform =>
            {
                if (Enum.TryParse<PackagingTarget>(platform, out var target))
                {
                    SelectedPublishTarget = target;
                }
            });

            StartEditingActionCommand = new Command<RagsCore.Models.Action>(action =>
            {
                if (action == null) return;
                ActiveAction = action;
                IsVisualEditing = true;
            });

            StopEditingActionCommand = new Command(async () =>
            {
                IsVisualEditing = false;
                ActiveAction = null;
                await SaveGameAsync();
                RagsCore.Services.GlobalActionLibraryService.SaveLibrary(GlobalActions.ToList());
            });

            LoadLastWorkspaceCommand = new Command(async () =>
            {
                LoadRecentProjects();
                var last = RecentProjects.FirstOrDefault();
                if (!string.IsNullOrEmpty(last))
                {
                    var game = await _storage.LoadAsync(last);
                    if (game != null)
                    {
                        CurrentGame = game;
                        ShowWelcomeOverlay = false;
                        ShowSplashOverlay = false;
                        ActiveView = "Dashboard";
                    }
                }
            });

            LoadRecentProjectCommand = new Command<string>(async path =>
            {
                if (string.IsNullOrEmpty(path)) return;
                var game = await _storage.LoadAsync(path);
                if (game != null)
                {
                    CurrentGame = game;
                    ShowWelcomeOverlay = false;
                    ShowSplashOverlay = false;
                    ActiveView = "Dashboard";
                    SaveRecentProject(path);
                }
                else
                {
                    RemoveRecentProject(path);
                }
            });

            RemoveRecentProjectCommand = new Command<string>(path =>
            {
                RemoveRecentProject(path);
            });

            // Load recents on startup
            LoadRecentProjects();

            // Initialize Global Action Library
            foreach (var act in RagsCore.Services.GlobalActionLibraryService.LoadLibrary())
            {
                act.PropertyChanged += OnGlobalActionPropertyChanged;
                GlobalActions.Add(act);
            }

            AddGlobalActionCommand = new Command(() =>
            {
                var newAct = new RagsCore.Models.Action
                {
                    Name = $"New Template Action {GlobalActions.Count + 1}",
                    Trigger = ActionTrigger.UserClicked
                };
                newAct.PropertyChanged += OnGlobalActionPropertyChanged;
                GlobalActions.Add(newAct);
                RagsCore.Services.GlobalActionLibraryService.SaveLibrary(GlobalActions.ToList());
            });

            DeleteGlobalActionCommand = new Command<RagsCore.Models.Action>(act =>
            {
                if (act == null) return;
                act.PropertyChanged -= OnGlobalActionPropertyChanged;
                GlobalActions.Remove(act);
                RagsCore.Services.GlobalActionLibraryService.SaveLibrary(GlobalActions.ToList());
            });

            PasteGlobalActionCommand = new Command(() =>
            {
                var pasted = RagNext.Designer.Avalonia.Services.ActionClipboardService.Paste() as RagsCore.Models.Action;
                if (pasted == null) return;

                string baseName = pasted.Name;
                string candidate = baseName;
                int counter = 1;
                while (System.Linq.Enumerable.Any(GlobalActions, a => string.Equals(a.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    if (counter == 1)
                    {
                        candidate = $"{baseName} - Copy";
                    }
                    else
                    {
                        candidate = $"{baseName} - Copy ({counter})";
                    }
                    counter++;
                }

                pasted.Name = candidate;
                pasted.PropertyChanged += OnGlobalActionPropertyChanged;
                GlobalActions.Add(pasted);
                RagsCore.Services.GlobalActionLibraryService.SaveLibrary(GlobalActions.ToList());
            });

            SelectActionTemplateCommand = new Command<RagsCore.Models.Action>(async template =>
            {
                Console.WriteLine($"[DEBUG] SelectActionTemplateCommand triggered. Template: {template?.Name ?? "null"}");
                if (_actionTargetEntity == null)
                {
                    Console.WriteLine("[DEBUG] SelectActionTemplateCommand: _actionTargetEntity is null");
                    ShowActionSelectorOverlay = false;
                    return;
                }

                RagsCore.Models.Action act;
                if (template == null || template.Name == "New Action (Blank)")
                {
                    string defaultName = "New Action";
                    if (_actionTargetEntity is Room) defaultName = "New Room Action";
                    else if (_actionTargetEntity is Character) defaultName = "New Character Action";
                    else if (_actionTargetEntity is GameObject) defaultName = "New Object Action";
                    else if (_actionTargetEntity is Player) defaultName = "New Player Action";

                    act = new RagsCore.Models.Action { Name = defaultName, Trigger = ActionTrigger.UserClicked, InitallyActive = true };
                }
                else
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(template, RagsCore.RagsJsonContext.CustomDefault.Action);
                        var clone = JsonSerializer.Deserialize<RagsCore.Models.Action>(json, RagsCore.RagsJsonContext.CustomDefault.Action);
                        if (clone != null)
                        {
                            clone.Id = Guid.NewGuid();
                            act = clone;
                        }
                        else
                        {
                            act = new RagsCore.Models.Action { Name = template.Name, Trigger = template.Trigger, InitallyActive = true };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] SelectActionTemplateCommand serialization exception: {ex.Message}");
                        act = new RagsCore.Models.Action { Name = template.Name, Trigger = template.Trigger, InitallyActive = true };
                    }
                }

                if (_actionTargetEntity is Room room) { room.Actions.Add(act); LastAddedAction = act; }
                else if (_actionTargetEntity is Character character) { character.Actions.Add(act); LastAddedAction = act; }
                else if (_actionTargetEntity is GameObject obj) { obj.Actions.Add(act); LastAddedAction = act; }
                else if (_actionTargetEntity is Player player) { player.Actions.Add(act); LastAddedAction = act; }

                Console.WriteLine("[DEBUG] SelectActionTemplateCommand: Saving game...");
                await SaveGameAsync();
                ShowActionSelectorOverlay = false;
                _actionTargetEntity = null;
                Console.WriteLine("[DEBUG] SelectActionTemplateCommand completed successfully");
            });

            CloseActionSelectorCommand = new Command(() =>
            {
                Console.WriteLine("[DEBUG] CloseActionSelectorCommand triggered");
                ShowActionSelectorOverlay = false;
                _actionTargetEntity = null;
            });

            AddActionCommand = new Command<object>(parameter =>
            {
                Console.WriteLine($"[DEBUG] AddActionCommand triggered. Parameter type: {parameter?.GetType().Name ?? "null"}");
                if (parameter == null) return;
                _actionTargetEntity = parameter;

                MatchActionTemplates.Clear();
                MatchActionTemplates.Add(new RagsCore.Models.Action { Name = "New Action (Blank)", Trigger = ActionTrigger.UserClicked });

                foreach (var act in GlobalActions)
                {
                    bool isMatch = false;
                    if (parameter is Room) isMatch = act.ApplyToRooms;
                    else if (parameter is Player) isMatch = act.ApplyToPlayer;
                    else if (parameter is Character) isMatch = act.ApplyToCharacters;
                    else if (parameter is GameObject obj)
                    {
                        if (obj.IsContainer && act.ApplyToContainerObjects) isMatch = true;
                        if (obj.IsWearable && act.ApplyToWearableObjects) isMatch = true;
                        if (obj.IsCollectible && act.ApplyToGrabableObjects) isMatch = true;
                        if (!obj.IsContainer && !obj.IsWearable && !obj.IsCollectible && act.ApplyToStaticObjects) isMatch = true;
                    }

                    if (isMatch)
                    {
                        MatchActionTemplates.Add(act);
                    }
                }

                ShowActionSelectorOverlay = true;
                Console.WriteLine("[DEBUG] AddActionCommand completed. ShowActionSelectorOverlay is now true.");
            });

            DeleteActionCommand = new Command<RagsCore.Models.Action>(async action =>
            {
                if (action == null || CurrentGame == null) return;
                if (CurrentGame.Player.Actions.Contains(action))
                {
                    CurrentGame.Player.Actions.Remove(action);
                }
                foreach (var r in CurrentGame.Rooms)
                {
                    if (r.Actions.Contains(action)) r.Actions.Remove(action);
                }
                foreach (var c in CurrentGame.Characters)
                {
                    if (c.Actions.Contains(action)) c.Actions.Remove(action);
                }
                foreach (var o in CurrentGame.Objects)
                {
                    if (o.Actions.Contains(action)) o.Actions.Remove(action);
                }
                if (action is GlobalFunction fn && CurrentGame.Functions.Contains(fn))
                {
                    CurrentGame.Functions.Remove(fn);
                }
                await SaveGameAsync();
            });

            CopyActionCommand = new Command<RagsCore.Models.Action>(action =>
            {
                if (action == null) return;
                RagNext.Designer.Avalonia.Services.ActionClipboardService.Copy(action);
                OnPropertyChanged(nameof(CanPasteAction));
            });

            PasteActionCommand = new Command<object>(async parameter =>
            {
                var pasted = RagNext.Designer.Avalonia.Services.ActionClipboardService.Paste() as RagsCore.Models.Action;
                if (pasted == null) return;

                if (parameter is Room room)
                {
                    room.Actions.Add(pasted);
                    await SaveGameAsync();
                }
                else if (parameter is Character character)
                {
                    character.Actions.Add(pasted);
                    await SaveGameAsync();
                }
                else if (parameter is GameObject obj)
                {
                    obj.Actions.Add(pasted);
                    await SaveGameAsync();
                }
                else if (parameter is Player player)
                {
                    player.Actions.Add(pasted);
                    await SaveGameAsync();
                }
            });

            OpenComposeCommand = new Command<object>(parameter =>
            {
                if (parameter == null) return;
                ComposeTarget = parameter;
                
                string name = "";
                var nameProp = parameter.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    name = nameProp.GetValue(parameter)?.ToString() ?? "";
                }

                var descProp = parameter.GetType().GetProperty("Description");
                var textProp = parameter.GetType().GetProperty("Text");
                if (descProp != null)
                {
                    ComposeTitle = $"Compose Description - {name}";
                    ComposeText = descProp.GetValue(parameter)?.ToString() ?? "";
                }
                else if (textProp != null)
                {
                    ComposeTitle = $"Compose Status Text - {name}";
                    ComposeText = textProp.GetValue(parameter)?.ToString() ?? "";
                }
                
                ShowComposeOverlay = true;
            });

            CloseComposeCommand = new Command(async () =>
            {
                ShowComposeOverlay = false;
                if (!string.IsNullOrEmpty(ComposeNodeId))
                {
                    if (ComposeApplied != null)
                    {
                        // Fire and wait for the event delegates to finish executing their async steps (such as WebView script invokes)
                        var tasks = ComposeApplied.GetInvocationList()
                            .Select(d => ((Func<string, string, string, Task>)d)(ComposeNodeId, ComposeFieldName ?? "", ComposeText));
                        await Task.WhenAll(tasks);
                    }
                    ComposeNodeId = null;
                    ComposeFieldName = null;
                }
                ComposeTarget = null;
                await SaveGameAsync();
            });

            ShowLauncherCommand = new Command(() =>
            {
                LoadRecentProjects();
                ShowWelcomeOverlay = true;
                ShowSplashOverlay = false; // Directly go to launcher when manually invoked
                ShowSavesOverlay = false;
            });

            // Inventory Item Selector Setup
            TriggerAddInventoryCommand = new Command<object>(target =>
            {
                if (target == null) return;
                InventoryTarget = target;
                ShowInventorySelectorOverlay = true;
            });

            CloseInventorySelectorCommand = new Command(() =>
            {
                ShowInventorySelectorOverlay = false;
                InventoryTarget = null;
            });

            ClearPortraitCommand = new Command<object>(target =>
            {
                if (target == null) return;
                if (target is Player p)
                {
                    p.PortraitImagePath = null;
                }
                else if (target is Room r)
                {
                    r.PortraitImagePath = null;
                }
                else if (target is GameObject go)
                {
                    go.PortraitImagePath = null;
                }
            });

            SelectInventoryItemCommand = new Command<GameObject>(async item =>
            {
                if (item == null || InventoryTarget == null) return;
                if (InventoryTarget is Player p && !p.Inventory.Contains(item))
                {
                    p.Inventory.Add(item);
                }
                else if (InventoryTarget is Character c && !c.Inventory.Contains(item))
                {
                    c.Inventory.Add(item);
                }
                ShowInventorySelectorOverlay = false;
                await SaveGameAsync();
            });

            RemoveInventoryItemCommand = new Command<object>(async parameter =>
            {
                if (parameter == null) return;

                // parameter is a tuple or we can inspect elements
                if (parameter is global::System.Collections.IList list && list.Count == 2)
                {
                    var owner = list[0];
                    var item = list[1] as GameObject;
                    if (item == null) return;

                    if (owner is Player p) p.Inventory.Remove(item);
                    else if (owner is Character c) c.Inventory.Remove(item);
                    await SaveGameAsync();
                }
                else if (parameter is GameObject item)
                {
                    if (CurrentGame != null)
                    {
                        if (CurrentGame.Player.Inventory.Contains(item))
                        {
                            CurrentGame.Player.Inventory.Remove(item);
                        }
                        else
                        {
                            foreach (var c in CurrentGame.Characters)
                            {
                                if (c.Inventory.Contains(item))
                                {
                                    c.Inventory.Remove(item);
                                    break;
                                }
                            }
                        }
                        await SaveGameAsync();
                    }
                }
            });

            // Attributes Editor Dialog Setup
            TriggerAddAttributeCommand = new Command<object>(target =>
            {
                Console.WriteLine($"[DEBUG] TriggerAddAttributeCommand triggered. Target type: {target?.GetType().Name ?? "null"}");
                if (target == null) return;
                AttributeTarget = target;
                _editingAttribute = null;
                NewAttributeName = string.Empty;
                NewAttributeValue = string.Empty;
                ShowAttributeDialogOverlay = true;
            });

            TriggerEditAttributeCommand = new Command<object>(parameter =>
            {
                if (parameter is CustomAttribute attr)
                {
                    object? owner = null;
                    if (CurrentGame != null)
                    {
                        if (CurrentGame.Player.Attributes.Contains(attr)) owner = CurrentGame.Player;
                        else
                        {
                            foreach (var r in CurrentGame.Rooms)
                            {
                                if (r.Attributes.Contains(attr)) { owner = r; break; }
                            }
                            if (owner == null)
                            {
                                foreach (var c in CurrentGame.Characters)
                                {
                                    if (c.Attributes.Contains(attr)) { owner = c; break; }
                                }
                            }
                            if (owner == null)
                            {
                                foreach (var o in CurrentGame.Objects)
                                {
                                    if (o.Attributes.Contains(attr)) { owner = o; break; }
                                }
                            }
                        }
                    }
                    if (owner != null)
                    {
                        AttributeTarget = owner;
                        _editingAttribute = attr;
                        NewAttributeName = attr.Name;
                        NewAttributeValue = attr.Value ?? string.Empty;
                        ShowAttributeDialogOverlay = true;
                    }
                }
            });

            CloseAttributeDialogCommand = new Command(() =>
            {
                Console.WriteLine("[DEBUG] CloseAttributeDialogCommand triggered");
                ShowAttributeDialogOverlay = false;
                AttributeTarget = null;
                _editingAttribute = null;
            });

            SaveAttributeCommand = new Command(async () =>
            {
                Console.WriteLine($"[DEBUG] SaveAttributeCommand triggered. Target: {AttributeTarget?.GetType().Name ?? "null"}, Name: '{NewAttributeName}', Value: '{NewAttributeValue}'");
                if (AttributeTarget == null || string.IsNullOrWhiteSpace(NewAttributeName))
                {
                    Console.WriteLine("[DEBUG] SaveAttributeCommand aborted: Target is null or Name is empty");
                    return;
                }

                global::System.Collections.ObjectModel.ObservableCollection<CustomAttribute>? attrs = null;
                if (AttributeTarget is Player p) attrs = p.Attributes;
                else if (AttributeTarget is Room r) attrs = r.Attributes;
                else if (AttributeTarget is Character c) attrs = c.Attributes;
                else if (AttributeTarget is GameObject o) attrs = o.Attributes;

                if (attrs != null)
                {
                    string cleanName = NewAttributeName.Trim();
                    string cleanVal = NewAttributeValue.Trim();

                    if (_editingAttribute != null)
                    {
                        var existing = attrs.FirstOrDefault(a => string.Equals(a.Name, cleanName, StringComparison.OrdinalIgnoreCase));
                        if (existing != null && existing != _editingAttribute)
                        {
                            existing.Value = cleanVal;
                            attrs.Remove(_editingAttribute);
                        }
                        else
                        {
                            _editingAttribute.Name = cleanName;
                            _editingAttribute.Value = cleanVal;
                        }
                    }
                    else
                    {
                        CustomAttribute.SetAttribute(cleanName, cleanVal, attrs);
                    }
                    Console.WriteLine($"[DEBUG] SaveAttributeCommand: Successfully set/edited attribute '{cleanName}' = '{cleanVal}'");
                }
                else
                {
                    Console.WriteLine("[DEBUG] SaveAttributeCommand: attrs collection was null");
                }

                ShowAttributeDialogOverlay = false;
                _editingAttribute = null;
                await SaveGameAsync();
            });

            RemoveAttributeCommand = new Command<object>(async parameter =>
            {
                if (parameter == null) return;

                if (parameter is global::System.Collections.IList list && list.Count == 2)
                {
                    var owner = list[0];
                    var attr = list[1] as CustomAttribute;
                    if (attr == null) return;

                    global::System.Collections.ObjectModel.ObservableCollection<CustomAttribute>? attrs = null;
                    if (owner is Player p) attrs = p.Attributes;
                    else if (owner is Room r) attrs = r.Attributes;
                    else if (owner is Character c) attrs = c.Attributes;
                    else if (owner is GameObject o) attrs = o.Attributes;

                    if (attrs != null)
                    {
                        attrs.Remove(attr);
                    }
                    await SaveGameAsync();
                }
                else if (parameter is CustomAttribute attr)
                {
                    if (CurrentGame != null)
                    {
                        bool found = false;
                        if (CurrentGame.Player.Attributes.Contains(attr))
                        {
                            CurrentGame.Player.Attributes.Remove(attr);
                            found = true;
                        }
                        if (!found)
                        {
                            foreach (var r in CurrentGame.Rooms)
                            {
                                if (r.Attributes.Contains(attr))
                                {
                                    r.Attributes.Remove(attr);
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (!found)
                        {
                            foreach (var c in CurrentGame.Characters)
                            {
                                if (c.Attributes.Contains(attr))
                                {
                                    c.Attributes.Remove(attr);
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (!found)
                        {
                            foreach (var o in CurrentGame.Objects)
                            {
                                if (o.Attributes.Contains(attr))
                                {
                                    o.Attributes.Remove(attr);
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (found)
                        {
                            await SaveGameAsync();
                        }
                    }
                }
            });
        }

        public ICommand ShowLauncherCommand { get; }

        public void RunValidation()
        {
            ValidationErrors.Clear();
            if (CurrentGame == null)
            {
                ValidationErrors.Add("No project loaded to validate.");
                return;
            }

            var results = ValidationEngine.Validate(CurrentGame);
            if (results == null || results.Length == 0)
            {
                ValidationErrors.Add("✅ No errors or warnings! The project database is completely valid.");
            }
            else
            {
                foreach (var err in results)
                {
                    ValidationErrors.Add(err);
                }
            }
        }

        private async Task PublishProjectAsync()
        {
            if (CurrentGame == null) return;

            var destination = PublishDestination?.Trim();
            var title = PublishTitle?.Trim();

            if (string.IsNullOrWhiteSpace(destination))
            {
                PublishLogs += "❌ Publish validation failed: Please select an output folder.\n";
                PublishStatus = "Failed";
                return;
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                PublishLogs += "❌ Publish validation failed: Please enter a game title.\n";
                PublishStatus = "Failed";
                return;
            }
            if (!PublishEngine.IsTemplateAvailable(SelectedPublishTarget))
            {
                PublishLogs += $"❌ Template Missing: No shell template found for {SelectedPublishTarget}.\n" +
                               $"Please build the Unity player first and copy to: {PublishEngine.GetTemplateDir(SelectedPublishTarget)}\n";
                PublishStatus = "Failed";
                return;
            }

            IsPublishing = true;
            PublishStatus = "Analyzing and building database...";
            PublishLogs = "Initializing publication packaging engine...\n";

            try
            {
                string cleanTitle = PublishEngine.SanitizeName(title);
                if (PublishEngine.IsTargetInUse(destination, cleanTitle, SelectedPublishTarget))
                {
                    if (ShowAlertDialogAsync != null)
                    {
                        await ShowAlertDialogAsync(
                            "Export Destination Locked",
                            "The game or one of its components (e.g. UnityCrashHandler64.exe) is currently running and using the target files. Please close the running game and try publishing again.");
                    }
                    PublishLogs += "❌ Publishing failed: Target files are in use. Please close the running game.\n";
                    PublishStatus = "Failed: Target in use";
                    IsPublishing = false;
                    return;
                }

                // We'll perform standard validation first
                var errors = ValidationEngine.Validate(CurrentGame);
                if (errors.Any(e => e.StartsWith("Error:")))
                {
                    PublishStatus = "Publish failed: Validation errors present";
                    PublishLogs += "❌ Cannot build package! Project has critical database validation errors.\n";
                    IsPublishing = false;
                    return;
                }

                PublishLogs += "✔️ Database validation passed.\n";

                // Overwrite warning check
                if (Directory.Exists(destination) && Directory.GetFileSystemEntries(destination).Length > 0)
                {
                    string clearResult = "No";
                    if (MediaLibraryViewModel.ConfirmPublishDialogAsync != null)
                    {
                        clearResult = await MediaLibraryViewModel.ConfirmPublishDialogAsync(
                            "Overwrite Warning",
                            "The destination folder is not empty. To prevent mixing game assets or loading outdated configurations, it is highly recommended to clear this folder.\n\nDo you want to delete its contents before publishing?");
                    }

                    if (clearResult == "Cancel")
                    {
                        PublishLogs += "❌ Publishing cancelled by user.\n";
                        PublishStatus = "Cancelled";
                        IsPublishing = false;
                        return;
                    }

                    if (clearResult == "Yes")
                    {
                        PublishLogs += "Clearing old game assets from destination folder...\n";
                        try
                        {
                            cleanTitle = PublishEngine.SanitizeName(title);
                            string templateDir = PublishEngine.GetTemplateDir(SelectedPublishTarget);
                            PublishEngine.SafeClearDestination(templateDir, destination, cleanTitle, SelectedPublishTarget);
                            PublishLogs += "✔️ Old game assets cleared successfully.\n";
                        }
                        catch (Exception ex)
                        {
                            PublishLogs += $"❌ Error: Could not clear old game assets: {ex.Message}\n";
                            PublishStatus = "Failed";
                            IsPublishing = false;
                            return;
                        }
                    }
                    else
                    {
                        PublishLogs += "⚠️ Continuing without clearing folder.\n";
                    }
                }

                // Sync UI metadata back to the active Game model
                CurrentGame.Title = title;
                CurrentGame.Author = PublishAuthor?.Trim() ?? string.Empty;
                CurrentGame.Version = PublishVersion?.Trim() ?? "1.0.0";

                // Wire progress reporting
                Action<string> progressHandler = msg =>
                {
                    Dispatcher.UIThread.Post(() => PublishLogs += msg + "\n");
                };

                PublishEngine.OnProgress += progressHandler;

                try
                {
                    PublishLogs += $"Exporting compiled branded assets for {SelectedPublishTarget} to: {destination}\n";
                    
                    await Task.Run(async () =>
                        await PublishEngine.PublishAsync(CurrentGame, SelectedPublishTarget, destination, PublishCreateZip));

                    PublishLogs += "🎉 Publication complete! Branded package is ready.\n";
                    PublishStatus = "Success! Package created.";
                    
                    if (Preferences != null)
                    {
                        Preferences.LastPublishDirectory = destination;
                    }
                    
                    // Open folder automatically
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = destination, UseShellExecute = true }); } catch { }
                }
                finally
                {
                    PublishEngine.OnProgress -= progressHandler;
                }
            }
            catch (Exception ex)
            {
                PublishLogs += $"❌ Error during publishing: {ex.Message}\n";
                PublishStatus = "Failed";
            }
            finally
            {
                IsPublishing = false;
            }
        }

        private string RecentProjectsFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", "recent_projects.json");

        public ObservableCollection<string> RecentProjects { get; } = new();

        public class SampleGameItem
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public ICommand? LoadCommand { get; set; }
        }

        public ObservableCollection<SampleGameItem> SampleGames { get; } = new();

        public ICommand LoadSampleGameCommand { get; }

        public void LoadSampleGames()
        {
            try
            {
                SampleGames.Clear();
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var samplesDir = Path.Combine(appDir, "Samples");
                if (Directory.Exists(samplesDir))
                {
                    foreach (var file in Directory.GetFiles(samplesDir, "*.ragnext"))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        var friendlyTitle = System.Text.RegularExpressions.Regex.Replace(fileName, "([a-z])([A-Z])", "$1 $2");
                        var item = new SampleGameItem
                        {
                            Title = friendlyTitle,
                            Description = $"Click to import a playable copy of the \"{friendlyTitle}\" demo.",
                            FilePath = file
                        };
                        item.LoadCommand = new Command(async () => await LoadSampleGameAsync(item));
                        SampleGames.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Failed to load sample games: {ex.Message}");
            }
        }

        public async Task LoadSampleGameAsync(SampleGameItem sample)
        {
            if (sample == null) return;
            if (!File.Exists(sample.FilePath))
            {
                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Import Error", $"The sample package file could not be found or read at:\n{sample.FilePath}");
                }
                return;
            }

            try
            {
                using var zip = ZipFile.OpenRead(sample.FilePath);
                var gameEntry = zip.GetEntry("game.json");
                if (gameEntry == null)
                {
                    if (ShowAlertDialogAsync != null)
                    {
                        await ShowAlertDialogAsync("Import Error", "The sample package is invalid (missing 'game.json').");
                    }
                    return;
                }

                Game? importedGame;
                using (var s = gameEntry.Open())
                {
                    importedGame = await JsonSerializer.DeserializeAsync(s, RagsCore.RagsJsonContext.CustomDefault.Game);
                }

                if (importedGame == null)
                {
                    if (ShowAlertDialogAsync != null)
                    {
                        await ShowAlertDialogAsync("Import Error", "Failed to deserialize the sample game data.");
                    }
                    return;
                }

                // Always assign a new ID to avoid conflict with the original template/demo
                importedGame.Id = Guid.NewGuid();
                var originalTitle = importedGame.Title ?? "Sample Adventure";
                
                // Ensure a unique filename and save path under active saved games
                var saves = await _storage.ListSavesAsync();
                var baseName = originalTitle;
                var cleanName = new string(baseName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
                if (string.IsNullOrEmpty(cleanName)) cleanName = "SampleGame";

                var attempt = 1;
                var testName = cleanName;
                while (saves.Any(s => string.Equals(s, testName, StringComparison.OrdinalIgnoreCase)))
                {
                    attempt++;
                    testName = $"{cleanName} ({attempt})";
                }
                
                importedGame.Title = attempt > 1 ? $"{originalTitle} ({attempt})" : originalTitle;
                importedGame.FileName = testName;

                // Extract assets to the new local app data path
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", importedGame.Id.ToString("N"));
                var assetsDir = Path.Combine(root, "Assets");
                if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.Equals("media_tree.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var relativePath = entry.FullName.Replace('\\', '/');
                        var targetPath = Path.Combine(root, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                        
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(targetPath, overwrite: true);
                        }
                    }
                }

                await _storage.SaveAsync(importedGame, importedGame.FileName);

                CurrentGame = importedGame;
                ShowWelcomeOverlay = false;
                ShowSplashOverlay = false;
                ShowSavesOverlay = false;
                ActiveView = "Dashboard";
                SaveRecentProject(importedGame.FileName);

                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Sample Loaded", $"Successfully imported demo copy: \"{importedGame.Title}\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Load sample game failed: {ex.Message}");
                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Import Error", $"An error occurred loading the sample:\n{ex.Message}");
                }
            }
        }

        private void AutoSaveActivePreset()
        {
            if (CurrentGame?.Theme == null) return;
            var active = CurrentGame.Theme.ActivePreset;
            if (string.IsNullOrEmpty(active) || string.Equals(active, "Default", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                var dir = GetPresetsDirectory();
                var presetPath = Path.Combine(dir, $"{active.Trim()}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(CurrentGame.Theme, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(presetPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Presets] Auto-save active preset failed: {ex.Message}");
            }
        }

        private readonly System.Threading.SemaphoreSlim _saveSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        public async Task SaveGameAsync()
        {
            if (_isProjectLoading || CurrentGame == null) return;
            AutoSaveActivePreset();
            IsSaving = true;
            SaveStatusText = "Saving changes...";
            await _saveSemaphore.WaitAsync();
            try
            {
                var targetName = string.IsNullOrWhiteSpace(CurrentGame.FileName) 
                    ? (string.IsNullOrWhiteSpace(CurrentGame.Title) ? "game" : CurrentGame.Title)
                    : CurrentGame.FileName;

                await _storage.SaveAsync(CurrentGame, targetName, false);
                if (!string.IsNullOrWhiteSpace(CurrentGame.FileName))
                {
                    SaveRecentProject(CurrentGame.FileName);
                }
                SaveStatusText = "Saved";
            }
            catch (IOException)
            {
                // Yield and retry once in case of temporary file locking
                await Task.Delay(200);
                try
                {
                    var targetName = string.IsNullOrWhiteSpace(CurrentGame.FileName) 
                        ? (string.IsNullOrWhiteSpace(CurrentGame.Title) ? "game" : CurrentGame.Title)
                        : CurrentGame.FileName;

                    await _storage.SaveAsync(CurrentGame, targetName, false);
                    SaveStatusText = "Saved";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Save retry failed: {ex.Message}");
                    SaveStatusText = "Save failed";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Save failed: {ex.Message}");
                SaveStatusText = "Save failed";
            }
            finally
            {
                _saveSemaphore.Release();
                IsSaving = false;
            }
        }

        private void OnGlobalActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RagsCore.Services.GlobalActionLibraryService.SaveLibrary(GlobalActions.ToList());
        }

        public void LoadRecentProjects()
        {
            try
            {
                RecentProjects.Clear();
                var path = RecentProjectsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize(json, DesignerJsonContext.Default.ListString);
                    if (list != null)
                    {
                        var availableSaves = _storage.ListSavesAsync().GetAwaiter().GetResult();
                        var availableSet = new System.Collections.Generic.HashSet<string>(availableSaves, System.StringComparer.OrdinalIgnoreCase);

                        var validRecentList = new System.Collections.Generic.List<string>();
                        foreach (var p in list)
                        {
                            if (!string.IsNullOrWhiteSpace(p) && availableSet.Contains(p))
                            {
                                if (!RecentProjects.Contains(p))
                                    RecentProjects.Add(p);
                                validRecentList.Add(p);
                            }
                        }

                        if (validRecentList.Count < list.Count)
                        {
                            var updatedJson = JsonSerializer.Serialize(validRecentList, DesignerJsonContext.Default.ListString);
                            File.WriteAllText(RecentProjectsFilePath, updatedJson);
                        }
                    }
                }
            }
            catch { }
        }

        public void SaveRecentProject(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                var list = RecentProjects.ToList();
                list.Remove(path);
                list.Insert(0, path);
                if (list.Count > 5) list = list.Take(5).ToList();

                RecentProjects.Clear();
                foreach (var p in list) RecentProjects.Add(p);

                var json = JsonSerializer.Serialize(list, DesignerJsonContext.Default.ListString);
                Directory.CreateDirectory(Path.GetDirectoryName(RecentProjectsFilePath)!);
                File.WriteAllText(RecentProjectsFilePath, json);
            }
            catch { }
        }

        public void RemoveRecentProject(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                RecentProjects.Remove(path);
                var list = RecentProjects.ToList();
                var json = JsonSerializer.Serialize(list, DesignerJsonContext.Default.ListString);
                File.WriteAllText(RecentProjectsFilePath, json);
            }
            catch { }
        }
        public async Task ImportPackageAsync()
        {
            if (PickImportPackageFileAsync == null) return;
            var file = await PickImportPackageFileAsync();
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            try
            {
                using var zip = ZipFile.OpenRead(file);
                var gameEntry = zip.GetEntry("game.json");
                if (gameEntry == null)
                {
                    if (ShowAlertDialogAsync != null)
                    {
                        await ShowAlertDialogAsync("Import Failed", "The selected archive is not a valid RagNext package (missing game.json).");
                    }
                    return;
                }

                Game? importedGame;
                using (var s = gameEntry.Open())
                {
                    importedGame = await JsonSerializer.DeserializeAsync(s, RagsCore.RagsJsonContext.CustomDefault.Game);
                }

                if (importedGame == null)
                {
                    if (ShowAlertDialogAsync != null)
                    {
                        await ShowAlertDialogAsync("Import Failed", "Failed to parse the game data file. The package may be corrupted.");
                    }
                    return;
                }

                // Check if a game with the same ID or title already exists in the system
                var saves = await _storage.ListSavesAsync();
                var exists = false;
                var overwriteId = importedGame.Id;
                
                // We consider it existing if the exact ID matches or if a save exists with the sanitized filename of the title
                var baseName = importedGame.Title ?? "Untitled Game";
                var cleanName = new string(baseName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
                if (string.IsNullOrEmpty(cleanName)) cleanName = "ImportedGame";

                if (saves.Any(s => string.Equals(s, cleanName, StringComparison.OrdinalIgnoreCase)))
                {
                    exists = true;
                }
                
                if (exists && ShowConfirmDialogAsync != null)
                {
                    var replace = await ShowConfirmDialogAsync("Game Already Exists", $"A game named '{cleanName}' already exists. Would you like to overwrite it?\n\nSelecting 'No' will import it as a separate game copy.");
                    if (!replace)
                    {
                        // Assign a new ID and modify filename to be unique
                        importedGame.Id = Guid.NewGuid();
                        
                        var attempt = 1;
                        var newTitle = $"{baseName} (Copy)";
                        var testName = $"{cleanName} (Copy)";
                        while (saves.Any(s => string.Equals(s, testName, StringComparison.OrdinalIgnoreCase)))
                        {
                            attempt++;
                            newTitle = $"{baseName} (Copy {attempt})";
                            testName = $"{cleanName} (Copy {attempt})";
                        }
                        importedGame.Title = newTitle;
                        importedGame.FileName = testName;
                    }
                    else
                    {
                        // Overwriting: keep original ID and original filename to allow replacement
                        importedGame.FileName = cleanName;
                    }
                }

                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", importedGame.Id.ToString("N"));
                var assetsDir = Path.Combine(root, "Assets");
                if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.Equals("media_tree.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var relativePath = entry.FullName.Replace('\\', '/');
                        var targetPath = Path.Combine(root, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                        
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(targetPath, overwrite: true);
                        }
                    }
                }

                await _storage.SaveAsync(importedGame, importedGame.FileName ?? importedGame.Title ?? "ImportedGame");

                CurrentGame = importedGame;
                ShowWelcomeOverlay = false;
                ShowSplashOverlay = false;
                ShowSavesOverlay = false;
                ActiveView = "Dashboard";
                if (!string.IsNullOrEmpty(importedGame.FileName))
                {
                    SaveRecentProject(importedGame.FileName);
                }

                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Import Success", $"Successfully imported game design package: \"{importedGame.Title ?? "Untitled Game"}\"");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Import package failed: {ex.Message}");
                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Import Error", $"An error occurred during import:\n{ex.Message}");
                }
            }
        }

        public async Task ExportPackageAsync()
        {
            if (CurrentGame == null || PickExportPackageFileAsync == null) return;
            var defaultName = $"{SanitizeFileName(CurrentGame.Title ?? "MyGame")}.ragnext";
            var file = await PickExportPackageFileAsync(defaultName);
            if (string.IsNullOrEmpty(file)) return;

            try
            {
                if (File.Exists(file)) File.Delete(file);

                using var zip = ZipFile.Open(file, ZipArchiveMode.Create);

                var gameEntry = zip.CreateEntry("game.json", CompressionLevel.Optimal);
                await using (var s = gameEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(s, CurrentGame, RagsCore.RagsJsonContext.CustomDefault.Game);
                }

                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", CurrentGame.Id.ToString("N"));
                
                var mediaTreePath = Path.Combine(root, "media_tree.json");
                if (File.Exists(mediaTreePath))
                {
                    zip.CreateEntryFromFile(mediaTreePath, "media_tree.json", CompressionLevel.Optimal);
                }

                var assetsDir = Path.Combine(root, "Assets");
                if (Directory.Exists(assetsDir))
                {
                    foreach (var f in Directory.EnumerateFiles(assetsDir))
                    {
                        var name = Path.GetFileName(f);
                        zip.CreateEntryFromFile(f, Path.Combine("Assets", name), CompressionLevel.Optimal);
                    }
                }

                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Export Success", $"Successfully exported design package to:\n{file}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Export package failed: {ex.Message}");
                if (ShowAlertDialogAsync != null)
                {
                    await ShowAlertDialogAsync("Export Error", $"An error occurred during export:\n{ex.Message}");
                }
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(sanitized) ? "save" : sanitized;
        }

        private static string GetSafeDirectoryName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "RagNextProject";
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(title.Where(c => !invalid.Contains(c) && !char.IsWhiteSpace(c)).ToArray());
            return string.IsNullOrEmpty(sanitized) ? "RagNextProject" : sanitized;
        }

        public static global::Avalonia.Data.Converters.IMultiValueConverter MakeTupleConverter { get; } = new FuncMultiValueConverter<object, Tuple<object, object>>(parts =>
        {
            if (parts == null || parts.Count < 2) return new Tuple<object, object>(null!, null!);
            return new Tuple<object, object>(parts[0], parts[1]);
        });
    }

    public class FuncMultiValueConverter<TIn, TOut> : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        private readonly Func<System.Collections.Generic.IList<object?>, TOut> _conv;
        public FuncMultiValueConverter(Func<System.Collections.Generic.IList<object?>, TOut> conv) => _conv = conv;
        public object? Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => _conv(values);
    }
}
