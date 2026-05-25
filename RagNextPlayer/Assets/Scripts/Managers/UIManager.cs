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
        private VisualElement  _exitsContainer;
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
            _exitsContainer         = _root.Q<VisualElement>("exits-container");
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
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (_settingsBtn is not null) _settingsBtn.clicked -= OpenSettingsMenu;
            if (_fullscreenToggleBtn is not null) _fullscreenToggleBtn.clicked -= ToggleFullscreen;
            if (_typewriterToggleBtn is not null) _typewriterToggleBtn.clicked -= ToggleTypewriter;
            if (_quitGameBtn is not null) _quitGameBtn.clicked -= QuitGame;
            if (_closeSettingsBtn is not null) _closeSettingsBtn.clicked -= CloseSettingsMenu;
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

            // Objects
            _objectsListContainer?.Clear();
            foreach (var obj in game.Objects.FindAll(o => room.ObjectIds.Contains(o.Id) && !o.IsCharacter))
                _objectsListContainer?.Add(CreateEntityRow(obj, false));

            // Characters
            _charactersListContainer?.Clear();
            foreach (var ch in game.Characters.FindAll(c => room.ObjectIds.Contains(c.Id)))
                _charactersListContainer?.Add(CreateEntityRow(ch, false));

            // Inventory
            _inventoryListContainer?.Clear();
            foreach (var item in game.Player.Inventory)
                _inventoryListContainer?.Add(CreateEntityRow(item, true));
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
            if (_exitsContainer is null) return;
            _exitsContainer.Clear();

            foreach (var exit in room.Exits)
            {
                var btn = new Button(() => GameManager.Instance?.MovePlayerToRoom(exit.Value));
                btn.text = exit.Key;
                btn.AddToClassList("compass-btn");
                btn.AddToClassList($"compass-btn--{exit.Key.ToLower()}");
                _exitsContainer.Add(btn);
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

            var lbl = new Label(entity.Name);
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

        private void LoadAndDisplayImage(string path, string elementName)
        {
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
            GameManager.Instance.SaveGame(slot);
            AppendNarrativeText($"Game saved successfully to Slot {slot}.");
            CloseSettingsMenu();
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

    }
}
