using System;
using System.Threading;
using System.Threading.Tasks;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using UnityEngine;

namespace RagNextPlayer.Managers
{
    public enum GameState { Initializing, MainMenu, Playing, Transitioning, GameOver }

    /// <summary>
    /// Singleton MonoBehaviour. Owns the game state machine and coordinates
    /// room transitions with a SemaphoreSlim to prevent race conditions when
    /// the player rapidly clicks exits before a transition animation completes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("File name to load from StreamingAssets (e.g. 'game.json')")]
        [SerializeField] private string _gameFileName = "game.json";

        // ── State ────────────────────────────────────────────────────────────
        public GameData?  ActiveGame    { get; private set; }
        public RoomData?  CurrentRoom   { get; private set; }
        public GameState  CurrentState  { get; private set; } = GameState.Initializing;

        // Prevents concurrent room transitions (e.g. spamming compass buttons)
        private readonly SemaphoreSlim _transitionLock = new SemaphoreSlim(1, 1);

        // ── Events ───────────────────────────────────────────────────────────
        public event Action<RoomData>? OnRoomEntered;
        public event Action<GameData>? OnGameLoaded;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void Start()
        {
            await LoadGameAsync();
        }

        public void RestartGame()
        {
            _ = LoadGameAsync();
        }

        // ── Loading ───────────────────────────────────────────────────────────
        private async Task LoadGameAsync()
        {
            CurrentState = GameState.Initializing;

            ActiveGame = await GameLoader.LoadFromStreamingAssetsAsync(_gameFileName);

            if (ActiveGame is null)
            {
                Debug.LogError("[GameManager] Failed to load game data. Entering GameOver state.");
                CurrentState = GameState.GameOver;
                return;
            }

            // Seed player.currentRoomId if not present
            EnsurePlayerRoomVariable();

            // Dynamically populate room ObjectIds from description hotlinks if empty
            PopulateRoomObjectIdsFromDescription(ActiveGame);

            CurrentState = GameState.Playing;
            OnGameLoaded?.Invoke(ActiveGame);

            // Navigate to the starting room
            var startId = ActiveGame.Player.StartingRoomId
                          ?? (ActiveGame.Rooms.Count > 0 ? ActiveGame.Rooms[0].Id : null);

            if (startId is not null)
                await TransitionToRoomAsync(startId);
        }


        // ── Room Transitions ─────────────────────────────────────────────────

        /// <summary>
        /// Thread-safe room transition entry point. Safe to call from button callbacks.
        /// </summary>
        public void MovePlayerToRoom(string roomId)
        {
            _ = TransitionToRoomAsync(roomId);
        }

