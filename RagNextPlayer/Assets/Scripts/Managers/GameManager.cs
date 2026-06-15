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

        private void Update()
        {
            if (ActiveGame == null || CurrentState != GameState.Playing) return;

            // Handle Keyboard Navigation for Room Exits
            HandleKeyboardNavigation();

            // Tick active background timers
            if (ActiveGame.Timers != null)
            {
                foreach (var timer in ActiveGame.Timers)
                {
                    if (!timer.IsActive) continue;

                    timer.ElapsedSeconds += Time.deltaTime;
                    if (timer.ElapsedSeconds >= timer.IntervalSeconds)
                    {
                        timer.ElapsedSeconds = 0f;
                        if (!timer.IsRepeating) timer.IsActive = false;

                        // Execute the timer nodes!
                        var actionData = new ActionData { Nodes = timer.Nodes };
                        var ctx = new GameExecutionContext(ActiveGame!, CurrentRoom, null, timer);
                        var sink = InteractionController.Instance?.GetComponent<CommandEffectRouter>();
                        
                        ActionExecutor.Execute(actionData, ctx, sink);
                        UIManager.Instance?.RefreshEntityLists();
                    }
                }
            }
        }

        private void HandleKeyboardNavigation()
        {
            if (CurrentRoom == null || CurrentRoom.Exits == null) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            string directionToMove = null;

            if (keyboard[UnityEngine.InputSystem.Key.W].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.UpArrow].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.Numpad8].wasPressedThisFrame)
                directionToMove = "North";
            else if (keyboard[UnityEngine.InputSystem.Key.S].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.DownArrow].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.Numpad2].wasPressedThisFrame)
                directionToMove = "South";
            else if (keyboard[UnityEngine.InputSystem.Key.D].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.RightArrow].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.Numpad6].wasPressedThisFrame)
                directionToMove = "East";
            else if (keyboard[UnityEngine.InputSystem.Key.A].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.LeftArrow].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.Numpad4].wasPressedThisFrame)
                directionToMove = "West";
            else if (keyboard[UnityEngine.InputSystem.Key.Numpad7].wasPressedThisFrame)
                directionToMove = "NorthWest";
            else if (keyboard[UnityEngine.InputSystem.Key.Numpad9].wasPressedThisFrame)
                directionToMove = "NorthEast";
            else if (keyboard[UnityEngine.InputSystem.Key.Numpad1].wasPressedThisFrame)
                directionToMove = "SouthWest";
            else if (keyboard[UnityEngine.InputSystem.Key.Numpad3].wasPressedThisFrame)
                directionToMove = "SouthEast";
            else if (keyboard[UnityEngine.InputSystem.Key.PageUp].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.E].wasPressedThisFrame)
                directionToMove = "Up";
            else if (keyboard[UnityEngine.InputSystem.Key.PageDown].wasPressedThisFrame || keyboard[UnityEngine.InputSystem.Key.Q].wasPressedThisFrame)
                directionToMove = "Down";
            else if (keyboard[UnityEngine.InputSystem.Key.I].wasPressedThisFrame)
                directionToMove = "In";
            else if (keyboard[UnityEngine.InputSystem.Key.O].wasPressedThisFrame)
                directionToMove = "Out";

            if (!string.IsNullOrEmpty(directionToMove))
            {
                if (CurrentRoom.Exits.TryGetValue(directionToMove, out var targetRoomId))
                {
                    if (!CurrentRoom.LockedExits.TryGetValue(directionToMove, out var isLocked) || !isLocked)
                    {
                        MovePlayerToRoom(targetRoomId);
                    }
                }
            }
        }

        private async void Start()
        {
            await LoadGameAsync();
        }

        public void RestartGame()
        {
            AudioManager.Instance?.StopAllSounds();
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

            // Wait for splash screen sequence to complete if one is playing
            while (UIManager.Instance == null || !UIManager.Instance.IsSplashFinished)
            {
                await Task.Yield();
            }

            CurrentState = GameState.Playing;
            OnGameLoaded?.Invoke(ActiveGame);
            
            // Execute all OnGameStart actions globally
            FireStartupTriggers();

            // Navigate to the starting room
            var startId = ActiveGame.Player.StartingRoomId
                          ?? (ActiveGame.Rooms.Count > 0 ? ActiveGame.Rooms[0].Id : null);

            if (startId is not null)
                await TransitionToRoomAsync(startId);
        }

        private void FireStartupTriggers()
        {
            if (ActiveGame is null) return;
            var ctx = MakeContext();
            var sink = InteractionController.Instance?.GetComponent<CommandEffectRouter>();

            // 1. Player actions
            if (ActiveGame.Player.Actions != null)
            {
                var playerStub = new GameObjectData { Id = ActiveGame.Player.Id, Name = ActiveGame.Player.Name, Description = ActiveGame.Player.Description, PortraitImagePath = ActiveGame.Player.PortraitImagePath };
                var playerCtx = new GameExecutionContext(ActiveGame, null, playerStub, ActiveGame.Player);
                foreach (var action in ActiveGame.Player.Actions)
                {
                    if (string.Equals(action.Trigger, "OnGameStart", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, playerCtx, sink);
                    }
                }
            }

            // 2. Room actions
            foreach (var room in ActiveGame.Rooms)
            {
                if (room.Actions != null)
                {
                    var roomCtx = new GameExecutionContext(ActiveGame, room, null, room);
                    foreach (var action in room.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnGameStart", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, roomCtx, sink);
                        }
                    }
                }
            }

            // 3. GameObject actions
            foreach (var obj in ActiveGame.Objects)
            {
                if (obj.Actions != null)
                {
                    var objCtx = new GameExecutionContext(ActiveGame, null, obj, obj);
                    foreach (var action in obj.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnGameStart", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, objCtx, sink);
                        }
                    }
                }
            }

            // 4. Character actions
            foreach (var ch in ActiveGame.Characters)
            {
                if (ch.Actions != null)
                {
                    var chCtx = new GameExecutionContext(ActiveGame, null, ch, ch);
                    foreach (var action in ch.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnGameStart", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, chCtx, sink);
                        }
                    }
                }
            }
        }


        // ── Room Transitions ─────────────────────────────────────────────────

        public static bool MatchesDirection(string filter, string? direction)
        {
            if (string.IsNullOrWhiteSpace(filter) || string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(direction))
                return false;
            
            if (string.Equals(filter, direction, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Abbreviation match: e.g. "N" matches "North"
            if (string.Equals(filter, "N", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "North", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "S", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "South", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "E", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "East", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "W", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "West", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "NW", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "NorthWest", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "NE", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "NorthEast", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "SW", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "SouthWest", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(filter, "SE", StringComparison.OrdinalIgnoreCase) && string.Equals(direction, "SouthEast", StringComparison.OrdinalIgnoreCase)) return true;
            
            return false;
        }

        public void FireTurnTickTriggers()
        {
            if (ActiveGame is null) return;
            var sink = InteractionController.Instance?.GetComponent<CommandEffectRouter>();

            // 1. Player OnTurnTick
            if (ActiveGame.Player?.Actions != null)
            {
                var playerStub = new GameObjectData { Id = ActiveGame.Player.Id, Name = ActiveGame.Player.Name, Description = ActiveGame.Player.Description };
                var playerCtx = new GameExecutionContext(ActiveGame, CurrentRoom, playerStub, ActiveGame.Player);
                foreach (var action in ActiveGame.Player.Actions)
                {
                    if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, playerCtx, sink);
                    }
                }
            }

            // 2. Current Room OnTurnTick
            if (CurrentRoom?.Actions != null)
            {
                var roomCtx = new GameExecutionContext(ActiveGame, CurrentRoom, null, CurrentRoom);
                foreach (var action in CurrentRoom.Actions)
                {
                    if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, roomCtx, sink);
                    }
                }
            }

            // 3. Characters in the current Room
            if (CurrentRoom != null)
            {
                foreach (var ch in ActiveGame.Characters)
                {
                    if (CurrentRoom.ObjectIds.Contains(ch.Id) && ch.Actions != null)
                    {
                        var chCtx = new GameExecutionContext(ActiveGame, CurrentRoom, ch, ch);
                        foreach (var action in ch.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                            {
                                ActionExecutor.Execute(action, chCtx, sink);
                            }
                        }
                    }
                }

                // 4. Objects in the current Room
                foreach (var obj in ActiveGame.Objects)
                {
                    if (CurrentRoom.ObjectIds.Contains(obj.Id) && obj.Actions != null)
                    {
                        var objCtx = new GameExecutionContext(ActiveGame, CurrentRoom, obj, obj);
                        foreach (var action in obj.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                            {
                                ActionExecutor.Execute(action, objCtx, sink);
                            }
                        }
                    }
                }
            }
        }

        public void FireCharacterKilledTriggers(GameObjectData character)
        {
            if (ActiveGame is null) return;
            var sink = InteractionController.Instance?.GetComponent<CommandEffectRouter>();

            // 1. Character's own OnCharacterKilled
            if (character.Actions != null)
            {
                var charCtx = new GameExecutionContext(ActiveGame, CurrentRoom, character, character);
                foreach (var action in character.Actions)
                {
                    if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, charCtx, sink);
                    }
                }
            }

            // Get character's current room
            var charRoomId = ActiveGame.Variables.Find(v => string.Equals(v.Name, $"char.{character.Id}.currentRoomId", StringComparison.OrdinalIgnoreCase))?.Value;
            var charRoom = ActiveGame.Rooms.Find(r => r.Id == charRoomId) ?? CurrentRoom;

            // 2. Room's OnCharacterKilled
            if (charRoom?.Actions != null)
            {
                var roomCtx = new GameExecutionContext(ActiveGame, charRoom, character, charRoom);
                foreach (var action in charRoom.Actions)
                {
                    if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, roomCtx, sink);
                    }
                }
            }

            // 3. GameObjects in the same Room's OnCharacterKilled
            if (charRoom != null)
            {
                foreach (var obj in ActiveGame.Objects)
                {
                    if (charRoom.ObjectIds.Contains(obj.Id) && obj.Actions != null)
                    {
                        var objCtx = new GameExecutionContext(ActiveGame, charRoom, character, obj);
                        foreach (var action in obj.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                            {
                                ActionExecutor.Execute(action, objCtx, sink);
                            }
                        }
                    }
                }
            }

            // 4. Player's OnCharacterKilled (Global)
            if (ActiveGame.Player?.Actions != null)
            {
                var playerStub = new GameObjectData { Id = ActiveGame.Player.Id, Name = ActiveGame.Player.Name, Description = ActiveGame.Player.Description };
                var playerCtx = new GameExecutionContext(ActiveGame, charRoom, character, ActiveGame.Player);
                foreach (var action in ActiveGame.Player.Actions)
                {
                    if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                    {
                        ActionExecutor.Execute(action, playerCtx, sink);
                    }
                }
            }
        }

        /// <summary>
        /// Thread-safe room transition entry point. Safe to call from button callbacks.
        /// </summary>
        public void MovePlayerToRoom(string roomId) => MovePlayerToRoom(roomId, null);

        public void MovePlayerToRoom(string roomId, string? direction)
        {
            InteractionController.Instance?.HideMenu();
            if (CurrentState == GameState.Transitioning)
            {
                var roomVar = ActiveGame?.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
                if (roomVar != null)
                {
                    roomVar.Value = roomId;
                }
                return;
            }
            _ = TransitionToRoomAsync(roomId, direction);
        }

        private async Task TransitionToRoomAsync(string roomId) => await TransitionToRoomAsync(roomId, null);

        private async Task TransitionToRoomAsync(string roomId, string? direction)
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

                // Resolve direction if not explicitly provided
                if (direction == null && CurrentRoom != null)
                {
                    foreach (var kvp in CurrentRoom.Exits)
                    {
                        if (string.Equals(kvp.Value, roomId, StringComparison.OrdinalIgnoreCase))
                        {
                            direction = kvp.Key;
                            break;
                        }
                    }
                }

                // Sync currentRoomId variable to the target room before firing exit actions
                var roomVar = ActiveGame!.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
                if (roomVar is null)
                {
                    ActiveGame.Variables.Add(new GameVariableData { Name = "player.currentRoomId", Value = roomId });
                }
                else
                {
                    roomVar.Value = roomId;
                }

                // Run current room's OnPlayerExit actions before moving
                if (CurrentRoom is not null)
                {
                    var exitCtx = new GameExecutionContext(ActiveGame!, CurrentRoom, null, CurrentRoom);
                    foreach (var action in CurrentRoom.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnPlayerExit", StringComparison.OrdinalIgnoreCase))
                        {
                            if (MatchesDirection(action.DirectionFilter, direction))
                            {
                                ActionExecutor.Execute(action, exitCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                            }
                        }
                    }

                    // Bubble OnPlayerExit globally to Player-level hooks
                    if (ActiveGame?.Player?.Actions != null)
                    {
                        var playerStub = new GameObjectData { Id = ActiveGame.Player.Id, Name = ActiveGame.Player.Name, Description = ActiveGame.Player.Description };
                        var playerCtx = new GameExecutionContext(ActiveGame!, CurrentRoom, playerStub, ActiveGame.Player);
                        foreach (var action in ActiveGame.Player.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnPlayerExit", StringComparison.OrdinalIgnoreCase))
                            {
                                if (MatchesDirection(action.DirectionFilter, direction))
                                {
                                    ActionExecutor.Execute(action, playerCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                                }
                            }
                        }
                    }

                    // Execute OnPlayerExit actions for items in the current room
                    var itemsInRoomExit = ActiveGame.Objects.FindAll(o => CurrentRoom.ObjectIds.Contains(o.Id));
                    foreach (var item in itemsInRoomExit)
                    {
                        if (item.Actions != null)
                        {
                            var itemCtx = new GameExecutionContext(ActiveGame!, CurrentRoom, item, item);
                            foreach (var action in item.Actions)
                            {
                                if (string.Equals(action.Trigger, "OnPlayerExit", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (MatchesDirection(action.DirectionFilter, direction))
                                    {
                                        ActionExecutor.Execute(action, itemCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                                    }
                                }
                            }
                        }
                    }

                    // Check if currentRoomId was redirected/overridden during OnPlayerExit
                    var finalRoomId = ActiveGame.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase))?.Value;
                    if (!string.IsNullOrEmpty(finalRoomId) && !string.Equals(finalRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                    {
                        roomId = finalRoomId;
                        room = ActiveGame.Rooms.Find(r => r.Id == roomId);
                        if (room is null)
                        {
                            Debug.LogWarning($"[GameManager] Redirect target room '{roomId}' not found.");
                            CurrentState = GameState.Playing;
                            return;
                        }
                    }

                    // Robust Interception Check: if redirected back to the starting/current room, abort early
                    if (string.Equals(roomId, CurrentRoom.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        var rVar = ActiveGame.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
                        if (rVar != null) rVar.Value = CurrentRoom.Id;
                        
                        CurrentState = GameState.Playing;
                        return;
                    }
                }

                // 1. Fade out
                if (UIManager.Instance is not null)
                    await UIManager.Instance.FadeNarrativeAsync(0f, 300);

                // 2. Update state
                CurrentRoom = room;
                roomVar = ActiveGame!.Variables.Find(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
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

                // Run room's OnPlayerEnter actions
                var enterCtx = new GameExecutionContext(ActiveGame!, room, null, room);
                foreach (var action in room.Actions)
                {
                    if (string.Equals(action.Trigger, "OnPlayerEnter", StringComparison.OrdinalIgnoreCase))
                    {
                        if (MatchesDirection(action.DirectionFilter, direction))
                        {
                            ActionExecutor.Execute(action, enterCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                        }
                    }
                }

                // Bubble OnPlayerEnter globally to Player-level hooks
                if (ActiveGame?.Player?.Actions != null)
                {
                    var playerStub = new GameObjectData { Id = ActiveGame.Player.Id, Name = ActiveGame.Player.Name, Description = ActiveGame.Player.Description };
                    var playerCtx = new GameExecutionContext(ActiveGame!, room, playerStub, ActiveGame.Player);
                    foreach (var action in ActiveGame.Player.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnPlayerEnter", StringComparison.OrdinalIgnoreCase))
                        {
                            if (MatchesDirection(action.DirectionFilter, direction))
                            {
                                ActionExecutor.Execute(action, playerCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                            }
                        }
                    }
                }

                // Execute OnPlayerEnter actions for items in the entered room
                var itemsInRoomEnter = ActiveGame.Objects.FindAll(o => room.ObjectIds.Contains(o.Id));
                foreach (var item in itemsInRoomEnter)
                {
                    if (item.Actions != null)
                    {
                        var itemCtx = new GameExecutionContext(ActiveGame!, room, item, item);
                        foreach (var action in item.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnPlayerEnter", StringComparison.OrdinalIgnoreCase))
                            {
                                if (MatchesDirection(action.DirectionFilter, direction))
                                {
                                    ActionExecutor.Execute(action, itemCtx, InteractionController.Instance?.GetComponent<CommandEffectRouter>());
                                }
                            }
                        }
                    }
                }

                // Run all unified OnTurnTick actions across player, rooms, and active entities
                FireTurnTickTriggers();

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
            string folder = Application.persistentDataPath;
            if (ActiveGame != null)
            {
                string gameFolder = string.IsNullOrEmpty(ActiveGame.Title) ? "DefaultGame" : ActiveGame.Title;
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    gameFolder = gameFolder.Replace(c, '_');
                }
                folder = System.IO.Path.Combine(folder, "Saves", gameFolder);
            }
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            return System.IO.Path.Combine(folder, $"save_slot_{slot}.json");
        }

        public bool HasSaveFile(int slot)
        {
            try
            {
                return System.IO.File.Exists(GetSaveFilePath(slot));
            }
            catch
            {
                return false;
            }
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
            AudioManager.Instance?.StopAllSounds();
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
