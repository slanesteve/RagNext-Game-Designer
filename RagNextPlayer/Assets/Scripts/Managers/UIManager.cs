using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace RagNextPlayer.Managers
{
    /// <summary>
    /// Controls the UI Toolkit document. Mirrors the UI logic in MAUI's MainPage
    /// but uses UXML/USS queries and UI Toolkit's element API.
    ///
    /// Attach to the same GameObject as your UIDocument component.
    /// Set the UIDocument's Source Asset to GameLayout.uxml.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        // ── UXML Element References ───────────────────────────────────────────
        private VisualElement  _root;
        private Label          _roomTitleLabel;
        private Label          _gameInfoLabel;
        private ScrollView     _narrativeScroll;
        private readonly System.Collections.Generic.Dictionary<string, Button> _compassButtons = new(System.StringComparer.OrdinalIgnoreCase);
        private VisualElement  _objectsListContainer;
        private VisualElement  _charactersListContainer;
        private VisualElement  _inventoryListContainer;
        private Label          _playerNameLabel;
        private Label          _playerGenderLabel;
        private VisualElement  _sceneImageContainer;
        private VisualElement  _narrativePanel;

        // Settings Elements
        private Button         _settingsBtn;
        private VisualElement  _settingsMenu;
        private Button         _fullscreenToggleBtn;
        private Button         _typewriterToggleBtn;
        private Slider         _typewriterSpeedSlider;
        private SliderInt      _volumeSlider;
        private Button         _quitGameBtn;
        private Button         _closeSettingsBtn;
        private VisualElement  _playerPortrait;
        private Label          _scenePlaceholder;
        private VisualElement  _splashScreen;

        // Game Over modal references
        private VisualElement  _gameOverMenu;
        private Label          _gameOverMessage;
        private Button         _gameOverRestartBtn;
        private Button         _gameOverLoadBtn;
        private Button         _gameOverExitBtn;

        // Prompt Input modal references
        private VisualElement  _promptInputMenu;
        private Label          _promptInputMessage;
        private TextField      _promptTextField;
        private ScrollView     _promptSelectionScroll;
        private Button         _promptSubmitBtn;
        private string         _promptTargetVarName = string.Empty;

        // ── Typewriter effect ─────────────────────────────────────────────────
        [Header("Narrative Settings")]
        [SerializeField] private bool  _typewriterEnabled = false; // Default to false (MAUI style fade-in transition)
        [SerializeField] private float _typewriterSpeed   = 0.018f; // seconds per char

        private struct TypewriterSession
        {
            public Label PlainLabel;
            public VisualElement RichElement;
            public System.Action CompleteAction;
        }

        private struct TypewriterJob
        {
            public VisualElement FlowElement;
            public string ParagraphText;
        }

        private TypewriterSession _currentSession;
        private readonly Queue<TypewriterJob> _typewriterQueue = new();
        private Coroutine _typewriterQueueCoroutine;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private bool _isSubscribed = false;

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;
            if (GameManager.Instance is not null)
            {
                GameManager.Instance.OnGameLoaded  += OnGameLoaded;
                GameManager.Instance.OnRoomEntered += OnRoomEntered;
                _isSubscribed = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            if (GameManager.Instance is not null)
            {
                GameManager.Instance.OnGameLoaded  -= OnGameLoaded;
                GameManager.Instance.OnRoomEntered -= OnRoomEntered;
                _isSubscribed = false;
            }
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root   = doc.rootVisualElement;

            // Query elements by their UXML names
            _roomTitleLabel         = _root.Q<Label>("room-title");
            _gameInfoLabel          = _root.Q<Label>("game-info");
            _narrativeScroll        = _root.Q<ScrollView>("narrative-scroll");
            // Bind static compass buttons
            _compassButtons.Clear();
            _compassButtons["North"] = _root.Q<Button>("compass-dir-north");
            _compassButtons["South"] = _root.Q<Button>("compass-dir-south");
            _compassButtons["East"]  = _root.Q<Button>("compass-dir-east");
            _compassButtons["West"]  = _root.Q<Button>("compass-dir-west");
            _compassButtons["NorthWest"] = _root.Q<Button>("compass-dir-nw");
            _compassButtons["NorthEast"] = _root.Q<Button>("compass-dir-ne");
            _compassButtons["SouthWest"] = _root.Q<Button>("compass-dir-sw");
            _compassButtons["SouthEast"] = _root.Q<Button>("compass-dir-se");
            _compassButtons["Up"]    = _root.Q<Button>("compass-dir-up");
            _compassButtons["Down"]  = _root.Q<Button>("compass-dir-down");
            _compassButtons["In"]    = _root.Q<Button>("compass-dir-in");
            _compassButtons["Out"]   = _root.Q<Button>("compass-dir-out");

            _objectsListContainer   = _root.Q<VisualElement>("objects-list");
            _charactersListContainer= _root.Q<VisualElement>("characters-list");
            _inventoryListContainer = _root.Q<VisualElement>("inventory-list");
            _playerNameLabel        = _root.Q<Label>("player-name");
            _playerGenderLabel      = _root.Q<Label>("player-gender");
            _sceneImageContainer    = _root.Q<VisualElement>("scene-image");
            _narrativePanel         = _root.Q<VisualElement>("narrative-panel");

            // Settings components
            _settingsBtn            = _root.Q<Button>("settings-btn");
            _settingsMenu           = _root.Q<VisualElement>("settings-menu");
            _fullscreenToggleBtn    = _root.Q<Button>("fullscreen-toggle-btn");
            _typewriterToggleBtn    = _root.Q<Button>("typewriter-toggle-btn");
            _typewriterSpeedSlider  = _root.Q<Slider>("typewriter-speed-slider");
            _volumeSlider           = _root.Q<SliderInt>("volume-slider");
            _quitGameBtn            = _root.Q<Button>("quit-game-btn");
            _closeSettingsBtn       = _root.Q<Button>("close-settings-btn");
            _playerPortrait         = _root.Q<VisualElement>("player-portrait");
            _scenePlaceholder       = _root.Q<Label>("scene-placeholder");
            _splashScreen           = _root.Q<VisualElement>("splash-screen");

            // Query Game Over menu elements
            _gameOverMenu        = _root.Q<VisualElement>("game-over-menu");
            _gameOverMessage     = _root.Q<Label>("game-over-message");
            _gameOverRestartBtn  = _root.Q<Button>("game-over-restart-btn");
            _gameOverLoadBtn     = _root.Q<Button>("game-over-load-btn");
            _gameOverExitBtn     = _root.Q<Button>("game-over-exit-btn");

            // Query Prompt Input elements
            _promptInputMenu       = _root.Q<VisualElement>("prompt-input-menu");
            _promptInputMessage    = _root.Q<Label>("prompt-input-message");
            _promptTextField       = _root.Q<TextField>("prompt-text-field");
            _promptSelectionScroll = _root.Q<ScrollView>("prompt-selection-scroll");
            _promptSubmitBtn       = _root.Q<Button>("prompt-submit-btn");

            // Bind Game Over / Prompt Input click handlers
            if (_gameOverRestartBtn is not null) _gameOverRestartBtn.clicked += RestartGameAction;
            if (_gameOverLoadBtn is not null)    _gameOverLoadBtn.clicked    += OpenLoadGameFromGameOver;
            if (_gameOverExitBtn is not null)    _gameOverExitBtn.clicked    += ExitGameAction;
            if (_promptSubmitBtn is not null)    _promptSubmitBtn.clicked    += SubmitPromptInput;

            // Load saved settings
            _typewriterEnabled     = PlayerPrefs.GetInt("Pref_TypewriterEnabled", 1) == 1;
            _typewriterSpeed       = PlayerPrefs.GetFloat("Pref_TypewriterSpeed", 0.018f);
            float savedVolume      = PlayerPrefs.GetFloat("Pref_MasterVolume", 1.0f);
            AudioListener.volume   = savedVolume;

            if (_settingsBtn is not null) _settingsBtn.clicked += OpenSettingsMenu;
            if (_fullscreenToggleBtn is not null) _fullscreenToggleBtn.clicked += ToggleFullscreen;
            if (_typewriterToggleBtn is not null) _typewriterToggleBtn.clicked += ToggleTypewriter;
            if (_quitGameBtn is not null) _quitGameBtn.clicked += QuitGame;
            if (_closeSettingsBtn is not null) _closeSettingsBtn.clicked += CloseSettingsMenu;

            if (_typewriterSpeedSlider is not null)
            {
                _typewriterSpeedSlider.value = _typewriterSpeed;
                _typewriterSpeedSlider.RegisterValueChangedCallback(OnTypewriterSpeedChanged);
            }

            if (_volumeSlider is not null)
            {
                _volumeSlider.value = Mathf.RoundToInt(AudioListener.volume * 100f);
                _volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);
            }

            if (_roomTitleLabel is not null)
            {
                _roomTitleLabel.pickingMode = PickingMode.Position;
                _roomTitleLabel.RegisterCallback<ClickEvent>(OnRoomTitleClicked);
            }

            if (_playerPortrait is not null)
            {
                _playerPortrait.pickingMode = PickingMode.Position;
                _playerPortrait.RegisterCallback<ClickEvent>(OnPlayerPortraitClicked);
            }

            // Wire up Save / Load slot buttons
            for (int slot = 1; slot <= 3; slot++)
            {
                int capturedSlot = slot;
                var saveBtn = _root.Q<Button>($"save-slot-{capturedSlot}-btn");
                if (saveBtn is not null) saveBtn.clicked += () => SaveGameSlot(capturedSlot);

                var loadBtn = _root.Q<Button>($"load-slot-{capturedSlot}-btn");
                if (loadBtn is not null) loadBtn.clicked += () => LoadGameSlot(capturedSlot);
            }

            SubscribeEvents();
        }

        private bool _firstRoomRendered = false;

        private void Start()
        {
            SubscribeEvents();

            // Self-healing synchronization: if game is already loaded AND no room has
            // been rendered yet (OnRoomEntered hasn't fired), render the current room now.
            if (GameManager.Instance?.ActiveGame is not null)
            {
                OnGameLoaded(GameManager.Instance.ActiveGame);
                if (GameManager.Instance.CurrentRoom is not null && !_firstRoomRendered)
                {
                    OnRoomEntered(GameManager.Instance.CurrentRoom);
                }
            }

            if (_splashScreen is not null)
            {
                StartCoroutine(FadeOutSplashScreenRoutine());
            }
        }

        private System.Collections.IEnumerator FadeOutSplashScreenRoutine()
        {
            yield return new UnityEngine.WaitForSeconds(1.8f);

            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (_splashScreen is not null)
                {
                    _splashScreen.style.opacity = Mathf.Lerp(1f, 0f, t);
                }
                yield return null;
            }

            if (_splashScreen is not null)
            {
                _splashScreen.style.display = DisplayStyle.None;
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (_settingsBtn is not null) _settingsBtn.clicked -= OpenSettingsMenu;
            if (_fullscreenToggleBtn is not null) _fullscreenToggleBtn.clicked -= ToggleFullscreen;
            if (_typewriterToggleBtn is not null) _typewriterToggleBtn.clicked -= ToggleTypewriter;
            if (_quitGameBtn is not null) _quitGameBtn.clicked -= QuitGame;
            if (_closeSettingsBtn is not null) _closeSettingsBtn.clicked -= CloseSettingsMenu;

            // Unbind Game Over / Prompt Input click handlers
            if (_gameOverRestartBtn is not null) _gameOverRestartBtn.clicked -= RestartGameAction;
            if (_gameOverLoadBtn is not null)    _gameOverLoadBtn.clicked    -= OpenLoadGameFromGameOver;
            if (_gameOverExitBtn is not null)    _gameOverExitBtn.clicked    -= ExitGameAction;
            if (_promptSubmitBtn is not null)    _promptSubmitBtn.clicked    -= SubmitPromptInput;
            if (_typewriterSpeedSlider is not null)
            {
                _typewriterSpeedSlider.UnregisterValueChangedCallback(OnTypewriterSpeedChanged);
            }
            if (_volumeSlider is not null)
            {
                _volumeSlider.UnregisterValueChangedCallback(OnVolumeChanged);
            }

            if (_roomTitleLabel is not null)
            {
                _roomTitleLabel.UnregisterCallback<ClickEvent>(OnRoomTitleClicked);
            }

            if (_playerPortrait is not null)
            {
                _playerPortrait.UnregisterCallback<ClickEvent>(OnPlayerPortraitClicked);
            }
        }



        // ── Game / Room Events ────────────────────────────────────────────────

        private void OnGameLoaded(GameData game)
        {
            if (_gameInfoLabel is not null)
                _gameInfoLabel.text = $"by {game.Author}  ·  v{game.Version}";

            // Clear narrative history on game load to restore pristine log state
            _narrativeScroll?.Clear();

            RefreshPlayerPanel();
            RefreshPlayerPortrait();
        }


        private void OnRoomEntered(RoomData room)
        {
            _firstRoomRendered = true;
            RenderRoom(room);
        }


        // ── Public Interface (called by CommandEffectRouter) ──────────────────

        public void RenderRoom(RoomData room)
        {
            if (room is null) return;

            // Room title
            if (_roomTitleLabel is not null)
                _roomTitleLabel.text = room.Name;

            // Scene image (preserve aspect ratio scale and hide placeholder)
            if (!string.IsNullOrWhiteSpace(room.PortraitImagePath))
            {
                DisplaySceneImage(room.PortraitImagePath);
            }
            else
            {
                var elem = _root?.Q<VisualElement>("scene-image");
                if (elem is not null) elem.style.backgroundImage = null;
                if (_scenePlaceholder is not null) _scenePlaceholder.style.display = DisplayStyle.Flex;
            }

            // Compass exits
            BuildExitButtons(room);

            // Narrative description — appended here only (CommandEffectRouter does NOT duplicate this)
            AppendNarrativeEntry(room.Name, room.Description);

            // Entity lists
            RefreshEntityLists();
        }


        public void AppendNarrativeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _narrativeScroll is null) return;
            AutocompleteActiveTypewriters();

            // Use BuildNarrativeBody so the text is wrapped in a narrative-paragraph
            // VisualElement (flex-direction:row, flex-wrap:wrap). A bare Label with
            // white-space:normal doesn't constrain its width in Unity UI Toolkit,
            // causing the layout engine to record a 1-line height even when the text
            // visually wraps — which makes the next element overlap this one.
            BuildNarrativeBody(text);
            ScrollNarrativeToBottom();
        }

        public void RefreshEntityLists()
        {
            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            if (game is null || room is null) return;

            // Gather all contained object IDs globally to exclude them from top-level room/inventory listings
            var containedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var o in game.Objects)
            {
                if (o.IsContainer && o.ContainedObjectIds is not null)
                {
                    foreach (var cId in o.ContainedObjectIds)
                        containedIds.Add(cId);
                }
            }

            // Objects
            _objectsListContainer?.Clear();
            foreach (var obj in game.Objects.FindAll(o => room.ObjectIds.Contains(o.Id) && !o.IsCharacter && !containedIds.Contains(o.Id)))
            {
                _objectsListContainer?.Add(CreateEntityRow(obj, false));
                if (obj.IsContainer && obj.ContainerOpen && obj.ContainedObjectIds is not null)
                {
                    foreach (var childId in obj.ContainedObjectIds)
                    {
                        var childObj = game.Objects.Find(o => string.Equals(o.Id, childId, StringComparison.OrdinalIgnoreCase));
                        if (childObj is not null)
                            _objectsListContainer?.Add(CreateNestedEntityRow(childObj, false));
                    }
                }
            }

            // Characters
            _charactersListContainer?.Clear();
            foreach (var ch in game.Characters)
            {
                // Check dynamic location variable first
                var charRoomVar = game.Variables.Find(v => string.Equals(v.Name, $"char.{ch.Id}.currentRoomId", StringComparison.OrdinalIgnoreCase))?.Value;
                bool isInThisRoom = false;

                if (!string.IsNullOrWhiteSpace(charRoomVar))
                {
                    isInThisRoom = string.Equals(charRoomVar, room.Id, StringComparison.OrdinalIgnoreCase);
                }
                else if (!string.IsNullOrWhiteSpace(ch.StartingRoomId))
                {
                    isInThisRoom = string.Equals(ch.StartingRoomId, room.Id, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // Fallback to static ObjectIds
                    isInThisRoom = room.ObjectIds.Contains(ch.Id);
                }

                if (isInThisRoom)
                {
                    _charactersListContainer?.Add(CreateEntityRow(ch, false));
                }
            }

            // Inventory
            _inventoryListContainer?.Clear();
            foreach (var item in game.Player.Inventory.FindAll(i => !containedIds.Contains(i.Id)))
            {
                _inventoryListContainer?.Add(CreateEntityRow(item, true));
                if (item.IsContainer && item.ContainerOpen && item.ContainedObjectIds is not null)
                {
                    foreach (var childId in item.ContainedObjectIds)
                    {
                        var childObj = game.Objects.Find(o => string.Equals(o.Id, childId, StringComparison.OrdinalIgnoreCase));
                        if (childObj is not null)
                            _inventoryListContainer?.Add(CreateNestedEntityRow(childObj, true));
                    }
                }
            }
        }

        public void RefreshPlayerPanel()
        {
            var player = GameManager.Instance?.ActiveGame?.Player;
            if (player is null) return;

            if (_playerNameLabel   is not null) _playerNameLabel.text   = player.Name;
            if (_playerGenderLabel is not null) _playerGenderLabel.text = player.Gender;
            RefreshPlayerPortrait();
        }


        public void RefreshPlayerPortrait()
        {
            // Portrait loading from file path via Texture2D
            var player = GameManager.Instance?.ActiveGame?.Player;
            if (player is null || string.IsNullOrWhiteSpace(player.PortraitImagePath)) return;
            LoadAndDisplayImage(player.PortraitImagePath, "player-portrait");
        }

        public void DisplaySceneImage(string path)
        {
            LoadAndDisplayImage(path, "scene-image");
        }

        // ── Narrative Fade (called by GameManager) ────────────────────────────

        public async System.Threading.Tasks.Task FadeNarrativeAsync(float targetOpacity, int durationMs)
        {
            if (_narrativePanel is null) return;
            float start   = _narrativePanel.resolvedStyle.opacity;
            float elapsed = 0f;
            float duration = durationMs / 1000f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                _narrativePanel.style.opacity = Mathf.Lerp(start, targetOpacity, t);
                await System.Threading.Tasks.Task.Yield();
            }
            _narrativePanel.style.opacity = targetOpacity;
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private void AppendNarrativeEntry(string roomName, string description)
        {
            if (_narrativeScroll is null) return;
            AutocompleteActiveTypewriters();

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            var resolved = game is not null
                ? TemplateResolver.Resolve(description, game, room)
                : description;


            // Separator
            var sep = new VisualElement();
            sep.AddToClassList("narrative-separator");
            _narrativeScroll.Add(sep);

            // Room name header
            var header = new Label(roomName);
            header.AddToClassList("narrative-room-header");
            _narrativeScroll.Add(header);

            // Body — built as inline spans with [hotlink] support
            BuildNarrativeBody(resolved);

            ScrollNarrativeToBottom();
        }

        private void BuildNarrativeBody(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Normalize newlines to standard Unix \n to handle \r\n and raw \r safely
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var paragraphs = text.Split(new[] { "\n" }, StringSplitOptions.None);

            if (!_typewriterEnabled)
            {
                foreach (var para in paragraphs)
                {
                    if (string.IsNullOrWhiteSpace(para))
                    {
                        var spacer = new VisualElement();
                        spacer.AddToClassList("narrative-spacer");
                        _narrativeScroll.Add(spacer);
                        continue;
                    }

                    var flow = BuildParagraphFlow(para);
                    _narrativeScroll.Add(flow);
                }
                ScrollNarrativeToBottom();
                return;
            }

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    _typewriterQueue.Enqueue(new TypewriterJob { ParagraphText = null });
                    continue;
                }

                var flow = BuildParagraphFlow(para);
                _typewriterQueue.Enqueue(new TypewriterJob { FlowElement = flow, ParagraphText = para });
            }

            if (_typewriterQueueCoroutine == null)
            {
                _typewriterQueueCoroutine = StartCoroutine(RunTypewriterQueue());
            }
        }

        private VisualElement BuildParagraphFlow(string para)
        {
            var flow = new VisualElement();
            flow.AddToClassList("narrative-paragraph");

            var matches = Regex.Matches(para, @"\[([^\]]+)\]");
            int lastIdx = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIdx)
                    flow.Add(MakePlainLabel(para.Substring(lastIdx, match.Index - lastIdx)));

                var entityName = match.Groups[1].Value;
                var link       = new Button(() => HandleInlineEntityClicked(entityName));
                link.text      = entityName;
                link.AddToClassList("narrative-hotlink");
                flow.Add(link);

                lastIdx = match.Index + match.Length;
            }

            if (lastIdx < para.Length)
                flow.Add(MakePlainLabel(para.Substring(lastIdx)));

            return flow;
        }

        private Label MakePlainLabel(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("narrative-text");
            return lbl;
        }

        public void AutocompleteActiveTypewriters()
        {
            if (_typewriterQueueCoroutine != null)
            {
                StopCoroutine(_typewriterQueueCoroutine);
                _typewriterQueueCoroutine = null;
            }

            if (_currentSession.CompleteAction != null)
            {
                _currentSession.CompleteAction();
                _currentSession = default;
            }

            while (_typewriterQueue.Count > 0)
            {
                var job = _typewriterQueue.Dequeue();
                if (job.ParagraphText == null)
                {
                    var spacer = new VisualElement();
                    spacer.AddToClassList("narrative-spacer");
                    _narrativeScroll.Add(spacer);
                }
                else
                {
                    _narrativeScroll.Add(job.FlowElement);
                }
            }

            ScrollNarrativeToBottom();
        }

        private IEnumerator RunTypewriterQueue()
        {
            while (_typewriterQueue.Count > 0)
            {
                var job = _typewriterQueue.Dequeue();
                if (job.ParagraphText == null)
                {
                    var spacer = new VisualElement();
                    spacer.AddToClassList("narrative-spacer");
                    _narrativeScroll.Add(spacer);
                    ScrollNarrativeToBottom();
                    yield return null;
                    continue;
                }

                yield return StartCoroutine(TypewriterRevealRoutine(job.FlowElement, job.ParagraphText));
            }

            _typewriterQueueCoroutine = null;
        }

        private IEnumerator TypewriterRevealRoutine(VisualElement element, string fullText)
        {
            var session = new TypewriterSession();
            var cleanText = Regex.Replace(fullText, @"\[([^\]]+)\]", "$1");

            var container = new VisualElement();
            container.AddToClassList("narrative-paragraph");

            var plain = new Label();
            plain.AddToClassList("narrative-text");
            container.Add(plain);

            _narrativeScroll.Add(container);

            session.PlainLabel = plain;
            session.RichElement = element;
            session.CompleteAction = () =>
            {
                if (_narrativeScroll.Contains(container))
                    _narrativeScroll.Remove(container);
                if (!_narrativeScroll.Contains(element))
                    _narrativeScroll.Add(element);
                ScrollNarrativeToBottom();
            };

            _currentSession = session;

            for (int i = 0; i <= cleanText.Length; i++)
            {
                plain.text = cleanText.Substring(0, i);
                yield return new WaitForSeconds(_typewriterSpeed);
            }

            _currentSession = default;
            session.CompleteAction();
        }


        private void BuildExitButtons(RoomData room)
        {
            // Reset all static compass buttons to inactive (dimmed, non-clickable) state
            foreach (var kvp in _compassButtons)
            {
                if (kvp.Value is null) continue;
                kvp.Value.RemoveFromClassList("compass-btn--active");
                kvp.Value.AddToClassList("compass-btn--inactive");
                kvp.Value.SetEnabled(false);
                kvp.Value.clickable = null; // Clear previous transition actions
            }

            // High-intensity glow highlights for active exit directions
            foreach (var exit in room.Exits)
            {
                string key = exit.Key;
                if (room.LockedExits.TryGetValue(key, out var isLocked) && isLocked)
                    continue;

                if (_compassButtons.TryGetValue(key, out var btn) && btn is not null)
                {
                    btn.RemoveFromClassList("compass-btn--inactive");
                    btn.AddToClassList("compass-btn--active");
                    btn.SetEnabled(true);

                    // Add dynamic move player click action
                    string targetRoomId = exit.Value;
                    btn.clickable = new Clickable(() => GameManager.Instance?.MovePlayerToRoom(targetRoomId));
                }
            }
        }

        private VisualElement CreateEntityRow(GameObjectData entity, bool isInventory)
        {
            var row = new VisualElement();
            row.AddToClassList("entity-row");
            row.pickingMode = PickingMode.Position;

            var dot = new VisualElement();
            dot.AddToClassList(entity.IsCharacter ? "entity-dot--character" : "entity-dot--object");
            row.Add(dot);

            string nameText = entity.Name;
            if (entity.IsContainer)
            {
                nameText += entity.ContainerOpen ? " [Open]" : " [Closed]";
            }
            var lbl = new Label(nameText);
            lbl.AddToClassList("entity-name");
            row.Add(lbl);

            var btn = new Button(() => ShowEntityInteractionMenu(entity, isInventory));
            btn.text = "⋯";
            btn.AddToClassList("entity-action-btn");
            row.Add(btn);

            // Tap on the whole row also opens the menu
            row.RegisterCallback<ClickEvent>(_ => ShowEntityInteractionMenu(entity, isInventory));

            return row;
        }

        private VisualElement CreateNestedEntityRow(GameObjectData entity, bool isInventory)
        {
            var row = new VisualElement();
            row.AddToClassList("entity-row");
            row.AddToClassList("entity-row--nested");
            row.pickingMode = PickingMode.Position;

            var arrow = new Label("↳");
            arrow.AddToClassList("entity-nested-arrow");
            row.Add(arrow);

            var dot = new VisualElement();
            dot.AddToClassList("entity-dot--object");
            row.Add(dot);

            var lbl = new Label(entity.Name);
            lbl.AddToClassList("entity-name");
            lbl.AddToClassList("entity-name--nested");
            row.Add(lbl);

            var btn = new Button(() => ShowEntityInteractionMenu(entity, isInventory));
            btn.text = "⋯";
            btn.AddToClassList("entity-action-btn");
            row.Add(btn);

            row.RegisterCallback<ClickEvent>(_ => ShowEntityInteractionMenu(entity, isInventory));

            return row;
        }

        private void ShowEntityInteractionMenu(GameObjectData entity, bool isInventory)
        {
            // Build interaction menu using Unity UI Toolkit popup or a custom panel
            InteractionController.Instance?.ShowMenu(entity, isInventory);
        }

        private void HandleInlineEntityClicked(string name)
        {
            InteractionController.Instance?.HandleInlineClick(name);
        }

        private void ScrollNarrativeToBottom()
        {
            // Schedule two frames out to let Unity UI Toolkit complete layout before scrolling
            _narrativeScroll?.schedule.Execute(() =>
            {
                _narrativeScroll?.schedule.Execute(() =>
                    _narrativeScroll.scrollOffset = new Vector2(0, float.MaxValue));
            });
        }

        private RenderTexture _videoTexture;
        private UnityEngine.Video.VideoPlayer _videoPlayer;

        private void PlayVideo(string path)
        {
            if (_videoTexture == null)
            {
                _videoTexture = new RenderTexture(1280, 720, 16, RenderTextureFormat.ARGB32);
                _videoTexture.Create();
            }

            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (_videoPlayer == null)
                {
                    _videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
                }
            }

            _videoPlayer.playOnAwake = false;
            _videoPlayer.isLooping = true;
            _videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _videoTexture;

            string url = FormatLocalPathForWeb(path);
            // Standalone fallback: URL might need standard path format instead of file:// for Unity's VideoPlayer on Windows
            if (url.StartsWith("file:///"))
            {
                _videoPlayer.url = url.Substring(8);
            }
            else if (url.StartsWith("file://"))
            {
                _videoPlayer.url = url.Substring(7);
            }
            else
            {
                _videoPlayer.url = url;
            }

            var elem = _root?.Q<VisualElement>("scene-image");
            if (elem is not null)
            {
                elem.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoTexture));
                if (_scenePlaceholder is not null)
                {
                    _scenePlaceholder.style.display = DisplayStyle.None;
                }
            }

            _videoPlayer.Play();
            Debug.Log($"[UIManager] Started video playback for '{path}' from URL '{_videoPlayer.url}'");
        }

        private void StopVideo()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
            }
        }

        private void LoadAndDisplayImage(string path, string elementName)
        {
            if (elementName == "scene-image")
            {
                string ext = System.IO.Path.GetExtension(path).ToLower();
                if (ext == ".mp4" || ext == ".webm" || ext == ".mov")
                {
                    PlayVideo(path);
                    return;
                }
                else
                {
                    StopVideo();
                }
            }
            StartCoroutine(LoadImageCoroutine(path, elementName));
        }

        private string FormatLocalPathForWeb(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            if (path.StartsWith("file://") || path.StartsWith("http://") || path.StartsWith("https://"))
                return path;

            string fullPath = path;
            if (!System.IO.Path.IsPathRooted(path))
            {
                fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, path);
            }
            else
            {
                // Standalone fallback: redirect designer AppData path to current StreamingAssets/Assets/ copy
                var fileName = System.IO.Path.GetFileName(path);
                var streamingLocalPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Assets", fileName);
                if (System.IO.File.Exists(streamingLocalPath))
                {
                    fullPath = streamingLocalPath;
                }
            }

            fullPath = fullPath.Replace("\\", "/");
            if (!fullPath.StartsWith("/"))
                return "file:///" + fullPath;
            else
                return "file://" + fullPath;
        }

        private IEnumerator LoadImageCoroutine(string path, string elementName)
        {
            string url = FormatLocalPathForWeb(path);
            Debug.Log($"[UIManager] Loading texture for '{elementName}' from URL: '{url}'");
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);

            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var tex  = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                var elem = _root?.Q<VisualElement>(elementName);
                if (elem is not null)
                {
                    elem.style.backgroundImage = new StyleBackground(tex);
                    if (elementName == "scene-image" && _scenePlaceholder is not null)
                    {
                        _scenePlaceholder.style.display = DisplayStyle.None;
                    }
                    Debug.Log($"[UIManager] Successfully applied texture to '{elementName}'");
                }
            }
            else
            {
                Debug.LogError($"[UIManager] Failed to load texture for '{elementName}' from URL '{url}': {req.error}");
            }
        }


        // ── Settings Callbacks & Save/Load Slots ────────────────────────────────
        private void OpenSettingsMenu()
        {
            if (_settingsMenu is null) return;

            // Sync toggle button texts with current engine states
            if (_fullscreenToggleBtn is not null)
            {
                _fullscreenToggleBtn.text = Screen.fullScreen ? "Windowed" : "Fullscreen";
            }
            if (_typewriterToggleBtn is not null)
            {
                _typewriterToggleBtn.text = _typewriterEnabled ? "Typewriter ON" : "Typewriter OFF";
            }
            if (_typewriterSpeedSlider is not null)
            {
                _typewriterSpeedSlider.value = _typewriterSpeed;
            }
            if (_volumeSlider is not null)
            {
                _volumeSlider.value = Mathf.RoundToInt(AudioListener.volume * 100f);
            }

            RefreshSaveLoadSlots();

            _settingsMenu.style.display = DisplayStyle.Flex;
            _settingsMenu.BringToFront();
        }

        private void CloseSettingsMenu()
        {
            if (_settingsMenu is not null)
                _settingsMenu.style.display = DisplayStyle.None;
        }

        private void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            if (_fullscreenToggleBtn is not null)
            {
                _fullscreenToggleBtn.text = Screen.fullScreen ? "Fullscreen" : "Windowed";
            }
        }

         private void ToggleTypewriter()
         {
             _typewriterEnabled = !_typewriterEnabled;
             PlayerPrefs.SetInt("Pref_TypewriterEnabled", _typewriterEnabled ? 1 : 0);
             PlayerPrefs.Save();
             if (_typewriterToggleBtn is not null)
             {
                 _typewriterToggleBtn.text = _typewriterEnabled ? "Typewriter ON" : "Typewriter OFF";
             }
         }

         private void OnTypewriterSpeedChanged(ChangeEvent<float> evt)
         {
             _typewriterSpeed = evt.newValue;
             PlayerPrefs.SetFloat("Pref_TypewriterSpeed", _typewriterSpeed);
             PlayerPrefs.Save();
         }

         private void OnVolumeChanged(ChangeEvent<int> evt)
         {
             float vol = evt.newValue / 100f;
             AudioListener.volume = vol;
             PlayerPrefs.SetFloat("Pref_MasterVolume", vol);
             PlayerPrefs.Save();
         }

        private void QuitGame()
        {
            Debug.Log("[UIManager] Gracefully quitting game standalone.");
            Application.Quit();
        }

        private void RefreshSaveLoadSlots()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                var loadBtn = _root.Q<Button>($"load-slot-{slot}-btn");
                if (loadBtn is not null)
                {
                    bool hasSave = GameManager.Instance is not null && GameManager.Instance.HasSaveFile(slot);
                    loadBtn.SetEnabled(hasSave);
                }
            }
        }

        private void SaveGameSlot(int slot)
        {
            if (GameManager.Instance is null) return;
            if (GameManager.Instance.HasSaveFile(slot))
            {
                ShowOverwriteConfirmation(slot);
            }
            else
            {
                PerformSave(slot);
            }
        }

        private void ShowOverwriteConfirmation(int slot)
        {
            // Create full-screen overlay to block input
            var overlay = new VisualElement();
            overlay.name = "overwrite-confirm-overlay";
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.width = new Length(100, LengthUnit.Percent);
            overlay.style.height = new Length(100, LengthUnit.Percent);
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            // Dialog container
            var dialog = new VisualElement();
            dialog.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            dialog.style.borderTopWidth = 1;
            dialog.style.borderBottomWidth = 1;
            dialog.style.borderLeftWidth = 1;
            dialog.style.borderRightWidth = 1;
            dialog.style.borderTopColor = new Color(1f, 1f, 1f, 0.1f);
            dialog.style.borderBottomColor = new Color(1f, 1f, 1f, 0.1f);
            dialog.style.borderLeftColor = new Color(1f, 1f, 1f, 0.1f);
            dialog.style.borderRightColor = new Color(1f, 1f, 1f, 0.1f);
            dialog.style.borderTopLeftRadius = 12;
            dialog.style.borderTopRightRadius = 12;
            dialog.style.borderBottomLeftRadius = 12;
            dialog.style.borderBottomRightRadius = 12;
            dialog.style.paddingLeft = 24;
            dialog.style.paddingRight = 24;
            dialog.style.paddingTop = 24;
            dialog.style.paddingBottom = 24;
            dialog.style.width = 400;

            // Title
            var title = new Label("Overwrite Save?");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 12;
            dialog.Add(title);

            // Message
            var message = new Label($"Slot {slot} already contains a saved game. Are you sure you want to overwrite it?");
            message.style.fontSize = 14;
            message.style.color = new Color(0.7f, 0.7f, 0.75f);
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.marginBottom = 24;
            dialog.Add(message);

            // Buttons Container
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;

            // Cancel Button
            var cancelBtn = new Button(() => {
                _root.Remove(overlay);
            }) { text = "Cancel" };
            cancelBtn.style.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            cancelBtn.style.color = Color.white;
            cancelBtn.style.borderTopWidth = 0;
            cancelBtn.style.borderBottomWidth = 0;
            cancelBtn.style.borderLeftWidth = 0;
            cancelBtn.style.borderRightWidth = 0;
            cancelBtn.style.borderTopLeftRadius = 6;
            cancelBtn.style.borderTopRightRadius = 6;
            cancelBtn.style.borderBottomLeftRadius = 6;
            cancelBtn.style.borderBottomRightRadius = 6;
            cancelBtn.style.paddingLeft = 16;
            cancelBtn.style.paddingRight = 16;
            cancelBtn.style.paddingTop = 8;
            cancelBtn.style.paddingBottom = 8;
            cancelBtn.style.marginRight = 12;
            cancelBtn.style.fontSize = 13;
            buttonRow.Add(cancelBtn);

            // Overwrite Button
            var overwriteBtn = new Button(() => {
                _root.Remove(overlay);
                PerformSave(slot);
            }) { text = "Yes, Overwrite" };
            overwriteBtn.style.backgroundColor = new Color(0.35f, 0.2f, 0.8f); // premium purple
            overwriteBtn.style.color = Color.white;
            overwriteBtn.style.borderTopWidth = 0;
            overwriteBtn.style.borderBottomWidth = 0;
            overwriteBtn.style.borderLeftWidth = 0;
            overwriteBtn.style.borderRightWidth = 0;
            overwriteBtn.style.borderTopLeftRadius = 6;
            overwriteBtn.style.borderTopRightRadius = 6;
            overwriteBtn.style.borderBottomLeftRadius = 6;
            overwriteBtn.style.borderBottomRightRadius = 6;
            overwriteBtn.style.paddingLeft = 16;
            overwriteBtn.style.paddingRight = 16;
            overwriteBtn.style.paddingTop = 8;
            overwriteBtn.style.paddingBottom = 8;
            overwriteBtn.style.fontSize = 13;
            buttonRow.Add(overwriteBtn);

            dialog.Add(buttonRow);
            overlay.Add(dialog);

            _root.Add(overlay);
            overlay.BringToFront();
        }

        private void PerformSave(int slot)
        {
            if (GameManager.Instance is null) return;
            GameManager.Instance.SaveGame(slot);
            AppendNarrativeText($"Game saved successfully to Slot {slot}.");
            CloseSettingsMenu();
            RefreshSaveLoadSlots();
        }

        private async void LoadGameSlot(int slot)
        {
            if (GameManager.Instance is null) return;
            AppendNarrativeText($"Loading save from Slot {slot}...");
            CloseSettingsMenu();
            await GameManager.Instance.LoadGameAsync(slot);
            AppendNarrativeText($"Game loaded successfully from Slot {slot}.");
        }

        private void OnRoomTitleClicked(ClickEvent evt)
        {
            var room = GameManager.Instance?.CurrentRoom;
            if (room is null) return;
            InteractionController.Instance?.ShowRoomMenu(room);
        }

        private void OnPlayerPortraitClicked(ClickEvent evt)
        {
            var player = GameManager.Instance?.ActiveGame?.Player;
            if (player is null) return;
            InteractionController.Instance?.ShowPlayerMenu(player);
        }

        // ── Game Over HUD Methods ─────────────────────────────────────────────

        public void ShowGameOverScreen(string finalMessage)
        {
            if (_gameOverMessage is not null)
                _gameOverMessage.text = finalMessage;

            if (_gameOverMenu is not null)
                _gameOverMenu.style.display = DisplayStyle.Flex;
        }

        private void RestartGameAction()
        {
            if (_gameOverMenu is not null)
                _gameOverMenu.style.display = DisplayStyle.None;

            GameManager.Instance?.RestartGame();
        }

        private void OpenLoadGameFromGameOver()
        {
            if (_gameOverMenu is not null)
                _gameOverMenu.style.display = DisplayStyle.None;

            OpenSettingsMenu();
        }

        private void ExitGameAction()
        {
            Debug.Log("[UIManager] Exiting game...");
            Application.Quit();
        }

        // ── Player Prompt Input Modal HUD Methods ─────────────────────────────

        public void ShowPromptInputScreen(string promptText, string inputType, string customOptions, string storeVariableName)
        {
            _promptTargetVarName = storeVariableName;

            if (_promptInputMessage is not null)
                _promptInputMessage.text = promptText;

            var textContainer = _root.Q<VisualElement>("prompt-text-container");
            var selScroll = _promptSelectionScroll;

            if (textContainer is null || selScroll is null) return;

            // Reset visibility
            textContainer.style.display = DisplayStyle.None;
            selScroll.style.display     = DisplayStyle.None;
            selScroll.Clear();

            bool isText = string.Equals(inputType, "Text", System.StringComparison.OrdinalIgnoreCase) || inputType == "0";
            bool isObjects = string.Equals(inputType, "Objects", System.StringComparison.OrdinalIgnoreCase) || inputType == "1";
            bool isCharacters = string.Equals(inputType, "Characters", System.StringComparison.OrdinalIgnoreCase) || inputType == "2";
            bool isCustom = string.Equals(inputType, "Custom", System.StringComparison.OrdinalIgnoreCase) || inputType == "3";

            if (isText)
            {
                textContainer.style.display = DisplayStyle.Flex;
                if (_promptTextField is not null)
                {
                    _promptTextField.value = string.Empty;
                    _promptTextField.Focus();
                }
                if (_promptSubmitBtn is not null)
                    _promptSubmitBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                selScroll.style.display = DisplayStyle.Flex;
                if (_promptSubmitBtn is not null)
                    _promptSubmitBtn.style.display = DisplayStyle.None;

                List<string> options = new List<string>();

                if (isObjects)
                {
                    var game = GameManager.Instance?.ActiveGame;
                    if (game is not null)
                    {
                        foreach (var obj in game.Objects)
                            options.Add(obj.Name);
                    }
                }
                else if (isCharacters)
                {
                    var game = GameManager.Instance?.ActiveGame;
                    if (game is not null)
                    {
                        foreach (var ch in game.Characters)
                            options.Add(ch.Name);
                    }
                }
                else if (isCustom)
                {
                    if (!string.IsNullOrWhiteSpace(customOptions))
                    {
                        foreach (var opt in customOptions.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
                            options.Add(opt.Trim());
                    }
                }

                // Render option buttons in scroll list
                foreach (var opt in options)
                {
                    var btn = new Button(() => SubmitPromptSelection(opt));
                    btn.text = opt;
                    btn.AddToClassList("prompt-choice-btn");
                    selScroll.Add(btn);
                }
            }

            if (_promptInputMenu is not null)
                _promptInputMenu.style.display = DisplayStyle.Flex;
        }

        private void SubmitPromptSelection(string value)
        {
            if (GameManager.Instance?.ActiveGame is null || string.IsNullOrWhiteSpace(_promptTargetVarName)) return;

            var vars = GameManager.Instance.ActiveGame.Variables;
            var targetVar = vars.Find(v => string.Equals(v.Name, _promptTargetVarName, System.StringComparison.OrdinalIgnoreCase));
            if (targetVar is null)
            {
                targetVar = new GameVariableData { Name = _promptTargetVarName, Value = value };
                vars.Add(targetVar);
            }
            else
            {
                targetVar.Value = value;
            }

            if (_promptInputMenu is not null)
                _promptInputMenu.style.display = DisplayStyle.None;

            var currentRoom = GameManager.Instance.CurrentRoom;
            if (currentRoom is not null)
                RenderRoom(currentRoom);

            // Resume the action execution engine
            ActionExecutor.ActiveRunner?.Resume();
        }

        private void SubmitPromptInput()
        {
            if (_promptTextField is null) return;
            string valueEntered = _promptTextField.value;
            SubmitPromptSelection(valueEntered);
        }

        public void ShowDialogueScreen(StartDialogueCommandData cmd, GameExecutionContext ctx)
        {
            if (_promptInputMessage is not null)
                _promptInputMessage.text = ctx.Resolve(cmd.CharacterLines);

            var textContainer = _root.Q<VisualElement>("prompt-text-container");
            var selScroll = _promptSelectionScroll;

            if (textContainer is null || selScroll is null) return;

            textContainer.style.display = DisplayStyle.None;
            selScroll.style.display     = DisplayStyle.Flex;
            selScroll.Clear();

            if (_promptSubmitBtn is not null)
                _promptSubmitBtn.style.display = DisplayStyle.None;

            foreach (var choice in cmd.Choices)
            {
                var resolvedChoiceText = ctx.Resolve(choice.Text);
                var btn = new Button(() => {
                    if (_promptInputMenu is not null)
                        _promptInputMenu.style.display = DisplayStyle.None;

                    // Execute choice sub-commands
                    if (choice.Commands != null && choice.Commands.Count > 0)
                    {
                        var actionData = new ActionData
                        {
                            Id = "choice_action",
                            Name = "Choice Action",
                            Nodes = choice.Commands
                        };
                        ActionExecutor.Execute(actionData, ctx, GetComponent<CommandEffectRouter>());
                    }
                }) { text = resolvedChoiceText };
                btn.AddToClassList("prompt-choice-btn");
                selScroll.Add(btn);
            }

            if (_promptInputMenu is not null)
                _promptInputMenu.style.display = DisplayStyle.Flex;
        }

    }
}
