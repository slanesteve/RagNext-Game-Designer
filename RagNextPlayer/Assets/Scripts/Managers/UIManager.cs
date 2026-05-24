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

        // ── Typewriter effect ─────────────────────────────────────────────────
        [Header("Narrative Settings")]
        [SerializeField] private bool  _typewriterEnabled = true;
        [SerializeField] private float _typewriterSpeed   = 0.018f; // seconds per char

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
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

            // Subscribe to game events
            if (GameManager.Instance is not null)
            {
                GameManager.Instance.OnGameLoaded  += OnGameLoaded;
                GameManager.Instance.OnRoomEntered += OnRoomEntered;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance is not null)
            {
                GameManager.Instance.OnGameLoaded  -= OnGameLoaded;
                GameManager.Instance.OnRoomEntered -= OnRoomEntered;
            }
        }

        // ── Game / Room Events ────────────────────────────────────────────────

        private void OnGameLoaded(GameData game)
        {
            if (_gameInfoLabel is not null)
                _gameInfoLabel.text = $"by {game.Author}  ·  v{game.Version}";

            RefreshPlayerPanel();
        }

        private void OnRoomEntered(RoomData room)
        {
            RenderRoom(room);
        }

        // ── Public Interface (called by CommandEffectRouter) ──────────────────

        public void RenderRoom(RoomData room)
        {
            if (room is null) return;
            var game = GameManager.Instance?.ActiveGame;

            // Room title
            if (_roomTitleLabel is not null)
                _roomTitleLabel.text = room.Name;

            // Scene image
            if (!string.IsNullOrWhiteSpace(room.PortraitImagePath))
                DisplaySceneImage(room.PortraitImagePath);

            // Compass exits
            BuildExitButtons(room);

            // Narrative description (typewriter)
            AppendNarrativeEntry(room.Name, room.Description);

            // Entity lists
            RefreshEntityLists();
        }

        public void AppendNarrativeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _narrativeScroll is null) return;

            var label = new Label();
            label.AddToClassList("narrative-status");
            label.text = $"» {text}";
            _narrativeScroll.Add(label);
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

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            var resolved = game is not null
                ? TemplateResolver.Resolve(description, game)
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

            var paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    var spacer = new VisualElement();
                    spacer.AddToClassList("narrative-spacer");
                    _narrativeScroll.Add(spacer);
                    continue;
                }

                var flow = new VisualElement();
                flow.AddToClassList("narrative-paragraph");

                // Parse [EntityName] hotlinks — same regex as MAUI player
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

                if (_typewriterEnabled)
                    StartCoroutine(TypewriterReveal(flow, para));
                else
                    _narrativeScroll.Add(flow);
            }
        }

        private Label MakePlainLabel(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("narrative-text");
            return lbl;
        }

        private IEnumerator TypewriterReveal(VisualElement element, string fullText)
        {
            // Reveal the full container immediately but fade in character by character
            // via a dedicated typewriter label on top, then swap to the rich element
            var plain = new Label();
            plain.AddToClassList("narrative-text");
            _narrativeScroll.Add(plain);

            for (int i = 0; i <= fullText.Length; i++)
            {
                plain.text = fullText.Substring(0, i);
                yield return new WaitForSeconds(_typewriterSpeed);
            }

            // Replace plain label with the rich (hotlink) version
            _narrativeScroll.Remove(plain);
            _narrativeScroll.Add(element);
            ScrollNarrativeToBottom();
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
            _narrativeScroll?.schedule.Execute(() =>
                _narrativeScroll.scrollOffset = new Vector2(0, float.MaxValue));
        }

        private void LoadAndDisplayImage(string path, string elementName)
        {
            StartCoroutine(LoadImageCoroutine(path, elementName));
        }

        private IEnumerator LoadImageCoroutine(string path, string elementName)
        {
            using var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture("file://" + path);
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var tex  = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                var elem = _root?.Q<VisualElement>(elementName);
                if (elem is not null)
                    elem.style.backgroundImage = new StyleBackground(tex);
            }
        }
    }
}
