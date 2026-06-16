using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern long mciSendString(string command, System.Text.StringBuilder? returnValue, int returnLength, IntPtr winHandle);

        private readonly IGameStorage _storage;

        private Game? _game;
        public Game? CurrentGame
        {
            get => _game;
            set
            {
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

                        if (!string.IsNullOrEmpty(Preferences?.LastPublishDirectory))
                        {
                            PublishDestination = Preferences.LastPublishDirectory;
                        }
                        else
                        {
                            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            PublishDestination = Path.Combine(docs, "RagNext_Published");
                        }

                        value.MediaAssets.CollectionChanged += (sender, args) =>
                        {
                            OnPropertyChanged(nameof(VideoMediaAssets));
                            OnPropertyChanged(nameof(ImageMediaAssets));
                            OnPropertyChanged(nameof(AudioMediaAssets));
                        };

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

                        if (value.SplashScreen != null)
                        {
                            value.SplashScreen.PropertyChanged += (sender, args) =>
                            {
                                if (args.PropertyName == nameof(SplashScreenSettings.ImageAssetId) ||
                                    args.PropertyName == nameof(SplashScreenSettings.VideoAssetId) ||
                                    args.PropertyName == nameof(SplashScreenSettings.SoundAssetId) ||
                                    args.PropertyName == nameof(SplashScreenSettings.Mode) ||
                                    args.PropertyName == nameof(SplashScreenSettings.Text) ||
                                    args.PropertyName == nameof(SplashScreenSettings.FontColor) ||
                                    args.PropertyName == nameof(SplashScreenSettings.FontSize) ||
                                    args.PropertyName == nameof(SplashScreenSettings.TextX) ||
                                    args.PropertyName == nameof(SplashScreenSettings.TextY))
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
                            };
                        }
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

        // Active properties
        public Player? Player => CurrentGame?.Player;
        public SplashScreenSettings? SplashScreen => CurrentGame?.SplashScreen;

        public double SplashPreviewTextLeft => (CurrentGame?.SplashScreen?.TextX ?? 50) * 19.2;
        public double SplashPreviewTextTop => (CurrentGame?.SplashScreen?.TextY ?? 50) * 10.8;
        public double SplashPreviewFontSize => (CurrentGame?.SplashScreen?.FontSize ?? 32) * 2.4;

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
                var splash = game?.SplashScreen;
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

        public bool IsSplashVideoMode => CurrentGame?.SplashScreen?.Mode == "Video";
        public bool IsSplashVideoPreviewVisible => IsSplashVideoMode && (ActiveView == "SplashScreen");

        public MediaAsset? SelectedSplashImageAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == CurrentGame?.SplashScreen?.ImageAssetId);
            set
            {
                if (CurrentGame == null) return;
                // Bug #3: Auto-initialize SplashScreen so setter never silently drops values.
                if (CurrentGame.SplashScreen == null)
                {
                    CurrentGame.SplashScreen = new RagsCore.Models.SplashScreenSettings();
                    CurrentGame.SplashScreen.PropertyChanged += (s, a) => _ = SaveGameAsync();
                }
                var newId = value?.IdString ?? string.Empty;
                // Guard: Avalonia resets ComboBox SelectedItem to null whenever ItemsSource
                // is refreshed. Prevent that from silently overwriting an already-saved ID.
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(CurrentGame.SplashScreen.ImageAssetId)) return;
                if (newId == CurrentGame.SplashScreen.ImageAssetId) return;
                CurrentGame.SplashScreen.ImageAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashImageAsset));
                OnPropertyChanged(nameof(SplashBackgroundPath));
                _ = SaveGameAsync();
            }
        }

        public MediaAsset? SelectedSplashVideoAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == CurrentGame?.SplashScreen?.VideoAssetId);
            set
            {
                if (CurrentGame == null) return;
                // Bug #3: Auto-initialize SplashScreen.
                if (CurrentGame.SplashScreen == null)
                {
                    CurrentGame.SplashScreen = new RagsCore.Models.SplashScreenSettings();
                    CurrentGame.SplashScreen.PropertyChanged += (s, a) => _ = SaveGameAsync();
                }
                var newId = value?.IdString ?? string.Empty;
                // Guard: prevent Avalonia ComboBox reset from blanking a saved ID.
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(CurrentGame.SplashScreen.VideoAssetId)) return;
                if (newId == CurrentGame.SplashScreen.VideoAssetId) return;
                CurrentGame.SplashScreen.VideoAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashVideoAsset));
                OnPropertyChanged(nameof(SplashBackgroundPath));
                _ = SaveGameAsync();
            }
        }

        public MediaAsset? SelectedSplashSoundAsset
        {
            get => CurrentGame?.MediaAssets.FirstOrDefault(a => a.IdString == CurrentGame?.SplashScreen?.SoundAssetId);
            set
            {
                if (CurrentGame == null) return;
                // Bug #3: Auto-initialize SplashScreen.
                if (CurrentGame.SplashScreen == null)
                {
                    CurrentGame.SplashScreen = new RagsCore.Models.SplashScreenSettings();
                    CurrentGame.SplashScreen.PropertyChanged += (s, a) => _ = SaveGameAsync();
                }
                var newId = value?.IdString ?? string.Empty;
                // Guard: prevent Avalonia ComboBox reset from blanking a saved ID.
                if (string.IsNullOrEmpty(newId) && !string.IsNullOrEmpty(CurrentGame.SplashScreen.SoundAssetId)) return;
                if (newId == CurrentGame.SplashScreen.SoundAssetId) return;
                CurrentGame.SplashScreen.SoundAssetId = newId;
                OnPropertyChanged(nameof(SelectedSplashSoundAsset));
                _ = SaveGameAsync();
            }
        }

        public IEnumerable<MediaAsset> VideoMediaAssets => CurrentGame?.MediaAssets.Where(a => a.Kind == MediaKind.Video) ?? Enumerable.Empty<MediaAsset>();
        public IEnumerable<MediaAsset> ImageMediaAssets => CurrentGame?.MediaAssets.Where(a => a.Kind == MediaKind.Image) ?? Enumerable.Empty<MediaAsset>();
        public IEnumerable<MediaAsset> AudioMediaAssets => CurrentGame?.MediaAssets.Where(a => a.Kind == MediaKind.Audio) ?? Enumerable.Empty<MediaAsset>();

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

        private bool _isPlayingSplashPreview = false;
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

        public event Action<string, string, string>? ComposeApplied;

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

        // Commands
        public ICommand NavigateCommand { get; }
        public ICommand NewGameCommand { get; }
        public ICommand ShowLoadGameCommand { get; }
        public ICommand LoadSelectedGameCommand { get; }
        public ICommand SaveGameCommand { get; }
        public ICommand CloseWelcomeCommand { get; }
        public ICommand PublishCommand { get; }

        public static Func<Task<string>> PickFolderAsync { get; set; }
        public ICommand BrowsePublishDestinationCommand { get; }

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

        public ICommand TriggerAddAttributeCommand { get; }
        public ICommand CloseAttributeDialogCommand { get; }
        public ICommand SaveAttributeCommand { get; }
        public ICommand RemoveAttributeCommand { get; }

        public ObservableCollection<RagsCore.Models.Action> GlobalActions { get; } = new();
        public ObservableCollection<RagsCore.Models.Action> MatchActionTemplates { get; } = new();

        private bool _showActionSelectorOverlay;
        public bool ShowActionSelectorOverlay { get => _showActionSelectorOverlay; set => SetProperty(ref _showActionSelectorOverlay, value); }

        private object? _actionTargetEntity;

        public ICommand AddGlobalActionCommand { get; }
        public ICommand DeleteGlobalActionCommand { get; }
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
            _storage = new AvaloniaGameStorage();

            Rooms = new RoomsViewModel(_storage);
            Characters = new CharactersViewModel(_storage);
            Objects = new GameObjectsViewModel(_storage);
            Variables = new GameVariablesViewModel(_storage);
            Timers = new GameTimersViewModel(_storage);
            Functions = new GlobalFunctionsViewModel(_storage);
            Media = new MediaLibraryViewModel();
            Preferences = new PreferencesViewModel();

            App.GameChanged += (g) => 
            { 
                CurrentGame = g; 
                OnPropertyChanged(nameof(Player));
                OnPropertyChanged(nameof(SplashScreen));
                Media.Refresh();
            };

            NavigateCommand = new Command<string>(view => ActiveView = view ?? "Dashboard");
            ToggleAssetsSidebarCommand = new Command(() => IsAssetsSidebarOpen = !IsAssetsSidebarOpen);

            PreviewTransitionCommand = new Command(async () =>
            {
                if (_isPlayingSplashPreview || CurrentGame?.SplashScreen == null) return;
                _isPlayingSplashPreview = true;

                var splash = CurrentGame.SplashScreen;
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
                    _isPlayingSplashPreview = false;
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
                        }
                        else if (style.Equals("Cinematic", StringComparison.OrdinalIgnoreCase))
                        {
                            // Slide text in horizontally slightly to simulate slow pan
                            SplashPreviewTextLeftOffset = -30.0 * (1.0 - progress);
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

                        SplashPreviewImageOpacity = imgOpacity;
                        SplashPreviewTextOpacity = txtOpacity;
                        await Task.Delay(stepDelayFadeOut);
                    }

                    // Done
                    SplashPreviewImageOpacity = 0.0;
                    SplashPreviewTextOpacity = 0.0;
                    SplashPreviewTextLeftOffset = 0.0;
                    SplashPreviewTextTopOffset = 0.0;
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
                    _isPlayingSplashPreview = false;
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

                if (_actionTargetEntity is Room room) room.Actions.Add(act);
                else if (_actionTargetEntity is Character character) character.Actions.Add(act);
                else if (_actionTargetEntity is GameObject obj) obj.Actions.Add(act);
                else if (_actionTargetEntity is Player player) player.Actions.Add(act);

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
                        if (obj.IsWearable) isMatch = act.ApplyToWearableObjects;
                        else if (obj.IsCollectible) isMatch = act.ApplyToGrabableObjects;
                        else isMatch = act.ApplyToStaticObjects;
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
                ComposeTitle = $"Compose Description - {name}";

                var descProp = parameter.GetType().GetProperty("Description");
                if (descProp != null)
                {
                    ComposeText = descProp.GetValue(parameter)?.ToString() ?? "";
                }
                
                ShowComposeOverlay = true;
            });

            CloseComposeCommand = new Command(async () =>
            {
                ShowComposeOverlay = false;
                if (!string.IsNullOrEmpty(ComposeNodeId))
                {
                    ComposeApplied?.Invoke(ComposeNodeId, ComposeFieldName ?? "", ComposeText);
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
                NewAttributeName = string.Empty;
                NewAttributeValue = string.Empty;
                ShowAttributeDialogOverlay = true;
            });

            CloseAttributeDialogCommand = new Command(() =>
            {
                Console.WriteLine("[DEBUG] CloseAttributeDialogCommand triggered");
                ShowAttributeDialogOverlay = false;
                AttributeTarget = null;
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
                    CustomAttribute.SetAttribute(NewAttributeName.Trim(), NewAttributeValue.Trim(), attrs);
                    Console.WriteLine($"[DEBUG] SaveAttributeCommand: Successfully set attribute '{NewAttributeName}' = '{NewAttributeValue}'");
                }
                else
                {
                    Console.WriteLine("[DEBUG] SaveAttributeCommand: attrs collection was null");
                }

                ShowAttributeDialogOverlay = false;
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
                    bool clearDir = false;
                    if (MediaLibraryViewModel.ConfirmDialogAsync != null)
                    {
                        clearDir = await MediaLibraryViewModel.ConfirmDialogAsync(
                            "Overwrite Warning",
                            "The destination folder is not empty. To prevent mixing game assets or loading outdated configurations, it is highly recommended to clear this folder.\n\nDo you want to delete its contents before publishing?");
                    }

                    if (clearDir)
                    {
                        PublishLogs += "Clearing destination folder...\n";
                        try
                        {
                            foreach (var file in Directory.GetFiles(destination)) File.Delete(file);
                            foreach (var dir in Directory.GetDirectories(destination)) Directory.Delete(dir, true);
                            PublishLogs += "✔️ Folder cleared successfully.\n";
                        }
                        catch (Exception ex)
                        {
                            PublishLogs += $"❌ Error: Could not clear folder: {ex.Message}\n";
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

        private readonly System.Threading.SemaphoreSlim _saveSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        public async Task SaveGameAsync()
        {
            if (CurrentGame == null) return;
            await _saveSemaphore.WaitAsync();
            try
            {
                await _storage.SaveAsync(CurrentGame, CurrentGame.Title, false);
                if (!string.IsNullOrWhiteSpace(CurrentGame.FileName))
                {
                    SaveRecentProject(CurrentGame.FileName);
                }
            }
            catch (IOException)
            {
                // Yield and retry once in case of temporary file locking
                await Task.Delay(200);
                try
                {
                    await _storage.SaveAsync(CurrentGame, CurrentGame.Title, false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Save retry failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Save failed: {ex.Message}");
            }
            finally
            {
                _saveSemaphore.Release();
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
