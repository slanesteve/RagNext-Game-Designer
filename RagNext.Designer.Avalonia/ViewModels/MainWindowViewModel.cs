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
                    OnPropertyChanged(nameof(Player));
                    OnPropertyChanged(nameof(SplashScreen));
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

        // Visual Graph Scripting Overlay State
        private bool _isVisualEditing = false;
        public bool IsVisualEditing
        {
            get => _isVisualEditing;
            set => SetProperty(ref _isVisualEditing, value);
        }

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

        public ICommand StartEditingActionCommand { get; }
        public ICommand StopEditingActionCommand { get; }
        public ICommand AddActionCommand { get; }
        public ICommand DeleteActionCommand { get; }
        public ICommand LoadLastWorkspaceCommand { get; }
        public ICommand LoadRecentProjectCommand { get; }
        public ICommand RemoveRecentProjectCommand { get; }

        public ICommand TriggerAddInventoryCommand { get; }
        public ICommand SelectInventoryItemCommand { get; }
        public ICommand RemoveInventoryItemCommand { get; }

        public ICommand TriggerAddAttributeCommand { get; }
        public ICommand SaveAttributeCommand { get; }
        public ICommand RemoveAttributeCommand { get; }

        // Items Creators Command delegation
        public ICommand AddRoomCommand => Rooms.AddRoomCommand;
        public ICommand AddCharacterCommand => Characters.AddCharacterCommand;
        public ICommand AddObjectCommand => Objects.AddObjectCommand;

        public MainWindowViewModel()
        {
            _storage = new AvaloniaGameStorage();

            Rooms = new RoomsViewModel(_storage);
            Characters = new CharactersViewModel(_storage);
            Objects = new GameObjectsViewModel(_storage);
            Variables = new GameVariablesViewModel();
            Timers = new GameTimersViewModel();
            Functions = new GlobalFunctionsViewModel();
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
                if (CurrentGame == null) return;
                await _storage.SaveAsync(CurrentGame, CurrentGame.Title, true);
            });

            CloseWelcomeCommand = new Command(() => ShowWelcomeOverlay = false);

            PublishCommand = new Command(async () => await PublishProjectAsync());

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
                if (CurrentGame != null)
                {
                    await _storage.SaveAsync(CurrentGame, CurrentGame.Title, false);
                }
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
            });

            RemoveRecentProjectCommand = new Command<string>(path =>
            {
                RemoveRecentProject(path);
            });

            // Load recents on startup
            LoadRecentProjects();

            AddActionCommand = new Command<object>(parameter =>
            {
                if (parameter is Room room)
                {
                    var act = new RagsCore.Models.Action { Name = "New Room Action", Trigger = ActionTrigger.UserClicked, InitallyActive = true };
                    room.Actions.Add(act);
                    SaveGameCommand.Execute(null);
                }
                else if (parameter is Character character)
                {
                    var act = new RagsCore.Models.Action { Name = "New Character Action", Trigger = ActionTrigger.OnInteract, InitallyActive = true };
                    character.Actions.Add(act);
                    SaveGameCommand.Execute(null);
                }
                else if (parameter is GameObject obj)
                {
                    var act = new RagsCore.Models.Action { Name = "New Object Action", Trigger = ActionTrigger.OnInteract, InitallyActive = true };
                    obj.Actions.Add(act);
                    SaveGameCommand.Execute(null);
                }
                else if (parameter is Player player)
                {
                    var act = new RagsCore.Models.Action { Name = "New Player Action", Trigger = ActionTrigger.OnGameStart, InitallyActive = true };
                    player.Actions.Add(act);
                    SaveGameCommand.Execute(null);
                }
            });

            DeleteActionCommand = new Command<RagsCore.Models.Action>(action =>
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
                SaveGameCommand.Execute(null);
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

            SelectInventoryItemCommand = new Command<GameObject>(item =>
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
                SaveGameCommand.Execute(null);
            });

            RemoveInventoryItemCommand = new Command<object>(parameter =>
            {
                // parameter is a tuple or we can inspect elements
                if (parameter is global::System.Collections.IList list && list.Count == 2)
                {
                    var owner = list[0];
                    var item = list[1] as GameObject;
                    if (item == null) return;

                    if (owner is Player p) p.Inventory.Remove(item);
                    else if (owner is Character c) c.Inventory.Remove(item);
                    SaveGameCommand.Execute(null);
                }
            });

            // Attributes Editor Dialog Setup
            TriggerAddAttributeCommand = new Command<object>(target =>
            {
                if (target == null) return;
                AttributeTarget = target;
                NewAttributeName = string.Empty;
                NewAttributeValue = string.Empty;
                ShowAttributeDialogOverlay = true;
            });

            SaveAttributeCommand = new Command(() =>
            {
                if (AttributeTarget == null || string.IsNullOrWhiteSpace(NewAttributeName)) return;

                global::System.Collections.ObjectModel.ObservableCollection<CustomAttribute>? attrs = null;
                if (AttributeTarget is Player p) attrs = p.Attributes;
                else if (AttributeTarget is Room r) attrs = r.Attributes;
                else if (AttributeTarget is Character c) attrs = c.Attributes;
                else if (AttributeTarget is GameObject o) attrs = o.Attributes;

                if (attrs != null)
                {
                    CustomAttribute.SetAttribute(NewAttributeName.Trim(), NewAttributeValue.Trim(), attrs);
                }

                ShowAttributeDialogOverlay = false;
                SaveGameCommand.Execute(null);
            });

            RemoveAttributeCommand = new Command<object>(parameter =>
            {
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
                    SaveGameCommand.Execute(null);
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

                PublishLogs += "✔️ Validation passed.\n";
                PublishLogs += "Exporting story-engine database assets...\n";

                // Direct packaging export
                var bytes = GameJsonExporter.Export(CurrentGame);
                PublishLogs += $"✔️ Database packaged. Size: {bytes.Length} bytes.\n";
                PublishLogs += "Triggering .NET Ahead-Of-Time Native AOT compiler checks...\n";
                
                // Invoke PublishEngine logic
                var outputDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RagNextPublish");
                PublishLogs += $"Exporting compiled assets to: {outputDir}\n";
                
                await PublishEngine.PublishAsync(CurrentGame, PackagingTarget.Windows, outputDir, false);

                PublishLogs += "✔️ Native AOT compatibility check passed.\n";
                PublishLogs += "🎉 Publication complete! Your secure Native AOT compiled storytelling package is ready on your Desktop.\n";
                PublishStatus = "Success! Package created.";
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

        public void LoadRecentProjects()
        {
            try
            {
                RecentProjects.Clear();
                var path = RecentProjectsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        foreach (var p in list)
                        {
                            if (!string.IsNullOrWhiteSpace(p) && !RecentProjects.Contains(p))
                                RecentProjects.Add(p);
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

                var json = JsonSerializer.Serialize(list);
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
                var json = JsonSerializer.Serialize(list);
                File.WriteAllText(RecentProjectsFilePath, json);
            }
            catch { }
        }
    }
}
