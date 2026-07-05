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
        public VisualElement RootElement => _root;
        private VisualElement  _roomActionsContainer;
        private PrimeTween.Tween _pulseTween;
        private VisualElement  _roomPortrait;
        private Label          _roomTitleLabel;
        private Label          _gameInfoLabel;
        private ScrollView     _narrativeScroll;
        private readonly System.Collections.Generic.Dictionary<string, Button> _compassButtons = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, Button> _compassButtonsHud = new(System.StringComparer.OrdinalIgnoreCase);
        private Button _sidebarToggleBtn;
        private VisualElement _rightSidebarContainer;
        private bool _sidebarCollapsed = false;
        private PrimeTween.Tween _sidebarTween;
        private VisualElement _contentSplitter;
        private float _lastSidebarWidth = 300f;
        private VisualElement  _objectsListContainer;
        private VisualElement  _charactersListContainer;
        private VisualElement  _inventoryListContainer;
        private Label          _playerNameLabel;
        private Label          _playerGenderLabel;
        private VisualElement  _sceneImageContainer;
        private VisualElement  _narrativePanel;

        // Settings Elements
        private Button         _settingsBtn;
        private Button         _historyLogBtn;
        private Button         _helpBtn;

        // Unified Game Menu Overlay
        private VisualElement  _gameMenuOverlay;
        private Label          _gameMenuTitle;
        private Button         _menuBtnReturn;
        private Button         _menuBtnSave;
        private Button         _menuBtnLoad;
        private Button         _menuBtnSettings;
        private Button         _menuBtnHistory;
        private Button         _menuBtnHelp;
        private Button         _menuBtnRestart;
        private Button         _menuBtnQuit;

        private VisualElement  _panelSaveLoad;
        private VisualElement  _panelSettings;
        private VisualElement  _panelHistory;
        private VisualElement  _panelHelp;

        private Label          _saveLoadSubtitle;
        private Button         _pagePrevBtn;
        private Button         _pageNextBtn;
        private readonly System.Collections.Generic.List<Button> _pageBtnList = new();

        private ScrollView     _historyLogScroll;

        private bool           _menuIsSaveMode = true;
        private int            _menuCurrentPage = 1;

        private Button         _fullscreenToggleBtn;
        private Button         _typewriterToggleBtn;
        private Button         _fontSizeToggleBtn;
        private string         _fontSizePref = "Normal";
        private Slider         _typewriterSpeedSlider;
        private SliderInt      _volumeSlider;
        private Button         _quitGameBtn; // Obsolete but kept for safety reference if needed

        private VisualElement  _playerPortrait;
        private Label          _scenePlaceholder;
        private VisualElement  _splashScreen;
        public bool IsSplashFinished { get; private set; } = false;

        // Game Over modal references
        private VisualElement  _gameOverMenu;
        private Label          _gameOverMessage;

        private struct UIParticle
        {
            public VisualElement element;
            public Vector2 startPos;
            public Vector2 targetPos;
            public float size;
            public float alpha;
            public float wiggleSeed;
        }
        private Button         _gameOverRestartBtn;
        private Button         _gameOverLoadBtn;
        private Button         _gameOverExitBtn;

        // Prompt Input modal references
        private VisualElement  _promptInputMenu;
        private Label          _promptInputMessage;
        private TextField      _promptTextField;
        private ScrollView     _promptSelectionScroll;
        private Button         _promptSubmitBtn;

        // Revamped HUD Elements
        private VisualElement  _roomActionThumbnail;
        private VisualElement  _roomActionThumbnailWrapper;
        private Button         _compassToggleBtn;
        private VisualElement  _compassDialOverlay;
        private VisualElement  _playerHudContainer;

        private readonly List<System.Tuple<string, string>> _historyLog = new();
        private string         _promptTargetVarName = string.Empty;
        private string         _promptName = string.Empty;
        private PrimeTween.Tween _promptMenuCloseTween;
        private bool           _hasClearedForCurrentAction = false;

        public void PrepareForNewAction()
        {
            _hasClearedForCurrentAction = false;
        }

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
        private Coroutine _revealCoroutine;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (TransitionVFXManager.Instance == null)
            {
                var vfxGo = new GameObject("TransitionVFXManager");
                vfxGo.AddComponent<TransitionVFXManager>();
            }
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

        private void SetupVFXOverlay()
        {
            if (_root == null) return;
            
            // Check if already created
            VisualElement existing = _root.Q("vfx-overlay");
            if (existing != null)
            {
                // Ensure RenderTexture is bound in case of transitions/reloads
                if (TransitionVFXManager.Instance != null && TransitionVFXManager.Instance.VFXRenderTexture != null)
                {
                    existing.style.backgroundImage = Background.FromRenderTexture(TransitionVFXManager.Instance.VFXRenderTexture);
                }
                return;
            }
            
            if (TransitionVFXManager.Instance != null && TransitionVFXManager.Instance.VFXRenderTexture != null)
            {
                VisualElement vfxElement = new VisualElement();
                vfxElement.name = "vfx-overlay";
                vfxElement.style.position = Position.Absolute;
                vfxElement.style.left = 0;
                vfxElement.style.right = 0;
                vfxElement.style.top = 0;
                vfxElement.style.bottom = 0;
                vfxElement.pickingMode = PickingMode.Ignore; // Clicks pass through
                
                vfxElement.style.backgroundImage = Background.FromRenderTexture(TransitionVFXManager.Instance.VFXRenderTexture);
                
                // Add it at the top of the root so it renders on top of UI elements
                _root.Add(vfxElement);
                UnityEngine.Debug.Log("[UIManager] Successfully added VFX Render Texture Overlay to UI Toolkit!");
            }
        }

        private void Update()
        {
            if (_root != null)
            {
                VisualElement vfx = _root.Q("vfx-overlay");
                if (vfx != null)
                {
                    vfx.MarkDirtyRepaint();
                }
            }

            if (Time.frameCount % 60 == 0 && TransitionVFXManager.Instance != null)
            {
                TransitionVFXManager.Instance.LogActiveParticles();
            }
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root   = doc != null ? doc.rootVisualElement : null;
            SetupVFXOverlay();

            // Query elements by their UXML names
            _roomTitleLabel         = _root.Q<Label>("room-title");
            _roomActionsContainer   = _root.Q<VisualElement>("room-actions-container");
            _gameInfoLabel          = _root.Q<Label>("game-info");
            _narrativeScroll        = _root.Q<ScrollView>("narrative-scroll");
            if (_narrativeScroll != null && _narrativeScroll.verticalScroller != null)
            {
                _narrativeScroll.verticalScroller.valueChanged += (val) =>
                {
                    UnityEngine.Debug.Log($"[UIManager] verticalScroller value changed to {val}. StackTrace:\n{System.Environment.StackTrace}");
                };
            }
            
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

            // Bind HUD compass buttons
            _compassButtonsHud.Clear();
            _compassButtonsHud["North"] = _root.Q<Button>("compass-dir-north-hud");
            _compassButtonsHud["South"] = _root.Q<Button>("compass-dir-south-hud");
            _compassButtonsHud["East"]  = _root.Q<Button>("compass-dir-east-hud");
            _compassButtonsHud["West"]  = _root.Q<Button>("compass-dir-west-hud");
            _compassButtonsHud["NorthWest"] = _root.Q<Button>("compass-dir-nw-hud");
            _compassButtonsHud["NorthEast"] = _root.Q<Button>("compass-dir-ne-hud");
            _compassButtonsHud["SouthWest"] = _root.Q<Button>("compass-dir-sw-hud");
            _compassButtonsHud["SouthEast"] = _root.Q<Button>("compass-dir-se-hud");
            _compassButtonsHud["Up"]    = _root.Q<Button>("compass-dir-up-hud");
            _compassButtonsHud["Down"]  = _root.Q<Button>("compass-dir-down-hud");
            _compassButtonsHud["In"]    = _root.Q<Button>("compass-dir-in-hud");
            _compassButtonsHud["Out"]   = _root.Q<Button>("compass-dir-out-hud");

            _sidebarToggleBtn = _root.Q<Button>("sidebar-toggle-btn");
            if (_sidebarToggleBtn is not null) _sidebarToggleBtn.clicked += ToggleSidebar;

            _rightSidebarContainer = _root.Q<VisualElement>("right-sidebar-container");
            if (_rightSidebarContainer is not null)
            {
                _rightSidebarContainer.pickingMode = PickingMode.Ignore;
                
                var compassCard = _rightSidebarContainer.Q<VisualElement>("navigation-panel");
                if (compassCard is not null) compassCard.pickingMode = PickingMode.Position;
                
                var legendCard = _rightSidebarContainer.Q<VisualElement>("room-legend");
                if (legendCard is not null) legendCard.pickingMode = PickingMode.Position;
            }

            _contentSplitter = _root.Q<VisualElement>("main-split-container");
            SetupSplitters();


            _objectsListContainer   = _root.Q<VisualElement>("objects-list");
            _charactersListContainer= _root.Q<VisualElement>("characters-list");
            _inventoryListContainer = _root.Q<VisualElement>("inventory-list");
            _playerNameLabel        = _root.Q<Label>("player-name");
            _playerGenderLabel      = _root.Q<Label>("player-gender");
            _sceneImageContainer    = _root.Q<VisualElement>("scene-image");
            _narrativePanel         = _root.Q<VisualElement>("narrative-panel");

            // Query revamped HUD elements
            _playerHudContainer = _root.Q<VisualElement>("player-hud-container");
            _roomActionThumbnail = _root.Q<VisualElement>("room-action-thumbnail");
            _roomActionThumbnailWrapper = _root.Q<VisualElement>("room-action-thumbnail-wrapper");
            if (_roomActionThumbnailWrapper is not null)
            {
                _roomActionThumbnailWrapper.RegisterCallback<ClickEvent>(OnRoomTitleClicked);
            }

            _compassToggleBtn = _root.Q<Button>("compass-toggle-btn");
            _compassDialOverlay = _root.Q<VisualElement>("compass-dial-overlay");
            if (_compassToggleBtn is not null)
            {
                _compassToggleBtn.clicked += ToggleCompassDial;
            }


            _historyLogBtn = _root.Q<Button>("history-log-btn");
            if (_historyLogBtn is not null) _historyLogBtn.clicked += () => OpenGameMenuTab("History");

            _helpBtn = _root.Q<Button>("help-btn");
            if (_helpBtn is not null) _helpBtn.clicked += () => OpenGameMenuTab("Help");

            _settingsBtn = _root.Q<Button>("settings-btn");
            if (_settingsBtn is not null) _settingsBtn.clicked += () => OpenGameMenuTab("Settings");

            // Unified Game Menu Elements
            _gameMenuOverlay = _root.Q<VisualElement>("game-menu-overlay");
            _gameMenuTitle = _root.Q<Label>("game-menu-title");

            _menuBtnReturn = _root.Q<Button>("menu-btn-return");
            _menuBtnSave = _root.Q<Button>("menu-btn-save");
            _menuBtnLoad = _root.Q<Button>("menu-btn-load");
            _menuBtnSettings = _root.Q<Button>("menu-btn-settings");
            _menuBtnHistory = _root.Q<Button>("menu-btn-history");
            _menuBtnHelp = _root.Q<Button>("menu-btn-help");
            _menuBtnRestart = _root.Q<Button>("menu-btn-restart");
            _menuBtnQuit = _root.Q<Button>("menu-btn-quit");

            _panelSaveLoad = _root.Q<VisualElement>("menu-panel-save-load");
            _panelSettings = _root.Q<VisualElement>("menu-panel-settings");
            _panelHistory = _root.Q<VisualElement>("menu-panel-history");
            _panelHelp = _root.Q<VisualElement>("menu-panel-help");

            _saveLoadSubtitle = _root.Q<Label>("save-load-subtitle");
            _pagePrevBtn = _root.Q<Button>("page-prev-btn");
            _pageNextBtn = _root.Q<Button>("page-next-btn");

            _pageBtnList.Clear();
            for (int i = 1; i <= 5; i++)
            {
                var pBtn = _root.Q<Button>($"page-btn-{i}");
                if (pBtn is not null) _pageBtnList.Add(pBtn);
            }

            _historyLogScroll = _root.Q<ScrollView>("history-log-scroll");

            // Event Bindings for Unified Menu
            if (_menuBtnReturn is not null) _menuBtnReturn.clicked += CloseGameMenu;
            if (_menuBtnSave is not null) _menuBtnSave.clicked += () => OpenGameMenuTab("Save");
            if (_menuBtnLoad is not null) _menuBtnLoad.clicked += () => OpenGameMenuTab("Load");
            if (_menuBtnSettings is not null) _menuBtnSettings.clicked += () => OpenGameMenuTab("Settings");
            if (_menuBtnHistory is not null) _menuBtnHistory.clicked += () => OpenGameMenuTab("History");
            if (_menuBtnHelp is not null) _menuBtnHelp.clicked += () => OpenGameMenuTab("Help");
            if (_menuBtnRestart is not null) _menuBtnRestart.clicked += RestartGameAction;
            if (_menuBtnQuit is not null) _menuBtnQuit.clicked += ExitGameAction;

            if (_pagePrevBtn is not null) _pagePrevBtn.clicked += PagePrev;
            if (_pageNextBtn is not null) _pageNextBtn.clicked += PageNext;

            for (int i = 0; i < _pageBtnList.Count; i++)
            {
                int pageNum = i + 1;
                _pageBtnList[i].clicked += () => SwitchPage(pageNum);
            }

            for (int i = 1; i <= 6; i++)
            {
                int slotIndex = i;
                var slotCard = _root.Q<Button>($"save-slot-{slotIndex}");
                if (slotCard is not null)
                {
                    slotCard.clicked += () => OnSaveSlotClicked(slotIndex);
                }
            }

            // Settings components bindings
            _fullscreenToggleBtn    = _root.Q<Button>("fullscreen-toggle-btn");
            _typewriterToggleBtn    = _root.Q<Button>("typewriter-toggle-btn");
            _fontSizeToggleBtn      = _root.Q<Button>("font-size-toggle-btn");
            _typewriterSpeedSlider  = _root.Q<Slider>("typewriter-speed-slider");
            _volumeSlider           = _root.Q<SliderInt>("volume-slider");
            _playerPortrait         = _root.Q<VisualElement>("player-portrait");
            _scenePlaceholder       = _root.Q<Label>("scene-placeholder");
            _splashScreen           = _root.Q<VisualElement>("splash-screen");

            // Query Game Over menu elements
            _gameOverMenu        = _root.Q<VisualElement>("game-over-menu");
            _gameOverMessage     = _root.Q<Label>("game-over-message");
            if (_gameOverMessage is not null) _gameOverMessage.enableRichText = true;
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

            _fontSizePref          = PlayerPrefs.GetString("Pref_FontSize", "Normal");
            UpdateFontSizeUI();

            if (_fullscreenToggleBtn is not null) _fullscreenToggleBtn.clicked += ToggleFullscreen;
            if (_typewriterToggleBtn is not null) _typewriterToggleBtn.clicked += ToggleTypewriter;
            if (_fontSizeToggleBtn is not null)   _fontSizeToggleBtn.clicked   += CycleFontSize;

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

            _roomPortrait = _root.Q<VisualElement>("room-portrait");
            if (_roomPortrait is not null)
            {
                _roomPortrait.pickingMode = PickingMode.Position;
                _roomPortrait.RegisterCallback<ClickEvent>(OnRoomTitleClicked);
            }

            if (_playerPortrait is not null)
            {
                _playerPortrait.pickingMode = PickingMode.Position;
                _playerPortrait.RegisterCallback<ClickEvent>(OnPlayerPortraitClicked);
            }


            if (_roomActionsContainer is not null)
            {
                _roomActionsContainer.RegisterCallback<ClickEvent>(evt => {
                    _pulseTween.Stop();
                    _roomActionsContainer.style.opacity = 1f;
                });
            }



            // Also add hover transitions for settings button
            if (_settingsBtn is not null)
            {
                RegisterHoverSwell(_settingsBtn);
            }

            SubscribeEvents();

            if (_splashScreen is not null)
            {
                SetGameplayUIVisible(false);
            }
        }

        private bool _firstRoomRendered = false;

        private void Start()
        {
            SetupVFXOverlay();
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
                StartCoroutine(PlaySplashScreenSequenceRoutine());
            }
        }
 
        private void InitTransitionEffects(
            SplashScreenSettingsData settings, 
            Label titleLabel, 
            ref Label cCyan, 
            ref Label cMagenta, 
            ref VisualElement cScanlines, 
            ref VisualElement pContainer, 
            List<UIParticle> particlesList)
        {
            if (_splashScreen == null) return;

            if (settings.TransitionStyle == "CRT")
            {
                cScanlines = new VisualElement();
                cScanlines.style.position = Position.Absolute;
                cScanlines.style.width = Length.Percent(100);
                cScanlines.style.height = Length.Percent(100);
                cScanlines.style.backgroundColor = new Color(0f, 0.2f, 0f, 0.08f); // Phosphor tint
                cScanlines.pickingMode = PickingMode.Ignore;
                _splashScreen.Add(cScanlines);
            }

            if (titleLabel != null && settings.TransitionStyle == "RGBSplit")
            {
                cCyan = new Label();
                cMagenta = new Label();
                
                cCyan.text = titleLabel.text;
                cCyan.style.position = Position.Absolute;
                cCyan.style.left = titleLabel.style.left;
                cCyan.style.top = titleLabel.style.top;
                cCyan.style.fontSize = titleLabel.style.fontSize;
                cCyan.style.unityTextAlign = titleLabel.style.unityTextAlign;
                cCyan.style.width = titleLabel.style.width;
                cCyan.style.height = titleLabel.style.height;
                cCyan.style.marginLeft = titleLabel.style.marginLeft;
                cCyan.style.marginTop = titleLabel.style.marginTop;
                cCyan.style.color = Color.cyan;
                cCyan.pickingMode = PickingMode.Ignore;
                
                cMagenta.text = titleLabel.text;
                cMagenta.style.position = Position.Absolute;
                cMagenta.style.left = titleLabel.style.left;
                cMagenta.style.top = titleLabel.style.top;
                cMagenta.style.fontSize = titleLabel.style.fontSize;
                cMagenta.style.unityTextAlign = titleLabel.style.unityTextAlign;
                cMagenta.style.width = titleLabel.style.width;
                cMagenta.style.height = titleLabel.style.height;
                cMagenta.style.marginLeft = titleLabel.style.marginLeft;
                cMagenta.style.marginTop = titleLabel.style.marginTop;
                cMagenta.style.color = Color.magenta;
                cMagenta.pickingMode = PickingMode.Ignore;

                _splashScreen.Add(cCyan);
                _splashScreen.Add(cMagenta);
            }

            Debug.Log($"[UIManager] InitTransitionEffects: TransitionStyle='{settings.TransitionStyle}', titleLabel.text='{titleLabel?.text}'");
            if (titleLabel != null && (
                settings.TransitionStyle == "ParticleSmoke" || 
                settings.TransitionStyle == "ParticleSand" || 
                settings.TransitionStyle == "ParticleEmbers" || 
                settings.TransitionStyle == "ParticleRain" || 
                settings.TransitionStyle == "ParticleSnow"))
            {
                titleLabel.style.opacity = 0f;
                if (TransitionVFXManager.Instance != null)
                {
                    float totalDuration = (float)(settings.FadeInDuration + settings.DisplayDuration);
                    Debug.Log($"[UIManager] Triggering 3D Particle effect: {settings.TransitionStyle} for {totalDuration}s");
                    TransitionVFXManager.Instance.PlayTransitionEffect(settings.TransitionStyle, totalDuration);
                }
                else
                {
                    Debug.LogWarning("[UIManager] InitTransitionEffects: TransitionVFXManager.Instance is NULL!");
                }
            }
        }

        private void CleanupTransitionEffects(
            Label splitCyan, 
            Label splitMagenta, 
            VisualElement crtScanlines, 
            VisualElement particleContainer)
        {
            if (_splashScreen == null) return;
            if (splitCyan != null) _splashScreen.Remove(splitCyan);
            if (splitMagenta != null) _splashScreen.Remove(splitMagenta);
            if (crtScanlines != null) _splashScreen.Remove(crtScanlines);
            if (particleContainer != null) _splashScreen.Remove(particleContainer);
            if (TransitionVFXManager.Instance != null)
            {
                TransitionVFXManager.Instance.StopAllTransitionEffects();
            }
        }

        private System.Collections.IEnumerator PlaySplashScreenSequenceRoutine()
        {
            var game = GameManager.Instance?.ActiveGame;
            var settings = game?.SplashScreen;

            // ── PART 1: PLAY ENGINE SPLASH SCREEN FIRST ──
            if (_splashScreen != null)
            {
                _splashScreen.style.display = DisplayStyle.Flex;
                _splashScreen.style.opacity = 1f;
                _splashScreen.style.alignItems = Align.Center;
                _splashScreen.style.justifyContent = Justify.Center;

                var badge = _splashScreen.Q<VisualElement>(className: "splash-badge");
                var title = _splashScreen.Q<Label>(className: "splash-title");
                var subt = _splashScreen.Q<Label>(className: "splash-subtitle");

                // Initialize standard engine logo state
                if (badge != null)
                {
                    badge.style.display = DisplayStyle.Flex;
                    badge.style.opacity = 0f;
                    badge.style.scale = new StyleScale(new Scale(new Vector3(0.4f, 0.4f, 1f)));
                }
                if (title != null)
                {
                    title.text = "RAGNEXT";
                    title.style.display = DisplayStyle.Flex;
                    title.style.position = Position.Relative;
                    title.style.opacity = 0f;
                    title.style.translate = new StyleTranslate(new Translate(0, 30));
                    // Reset styling in case custom changes it later
                    title.style.left = StyleKeyword.Null;
                    title.style.top = StyleKeyword.Null;
                    title.style.fontSize = StyleKeyword.Null;
                    title.style.color = StyleKeyword.Null;
                }
                if (subt != null)
                {
                    subt.style.display = DisplayStyle.Flex;
                    subt.style.opacity = 0f;
                    subt.style.translate = new StyleTranslate(new Translate(0, 20));
                }

                // Animate engine splash in
                float introDuration = 0.95f;
                float elapsed = 0f;
                while (elapsed < introDuration)
                {
                    elapsed += UnityEngine.Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / introDuration);
                    float springT = EasingSpring(t);

                    if (badge != null)
                    {
                        badge.style.opacity = t;
                        badge.style.scale = new StyleScale(new Scale(new Vector3(
                            Mathf.Lerp(0.4f, 1.0f, springT),
                            Mathf.Lerp(0.4f, 1.0f, springT),
                            1f
                        )));
                    }

                    if (title != null && t > 0.2f)
                    {
                        float titleT = Mathf.Clamp01((t - 0.2f) / 0.8f);
                        title.style.opacity = titleT;
                        title.style.translate = new StyleTranslate(new Translate(0, Mathf.Lerp(30f, 0f, titleT)));
                    }

                    if (subt != null && t > 0.4f)
                    {
                        float subtT = Mathf.Clamp01((t - 0.4f) / 0.6f);
                        subt.style.opacity = subtT;
                        subt.style.translate = new StyleTranslate(new Translate(0, Mathf.Lerp(20f, 0f, subtT)));
                    }

                    yield return null;
                }

                // Hold engine splash
                yield return new UnityEngine.WaitForSeconds(1.0f);

                // Fade out engine splash elements
                float fadeOutEngineDuration = 0.4f;
                float elapsedOut = 0f;
                while (elapsedOut < fadeOutEngineDuration)
                {
                    elapsedOut += UnityEngine.Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedOut / fadeOutEngineDuration);
                    float opp = Mathf.Lerp(1f, 0f, t);
                    
                    if (badge != null) badge.style.opacity = opp;
                    if (title != null) title.style.opacity = opp;
                    if (subt != null) subt.style.opacity = opp;

                    yield return null;
                }

                // Hide engine components
                if (badge != null) badge.style.display = DisplayStyle.None;
                if (subt != null) subt.style.display = DisplayStyle.None;
            }

            // ── PART 2: TRANSITION TO CUSTOM SPLASH IF ENABLED ──
            while (GameManager.Instance == null || GameManager.Instance.ActiveGame == null)
            {
                yield return null;
            }

            game = GameManager.Instance.ActiveGame;
            settings = game?.SplashScreen;

            if (game == null || settings == null || !settings.Enabled)
            {
                // Just fade out container completely and finish
                float fadeContainerDuration = 0.4f;
                float elapsedOut = 0f;
                while (elapsedOut < fadeContainerDuration)
                {
                    elapsedOut += UnityEngine.Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedOut / fadeContainerDuration);
                    if (_splashScreen != null)
                    {
                        _splashScreen.style.opacity = Mathf.Lerp(1f, 0f, t);
                    }
                    yield return null;
                }

                if (_splashScreen != null)
                {
                    _splashScreen.style.display = DisplayStyle.None;
                }
                SetGameplayUIVisible(true);
                IsSplashFinished = true;
                if (GameManager.Instance?.CurrentRoom != null)
                {
                    TriggerRoomAmbientEffects(GameManager.Instance.CurrentRoom);
                }
                yield break;
            }

            // Setup Custom Splash settings
            if (_splashScreen != null)
            {
                _splashScreen.style.opacity = 0f;
                _splashScreen.style.alignItems = Align.FlexStart;
                _splashScreen.style.justifyContent = Justify.FlexStart;
            }

            var titleLabel = _splashScreen?.Q<Label>(className: "splash-title");

            Label splitCyan = null;
            Label splitMagenta = null;
            VisualElement crtScanlines = null;
            VisualElement particleContainer = null;
            List<UIParticle> particles = new List<UIParticle>();

            if (settings.Mode == "Video")
            {
                // ── VIDEO PLAYBACK MODE ──
                var asset = game.MediaAssets.Find(a => 
                    string.Equals(a.Id, settings.VideoAssetId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.OriginalFileName, settings.VideoAssetId, StringComparison.OrdinalIgnoreCase));
                
                if (asset != null)
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
                            _videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
                    }

                    _videoPlayer.playOnAwake = false;
                    _videoPlayer.isLooping = false;
                    _videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
                    _videoPlayer.targetTexture = _videoTexture;

                    string url = FormatLocalPathForWeb(asset.RelativePath);
                    if (url.StartsWith("file://")) _videoPlayer.url = new Uri(url).LocalPath;
                    else _videoPlayer.url = url;

                    _splashScreen.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoTexture));
                    
                    if (titleLabel != null)
                    {
                        titleLabel.text = settings.Text;
                        titleLabel.style.display = DisplayStyle.Flex;
                        titleLabel.style.position = Position.Absolute;
                        titleLabel.style.opacity = 1f;
                        titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                        titleLabel.style.left = Length.Percent((float)settings.TextX);
                        titleLabel.style.top = Length.Percent((float)settings.TextY);
                        titleLabel.style.width = 2000f;
                        titleLabel.style.height = 200f;
                        titleLabel.style.marginLeft = -1000f;
                        titleLabel.style.marginTop = -100f;
                        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                        titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;

                        if (double.TryParse(settings.FontSize.ToString(), out var sizeVal))
                        {
                            titleLabel.style.fontSize = (float)sizeVal * 2.75f;
                        }

                        string fontColorHex = settings.FontColor;
                        if (fontColorHex != null && fontColorHex.StartsWith("#") && fontColorHex.Length == 9)
                        {
                            fontColorHex = "#" + fontColorHex.Substring(3, 6) + fontColorHex.Substring(1, 2);
                        }
                        if (ColorUtility.TryParseHtmlString(fontColorHex, out var clr))
                        {
                            titleLabel.style.color = clr;
                        }
                        else
                        {
                            titleLabel.style.color = Color.white;
                        }
                    }

                    // Handle Rise / Cinematic / Glitch / Exposure setup for Video Mode
                    if (titleLabel != null)
                    {
                        titleLabel.style.marginLeft = -1000f;
                        titleLabel.style.marginTop = -100f;
                        if (settings.TransitionStyle == "Rise")
                        {
                            titleLabel.style.translate = new StyleTranslate(new Translate(0, 60f));
                        }
                        else if (settings.TransitionStyle == "Cinematic")
                        {
                            if (_splashScreen != null)
                                _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                        }
                        else if (settings.TransitionStyle == "Exposure")
                        {
                            titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1.5f, 1.5f, 1f)));
                        }
                    }

                    InitTransitionEffects(settings, titleLabel, ref splitCyan, ref splitMagenta, ref crtScanlines, ref particleContainer, particles);
                    _splashScreen.style.opacity = 0f;
                    if (titleLabel != null) titleLabel.style.opacity = 0f;
                    _videoPlayer.Play();

                    // Smooth cinematic Fade In over FadeInDuration
                    float elapsedIn = 0f;
                    float fadeInDuration = (float)settings.FadeInDuration;
                    if (fadeInDuration < 0.1f) fadeInDuration = 0.1f;
                    while (elapsedIn < fadeInDuration)
                    {
                        elapsedIn += UnityEngine.Time.deltaTime;
                        float t = Mathf.Clamp01(elapsedIn / fadeInDuration);
                        if (_splashScreen != null)
                        {
                            if (settings.TransitionStyle == "Exposure")
                            {
                                float expT = Mathf.Pow(t, 0.4f);
                                _splashScreen.style.opacity = expT;
                            }
                            else
                            {
                                _splashScreen.style.opacity = t;
                            }

                            if (settings.TransitionStyle == "Cinematic")
                            {
                                float curScale = Mathf.Lerp(1f, 1.08f, t);
                                _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(curScale, curScale, 1f)));
                            }
                            else if (settings.TransitionStyle == "SoundReactive")
                            {
                                float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                                if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                                float pulse = 1f + loudness * 0.5f;
                                _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                            }
                        }

                        if (particles.Count > 0)
                        {
                            foreach (var p in particles)
                            {
                                float curX = Mathf.Lerp(p.startPos.x, p.targetPos.x, t);
                                float curY = Mathf.Lerp(p.startPos.y, p.targetPos.y, t);
                                p.element.style.left = curX;
                                p.element.style.top = curY;
                                p.element.style.opacity = t * p.alpha;
                            }
                        }

                        if (titleLabel != null)
                        {
                            if (settings.TransitionStyle == "Rise")
                            {
                                float yOffset = Mathf.Lerp(60f, 0f, EasingSpring(t));
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, yOffset));
                            }
                            else if (settings.TransitionStyle == "Exposure")
                            {
                                float scaleVal = Mathf.Lerp(1.5f, 1f, t);
                                titleLabel.style.scale = new StyleScale(new Scale(new Vector3(scaleVal, scaleVal, 1f)));
                            }
                            else if (settings.TransitionStyle == "Glitch")
                            {
                                if (UnityEngine.Random.value < 0.15f)
                                {
                                    titleLabel.style.opacity = UnityEngine.Random.Range(0.2f, 0.7f);
                                    titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-5f, 5f)));
                                }
                                else
                                {
                                    titleLabel.style.opacity = t;
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                }
                            }
                            else if (settings.TransitionStyle == "CRT")
                            {
                                if (UnityEngine.Random.value < 0.1f)
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, UnityEngine.Random.Range(-4f, 4f)));
                                }
                                else
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                }
                                titleLabel.style.opacity = t;
                            }
                            else if (settings.TransitionStyle == "RGBSplit")
                            {
                                titleLabel.style.opacity = t;
                                float offset = 18f * Mathf.Sin(t * Mathf.PI * 4);
                                if (UnityEngine.Random.value < 0.2f)
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-6f, 6f)));
                                    titleLabel.style.opacity = UnityEngine.Random.Range(0.5f, 1.0f);
                                }
                                else
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                }
                                if (splitCyan != null)
                                {
                                    splitCyan.style.translate = new StyleTranslate(new Translate(offset, UnityEngine.Random.Range(-2f, 2f)));
                                    splitCyan.style.opacity = t;
                                }
                                if (splitMagenta != null)
                                {
                                    splitMagenta.style.translate = new StyleTranslate(new Translate(-offset, UnityEngine.Random.Range(-2f, 2f)));
                                    splitMagenta.style.opacity = t;
                                }
                            }
                            else if (settings.TransitionStyle == "SoundReactive")
                            {
                                float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                                if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                                float pulse = 1f + loudness * 0.5f;
                                titleLabel.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                                titleLabel.style.opacity = t;
                            }
                            else if (settings.TransitionStyle == "ParticleSmoke" || settings.TransitionStyle == "ParticleSand")
                            {
                                titleLabel.style.opacity = t > 0.7f ? (t - 0.7f) / 0.3f : 0f;
                            }
                            else
                            {
                                titleLabel.style.opacity = t;
                            }
                        }
                        yield return null;
                    }
                    if (_splashScreen != null) _splashScreen.style.opacity = 1f;
                    if (titleLabel != null)
                    {
                        titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                        titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                        titleLabel.style.opacity = 1f;
                    }

                    float displayDuration = (float)settings.DisplayDuration;
                    if (displayDuration < 0.1f) displayDuration = 0.1f;
                    float elapsedDisplay = 0f;
                    while (elapsedDisplay < displayDuration)
                    {
                        elapsedDisplay += UnityEngine.Time.deltaTime;
                        float progress = elapsedDisplay / displayDuration;

                        if (_splashScreen != null)
                        {
                            if (settings.TransitionStyle == "Cinematic")
                            {
                                float curScale = Mathf.Lerp(1.08f, 1.20f, progress);
                                _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(curScale, curScale, 1f)));
                            }
                            else if (settings.TransitionStyle == "SoundReactive")
                            {
                                float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                                if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                                float pulse = 1f + loudness * 0.5f;
                                _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                            }
                        }

                        if (particles.Count > 0)
                        {
                            foreach (var p in particles)
                            {
                                float curX = p.targetPos.x + Mathf.Sin(elapsedDisplay + p.wiggleSeed) * 4f;
                                float curY = p.targetPos.y + Mathf.Cos(elapsedDisplay + p.wiggleSeed) * 3f;
                                p.element.style.left = curX;
                                p.element.style.top = curY;
                                p.element.style.opacity = (1f - progress) * p.alpha;
                            }
                        }

                        if (titleLabel != null)
                        {
                            if (settings.TransitionStyle == "Glitch")
                            {
                                if (UnityEngine.Random.value < 0.08f)
                                {
                                    titleLabel.style.opacity = UnityEngine.Random.Range(0.3f, 0.9f);
                                    titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(-8f, 8f)));
                                }
                                else
                                {
                                    titleLabel.style.opacity = 1f;
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                }
                            }
                            else if (settings.TransitionStyle == "CRT")
                            {
                                if (UnityEngine.Random.value < 0.08f)
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, UnityEngine.Random.Range(-3f, 3f)));
                                    titleLabel.style.opacity = UnityEngine.Random.Range(0.6f, 1.0f);
                                }
                                else
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                    titleLabel.style.opacity = 1f;
                                }
                            }
                            else if (settings.TransitionStyle == "RGBSplit")
                            {
                                float offset = 6f * Mathf.Sin(progress * Mathf.PI * 10);
                                if (UnityEngine.Random.value < 0.15f)
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-16f, 16f), UnityEngine.Random.Range(-8f, 8f)));
                                    titleLabel.style.opacity = UnityEngine.Random.Range(0.4f, 1.0f);
                                }
                                else
                                {
                                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                    titleLabel.style.opacity = 1f;
                                }
                                if (splitCyan != null)
                                {
                                    splitCyan.style.translate = new StyleTranslate(new Translate(offset, UnityEngine.Random.Range(-3f, 3f)));
                                    splitCyan.style.opacity = 1f;
                                }
                                if (splitMagenta != null)
                                {
                                    splitMagenta.style.translate = new StyleTranslate(new Translate(-offset, UnityEngine.Random.Range(-3f, 3f)));
                                    splitMagenta.style.opacity = 1f;
                                }
                            }
                            else if (settings.TransitionStyle == "SoundReactive")
                            {
                                float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                                if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                                float pulse = 1f + loudness * 0.5f;
                                titleLabel.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                                titleLabel.style.opacity = 1f;
                            }
                            else if (settings.TransitionStyle == "ParticleSmoke" || settings.TransitionStyle == "ParticleSand")
                            {
                                titleLabel.style.opacity = 1f;
                            }
                        }
                        yield return null;
                    }
                }
            }
            else
            {
                // ── IMAGE & TEXT CUSTOM MODE ──
                var asset = game.MediaAssets.Find(a => 
                    string.Equals(a.Id, settings.ImageAssetId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.OriginalFileName, settings.ImageAssetId, StringComparison.OrdinalIgnoreCase));
                
                if (asset != null)
                {
                    yield return StartCoroutine(LoadImageCoroutine(asset.RelativePath, "splash-screen"));
                }
                else
                {
                    if (_splashScreen != null)
                    {
                        _splashScreen.style.backgroundImage = null;
                        _splashScreen.style.backgroundColor = Color.black;
                    }
                }

                if (titleLabel != null)
                {
                    titleLabel.text = settings.Text;
                    titleLabel.style.display = DisplayStyle.Flex;
                    titleLabel.style.position = Position.Absolute;
                    titleLabel.style.opacity = 1f;
                    titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                    titleLabel.style.left = Length.Percent((float)settings.TextX);
                    titleLabel.style.top = Length.Percent((float)settings.TextY);
                    titleLabel.style.width = 2000f;
                    titleLabel.style.height = 200f;
                    titleLabel.style.marginLeft = -1000f;
                    titleLabel.style.marginTop = -100f;
                    titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                    titleLabel.style.whiteSpace = WhiteSpace.NoWrap;

                    if (double.TryParse(settings.FontSize.ToString(), out var sizeVal))
                    {
                        titleLabel.style.fontSize = (float)sizeVal * 2.75f;
                    }

                    string fontColorHex = settings.FontColor;
                    if (fontColorHex != null && fontColorHex.StartsWith("#") && fontColorHex.Length == 9)
                    {
                        fontColorHex = "#" + fontColorHex.Substring(3, 6) + fontColorHex.Substring(1, 2);
                    }
                    if (ColorUtility.TryParseHtmlString(fontColorHex, out var clr))
                    {
                        titleLabel.style.color = clr;
                    }
                    else
                    {
                        titleLabel.style.color = Color.white;
                    }
                }

                if (!string.IsNullOrEmpty(settings.SoundAssetId) && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound(settings.SoundAssetId, 1f, false);
                }

                InitTransitionEffects(settings, titleLabel, ref splitCyan, ref splitMagenta, ref crtScanlines, ref particleContainer, particles);

                float elapsed = 0f;
                float fadeInDuration = (float)settings.FadeInDuration;
                if (fadeInDuration < 0.1f) fadeInDuration = 0.1f;

                if (titleLabel != null)
                {
                    if (settings.TransitionStyle == "Rise")
                    {
                        titleLabel.style.translate = new StyleTranslate(new Translate(0, 60));
                    }
                    else if (settings.TransitionStyle == "Cinematic")
                    {
                        if (_splashScreen != null)
                            _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                    }
                    else if (settings.TransitionStyle == "Exposure")
                    {
                        titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1.5f, 1.5f, 1f)));
                    }
                }

                while (elapsed < fadeInDuration)
                {
                    elapsed += UnityEngine.Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeInDuration);
                    if (_splashScreen != null)
                    {
                        if (settings.TransitionStyle == "Exposure")
                        {
                            float expT = Mathf.Pow(t, 0.4f);
                            _splashScreen.style.opacity = expT;
                        }
                        else
                        {
                            _splashScreen.style.opacity = t;
                        }

                        if (settings.TransitionStyle == "Cinematic")
                        {
                            float curScale = Mathf.Lerp(1f, 1.08f, t);
                            _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(curScale, curScale, 1f)));
                        }
                        else if (settings.TransitionStyle == "SoundReactive")
                        {
                            float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                            if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                            float pulse = 1f + loudness * 0.5f;
                            _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                        }
                    }

                    if (particles.Count > 0)
                    {
                        foreach (var p in particles)
                        {
                            float curX = Mathf.Lerp(p.startPos.x, p.targetPos.x, t);
                            float curY = Mathf.Lerp(p.startPos.y, p.targetPos.y, t);
                            p.element.style.left = curX;
                            p.element.style.top = curY;
                            p.element.style.opacity = t * p.alpha;
                        }
                    }

                    if (titleLabel != null)
                    {
                        if (settings.TransitionStyle == "Rise")
                        {
                            float yOffset = Mathf.Lerp(60f, 0f, EasingSpring(t));
                            titleLabel.style.translate = new StyleTranslate(new Translate(0, yOffset));
                        }
                        else if (settings.TransitionStyle == "Exposure")
                        {
                            float scaleVal = Mathf.Lerp(1.5f, 1f, t);
                            titleLabel.style.scale = new StyleScale(new Scale(new Vector3(scaleVal, scaleVal, 1f)));
                        }
                        else if (settings.TransitionStyle == "Glitch")
                        {
                            if (UnityEngine.Random.value < 0.15f)
                            {
                                titleLabel.style.opacity = UnityEngine.Random.Range(0.2f, 0.7f);
                                titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-5f, 5f)));
                            }
                            else
                            {
                                titleLabel.style.opacity = t;
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                            }
                        }
                        else if (settings.TransitionStyle == "CRT")
                        {
                            if (UnityEngine.Random.value < 0.1f)
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, UnityEngine.Random.Range(-4f, 4f)));
                            }
                            else
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                            }
                            titleLabel.style.opacity = t;
                        }
                        else if (settings.TransitionStyle == "RGBSplit")
                        {
                            titleLabel.style.opacity = t;
                            float offset = 18f * Mathf.Sin(t * Mathf.PI * 4);
                            if (UnityEngine.Random.value < 0.2f)
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-6f, 6f)));
                                titleLabel.style.opacity = UnityEngine.Random.Range(0.5f, 1.0f);
                            }
                            else
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                            }
                            if (splitCyan != null)
                            {
                                splitCyan.style.translate = new StyleTranslate(new Translate(offset, UnityEngine.Random.Range(-2f, 2f)));
                                splitCyan.style.opacity = t;
                            }
                            if (splitMagenta != null)
                            {
                                splitMagenta.style.translate = new StyleTranslate(new Translate(-offset, UnityEngine.Random.Range(-2f, 2f)));
                                splitMagenta.style.opacity = t;
                            }
                        }
                        else if (settings.TransitionStyle == "SoundReactive")
                        {
                            float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                            if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                            float pulse = 1f + loudness * 0.5f;
                            titleLabel.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                            titleLabel.style.opacity = t;
                        }
                        else if (settings.TransitionStyle == "ParticleSmoke" || settings.TransitionStyle == "ParticleSand")
                        {
                            titleLabel.style.opacity = t > 0.7f ? (t - 0.7f) / 0.3f : 0f;
                        }
                        else
                        {
                            titleLabel.style.opacity = t;
                        }
                    }
                    yield return null;
                }

                if (_splashScreen != null)
                {
                    _splashScreen.style.opacity = 1f;
                }
                if (titleLabel != null)
                {
                    titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                    titleLabel.style.scale = new StyleScale(new Scale(new Vector3(1f, 1f, 1f)));
                    titleLabel.style.opacity = 1f;
                }

                float holdDuration = (float)settings.DisplayDuration;
                float elapsedHold = 0f;
                while (elapsedHold < holdDuration)
                {
                    elapsedHold += UnityEngine.Time.deltaTime;
                    float progress = elapsedHold / holdDuration;

                    if (_splashScreen != null)
                    {
                        if (settings.TransitionStyle == "Cinematic")
                        {
                            float curScale = Mathf.Lerp(1.08f, 1.20f, progress);
                            _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(curScale, curScale, 1f)));
                        }
                        else if (settings.TransitionStyle == "SoundReactive")
                        {
                            float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                            if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                            float pulse = 1f + loudness * 0.5f;
                            _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                        }
                    }

                    if (particles.Count > 0)
                    {
                        foreach (var p in particles)
                        {
                            float curX = p.targetPos.x + Mathf.Sin(elapsedHold + p.wiggleSeed) * 4f;
                            float curY = p.targetPos.y + Mathf.Cos(elapsedHold + p.wiggleSeed) * 3f;
                            p.element.style.left = curX;
                            p.element.style.top = curY;
                            p.element.style.opacity = (1f - progress) * p.alpha;
                        }
                    }

                    if (titleLabel != null)
                    {
                        if (settings.TransitionStyle == "Glitch")
                        {
                            if (UnityEngine.Random.value < 0.08f)
                            {
                                titleLabel.style.opacity = UnityEngine.Random.Range(0.3f, 0.9f);
                                titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(-8f, 8f)));
                            }
                            else
                            {
                                titleLabel.style.opacity = 1f;
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                            }
                        }
                        else if (settings.TransitionStyle == "CRT")
                        {
                            if (UnityEngine.Random.value < 0.08f)
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, UnityEngine.Random.Range(-3f, 3f)));
                                titleLabel.style.opacity = UnityEngine.Random.Range(0.6f, 1.0f);
                            }
                            else
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                titleLabel.style.opacity = 1f;
                            }
                        }
                        else if (settings.TransitionStyle == "RGBSplit")
                        {
                            float offset = 6f * Mathf.Sin(progress * Mathf.PI * 10);
                            if (UnityEngine.Random.value < 0.15f)
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(UnityEngine.Random.Range(-16f, 16f), UnityEngine.Random.Range(-8f, 8f)));
                                titleLabel.style.opacity = UnityEngine.Random.Range(0.4f, 1.0f);
                            }
                            else
                            {
                                titleLabel.style.translate = new StyleTranslate(new Translate(0, 0));
                                titleLabel.style.opacity = 1f;
                            }
                            if (splitCyan != null)
                            {
                                splitCyan.style.translate = new StyleTranslate(new Translate(offset, UnityEngine.Random.Range(-3f, 3f)));
                                splitCyan.style.opacity = 1f;
                            }
                            if (splitMagenta != null)
                            {
                                splitMagenta.style.translate = new StyleTranslate(new Translate(-offset, UnityEngine.Random.Range(-3f, 3f)));
                                splitMagenta.style.opacity = 1f;
                            }
                        }
                        else if (settings.TransitionStyle == "SoundReactive")
                        {
                            float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                            if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                            float pulse = 1f + loudness * 0.5f;
                            titleLabel.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                            titleLabel.style.opacity = 1f;
                        }
                        else if (settings.TransitionStyle == "ParticleSmoke" || settings.TransitionStyle == "ParticleSand")
                        {
                            titleLabel.style.opacity = 1f;
                        }
                    }
                    yield return null;
                }
            }

            // Cinematic fade-out of custom overlay
            float elapsedOutCustom = 0f;
            float fadeOutDuration = (float)settings.FadeOutDuration;
            if (fadeOutDuration < 0.1f) fadeOutDuration = 0.1f;

            while (elapsedOutCustom < fadeOutDuration)
            {
                elapsedOutCustom += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsedOutCustom / fadeOutDuration);
                if (_splashScreen != null)
                {
                    _splashScreen.style.opacity = Mathf.Lerp(1f, 0f, t);
                    if (settings != null && settings.TransitionStyle == "Cinematic")
                    {
                        float curScale = Mathf.Lerp(1.20f, 1.25f, t);
                        _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(curScale, curScale, 1f)));
                    }
                    else if (settings != null && settings.TransitionStyle == "SoundReactive")
                    {
                        float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                        if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                        float pulse = (1f + loudness * 0.5f) * (1f - t);
                        _splashScreen.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                    }
                }

                if (particles.Count > 0)
                {
                    float speed = (settings.TransitionStyle == "ParticleSmoke") ? 150f : 250f;
                    foreach (var p in particles)
                    {
                        float curX = p.targetPos.x + (Mathf.Sin(t * 5f + p.wiggleSeed) * 50f * t);
                        float curY = p.targetPos.y - (speed * t);
                        p.element.style.left = curX;
                        p.element.style.top = curY;
                        p.element.style.opacity = (1f - t) * p.alpha;
                    }
                }

                if (titleLabel != null)
                {
                    if (settings.TransitionStyle == "CRT")
                    {
                        if (UnityEngine.Random.value < 0.1f)
                        {
                            titleLabel.style.translate = new StyleTranslate(new Translate(0, UnityEngine.Random.Range(-4f, 4f)));
                        }
                    }
                    else if (settings.TransitionStyle == "RGBSplit")
                    {
                        float offset = 10f * Mathf.Sin(t * Mathf.PI * 4) * (1f - t);
                        if (splitCyan != null)
                        {
                            splitCyan.style.translate = new StyleTranslate(new Translate(offset, 0));
                            splitCyan.style.opacity = 1f - t;
                        }
                        if (splitMagenta != null)
                        {
                            splitMagenta.style.translate = new StyleTranslate(new Translate(-offset, 0));
                            splitMagenta.style.opacity = 1f - t;
                        }
                    }
                    else if (settings.TransitionStyle == "SoundReactive")
                    {
                        float loudness = AudioManager.Instance != null ? AudioManager.Instance.GetLoudnessOfSound(settings.SoundAssetId) : 0f;
                        if (loudness < 0.001f) loudness = Mathf.Abs(Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f)) * 0.2f;
                        float pulse = (1f + loudness * 0.5f) * (1f - t);
                        titleLabel.style.scale = new StyleScale(new Scale(new Vector3(pulse, pulse, 1f)));
                        titleLabel.style.opacity = 1f - t;
                    }
                }
                yield return null;
            }

            if (_splashScreen != null)
            {
                _splashScreen.style.display = DisplayStyle.None;
            }

            // Clean up temporary effects elements
            CleanupTransitionEffects(splitCyan, splitMagenta, crtScanlines, particleContainer);

            // Bug #4: Stop video AFTER the fade-out so transitions play over the live video.
            if (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
            }

            // Stop splash screen audio when done
            if (settings != null && !string.IsNullOrEmpty(settings.SoundAssetId) && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopSound(settings.SoundAssetId);
            }

            SetGameplayUIVisible(true);
            IsSplashFinished = true;
            if (GameManager.Instance?.CurrentRoom != null)
            {
                TriggerRoomAmbientEffects(GameManager.Instance.CurrentRoom);
            }
        }

        private float EasingSpring(float t)
        {
            float c4 = (2f * Mathf.PI) / 3f;
            return t == 0f ? 0f : t == 1f ? 1f : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        private System.Collections.IEnumerator FadeOutSplashScreenRoutine()
        {
            var badge = _splashScreen?.Q<VisualElement>(className: "splash-badge");
            var title = _splashScreen?.Q<Label>(className: "splash-title");
            var subt = _splashScreen?.Q<Label>(className: "splash-subtitle");

            if (badge != null)
            {
                badge.style.opacity = 0f;
                badge.style.scale = new StyleScale(new Scale(new Vector3(0.4f, 0.4f, 1f)));
            }
            if (title != null)
            {
                title.style.opacity = 0f;
                title.style.translate = new StyleTranslate(new Translate(0, 30));
            }
            if (subt != null)
            {
                subt.style.opacity = 0f;
                subt.style.translate = new StyleTranslate(new Translate(0, 20));
            }

            if (_splashScreen != null)
            {
                _splashScreen.style.opacity = 1f;
            }

            float introDuration = 0.95f;
            float elapsed = 0f;
            while (elapsed < introDuration)
            {
                elapsed += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / introDuration);
                float springT = EasingSpring(t);

                if (badge != null)
                {
                    badge.style.opacity = t;
                    badge.style.scale = new StyleScale(new Scale(new Vector3(
                        Mathf.Lerp(0.4f, 1.0f, springT),
                        Mathf.Lerp(0.4f, 1.0f, springT),
                        1f
                    )));
                }

                if (title != null && t > 0.2f)
                {
                    float titleT = Mathf.Clamp01((t - 0.2f) / 0.8f);
                    title.style.opacity = titleT;
                    title.style.translate = new StyleTranslate(new Translate(0, Mathf.Lerp(30f, 0f, titleT)));
                }

                if (subt != null && t > 0.4f)
                {
                    float subtT = Mathf.Clamp01((t - 0.4f) / 0.6f);
                    subt.style.opacity = subtT;
                    subt.style.translate = new StyleTranslate(new Translate(0, Mathf.Lerp(20f, 0f, subtT)));
                }

                yield return null;
            }

            yield return new UnityEngine.WaitForSeconds(1.0f);

            float fadeOutDuration = 0.6f;
            float elapsedOut = 0f;
            while (elapsedOut < fadeOutDuration)
            {
                elapsedOut += UnityEngine.Time.deltaTime;
                float t = Mathf.Clamp01(elapsedOut / fadeOutDuration);
                if (_splashScreen != null)
                {
                    _splashScreen.style.opacity = Mathf.Lerp(1f, 0f, t);
                }

                if (badge != null)
                {
                    badge.style.scale = new StyleScale(new Scale(new Vector3(
                        Mathf.Lerp(1.0f, 0.85f, t),
                        Mathf.Lerp(1.0f, 0.85f, t),
                        1f
                    )));
                }
                yield return null;
            }

            if (_splashScreen != null)
            {
                _splashScreen.style.display = DisplayStyle.None;
            }
        }

        private void OnDisable()
        {
            if (_pulseTween.isAlive)
            {
                _pulseTween.Stop();
            }

            UnsubscribeEvents();

            if (_fullscreenToggleBtn is not null) _fullscreenToggleBtn.clicked -= ToggleFullscreen;
            if (_typewriterToggleBtn is not null) _typewriterToggleBtn.clicked -= ToggleTypewriter;
            if (_fontSizeToggleBtn is not null)   _fontSizeToggleBtn.clicked   -= CycleFontSize;

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

            if (_roomPortrait is not null)
            {
                _roomPortrait.UnregisterCallback<ClickEvent>(OnRoomTitleClicked);
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

            // Apply customized viewport border
            var rootEl = _root?.Q<VisualElement>("root");
            if (rootEl is not null && game.SplashScreen is not null)
            {
                float borderWidth = (float)game.SplashScreen.BorderWidth;
                float borderRadius = (float)game.SplashScreen.BorderRadius;
                
                rootEl.style.borderLeftWidth = borderWidth;
                rootEl.style.borderRightWidth = borderWidth;
                rootEl.style.borderTopWidth = borderWidth;
                rootEl.style.borderBottomWidth = borderWidth;
                
                rootEl.style.borderTopLeftRadius = borderRadius;
                rootEl.style.borderTopRightRadius = borderRadius;
                rootEl.style.borderBottomLeftRadius = borderRadius;
                rootEl.style.borderBottomRightRadius = borderRadius;
                
                string hexColor = game.SplashScreen.BorderColor;
                if (hexColor != null && hexColor.StartsWith("#") && hexColor.Length == 9)
                {
                    hexColor = "#" + hexColor.Substring(3, 6) + hexColor.Substring(1, 2);
                }
                if (ColorUtility.TryParseHtmlString(hexColor, out var clr))
                {
                    rootEl.style.borderLeftColor = clr;
                    rootEl.style.borderRightColor = clr;
                    rootEl.style.borderTopColor = clr;
                    rootEl.style.borderBottomColor = clr;
                }
            }

            // Clear narrative history on game load to restore pristine log state
            _narrativeScroll?.Clear();
            _firstRoomRendered = false;

            RefreshPlayerPanel();
            RefreshPlayerPortrait();
        }


        private void OnRoomEntered(RoomData room)
        {
            RenderRoom(room);
            _firstRoomRendered = true;
        }


        // ── Public Interface (called by CommandEffectRouter) ──────────────────

        public void RenderRoom(RoomData room)
        {
            if (room is null) return;

            SetupVFXOverlay();

            if (IsSplashFinished)
            {
                TriggerRoomAmbientEffects(room);
            }

            if (_firstRoomRendered)
            {
                PrepareForNewAction();
            }

            // Room title
            if (_roomTitleLabel is not null)
            {
                var game = GameManager.Instance?.ActiveGame;
                _roomTitleLabel.text = game is not null
                    ? TemplateResolver.Resolve(room.Name, game, room, room)
                    : room.Name;
            }

            // Room portrait (circle next to room name)
            if (_roomPortrait is not null)
            {
                if (!string.IsNullOrWhiteSpace(room.PortraitImagePath))
                {
                    _roomPortrait.style.display = DisplayStyle.Flex;
                    LoadAndDisplayImage(room.PortraitImagePath, "room-portrait");
                }
                else
                {
                    _roomPortrait.style.backgroundImage = null;
                    _roomPortrait.style.display = DisplayStyle.None;
                }
            }

            // Revamped floating room actions thumbnail
            if (_roomActionThumbnail is not null)
            {
                if (!string.IsNullOrWhiteSpace(room.PortraitImagePath))
                {
                    _roomActionThumbnail.style.display = DisplayStyle.Flex;
                    LoadAndDisplayImage(room.PortraitImagePath, "room-action-thumbnail");
                }
                else
                {
                    _roomActionThumbnail.style.backgroundImage = null;
                }
            }

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

            // Pulse room-actions-container if active actions are available
            UpdateRoomActionsPulse(room);
        }

        private void TriggerRoomAmbientEffects(RoomData room)
        {
            if (TransitionVFXManager.Instance == null) return;

            if (room.Attributes != null)
            {
                if (room.Attributes.TryGetValue("Weather", out var weatherVal))
                {
                    Debug.Log($"[UIManager] Setting Weather Overlay: '{weatherVal}'");
                    TransitionVFXManager.Instance.SetAmbientOverlay("Embers", weatherVal.Equals("Embers", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Rain", weatherVal.Equals("Rain", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Snow", weatherVal.Equals("Snow", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Sand", weatherVal.Equals("Sand", StringComparison.OrdinalIgnoreCase) || weatherVal.Equals("Sandstorm", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Smoke", weatherVal.Equals("Smoke", StringComparison.OrdinalIgnoreCase));
                }
                else if (room.Attributes.TryGetValue("Atmosphere", out var atmosVal))
                {
                    Debug.Log($"[UIManager] Setting Atmosphere Overlay: '{atmosVal}'");
                    TransitionVFXManager.Instance.SetAmbientOverlay("Embers", atmosVal.Equals("Embers", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Rain", atmosVal.Equals("Rain", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Snow", atmosVal.Equals("Snow", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Sand", atmosVal.Equals("Sand", StringComparison.OrdinalIgnoreCase) || atmosVal.Equals("Sandstorm", StringComparison.OrdinalIgnoreCase));
                    TransitionVFXManager.Instance.SetAmbientOverlay("Smoke", atmosVal.Equals("Smoke", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    TransitionVFXManager.Instance.SetAmbientOverlay("Embers", false);
                    TransitionVFXManager.Instance.SetAmbientOverlay("Rain", false);
                    TransitionVFXManager.Instance.SetAmbientOverlay("Snow", false);
                    TransitionVFXManager.Instance.SetAmbientOverlay("Sand", false);
                    TransitionVFXManager.Instance.SetAmbientOverlay("Smoke", false);
                }

                if (room.Attributes.TryGetValue("Shake", out var shakeVal) && float.TryParse(shakeVal, out var shakeInt))
                {
                    Debug.Log($"[UIManager] Triggering Screen Shake: {shakeInt}");
                    TransitionVFXManager.Instance.TriggerScreenShake(shakeInt, 1.0f);
                }
            }
            else
            {
                TransitionVFXManager.Instance.SetAmbientOverlay("Embers", false);
                TransitionVFXManager.Instance.SetAmbientOverlay("Rain", false);
                TransitionVFXManager.Instance.SetAmbientOverlay("Snow", false);
                TransitionVFXManager.Instance.SetAmbientOverlay("Sand", false);
                TransitionVFXManager.Instance.SetAmbientOverlay("Smoke", false);
            }
        }

        private void UpdateRoomActionsPulse(RoomData room)
        {
            if (_pulseTween.isAlive)
            {
                _pulseTween.Stop();
            }

            if (_roomActionThumbnailWrapper is null && _roomActionsContainer is null) return;

            // Reset style states
            if (_roomActionsContainer is not null) _roomActionsContainer.style.opacity = 1f;
            if (_roomActionThumbnailWrapper is not null)
            {
                _roomActionThumbnailWrapper.RemoveFromClassList("room-action-thumbnail--pulse");
                _roomActionThumbnailWrapper.style.borderLeftColor = new Color(0f, 188/255f, 212/255f, 0.4f);
                _roomActionThumbnailWrapper.style.borderRightColor = new Color(0f, 188/255f, 212/255f, 0.4f);
                _roomActionThumbnailWrapper.style.borderTopColor = new Color(0f, 188/255f, 212/255f, 0.4f);
                _roomActionThumbnailWrapper.style.borderBottomColor = new Color(0f, 188/255f, 212/255f, 0.4f);
            }

            bool hasActions = false;
            if (room?.Actions != null)
            {
                foreach (var act in room.Actions)
                {
                    if (act.InitallyActive && (string.IsNullOrEmpty(act.Trigger) || string.Equals(act.Trigger, "UserClicked", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        hasActions = true;
                        break;
                    }
                }
            }

            if (hasActions)
            {
                if (_roomActionThumbnailWrapper is not null)
                {
                    _roomActionThumbnailWrapper.AddToClassList("room-action-thumbnail--pulse");
                }

                // Pulse opacity and border color continuously using PrimeTween (2.0s sine oscillation)
                _pulseTween = PrimeTween.Tween.Custom(0.2f, 0.8f, duration: 1.0f, ease: PrimeTween.Ease.InOutSine, cycles: -1, cycleMode: PrimeTween.CycleMode.Yoyo, onValueChange: val => {
                    if (_roomActionsContainer is not null)
                    {
                        _roomActionsContainer.style.opacity = val;
                    }
                    if (_roomActionThumbnailWrapper is not null)
                    {
                        Color glowColor = new Color(0f, 188/255f, 212/255f, val + 0.2f);
                        _roomActionThumbnailWrapper.style.borderLeftColor = glowColor;
                        _roomActionThumbnailWrapper.style.borderRightColor = glowColor;
                        _roomActionThumbnailWrapper.style.borderTopColor = glowColor;
                        _roomActionThumbnailWrapper.style.borderBottomColor = glowColor;
                    }
                });
            }
        }


        public void AppendNarrativeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _narrativeScroll is null) return;
            AutocompleteActiveTypewriters();

            // Save old narrative entry into history log
            var room = GameManager.Instance?.CurrentRoom;
            string roomName = room is not null ? room.Name : "Action";
            _historyLog.Add(new System.Tuple<string, string>(roomName, text));

            if (!_hasClearedForCurrentAction)
            {
                // Clear narrative scroll to show current action text only
                _narrativeScroll.Clear();

                // Room name header
                if (room is not null)
                {
                    var header = new Label(room.Name);
                    header.AddToClassList("narrative-room-header");
                    _narrativeScroll.Add(header);
                }
                _hasClearedForCurrentAction = true;
            }
            else
            {
                // Add a spacer or double-newline to separate sequential display text commands
                if (_typewriterEnabled)
                {
                    _typewriterQueue.Enqueue(new TypewriterJob { ParagraphText = null });
                }
                else
                {
                    var spacer = new VisualElement();
                    spacer.AddToClassList("narrative-spacer");
                    _narrativeScroll.Add(spacer);
                }
            }

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
                if (o.Properties.TryGetValue("ParentContainerId", out var pId) && !string.IsNullOrEmpty(pId))
                {
                    containedIds.Add(o.Id);
                }
            }

            // Local helper to recursively add nested container items
            void AddContainedObjectsRecursively(GameObjectData parent, List<(GameObjectData data, bool isNested)> list)
            {
                if (parent.IsContainer && parent.ContainerOpen)
                {
                    var children = game.Objects.FindAll(o =>
                        (parent.ContainedObjectIds != null && parent.ContainedObjectIds.Contains(o.Id)) ||
                        (o.Properties.TryGetValue("ParentContainerId", out var pId) && string.Equals(pId, parent.Id, StringComparison.OrdinalIgnoreCase))
                    );

                    foreach (var childObj in children)
                    {
                        list.Add((childObj, true));
                        AddContainedObjectsRecursively(childObj, list);
                    }
                }
            }

            // Objects
            var requiredObjects = new List<(GameObjectData data, bool isNested)>();
            foreach (var obj in game.Objects.FindAll(o => room.ObjectIds.Contains(o.Id) && !o.IsCharacter && !containedIds.Contains(o.Id)))
            {
                requiredObjects.Add((obj, false));
                AddContainedObjectsRecursively(obj, requiredObjects);
            }

            ReconcileListContainer(_objectsListContainer, requiredObjects, tuple => {
                return tuple.isNested ? CreateNestedEntityRow(tuple.data, false) : CreateEntityRow(tuple.data, false);
            }, (element, tuple) => {
                var label = element.Q<Label>(className: "entity-name");
                if (label != null)
                {
                    string nameText = game is not null ? TemplateResolver.Resolve(tuple.data.Name, game, room, tuple.data) : tuple.data.Name;
                    if (tuple.data.IsWearable)
                    {
                        nameText += tuple.data.IsWorn ? " (Worn)" : " (Not Worn)";
                    }
                    if (tuple.data.IsContainer)
                    {
                        nameText += tuple.data.ContainerOpen ? " [Open]" : " [Closed]";
                    }
                    label.text = nameText;
                }
            });

            // Characters
            var requiredCharacters = new List<GameObjectData>();
            foreach (var ch in game.Characters)
            {
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
                    isInThisRoom = room.ObjectIds.Contains(ch.Id);
                }

                if (isInThisRoom)
                {
                    requiredCharacters.Add(ch);
                }
            }

            ReconcileListContainer(_charactersListContainer, requiredCharacters, ch => {
                return CreateEntityRow(ch, false);
            }, (element, ch) => {
                var label = element.Q<Label>(className: "entity-name");
                if (label != null)
                {
                    label.text = game is not null ? TemplateResolver.Resolve(ch.Name, game, room, ch) : ch.Name;
                }
            });

            // Inventory
            var requiredInventory = new List<(GameObjectData data, bool isNested)>();
            foreach (var item in game.Player.Inventory.FindAll(i => !containedIds.Contains(i.Id)))
            {
                requiredInventory.Add((item, false));
                AddContainedObjectsRecursively(item, requiredInventory);
            }

            ReconcileListContainer(_inventoryListContainer, requiredInventory, tuple => {
                return tuple.isNested ? CreateNestedEntityRow(tuple.data, true) : CreateEntityRow(tuple.data, true);
            }, (element, tuple) => {
                var label = element.Q<Label>(className: "entity-name");
                if (label != null)
                {
                    string nameText = game is not null ? TemplateResolver.Resolve(tuple.data.Name, game, room, tuple.data) : tuple.data.Name;
                    if (tuple.data.IsWearable)
                    {
                        nameText += tuple.data.IsWorn ? " (Worn)" : " (Not Worn)";
                    }
                    if (tuple.data.IsContainer)
                    {
                        nameText += tuple.data.ContainerOpen ? " [Open]" : " [Closed]";
                    }
                    label.text = nameText;
                }
            });

        }

        public void RefreshPlayerPanel()
        {
            var game = GameManager.Instance?.ActiveGame;
            var player = game?.Player;
            if (player is null) return;

            if (_playerNameLabel is not null)
            {
                var room = GameManager.Instance?.CurrentRoom;
                _playerNameLabel.text = game is not null
                    ? TemplateResolver.Resolve(player.Name, game, room, player)
                    : player.Name;

            }
            if (_playerGenderLabel is not null) _playerGenderLabel.text = player.Gender;
            RefreshPlayerPortrait();

            // Render status bar elements
            if (_playerHudContainer != null && game != null)
            {
                var isVisible = true;
                var visVar = game.Variables.Find(v => string.Equals(v.Name, "ui.statusBarVisible", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!string.IsNullOrEmpty(visVar) && string.Equals(visVar, "false", StringComparison.OrdinalIgnoreCase))
                {
                    isVisible = false;
                }
                _playerHudContainer.style.display = isVisible ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;

                _playerHudContainer.Clear();
                var room = GameManager.Instance?.CurrentRoom;
                if (game.StatusBarElements != null)
                {
                    foreach (var elem in game.StatusBarElements)
                    {
                        if (elem == null || !elem.IsVisible) continue;

                        var container = new VisualElement();
                        container.AddToClassList("status-bar-element");

                        // Background icon/image
                        if (elem.VisualOption == "ImageOnly" || elem.VisualOption == "ImageAndText")
                        {
                            if (!string.IsNullOrEmpty(elem.MediaAssetId))
                            {
                                var asset = game.MediaAssets.Find(a => a.Id == elem.MediaAssetId);
                                var path = asset != null ? asset.RelativePath : elem.MediaAssetId;
                                var imgEl = new VisualElement();
                                imgEl.AddToClassList("status-bar-image");
                                if (elem.VisualOption == "ImageAndText")
                                {
                                    imgEl.style.marginRight = 6;
                                }
                                LoadAndDisplayImageForElement(path, imgEl);
                                container.Add(imgEl);
                            }
                        }

                        // Text Label
                        if (elem.VisualOption == "TextOnly" || elem.VisualOption == "ImageAndText")
                        {
                            var lbl = new Label();
                            lbl.AddToClassList("status-bar-label");
                            lbl.text = TemplateResolver.Resolve(elem.Text, game, room, player);
                            lbl.style.whiteSpace = WhiteSpace.Normal;
                            lbl.style.flexGrow = 1;
                            lbl.style.flexShrink = 1;
                            
                            container.Add(lbl);
                        }

                        _playerHudContainer.Add(container);
                    }
                }
            }
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

        private double _targetStartTime;
        private double _targetEndTime;
        private float _targetVolume;

        public void PlaySceneVideo(string path, float volume, bool loop, float startTime, float endTime)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            if (_videoMonitorCoroutine != null)
            {
                StopCoroutine(_videoMonitorCoroutine);
                _videoMonitorCoroutine = null;
            }

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
            _videoPlayer.isLooping = loop;
            _videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _videoTexture;

            string url = FormatLocalPathForWeb(path);
            if (url.StartsWith("file://"))
            {
                _videoPlayer.url = new Uri(url).LocalPath;
            }
            else
            {
                _videoPlayer.url = url;
            }

            _videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;

            var elem = _root?.Q<VisualElement>("scene-image");
            if (elem is not null)
            {
                elem.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoTexture));
                elem.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                if (_scenePlaceholder is not null)
                {
                    _scenePlaceholder.style.display = DisplayStyle.None;
                }
            }

            _targetStartTime = startTime;
            _targetEndTime = endTime;
            _targetVolume = volume;

            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _videoPlayer.prepareCompleted += OnVideoPrepared;

            _videoPlayer.Prepare();

            Debug.Log($"[PlaySceneVideo] Preparing video ID/Path: {path}, Target Volume: {volume}, Loop: {loop}, Target Start: {startTime}, Target End: {endTime}");
        }

        private void OnVideoPrepared(UnityEngine.Video.VideoPlayer vp)
        {
            // Direct audio settings and time positioning are only valid once prepared.
            for (ushort i = 0; i < vp.controlledAudioTrackCount; i++)
            {
                vp.SetDirectAudioVolume(i, _targetVolume);
            }

            vp.time = _targetStartTime;
            vp.Play();

            if (_targetEndTime > _targetStartTime)
            {
                _videoMonitorCoroutine = StartCoroutine(MonitorVideoPlaybackCoroutine(_targetEndTime));
            }
            
            Debug.Log($"[OnVideoPrepared] Video prepared. Playback started at time: {vp.time}");
        }

        private System.Collections.IEnumerator MonitorVideoPlaybackCoroutine(double endTime)
        {
            // Give the player a frame to spin up
            yield return new UnityEngine.WaitForEndOfFrame();

            while (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                if (_videoPlayer.time >= endTime)
                {
                    Debug.Log($"[MonitorVideoPlaybackCoroutine] Video reached target end time ({endTime}s). Stopping playback.");
                    _videoPlayer.Stop();
                    yield break;
                }
                yield return null;
            }
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

        private VisualElement _transitionBackdrop;

        public void SetTransitionBackdrop(bool active, float durationSec)
        {
            if (_root == null) return;

            if (active)
            {
                if (_transitionBackdrop == null)
                {
                    _transitionBackdrop = new VisualElement();
                    _transitionBackdrop.name = "transition-backdrop";
                    _transitionBackdrop.style.position = Position.Absolute;
                    _transitionBackdrop.style.left = 0;
                    _transitionBackdrop.style.right = 0;
                    _transitionBackdrop.style.top = 0;
                    _transitionBackdrop.style.bottom = 0;
                    _transitionBackdrop.style.backgroundColor = Color.black;
                    _transitionBackdrop.pickingMode = PickingMode.Position; // block click events
                }

                // Insert it just before the vfx-overlay so it lies on top of gameplay UI but underneath particles
                VisualElement vfx = _root.Q("vfx-overlay");
                if (vfx != null)
                {
                    int index = _root.IndexOf(vfx);
                    if (!_root.Contains(_transitionBackdrop))
                    {
                        _root.Insert(index, _transitionBackdrop);
                    }
                }
                else
                {
                    if (!_root.Contains(_transitionBackdrop))
                    {
                        _root.Add(_transitionBackdrop);
                    }
                }

                // Fade in to 1 over half of transition duration
                _transitionBackdrop.style.opacity = 0f;
                _transitionBackdrop.style.display = DisplayStyle.Flex;
                
                StartCoroutine(FadeBackdropRoutine(0f, 1f, durationSec * 0.5f));
            }
            else
            {
                if (_transitionBackdrop != null && _root.Contains(_transitionBackdrop))
                {
                    // Fade out over second half, then hide
                    StartCoroutine(FadeBackdropRoutine(1f, 0f, durationSec * 0.5f, () => {
                        _transitionBackdrop.style.display = DisplayStyle.None;
                    }));
                }
            }
        }

        private System.Collections.IEnumerator FadeBackdropRoutine(float startAlpha, float targetAlpha, float duration, System.Action onComplete = null)
        {
            if (_transitionBackdrop == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _transitionBackdrop.style.opacity = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            _transitionBackdrop.style.opacity = targetAlpha;
            onComplete?.Invoke();
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

            // Save old narrative entry into history log
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                _historyLog.Add(new System.Tuple<string, string>(roomName, resolved));
            }

            // Clear narrative scroll to show current action/room text only
            if (!_hasClearedForCurrentAction)
            {
                _narrativeScroll.Clear();
            }

            // Room name header
            var header = new Label(roomName);
            header.AddToClassList("narrative-room-header");
            _narrativeScroll.Add(header);

            // Since we cleared and printed the room description, subsequent display texts in the same action should append
            _hasClearedForCurrentAction = true;

            // Body — built as inline spans with [hotlink] support
            BuildNarrativeBody(resolved);

            if (_typewriterEnabled)
            {
                ScrollNarrativeToBottom();
            }
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
                link.focusable = false;
                link.AddToClassList("narrative-hotlink");
                flow.Add(link);

                lastIdx = match.Index + match.Length;
            }

            if (lastIdx < para.Length)
            {
                var label = MakePlainLabel(para.Substring(lastIdx));
                if (matches.Count == 0)
                {
                    label.style.flexGrow = 1f;
                }
                flow.Add(label);
            }

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

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
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

            if (_typewriterEnabled)
            {
                ScrollNarrativeToBottom();
            }
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

                _revealCoroutine = StartCoroutine(TypewriterRevealRoutine(job.FlowElement, job.ParagraphText));
                yield return _revealCoroutine;
                _revealCoroutine = null;
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
            plain.style.flexGrow = 1f;
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
                if (kvp.Value.ClassListContains("compass-btn--active") || kvp.Value.enabledSelf)
                {
                    kvp.Value.RemoveFromClassList("compass-btn--active");
                    kvp.Value.AddToClassList("compass-btn--inactive");
                    kvp.Value.SetEnabled(false);
                    kvp.Value.clickable = null;
                    PrimeTween.Tween.StopAll(kvp.Value);
                    PrimeTween.Tween.Custom(kvp.Value.transform.scale.x, 0.9f, duration: 0.15f, onValueChange: val => {
                        kvp.Value.transform.scale = new Vector3(val, val, 1f);
                    });
                }
            }

            foreach (var kvp in _compassButtonsHud)
            {
                if (kvp.Value is null) continue;
                if (kvp.Value.ClassListContains("compass-btn--active") || kvp.Value.enabledSelf)
                {
                    kvp.Value.RemoveFromClassList("compass-btn--active");
                    kvp.Value.AddToClassList("compass-btn--inactive");
                    kvp.Value.SetEnabled(false);
                    kvp.Value.clickable = null;
                    PrimeTween.Tween.StopAll(kvp.Value);
                    PrimeTween.Tween.Custom(kvp.Value.transform.scale.x, 0.9f, duration: 0.15f, onValueChange: val => {
                        kvp.Value.transform.scale = new Vector3(val, val, 1f);
                    });
                }
            }

            // High-intensity glow highlights for active exit directions
            foreach (var exit in room.Exits)
            {
                string key = exit.Key;
                if (room.LockedExits.TryGetValue(key, out var isLocked) && isLocked)
                    continue;

                string targetRoomId = exit.Value;

                if (_compassButtons.TryGetValue(key, out var btn) && btn is not null)
                {
                    if (btn.ClassListContains("compass-btn--inactive") || !btn.enabledSelf)
                    {
                        btn.RemoveFromClassList("compass-btn--inactive");
                        btn.AddToClassList("compass-btn--active");
                        btn.SetEnabled(true);
                        btn.clickable = new Clickable(() => OnCompassExitClicked(targetRoomId));

                        PrimeTween.Tween.StopAll(btn);
                        PrimeTween.Tween.Custom(0.8f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                            btn.transform.scale = new Vector3(val, val, 1f);
                        });
                    }
                }

                if (_compassButtonsHud.TryGetValue(key, out var btnHud) && btnHud is not null)
                {
                    if (btnHud.ClassListContains("compass-btn--inactive") || !btnHud.enabledSelf)
                    {
                        btnHud.RemoveFromClassList("compass-btn--inactive");
                        btnHud.AddToClassList("compass-btn--active");
                        btnHud.SetEnabled(true);
                        btnHud.clickable = new Clickable(() => OnCompassExitClicked(targetRoomId));

                        PrimeTween.Tween.StopAll(btnHud);
                        PrimeTween.Tween.Custom(0.8f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                            btnHud.transform.scale = new Vector3(val, val, 1f);
                        });
                    }
                }
            }
        }

        private VisualElement CreateEntityRow(GameObjectData entity, bool isInventory)
        {
            var row = new VisualElement();
            row.userData = entity.Id;
            row.AddToClassList("entity-row");
            row.pickingMode = PickingMode.Position;

            var thumb = new VisualElement();
            thumb.AddToClassList("entity-thumbnail");
            if (!string.IsNullOrWhiteSpace(entity.PortraitImagePath))
            {
                LoadAndDisplayImageForElement(entity.PortraitImagePath, thumb);
            }
            else
            {
                var icon = new Label(entity.IsCharacter ? "👤" : "📦");
                icon.style.unityTextAlign = TextAnchor.MiddleCenter;
                icon.style.fontSize = 20f;
                icon.style.color = new Color(0f, 188/255f, 212/255f, 0.6f);
                thumb.Add(icon);
            }
            row.Add(thumb);

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            string nameText = game is not null
                ? TemplateResolver.Resolve(entity.Name, game, room, entity)
                : entity.Name;
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

            RegisterHoverSwell(row);

            return row;
        }

        private VisualElement CreateNestedEntityRow(GameObjectData entity, bool isInventory)
        {
            var row = new VisualElement();
            row.userData = entity.Id;
            row.AddToClassList("entity-row");
            row.AddToClassList("entity-row--nested");
            row.pickingMode = PickingMode.Position;

            var arrow = new Label("↳");
            arrow.AddToClassList("entity-nested-arrow");
            row.Add(arrow);

            var thumb = new VisualElement();
            thumb.AddToClassList("entity-thumbnail");
            if (!string.IsNullOrWhiteSpace(entity.PortraitImagePath))
            {
                LoadAndDisplayImageForElement(entity.PortraitImagePath, thumb);
            }
            else
            {
                var icon = new Label("📦");
                icon.style.unityTextAlign = TextAnchor.MiddleCenter;
                icon.style.fontSize = 20f;
                icon.style.color = new Color(0f, 188/255f, 212/255f, 0.6f);
                thumb.Add(icon);
            }
            row.Add(thumb);

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            string nameText = game is not null
                ? TemplateResolver.Resolve(entity.Name, game, room, entity)
                : entity.Name;
            var lbl = new Label(nameText);
            lbl.AddToClassList("entity-name");
            lbl.AddToClassList("entity-name--nested");
            row.Add(lbl);

            var btn = new Button(() => ShowEntityInteractionMenu(entity, isInventory));
            btn.text = "⋯";
            btn.AddToClassList("entity-action-btn");
            row.Add(btn);

            row.RegisterCallback<ClickEvent>(_ => ShowEntityInteractionMenu(entity, isInventory));

            RegisterHoverSwell(row);

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

        private bool _scrollNarrativeToBottomPending;
        private Coroutine _coalescedScrollCoroutine;

        private void ScrollNarrativeToBottom()
        {
            if (_narrativeScroll == null) return;
            
            // Clear focus to prevent Unity's auto-scroll-to-focus system from locking the scroll view
            _root?.focusController?.focusedElement?.Blur();
            
            _scrollNarrativeToBottomPending = true;
            if (_coalescedScrollCoroutine == null)
            {
                _coalescedScrollCoroutine = StartCoroutine(CoalescedScrollCoroutine());
            }
        }

        private IEnumerator CoalescedScrollCoroutine()
        {
            yield return new WaitForEndOfFrame();
            yield return null; // Frame 1: Wait for layout engine to run
            yield return null; // Frame 2: Ensure layout has fully calculated and settled
            
            _coalescedScrollCoroutine = null;
            bool toBottom = _scrollNarrativeToBottomPending;
            _scrollNarrativeToBottomPending = false;
            
            if (_narrativeScroll == null) yield break;

            if (toBottom && _narrativeScroll.verticalScroller != null)
            {
                float oldVal = _narrativeScroll.verticalScroller.value;
                float highVal = UnityEngine.Mathf.Max(0f, _narrativeScroll.verticalScroller.highValue);
                _narrativeScroll.verticalScroller.value = highVal;
                UnityEngine.Debug.Log($"[UIManager] CoalescedScrollCoroutine scrolled to bottom. Old Val: {oldVal}, New Val (High Clamped): {highVal}, Current Val: {_narrativeScroll.verticalScroller.value}");
            }
        }

        private RenderTexture _videoTexture;
        private UnityEngine.Video.VideoPlayer _videoPlayer;
        private Coroutine _videoMonitorCoroutine;

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
            if (url.StartsWith("file://"))
            {
                _videoPlayer.url = new Uri(url).LocalPath;
            }
            else
            {
                _videoPlayer.url = url;
            }

            var elem = _root?.Q<VisualElement>("scene-image");
            if (elem is not null)
            {
                elem.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoTexture));
                elem.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
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

        private readonly System.Collections.Generic.Dictionary<string, string> _latestElementUrls = new();
        private readonly System.Collections.Generic.Dictionary<VisualElement, string> _latestElementDirectUrls = new();

        private void LoadAndDisplayImage(string path, string elementName)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string url = FormatLocalPathForWeb(path);
            _latestElementUrls[elementName] = url;

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
            StartCoroutine(LoadImageCoroutine(url, elementName));
        }

        private void LoadAndDisplayImageForElement(string path, VisualElement targetElement)
        {
            if (targetElement is null || string.IsNullOrWhiteSpace(path)) return;
            string url = FormatLocalPathForWeb(path);
            _latestElementDirectUrls[targetElement] = url;
            StartCoroutine(LoadImageForElementCoroutine(url, targetElement));
        }

        private IEnumerator LoadImageForElementCoroutine(string url, VisualElement targetElement)
        {
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);

            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (_latestElementDirectUrls.TryGetValue(targetElement, out var latestUrl) && latestUrl != url)
                {
                    yield break;
                }

                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (targetElement is not null)
                {
                    targetElement.style.backgroundImage = new StyleBackground(tex);
                }
            }
        }

        private string FormatLocalPathForWeb(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            if (path.StartsWith("file://") || path.StartsWith("http://") || path.StartsWith("https://"))
                return path;

            string fullPath = path;
            bool isRooted = System.IO.Path.IsPathRooted(path) || (path.Length >= 2 && path[1] == ':');
            if (!isRooted)
            {
                fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, path);
            }
            else
            {
                if (System.IO.File.Exists(path))
                {
                    fullPath = path;
                }
                else
                {
                    // Standalone fallback: redirect designer AppData path to current StreamingAssets/Assets/ copy
                    var fileName = System.IO.Path.GetFileName(path.Replace("\\", "/"));
                    fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Assets", fileName);
                }
            }

            fullPath = fullPath.Replace("\\", "/");
            if (!fullPath.StartsWith("/"))
                return "file:///" + fullPath;
            else
                return "file://" + fullPath;
        }

        private IEnumerator LoadImageCoroutine(string url, string elementName)
        {
            Debug.Log($"[UIManager] Loading texture for '{elementName}' from URL: '{url}'");
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);

            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (_latestElementUrls.TryGetValue(elementName, out var latestUrl) && latestUrl != url)
                {
                    yield break;
                }

                var tex  = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                var elem = _root?.Q<VisualElement>(elementName);
                if (elem is not null)
                {
                    elem.style.backgroundImage = new StyleBackground(tex);
                    if (elementName == "scene-image")
                    {
                        if (tex != null)
                        {
                            float aspect = (float)tex.width / tex.height;
                            elem.style.unityBackgroundScaleMode = aspect > 1.2f ? ScaleMode.ScaleAndCrop : ScaleMode.ScaleToFit;
                        }
                        if (_scenePlaceholder is not null)
                        {
                            _scenePlaceholder.style.display = DisplayStyle.None;
                        }
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
        // ── Unified Menu Callbacks ─────────────────────────────────────────────
        public void ToggleGameMenu()
        {
            if (_gameMenuOverlay == null) return;
            if (_gameMenuOverlay.style.display == DisplayStyle.Flex)
            {
                CloseGameMenu();
            }
            else
            {
                OpenGameMenuTab(_menuIsSaveMode ? "Save" : "Load");
            }
        }

        public void OpenGameMenuTab(string tabName)
        {
            if (_gameMenuOverlay == null) return;

            // Stop scrolling typewriters to prevent overlaps
            AutocompleteActiveTypewriters();

            // Set Title text
            if (_gameMenuTitle != null)
            {
                _gameMenuTitle.text = tabName.ToUpper() + " GAME";
                if (tabName == "Settings") _gameMenuTitle.text = "SETTINGS / PREFERENCES";
                else if (tabName == "History") _gameMenuTitle.text = "DIALOGUE LOG";
                else if (tabName == "Help") _gameMenuTitle.text = "CONTROLS GUIDE";
            }

            // Sync menu navigation buttons highlight
            HighlightNavButton(tabName);

            bool isOpening = (_gameMenuOverlay.style.display != DisplayStyle.Flex);

            // Toggle panels display (hide during initial scale-up animation, toggle instantly if already open)
            if (isOpening)
            {
                _panelSaveLoad.style.display = DisplayStyle.None;
                _panelSettings.style.display = DisplayStyle.None;
                _panelHistory.style.display = DisplayStyle.None;
                _panelHelp.style.display = DisplayStyle.None;
            }
            else
            {
                _panelSaveLoad.style.display = (tabName == "Save" || tabName == "Load") ? DisplayStyle.Flex : DisplayStyle.None;
                _panelSettings.style.display = (tabName == "Settings") ? DisplayStyle.Flex : DisplayStyle.None;
                _panelHistory.style.display = (tabName == "History") ? DisplayStyle.Flex : DisplayStyle.None;
                _panelHelp.style.display = (tabName == "Help") ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (tabName == "Save" || tabName == "Load")
            {
                _menuIsSaveMode = (tabName == "Save");
                RefreshSaveLoadGrid();
            }
            else if (tabName == "Settings")
            {
                // Sync settings states
                if (_fullscreenToggleBtn is not null)
                    _fullscreenToggleBtn.text = Screen.fullScreen ? "Windowed" : "Fullscreen";
                if (_typewriterToggleBtn is not null)
                    _typewriterToggleBtn.text = _typewriterEnabled ? "Typewriter ON" : "Typewriter OFF";
                if (_typewriterSpeedSlider is not null)
                    _typewriterSpeedSlider.value = _typewriterSpeed;
                if (_volumeSlider is not null)
                    _volumeSlider.value = Mathf.RoundToInt(AudioListener.volume * 100f);
            }
            else if (tabName == "History")
            {
                PopulateHistoryLogScroll();
            }

            // Bring overlay into view with PrimeTween animation
            if (isOpening)
            {
                _gameMenuOverlay.style.display = DisplayStyle.Flex;
                _gameMenuOverlay.BringToFront();
                _gameMenuOverlay.transform.scale = Vector3.zero;
                PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    _gameMenuOverlay.transform.scale = new Vector3(val, val, 1f);
                }).OnComplete(() => {
                    // Display the target panel now that parent scale is 1
                    _panelSaveLoad.style.display = (tabName == "Save" || tabName == "Load") ? DisplayStyle.Flex : DisplayStyle.None;
                    _panelSettings.style.display = (tabName == "Settings") ? DisplayStyle.Flex : DisplayStyle.None;
                    _panelHistory.style.display = (tabName == "History") ? DisplayStyle.Flex : DisplayStyle.None;
                    _panelHelp.style.display = (tabName == "Help") ? DisplayStyle.Flex : DisplayStyle.None;

                    // Re-apply values to force UI Toolkit to layout drag handles correctly
                    if (tabName == "Settings")
                    {
                        if (_typewriterSpeedSlider is not null)
                        {
                            float originalSpeed = _typewriterSpeed;
                            _typewriterSpeedSlider.value = originalSpeed + 1f; // force event
                            _typewriterSpeedSlider.value = originalSpeed;
                        }
                        if (_volumeSlider is not null)
                        {
                            int originalVol = Mathf.RoundToInt(AudioListener.volume * 100f);
                            _volumeSlider.value = originalVol + 1; // force event
                            _volumeSlider.value = originalVol;
                        }
                    }
                });
            }
        }

        private void HighlightNavButton(string tabName)
        {
            if (_menuBtnSave is null || _menuBtnLoad is null || _menuBtnSettings is null || _menuBtnHistory is null || _menuBtnHelp is null) return;
            
            _menuBtnSave.RemoveFromClassList("menu-nav-btn--active");
            _menuBtnLoad.RemoveFromClassList("menu-nav-btn--active");
            _menuBtnSettings.RemoveFromClassList("menu-nav-btn--active");
            _menuBtnHistory.RemoveFromClassList("menu-nav-btn--active");
            _menuBtnHelp.RemoveFromClassList("menu-nav-btn--active");

            if (tabName == "Save") _menuBtnSave.AddToClassList("menu-nav-btn--active");
            else if (tabName == "Load") _menuBtnLoad.AddToClassList("menu-nav-btn--active");
            else if (tabName == "Settings") _menuBtnSettings.AddToClassList("menu-nav-btn--active");
            else if (tabName == "History") _menuBtnHistory.AddToClassList("menu-nav-btn--active");
            else if (tabName == "Help") _menuBtnHelp.AddToClassList("menu-nav-btn--active");
        }

        public void CloseGameMenu()
        {
            if (_gameMenuOverlay == null || _gameMenuOverlay.style.display == DisplayStyle.None) return;
            PrimeTween.Tween.Custom(_gameMenuOverlay.transform.scale.x, 0.0f, 0.1f, val => {
                _gameMenuOverlay.transform.scale = new Vector3(val, val, 1f);
            }).OnComplete(() => {
                _gameMenuOverlay.style.display = DisplayStyle.None;
            });
        }

        private void CloseGameMenu(ClickEvent evt) => CloseGameMenu();

        private void SwitchPage(int page)
        {
            _menuCurrentPage = page;
            
            // Highlight active page button
            for (int i = 0; i < _pageBtnList.Count; i++)
            {
                _pageBtnList[i].RemoveFromClassList("pagination-btn--active");
                if (i == page - 1)
                {
                    _pageBtnList[i].AddToClassList("pagination-btn--active");
                }
            }

            if (_saveLoadSubtitle != null)
            {
                _saveLoadSubtitle.text = $"Page {page}";
            }

            RefreshSaveLoadGrid();
        }

        private void PagePrev()
        {
            int nextP = _menuCurrentPage - 1;
            if (nextP < 1) nextP = 5;
            SwitchPage(nextP);
        }

        private void PageNext()
        {
            int nextP = _menuCurrentPage + 1;
            if (nextP > 5) nextP = 1;
            SwitchPage(nextP);
        }

        private void RefreshSaveLoadGrid()
        {
            if (GameManager.Instance == null) return;

            int startSlot = (_menuCurrentPage - 1) * 6 + 1;

            for (int i = 1; i <= 6; i++)
            {
                int slotId = startSlot + i - 1;
                var slotInfo = GameManager.Instance.GetSaveInfo(slotId);

                var slotCard = _root.Q<Button>($"save-slot-{i}");
                var thumb = slotCard?.Q<VisualElement>($"save-slot-{i}-thumb");
                var emptyLabel = thumb?.Q<Label>(className: "save-slot-empty-label");
                var infoLabel = slotCard?.Q<Label>($"save-slot-{i}-info");
                var numLabel = slotCard?.Q<Label>(className: "save-slot-num-label");

                if (numLabel != null) numLabel.text = $"Slot {slotId}";

                if (slotInfo.HasSave)
                {
                    if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;
                    if (infoLabel != null)
                    {
                        infoLabel.text = $"{slotInfo.RoomName}\n{slotInfo.Timestamp}";
                    }

                    // Load screenshot image into the visual element background
                    string savePath = GameManager.Instance.GetSaveFilePath(slotId);
                    string screenshotPath = System.IO.Path.ChangeExtension(savePath, ".png");
                    if (System.IO.File.Exists(screenshotPath) && thumb != null)
                    {
                        LoadAndDisplayImageForElement(screenshotPath, thumb);
                    }
                    else if (thumb != null)
                    {
                        thumb.style.backgroundImage = null;
                        if (emptyLabel != null)
                        {
                            emptyLabel.text = "No Screenshot";
                            emptyLabel.style.display = DisplayStyle.Flex;
                        }
                    }
                }
                else
                {
                    if (thumb != null) thumb.style.backgroundImage = null;
                    if (emptyLabel != null)
                    {
                        emptyLabel.text = "Empty Slot";
                        emptyLabel.style.display = DisplayStyle.Flex;
                    }
                    if (infoLabel != null) infoLabel.text = "No Save Data";
                }
            }
        }

        private void OnSaveSlotClicked(int localSlot)
        {
            int globalSlot = (_menuCurrentPage - 1) * 6 + localSlot;
            if (_menuIsSaveMode)
            {
                if (GameManager.Instance != null && GameManager.Instance.HasSaveFile(globalSlot))
                {
                    ShowOverwriteConfirmation(globalSlot);
                }
                else
                {
                    PerformSave(globalSlot);
                }
            }
            else
            {
                if (GameManager.Instance != null && GameManager.Instance.HasSaveFile(globalSlot))
                {
                    CloseGameMenu();
                    _ = GameManager.Instance.LoadGameAsync(globalSlot);
                }
            }
        }

        private void PopulateHistoryLogScroll()
        {
            if (_historyLogScroll is null) return;
            _historyLogScroll.Clear();

            foreach (var entry in _historyLog)
            {
                var itemContainer = new VisualElement();
                itemContainer.style.marginBottom = 12f;

                var header = new Label(entry.Item1);
                header.AddToClassList("narrative-room-header");
                header.style.color = new Color(0f, 188/255f, 212/255f); // Neon cyan
                itemContainer.Add(header);

                var bodyText = entry.Item2;
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    bodyText = bodyText.Replace("\r\n", "\n").Replace("\r", "\n");
                    var paragraphs = bodyText.Split(new[] { "\n" }, StringSplitOptions.None);
                    foreach (var para in paragraphs)
                    {
                        if (string.IsNullOrWhiteSpace(para))
                        {
                            var spacer = new VisualElement();
                            spacer.AddToClassList("narrative-spacer");
                            itemContainer.Add(spacer);
                            continue;
                        }

                        var cleanPara = Regex.Replace(para, @"\[([^\]]+)\]", "$1");
                        var paraLabel = new Label(cleanPara);
                        paraLabel.AddToClassList("narrative-paragraph");
                        paraLabel.style.whiteSpace = WhiteSpace.Normal;
                        itemContainer.Add(paraLabel);
                    }
                }

                var sep = new VisualElement();
                sep.AddToClassList("narrative-separator");
                itemContainer.Add(sep);

                _historyLogScroll.Add(itemContainer);
            }

            ScrollHistoryLogToBottom();
        }

        private void ScrollHistoryLogToBottom()
        {
            if (_historyLogScroll is null) return;
            StartCoroutine(ScrollHistoryLogToBottomCoroutine());
        }

        private IEnumerator ScrollHistoryLogToBottomCoroutine()
        {
            yield return new WaitForEndOfFrame();
            if (_historyLogScroll != null && _historyLogScroll.verticalScroller != null)
            {
                _historyLogScroll.verticalScroller.value = UnityEngine.Mathf.Max(0f, _historyLogScroll.verticalScroller.highValue);
            }
        }

        private void ToggleFullscreen()
        {
            if (Screen.fullScreen)
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                if (_fullscreenToggleBtn is not null)
                {
                    _fullscreenToggleBtn.text = "Fullscreen";
                }
            }
            else
            {
                Resolution res = Screen.currentResolution;
                Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
                if (_fullscreenToggleBtn is not null)
                {
                    _fullscreenToggleBtn.text = "Windowed";
                }
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

        private void CycleFontSize()
        {
            if (_fontSizePref == "Small") _fontSizePref = "Normal";
            else if (_fontSizePref == "Normal") _fontSizePref = "Large";
            else _fontSizePref = "Small";

            PlayerPrefs.SetString("Pref_FontSize", _fontSizePref);
            PlayerPrefs.Save();

            UpdateFontSizeUI();
        }

        private void UpdateFontSizeUI()
        {
            if (_fontSizeToggleBtn is not null)
            {
                _fontSizeToggleBtn.text = $"Text: {_fontSizePref}";
            }

            if (_root is not null)
            {
                _root.RemoveFromClassList("font-size-small");
                _root.RemoveFromClassList("font-size-normal");
                _root.RemoveFromClassList("font-size-large");

                if (_fontSizePref == "Small") _root.AddToClassList("font-size-small");
                else if (_fontSizePref == "Normal") _root.AddToClassList("font-size-normal");
                else if (_fontSizePref == "Large") _root.AddToClassList("font-size-large");
            }
        }

        private void QuitGame()
        {
            Debug.Log("[UIManager] Gracefully quitting game standalone.");
            Application.Quit();
        }

        private void ToggleCompassDial()
        {
            if (_compassDialOverlay is null) return;

            bool isCurrentlyVisible = _compassDialOverlay.style.display == DisplayStyle.Flex;

            if (isCurrentlyVisible)
            {
                // Fade out
                PrimeTween.Tween.Custom(1.0f, 0.0f, duration: 0.12f, ease: PrimeTween.Ease.InQuad, onValueChange: val => {
                    _compassDialOverlay.style.opacity = val;
                    _compassDialOverlay.transform.scale = new Vector3(val, val, 1f);
                }).OnComplete(() => {
                    _compassDialOverlay.style.display = DisplayStyle.None;
                });
            }
            else
            {
                // Fade in
                _compassDialOverlay.style.display = DisplayStyle.Flex;
                _compassDialOverlay.style.opacity = 0f;
                _compassDialOverlay.transform.scale = Vector3.zero;
                _compassDialOverlay.BringToFront();
                PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    _compassDialOverlay.style.opacity = val;
                    _compassDialOverlay.transform.scale = new Vector3(val, val, 1f);
                });
            }
        }

        private void OnCompassExitClicked(string targetRoomId)
        {
            // Trigger room movement
            PrepareForNewAction();
            GameManager.Instance?.MovePlayerToRoom(targetRoomId);
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
            StartCoroutine(PerformSaveCoroutine(slot));
        }

        private System.Collections.IEnumerator PerformSaveCoroutine(int slot)
        {
            if (_gameMenuOverlay is not null)
            {
                _gameMenuOverlay.style.display = DisplayStyle.None;
            }

            yield return new UnityEngine.WaitForEndOfFrame();

            GameManager.Instance.SaveGame(slot);
            AppendNarrativeText($"Game saved successfully to Slot {slot}.");
            RefreshSaveLoadGrid();
        }

        private async void LoadGameSlot(int slot)
        {
            if (GameManager.Instance is null) return;
            AppendNarrativeText($"Loading save from Slot {slot}...");
            CloseGameMenu();
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
            UnityEngine.Debug.Log($"[UIManager] ShowGameOverScreen called. finalMessage: '{finalMessage}', _gameOverMessage is {(_gameOverMessage != null ? "NOT null" : "null")}");

            if (string.IsNullOrWhiteSpace(finalMessage))
            {
                finalMessage = "The game has ended.";
            }

            if (_gameOverMessage is not null)
            {
                _gameOverMessage.enableRichText = true;
                _gameOverMessage.text = finalMessage;
            }

            if (_gameOverMenu is not null)
            {
                _gameOverMenu.style.display = DisplayStyle.Flex;
                _gameOverMenu.BringToFront();
                _gameOverMenu.transform.scale = Vector3.zero;
                PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    _gameOverMenu.transform.scale = new Vector3(val, val, 1f);
                });
            }
        }

        private void RestartGameAction()
        {
            if (_gameOverMenu is not null && _gameOverMenu.style.display == DisplayStyle.Flex)
            {
                PrimeTween.Tween.Custom(_gameOverMenu.transform.scale.x, 0.0f, 0.1f, val => {
                    _gameOverMenu.transform.scale = new Vector3(val, val, 1f);
                }).OnComplete(() => {
                    _gameOverMenu.style.display = DisplayStyle.None;
                    GameManager.Instance?.RestartGame();
                });
            }
            else
            {
                CloseGameMenu();
                GameManager.Instance?.RestartGame();
            }
        }

        private void OpenLoadGameFromGameOver()
        {
            if (_gameOverMenu is null) return;
            PrimeTween.Tween.Custom(_gameOverMenu.transform.scale.x, 0.0f, 0.1f, val => {
                _gameOverMenu.transform.scale = new Vector3(val, val, 1f);
            }).OnComplete(() => {
                _gameOverMenu.style.display = DisplayStyle.None;
                OpenGameMenuTab("Load");
            });
        }

        private void ExitGameAction()
        {
            Debug.Log("[UIManager] Exiting game...");
            Application.Quit();
        }

        // ── Player Prompt Input Modal HUD Methods ─────────────────────────────

        public void ShowPromptInputScreen(string promptName, string promptText, string inputType, string customOptions, string storeVariableName)
        {
            _promptName = promptName;
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
                    _promptTextField.style.display = DisplayStyle.Flex;
                    _promptTextField.Focus();
                }
                if (_promptSubmitBtn is not null)
                    _promptSubmitBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                selScroll.style.display = DisplayStyle.Flex;
                if (_promptTextField is not null)
                    _promptTextField.style.display = DisplayStyle.None;

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

                    var game = GameManager.Instance?.ActiveGame;
                    if (game is not null && game.CustomChoices is not null)
                    {
                        foreach (var ch in game.CustomChoices)
                        {
                            if (string.Equals(ch.PromptName, _promptName, System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ch.ChoiceText))
                                options.Add(ch.ChoiceText);
                        }
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
            {
                if (_promptMenuCloseTween.isAlive)
                {
                    _promptMenuCloseTween.Stop();
                }
                _promptInputMenu.style.display = DisplayStyle.Flex;
                _promptInputMenu.BringToFront();
                _promptInputMenu.transform.scale = Vector3.zero;
                PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    _promptInputMenu.transform.scale = new Vector3(val, val, 1f);
                });
            }
        }

        private void SubmitPromptSelection(string value)
        {
            UnityEngine.Debug.Log($"[UIManager] SubmitPromptSelection called. value: '{value}', _promptTargetVarName: '{_promptTargetVarName}', ActiveRunner is {(ActionExecutor.ActiveRunner != null ? "NOT null" : "null")}");
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

            var game = GameManager.Instance?.ActiveGame;
            if (game is not null && game.CustomChoices is not null)
            {
                var customChoice = game.CustomChoices.Find(c => string.Equals(c.PromptName, _promptName, System.StringComparison.OrdinalIgnoreCase) && string.Equals(c.ChoiceText, value, System.StringComparison.OrdinalIgnoreCase));
                if (customChoice is not null && !string.IsNullOrWhiteSpace(customChoice.VariableName))
                {
                    var customVar = vars.Find(v => string.Equals(v.Name, customChoice.VariableName, System.StringComparison.OrdinalIgnoreCase));
                    if (customVar is null)
                    {
                        vars.Add(new GameVariableData { Name = customChoice.VariableName, Value = value });
                    }
                    else
                    {
                        customVar.Value = value;
                    }
                }
            }

            var currentRoom = GameManager.Instance.CurrentRoom;
            if (currentRoom is not null)
            {
                BuildExitButtons(currentRoom);
                RefreshEntityLists();
            }

            // Resume the action execution engine first to see if a new prompt gets activated
            ActionExecutor.ResumeSuspended();

            if (_promptInputMenu is null) return;

            // If a new prompt is active, do not start the close tween.
            var promptActive = GameManager.Instance?.ActiveGame?.Variables?.Find(v => string.Equals(v.Name, "system.prompt.active", System.StringComparison.OrdinalIgnoreCase))?.Value == "true";
            if (promptActive) return;

            _promptMenuCloseTween = PrimeTween.Tween.Custom(_promptInputMenu.transform.scale.x, 0.0f, 0.1f, val => {
                _promptInputMenu.transform.scale = new Vector3(val, val, 1f);
            }).OnComplete(() => {
                // Only hide the menu if no new prompt has been activated during the resume sequence
                if (GameManager.Instance?.ActiveGame?.Variables?.Find(v => string.Equals(v.Name, "system.prompt.active", System.StringComparison.OrdinalIgnoreCase))?.Value != "true")
                {
                    _promptInputMenu.style.display = DisplayStyle.None;
                }
            });
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
                    {
                        _promptMenuCloseTween = PrimeTween.Tween.Custom(_promptInputMenu.transform.scale.x, 0.0f, 0.1f, val => {
                            _promptInputMenu.transform.scale = new Vector3(val, val, 1f);
                        }).OnComplete(() => {
                            _promptInputMenu.style.display = DisplayStyle.None;
                        });
                    }

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
            {
                if (_promptMenuCloseTween.isAlive)
                {
                    _promptMenuCloseTween.Stop();
                }
                _promptInputMenu.style.display = DisplayStyle.Flex;
                _promptInputMenu.BringToFront();
                _promptInputMenu.transform.scale = Vector3.zero;
                PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    _promptInputMenu.transform.scale = new Vector3(val, val, 1f);
                });
            }
        }

        public void RefreshExits()
        {
            var room = GameManager.Instance?.CurrentRoom;
            if (room is not null)
            {
                BuildExitButtons(room);
            }
        }

        private void SetGameplayUIVisible(bool visible)
        {
            if (_root == null) return;
            var topBar = _root.Q<VisualElement>("top-bar");
            var hudLayout = _root.Q<VisualElement>("hud-layout-wrapper");
            var mediaCanvas = _root.Q<VisualElement>("media-canvas");

            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (topBar != null) topBar.style.display = display;
            if (hudLayout != null) hudLayout.style.display = display;
            if (mediaCanvas != null) mediaCanvas.style.display = display;
        }

        public void RegisterHoverSwell(VisualElement element)
        {
            if (element is null) return;

            var textLabel = element.Q<Label>(className: "entity-name") ?? element;

            // Let stylesheet define the height (e.g. min-height: 52px for thumbnails)
            element.pickingMode = PickingMode.Position;

            element.RegisterCallback<PointerOverEvent>(evt => {
                if (!element.enabledSelf) return;
                PrimeTween.Tween.StopAll(textLabel); // Stop specifically on the text element
                
                // 1. Crisp Color Cross-fade
                Color currentColor = textLabel.style.color.keyword == StyleKeyword.Undefined ? Color.white : textLabel.style.color.value;
                PrimeTween.Tween.Custom(currentColor, new Color(0f, 0.65f, 0.80f), duration: 0.08f, onValueChange: val => {
                    textLabel.style.color = val;
                });
                
                // 2. Tactile Local Wiggle (Shifts text slightly right over 0.1s using an elastic overshoot)
                PrimeTween.Tween.Custom(textLabel.transform.position.x, 4.0f, duration: 0.1f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    textLabel.transform.position = new Vector3(val, textLabel.transform.position.y, 0f);
                });
            });

            element.RegisterCallback<PointerOutEvent>(evt => {
                PrimeTween.Tween.StopAll(textLabel);
                
                // Smoothly restore position and color instead of a harsh snapping reset
                Color currentColor = textLabel.style.color.keyword == StyleKeyword.Undefined ? Color.white : textLabel.style.color.value;
                PrimeTween.Tween.Custom(currentColor, Color.white, duration: 0.08f, onValueChange: val => {
                    textLabel.style.color = val;
                });
                PrimeTween.Tween.Custom(textLabel.transform.position.x, 0.0f, duration: 0.08f, onValueChange: val => {
                    textLabel.transform.position = new Vector3(val, textLabel.transform.position.y, 0f);
                }).OnComplete(() => {
                    textLabel.style.opacity = 1.0f;
                });
            });
        }

        private void ReconcileListContainer<TData>(
            VisualElement container, 
            List<TData> requiredData, 
            System.Func<TData, VisualElement> createFunc,
            System.Action<VisualElement, TData> updateFunc)
        {
            if (container == null) return;

            // Stop tweens and remove any extra children
            while (container.childCount > requiredData.Count)
            {
                var index = container.childCount - 1;
                var child = container[index];
                PrimeTween.Tween.StopAll(child);
                var label = child.Q<Label>(className: "entity-name");
                if (label != null) PrimeTween.Tween.StopAll(label);
                container.RemoveAt(index);
            }

            for (int i = 0; i < requiredData.Count; i++)
            {
                var data = requiredData[i];
                if (i < container.childCount)
                {
                    var existingElement = container[i];
                    string? existingId = existingElement.userData as string;
                    string? requiredId = GetDataId(data);
                    
                    if (existingId == requiredId)
                    {
                        updateFunc(existingElement, data);
                    }
                    else
                    {
                        // Replace the element
                        PrimeTween.Tween.StopAll(existingElement);
                        var label = existingElement.Q<Label>(className: "entity-name");
                        if (label != null) PrimeTween.Tween.StopAll(label);

                        var newElement = createFunc(data);
                        container.RemoveAt(i);
                        container.Insert(i, newElement);
                    }
                }
                else
                {
                    var newElement = createFunc(data);
                    container.Add(newElement);
                }
            }
        }

        private string? GetDataId(object data)
        {
            if (data is GameObjectData god) return god.Id;
            if (data is (GameObjectData godTuple, bool _)) return godTuple.Id;
            return null;
        }

        private void ToggleSidebar()
        {
            if (_rightSidebarContainer is null) return;

            _sidebarTween.Stop();
            _sidebarCollapsed = !_sidebarCollapsed;
            float startWidth = _rightSidebarContainer.resolvedStyle.width;

            if (!_sidebarCollapsed)
            {
                _rightSidebarContainer.RemoveFromClassList("sidebar--collapsed");
            }
            else
            {
                if (startWidth > 50f)
                {
                    _lastSidebarWidth = startWidth;
                }
            }

            float endWidth = _sidebarCollapsed ? 0f : _lastSidebarWidth;

            if (_sidebarToggleBtn is not null)
            {
                _sidebarToggleBtn.text = _sidebarCollapsed ? "⏵" : "⏴";
            }

            _sidebarTween = PrimeTween.Tween.Custom(startWidth, endWidth, duration: 0.25f, onValueChange: val => {
                _rightSidebarContainer.style.width = val;
                _rightSidebarContainer.style.flexBasis = val;
            }).OnComplete(() => {
                if (_sidebarCollapsed)
                {
                    _rightSidebarContainer.AddToClassList("sidebar--collapsed");
                }
            });
        }



        private void SetupSplitters()
        {
            var mainHandle = _root.Q<VisualElement>("main-split-handle");
            var mainLine = mainHandle?.Q<VisualElement>(className: "drag-splitter-line");
            if (mainHandle != null && mainLine != null && _rightSidebarContainer != null)
            {
                bool isDragging = false;
                
                mainHandle.RegisterCallback<PointerDownEvent>(evt => {
                    isDragging = true;
                    mainHandle.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                });
                
                mainHandle.RegisterCallback<PointerMoveEvent>(evt => {
                    if (isDragging)
                    {
                        float deltaX = evt.localPosition.x;
                        float newWidth = _rightSidebarContainer.resolvedStyle.width - deltaX;
                        newWidth = Mathf.Clamp(newWidth, 150f, 600f);
                        _rightSidebarContainer.style.width = newWidth;
                        _rightSidebarContainer.style.flexBasis = newWidth;
                        _lastSidebarWidth = newWidth;
                        evt.StopPropagation();
                    }
                });
                
                mainHandle.RegisterCallback<PointerUpEvent>(evt => {
                    if (isDragging)
                    {
                        isDragging = false;
                        mainHandle.ReleasePointer(evt.pointerId);
                        evt.StopPropagation();
                    }
                });

                mainHandle.RegisterCallback<PointerOverEvent>(evt => {
                    PrimeTween.Tween.StopAll(mainLine);
                    Color curColor = mainLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(90/255f, 95/255f, 110/255f, 0.4f) : mainLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(0f, 168/255f, 204/255f), duration: 0.1f, onValueChange: val => {
                        mainLine.style.backgroundColor = val;
                    });
                });

                mainHandle.RegisterCallback<PointerOutEvent>(evt => {
                    PrimeTween.Tween.StopAll(mainLine);
                    Color curColor = mainLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(0f, 168/255f, 204/255f) : mainLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(90/255f, 95/255f, 110/255f, 0.4f), duration: 0.1f, onValueChange: val => {
                        mainLine.style.backgroundColor = val;
                    });
                });
            }

            var textMediaHandle = _root.Q<VisualElement>("text-media-splitter");
            var mediaLine = textMediaHandle?.Q<VisualElement>(className: "drag-splitter-line");
            var mediaCanvas = _root.Q<VisualElement>("media-canvas");
            var narrativePanel = _root.Q<VisualElement>("narrative-panel");
            if (textMediaHandle != null && mediaLine != null && mediaCanvas != null && narrativePanel != null)
            {
                bool isDragging = false;

                textMediaHandle.RegisterCallback<PointerDownEvent>(evt => {
                    isDragging = true;
                    textMediaHandle.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                });

                textMediaHandle.RegisterCallback<PointerMoveEvent>(evt => {
                    if (isDragging)
                    {
                        float deltaY = evt.localPosition.y;
                        float curHeightMedia = mediaCanvas.resolvedStyle.height;
                        float curHeightNarrative = narrativePanel.resolvedStyle.height;
                        float totalHeight = curHeightMedia + curHeightNarrative;
                        
                        float targetHeightMedia = curHeightMedia + deltaY;
                        targetHeightMedia = Mathf.Clamp(targetHeightMedia, 50f, totalHeight - 50f);
                        
                        float growMedia = (targetHeightMedia / totalHeight) * 2.0f;
                        float growNarrative = 2.0f - growMedia;

                        mediaCanvas.style.flexGrow = growMedia;
                        narrativePanel.style.flexGrow = growNarrative;
                        
                        evt.StopPropagation();
                    }
                });

                textMediaHandle.RegisterCallback<PointerUpEvent>(evt => {
                    if (isDragging)
                    {
                        isDragging = false;
                        textMediaHandle.ReleasePointer(evt.pointerId);
                        evt.StopPropagation();
                    }
                });

                textMediaHandle.RegisterCallback<PointerOverEvent>(evt => {
                    PrimeTween.Tween.StopAll(mediaLine);
                    Color curColor = mediaLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(90/255f, 95/255f, 110/255f, 0.4f) : mediaLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(0f, 168/255f, 204/255f), duration: 0.1f, onValueChange: val => {
                        mediaLine.style.backgroundColor = val;
                    });
                });

                textMediaHandle.RegisterCallback<PointerOutEvent>(evt => {
                    PrimeTween.Tween.StopAll(mediaLine);
                    Color curColor = mediaLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(0f, 168/255f, 204/255f) : mediaLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(90/255f, 95/255f, 110/255f, 0.4f), duration: 0.1f, onValueChange: val => {
                        mediaLine.style.backgroundColor = val;
                    });
                });
            }

            var profileHandle = _root.Q<VisualElement>("profile-splitter");
            var profileLine = profileHandle?.Q<VisualElement>(className: "drag-splitter-line");
            var bottomProfile = _root.Q<VisualElement>("bottom-profile-bar");
            if (profileHandle != null && profileLine != null && bottomProfile != null)
            {
                bool isDragging = false;

                profileHandle.RegisterCallback<PointerDownEvent>(evt => {
                    isDragging = true;
                    profileHandle.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                });

                profileHandle.RegisterCallback<PointerMoveEvent>(evt => {
                    if (isDragging)
                    {
                        float deltaY = evt.localPosition.y;
                        float newHeight = bottomProfile.resolvedStyle.height - deltaY;
                        newHeight = Mathf.Clamp(newHeight, 80f, 250f);
                        bottomProfile.style.height = newHeight;
                        evt.StopPropagation();
                    }
                });

                profileHandle.RegisterCallback<PointerUpEvent>(evt => {
                    if (isDragging)
                    {
                        isDragging = false;
                        profileHandle.ReleasePointer(evt.pointerId);
                        evt.StopPropagation();
                    }
                });

                profileHandle.RegisterCallback<PointerOverEvent>(evt => {
                    PrimeTween.Tween.StopAll(profileLine);
                    Color curColor = profileLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(90/255f, 95/255f, 110/255f, 0.4f) : profileLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(0f, 168/255f, 204/255f), duration: 0.1f, onValueChange: val => {
                        profileLine.style.backgroundColor = val;
                    });
                });

                profileHandle.RegisterCallback<PointerOutEvent>(evt => {
                    PrimeTween.Tween.StopAll(profileLine);
                    Color curColor = profileLine.style.backgroundColor.keyword == StyleKeyword.Undefined ? new Color(0f, 168/255f, 204/255f) : profileLine.style.backgroundColor.value;
                    PrimeTween.Tween.Custom(curColor, new Color(90/255f, 95/255f, 110/255f, 0.4f), duration: 0.1f, onValueChange: val => {
                        profileLine.style.backgroundColor = val;
                    });
                });
            }
        }
    }
}
