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

            CurrentState = GameState.Playing;
            OnGameLoaded?.Invoke(ActiveGame);

            // Navigate to the starting room
            var startId = ActiveGame.Player.StartingRoom?.Id
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
                ActiveGame!.Variables
                    .Find(v => v.Name == "player.currentRoomId")
                    .Let(v => v!.Value = roomId);

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

            var startId = ActiveGame.Player.StartingRoom?.Id
                          ?? (ActiveGame.Rooms.Count > 0 ? ActiveGame.Rooms[0].Id : string.Empty);

            ActiveGame.Variables.Add(new GameVariableData
            {
                Name  = "player.currentRoomId",
                Value = startId
            });
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
