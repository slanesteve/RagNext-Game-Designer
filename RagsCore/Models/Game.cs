using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RagsCore.Actions;

namespace RagsCore.Models
{
    /// <summary>
    /// Root game model containing metadata and collections of game entities.
    /// </summary>
    public class Game : BaseModel
    {

        public static List<CommandDefinition>? AvailableCommands = null;
        public static List<ConditionDefinition>? AvailableConditions = null;
        // Combined, read-only view for UI pickers
        // References into Game.MediaAssets (keeps model portable/serializable)
        


        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonIgnore]
        public string? FileName { get; set; }

        private string _title = string.Empty;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _author = string.Empty;
        public string Author { get => _author; set => SetProperty(ref _author, value); }

        private string _version = "1.0.0";
        public string Version { get => _version; set => SetProperty(ref _version, value); }

        private string _iconPath = string.Empty;
        public string IconPath { get => _iconPath; set => SetProperty(ref _iconPath, value); }

        private string _steamUrl = "https://store.steampowered.com/app/4944750/RagNext_Studio/";
        public string SteamUrl { get => _steamUrl; set => SetProperty(ref _steamUrl, value); }

        private string _discordUrl = string.Empty;
        public string DiscordUrl { get => _discordUrl; set => SetProperty(ref _discordUrl, value); }

        private string _websiteUrl = string.Empty;
        public string WebsiteUrl { get => _websiteUrl; set => SetProperty(ref _websiteUrl, value); }

        private bool _showEngineCredits = true;
        public bool ShowEngineCredits { get => _showEngineCredits; set => SetProperty(ref _showEngineCredits, value); }

        public ObservableCollection<PromotionalLink> PromotionalLinks { get; set; } = new();

        public Player Player { get; set; } = new();

        // Make collections settable so System.Text.Json can assign them during deserialization.
        // Stored alongside game save; referenced by Id from your models
        public ObservableCollection<MediaAsset> MediaAssets { get; set; } = new();

        public ObservableCollection<Room> Rooms { get; set; } = new();
        public ObservableCollection<GameObject> Objects { get; set; } = new();
        public ObservableCollection<Character> Characters { get; set; } = new();
        public ObservableCollection<GameVariable> Variables { get; set; } = new();
        public ObservableCollection<GlobalFunction> Functions { get; set; } = new();
        public ObservableCollection<GameTimer> Timers { get; set; } = new();
        public ObservableCollection<StatusBarElement> StatusBarElements { get; set; } = new();

        private ObservableCollection<string> _wearSlots = new() { "Head", "Torso", "Legs", "Feet", "Hands", "Neck", "Back" };
        public ObservableCollection<string> WearSlots { get => _wearSlots; set => SetProperty(ref _wearSlots, value); }

        public Guid ActivePlayerCharacterId { get; set; }

        public ObservableCollection<SplashScreenSettings> SplashScreens { get; set; } = new();
        public string DefaultSplashScreenName { get; set; } = "Default";

        private SplashScreenSettings _splashScreen = new() { Name = "Default" };
        public SplashScreenSettings SplashScreen
        {
            get => _splashScreen;
            set
            {
                _splashScreen = value;
                if (SplashScreens != null)
                {
                    var existing = System.Linq.Enumerable.FirstOrDefault(SplashScreens, s => s.Name == (value.Name ?? "Default"));
                    if (existing == null)
                    {
                        SplashScreens.Add(value);
                    }
                }
            }
        }

        private ThemeSettings _theme = new();
        public ThemeSettings Theme { get => _theme; set => SetProperty(ref _theme, value); }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Game() { }

        public static async Task EnsureAvailableCommandsAsync(
            Func<Task<Stream>> openStreamAsync,
            CancellationToken ct = default)
        {
            try
            {
                if (AvailableCommands is not null) return;

                await using var stream = await openStreamAsync().ConfigureAwait(false);
                var catalog = await JsonSerializer.DeserializeAsync(stream, RagsJsonContext.CustomDefault.CommandCatalog, ct)
                               .ConfigureAwait(false);
                AvailableCommands = catalog?.Commands ?? new List<CommandDefinition>();
            }
            catch (Exception ex)
            {
                AvailableCommands = new List<CommandDefinition>();
                throw new InvalidOperationException("Failed to load available commands.", ex);
            }
        }

        public static async Task EnsureAvailableConditionsAsync(
            Func<Task<Stream>> openStreamAsync,
            CancellationToken ct = default)
        {
            try
            {
                if (AvailableConditions is not null) return;

                await using var stream = await openStreamAsync().ConfigureAwait(false);
                var catalog = await JsonSerializer.DeserializeAsync(stream, RagsJsonContext.CustomDefault.ConditionCatalog, ct)
                               .ConfigureAwait(false);
                AvailableConditions = catalog?.Conditions ?? new List<ConditionDefinition>();
            }
            catch (Exception ex)
            {
                AvailableConditions = new List<ConditionDefinition>();
                throw new InvalidOperationException("Failed to load available conditions.", ex);
            }
        }

        public static Game CreateNew(string title, string author, string version = "1.0.0")
        {
            return new Game
            {
                Title = title,
                Author = author,
                Version = version
            };
        }
    }
}