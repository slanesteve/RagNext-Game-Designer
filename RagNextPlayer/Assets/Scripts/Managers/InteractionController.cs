#nullable enable
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
            GetMenuPanel();
            HideMenu();
        }

        private VisualElement? GetMenuPanel()
        {
            if (_menuPanel is not null) return _menuPanel;
            if (_uiDocument is null) _uiDocument = GetComponent<UIDocument>();
            var root = _uiDocument?.rootVisualElement;
            _menuPanel = root?.Q<VisualElement>("interaction-menu");
            if (_menuPanel is not null)
            {
                _menuPanel.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            }
            return _menuPanel;
        }


        // ── Public API ────────────────────────────────────────────────────────

        public void ShowMenu(GameObjectData entity, bool isInventory)
        {
            _currentEntity = entity;
            _isInventory   = isInventory;

            var panel = GetMenuPanel();
            if (panel is null) return;

            // Only show actions explicitly defined by the game designer
            var options = new List<(string Label, System.Action Handler)>();

            foreach (var act in entity.Actions)
            {
                if (act.InitallyActive)
                {
                    var captured = act;
                    options.Add((act.Name, () => ExecuteCustomAction(entity, captured)));
                }
            }

            if (options.Count == 0)
            {
                // Nothing to show — silently ignore the tap
                return;
            }

            BuildMenuUI(entity.Name, options);
            _menuPanel.style.display = DisplayStyle.Flex;
            _menuPanel.BringToFront();
        }


        public void HideMenu()
        {
            var panel = GetMenuPanel();
            if (panel is not null)
                panel.style.display = DisplayStyle.None;
        }

        public void ShowRoomMenu(RoomData room)
        {
            var panel = GetMenuPanel();
            if (panel is null) return;

            var options = new List<(string Label, System.Action Handler)>();
            foreach (var act in room.Actions)
            {
                if (act.InitallyActive)
                {
                    var captured = act;
                    options.Add((act.Name, () => ExecuteRoomAction(room, captured)));
                }
            }

            if (options.Count == 0) return;

            BuildMenuUI(room.Name, options);
            panel.style.display = DisplayStyle.Flex;
            panel.BringToFront();
        }



        public void ShowPlayerMenu(PlayerData player)
        {
            var panel = GetMenuPanel();
            if (panel is null) return;

            var options = new List<(string Label, System.Action Handler)>();
            foreach (var act in player.Actions)
            {
                if (act.InitallyActive)
                {
                    var captured = act;
                    options.Add((act.Name, () => ExecutePlayerAction(player, captured)));
                }
            }

            if (options.Count == 0) return;

            BuildMenuUI(player.Name, options);
            panel.style.display = DisplayStyle.Flex;
            panel.BringToFront();
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

            // Global fallback for objects
            var globalObj = game.Objects.Find(o =>
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (globalObj is not null) { ShowMenu(globalObj, false); return; }

            // Global fallback for characters
            var globalChar = game.Characters.Find(c =>
                string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (globalChar is not null) { ShowMenu(globalChar, false); return; }
        }


        // ── Action Handlers ───────────────────────────────────────────────────

        private void ExecuteCustomAction(GameObjectData entity, ActionData action)
        {
            HideMenu();
            var ctx  = GameManager.Instance?.MakeContext(entity);
            var sink = GetComponent<CommandEffectRouter>();
            if (ctx is not null)
            {
                ActionExecutor.Execute(action, ctx, sink);
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        private void ExecuteRoomAction(RoomData room, ActionData action)
        {
            HideMenu();
            var game = GameManager.Instance?.ActiveGame;
            if (game is null) return;
            var ctx = new GameExecutionContext(game, room, null);
            var sink = GetComponent<CommandEffectRouter>();
            if (sink is not null)
            {
                ActionExecutor.Execute(action, ctx, sink);
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        private void ExecutePlayerAction(PlayerData player, ActionData action)
        {
            HideMenu();
            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            if (game is null) return;

            // Build a lightweight GameObjectData stub so {this.Name} etc. resolve to the player
            var playerStub = new GameObjectData
            {
                Id                = player.Id,
                Name              = player.Name,
                Description       = player.Description,
                PortraitImagePath = player.PortraitImagePath,
            };

            var ctx  = new GameExecutionContext(game, room, playerStub);
            var sink = GetComponent<CommandEffectRouter>();
            if (sink is not null)
            {
                ActionExecutor.Execute(action, ctx, sink);
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
