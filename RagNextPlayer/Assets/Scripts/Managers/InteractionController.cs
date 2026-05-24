using System.Collections.Generic;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace RagNextPlayer.Managers
{
    /// <summary>
    /// Handles player interaction with game objects and inline narrative entities.
    /// Mirrors the ShowEntityInteractionMenu / HandleInlineEntityClicked logic
    /// from the MAUI MainPage, using a custom UI Toolkit popup panel.
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        public static InteractionController Instance { get; private set; }

        [SerializeField] private UIDocument _uiDocument;

        private VisualElement? _menuPanel;
        private GameObjectData? _currentEntity;
        private bool _isInventory;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            var root = _uiDocument?.rootVisualElement;
            _menuPanel = root?.Q<VisualElement>("interaction-menu");
            _menuPanel?.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            HideMenu();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ShowMenu(GameObjectData entity, bool isInventory)
        {
            _currentEntity = entity;
            _isInventory   = isInventory;

            if (_menuPanel is null) return;

            // Build action options
            var options = new List<(string Label, System.Action Handler)>();

            options.Add(("👁️ Examine", () => ExecuteExamine(entity)));

            if (!isInventory && entity.IsCollectible && !entity.IsCharacter)
                options.Add(("✋ Pick Up", () => ExecutePickUp(entity)));

            if (entity.IsCharacter)
                options.Add(("💬 Talk To", () => ExecuteTalkTo(entity)));

            foreach (var act in entity.Actions)
            {
                if (act.InitallyActive)
                {
                    var captured = act;
                    options.Add(($"⚡ {act.Name}", () => ExecuteCustomAction(entity, captured)));
                }
            }

            BuildMenuUI(entity.Name, options);
            _menuPanel.style.display = DisplayStyle.Flex;
            _menuPanel.BringToFront();
        }

        public void HideMenu()
        {
            if (_menuPanel is not null)
                _menuPanel.style.display = DisplayStyle.None;
        }

        public void HandleInlineClick(string name)
        {
            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            if (game is null || room is null) return;

            // Search priority: character in room → object in room → inventory → exit → global
            var ch = game.Characters.Find(c =>
                room.ObjectIds.Contains(c.Id) &&
                string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (ch is not null) { ShowMenu(ch, false); return; }

            var obj = game.Objects.Find(o =>
                room.ObjectIds.Contains(o.Id) &&
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (obj is not null) { ShowMenu(obj, false); return; }

            var invObj = game.Player.Inventory.Find(o =>
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (invObj is not null) { ShowMenu(invObj, true); return; }

            // Try exit
            if (room.Exits.TryGetValue(name, out var exitId))
            {
                GameManager.Instance?.MovePlayerToRoom(exitId);
                return;
            }

            // Global fallback
            var globalObj = game.Objects.Find(o =>
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (globalObj is not null) ShowMenu(globalObj, false);
        }

        // ── Action Handlers ───────────────────────────────────────────────────

        private void ExecuteExamine(GameObjectData entity)
        {
            HideMenu();
            var ctx  = GameManager.Instance?.MakeContext(entity);
            var desc = string.IsNullOrWhiteSpace(entity.Description)
                ? $"You examine the {entity.Name}. Nothing remarkable stands out."
                : ctx!.Resolve(entity.Description);
            UIManager.Instance?.AppendNarrativeText($"[{entity.Name}] » {desc}");
        }

        private void ExecutePickUp(GameObjectData entity)
        {
            HideMenu();
            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            if (game is null || room is null) return;

            room.ObjectIds.Remove(entity.Id);
            game.Player.Inventory.Add(entity);
            UIManager.Instance?.AppendNarrativeText($"✋ You pick up the {entity.Name}.");
            UIManager.Instance?.RefreshEntityLists();
        }

        private void ExecuteTalkTo(GameObjectData entity)
        {
            HideMenu();
            var ctx  = GameManager.Instance?.MakeContext(entity);
            var text = string.IsNullOrWhiteSpace(entity.Description)
                ? $"{entity.Name} has nothing to say."
                : ctx!.Resolve(entity.Description);
            UIManager.Instance?.AppendNarrativeText($"[{entity.Name}] \"{text}\"");
        }

        private void ExecuteCustomAction(GameObjectData entity, ActionData action)
        {
            HideMenu();
            var ctx  = GameManager.Instance?.MakeContext(entity);
            var sink = GetComponent<CommandEffectRouter>();
            if (ctx is not null)
            {
                ActionExecutor.Execute(action, ctx, sink);
                UIManager.Instance?.AppendNarrativeText($"⚡ {action.Name} executed.");
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        // ── Menu UI Builder ───────────────────────────────────────────────────

        private void BuildMenuUI(string title, List<(string Label, System.Action Handler)> options)
        {
            if (_menuPanel is null) return;
            _menuPanel.Clear();

            var titleLbl = new Label(title);
            titleLbl.AddToClassList("interaction-menu-title");
            _menuPanel.Add(titleLbl);

            foreach (var (label, handler) in options)
            {
                var btn = new Button(handler);
                btn.text = label;
                btn.AddToClassList("interaction-menu-btn");
                btn.clicked += HideMenu;
                _menuPanel.Add(btn);
            }

            var cancelBtn = new Button(HideMenu) { text = "✕ Cancel" };
            cancelBtn.AddToClassList("interaction-menu-cancel");
            _menuPanel.Add(cancelBtn);
        }
    }
}