        private async Task TransitionToRoomAsync(string roomId)
        {
            await _transitionLock.WaitAsync();
            try
            {
                var room = ActiveGame?.Rooms.Find(r => r.Id == roomId);
                if (room is null)
                {
                    Debug.LogWarning($"[GameManager] Target room '{roomId}' not found.");
                    return;
                }

                CurrentState = GameState.Transitioning;

                // 1. Fade out
                if (UIManager.Instance is not null)
                    await UIManager.Instance.FadeNarrativeAsync(0f, 300);

                // 2. Update state
                CurrentRoom = room;
                var roomVar = ActiveGame!.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
                if (roomVar is null)
                {
                    ActiveGame.Variables.Add(new GameVariableData { Name = "player.currentRoomId", Value = roomId });
                }
                else
                {
                    roomVar.Value = roomId;
                }

                // 3. Notify UI
                OnRoomEntered?.Invoke(room);

                // 4. Fade in
                if (UIManager.Instance is not null)
                    await UIManager.Instance.FadeNarrativeAsync(1f, 300);

                CurrentState = GameState.Playing;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Transition error: {ex.Message}");
                CurrentState = GameState.Playing;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        // ── Variable Helpers ─────────────────────────────────────────────────
        private void EnsurePlayerRoomVariable()
        {
            if (ActiveGame is null) return;
            if (ActiveGame.Variables.Exists(v => v.Name == "player.currentRoomId")) return;

            var startId = ActiveGame.Player.StartingRoomId
                          ?? (ActiveGame.Rooms.Count > 0 ? ActiveGame.Rooms[0].Id : string.Empty);

            ActiveGame.Variables.Add(new GameVariableData
            {
                Name  = "player.currentRoomId",
                Value = startId
            });
        }

        private void PopulateRoomObjectIdsFromDescription(GameData game)
        {
            foreach (var room in game.Rooms)
            {
                if (string.IsNullOrWhiteSpace(room.Description)) continue;

                var matches = System.Text.RegularExpressions.Regex.Matches(room.Description, @"\[([^\]]+)\]");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var entityName = match.Groups[1].Value.Trim();

                    // Search in objects
                    var obj = game.Objects.Find(o => string.Equals(o.Name, entityName, StringComparison.OrdinalIgnoreCase));
                    if (obj is not null)
                    {
                        if (!room.ObjectIds.Contains(obj.Id))
                        {
                            room.ObjectIds.Add(obj.Id);
                        }
                        continue;
                    }

                    // Search in characters
                    var ch = game.Characters.Find(c => string.Equals(c.Name, entityName, StringComparison.OrdinalIgnoreCase));
                    if (ch is not null)
                    {
                        if (!room.ObjectIds.Contains(ch.Id))
                        {
                            room.ObjectIds.Add(ch.Id);
                        }
                    }
                }
            }
        }

        // ── Save / Load System ────────────────────────────────────────────────
        public string GetSaveFilePath(int slot)
        {
            return System.IO.Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");
        }

        public bool HasSaveFile(int slot)
        {
            return System.IO.File.Exists(GetSaveFilePath(slot));
        }

        private Newtonsoft.Json.JsonSerializerSettings GetSaveLoadSettings()
        {
            return new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                Converters = { new ActionStepConverter(), new ActionStepListConverter() }
            };
        }

        public void SaveGame(int slot)
        {
            if (ActiveGame is null) return;
            try
            {
                // Ensure room variable matches CurrentRoom before saving
                if (CurrentRoom is not null)
                {
                    var roomVar = ActiveGame.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
                    if (roomVar is null)
                    {
                        ActiveGame.Variables.Add(new GameVariableData { Name = "player.currentRoomId", Value = CurrentRoom.Id });
                    }
                    else
                    {
                        roomVar.Value = CurrentRoom.Id;
                    }
                }

                var path = GetSaveFilePath(slot);
                var settings = GetSaveLoadSettings();
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(ActiveGame, settings);
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[GameManager] Game saved to slot {slot} at: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Save error: {ex.Message}");
            }
        }

        public async Task LoadGameAsync(int slot)
        {
            if (!HasSaveFile(slot)) return;
            try
            {
                var path = GetSaveFilePath(slot);
                var json = System.IO.File.ReadAllText(path);
                var settings = GetSaveLoadSettings();
                var loadedGame = Newtonsoft.Json.JsonConvert.DeserializeObject<GameData>(json, settings);

                if (loadedGame is not null)
                {
                    ActiveGame = loadedGame;
                    CurrentState = GameState.Playing;

                    // Trigger loaded callbacks
                    OnGameLoaded?.Invoke(ActiveGame);

                    // Find and enter the current room
                    var roomIdVar = ActiveGame.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase))?.Value;
                    if (roomIdVar is null)
                    {
                        // Fallback for old save files created before the fix
                        roomIdVar = ActiveGame.Player.StartingRoomId ?? (ActiveGame.Rooms.Count > 0 ? ActiveGame.Rooms[0].Id : null);
                    }
                    if (roomIdVar is not null)
                    {
                        await TransitionToRoomAsync(roomIdVar);
                    }
                    Debug.Log($"[GameManager] Game loaded from slot {slot}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Load error: {ex.Message}");
            }
        }

        /// <summary>Build an execution context for the current room.</summary>
        public GameExecutionContext MakeContext(GameObjectData? focusObject = null)
            => new GameExecutionContext(ActiveGame!, CurrentRoom, focusObject);

    }

    // Tiny helper to avoid null-conditional chain verbosity
    internal static class ObjectExtensions
    {
        public static T? Let<T>(this T? obj, Action<T> action) where T : class
        {
            if (obj is not null) action(obj);
            return obj;
        }
    }
}
