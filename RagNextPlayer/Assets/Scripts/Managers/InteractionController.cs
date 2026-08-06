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
                if (act.InitallyActive && (string.IsNullOrEmpty(act.Trigger) || string.Equals(act.Trigger, "UserClicked", System.StringComparison.OrdinalIgnoreCase)))
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

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            string resolvedTitle = game is not null
                ? TemplateResolver.Resolve(entity.Name, game, room, entity)
                : entity.Name;

            BuildMenuUI(resolvedTitle, options);
            DisplayMenuPanel();
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
                if (act.InitallyActive && (string.IsNullOrEmpty(act.Trigger) || string.Equals(act.Trigger, "UserClicked", System.StringComparison.OrdinalIgnoreCase)))
                {
                    var captured = act;
                    options.Add((act.Name, () => ExecuteRoomAction(room, captured)));
                }
            }

            if (options.Count == 0) return;

            var game = GameManager.Instance?.ActiveGame;
            string resolvedTitle = game is not null
                ? TemplateResolver.Resolve(room.Name, game, room, room)
                : room.Name;

            BuildMenuUI(resolvedTitle, options);
            DisplayMenuPanel();
        }



        public void ShowPlayerMenu(PlayerData player)
        {
            var panel = GetMenuPanel();
            if (panel is null) return;

            var options = new List<(string Label, System.Action Handler)>();
            foreach (var act in player.Actions)
            {
                if (act.InitallyActive && (string.IsNullOrEmpty(act.Trigger) || string.Equals(act.Trigger, "UserClicked", System.StringComparison.OrdinalIgnoreCase)))
                {
                    var captured = act;
                    options.Add((act.Name, () => ExecutePlayerAction(player, captured)));
                }
            }

            if (options.Count == 0) return;

            var game = GameManager.Instance?.ActiveGame;
            var room = GameManager.Instance?.CurrentRoom;
            string resolvedTitle = game is not null
                ? TemplateResolver.Resolve(player.Name, game, room, player)
                : player.Name;

            BuildMenuUI(resolvedTitle, options);
            DisplayMenuPanel();
        }

        private void DisplayMenuPanel()
        {
            if (_menuPanel is null) return;
            _menuPanel.style.display = DisplayStyle.Flex;
            _menuPanel.BringToFront();
            _menuPanel.transform.scale = Vector3.zero;
            PrimeTween.Tween.Custom(0.0f, 1.0f, duration: 0.15f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                _menuPanel.transform.scale = new Vector3(val, val, 1f);
            });
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
            if (ch is not null)
            {
                ShowMenu(ch, false);
                return;
            }

            var obj = game.Objects.Find(o =>
                room.ObjectIds.Contains(o.Id) &&
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (obj is not null)
            {
                ShowMenu(obj, false);
                return;
            }

            var invObj = game.Player.Inventory.Find(o =>
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (invObj is not null)
            {
                ShowMenu(invObj, true);
                return;
            }

            // Try exit
            if (room.Exits.TryGetValue(name, out var exitId))
            {
                if (room.LockedExits.TryGetValue(name, out var isLocked) && isLocked)
                {
                    UIManager.Instance?.AppendNarrativeText($"\nThe exit to the {name} is locked.");
                    return;
                }
                GameManager.Instance?.MovePlayerToRoom(exitId, name);
                return;
            }

            // Global fallback for objects
            var globalObj = game.Objects.Find(o =>
                string.Equals(o.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (globalObj is not null)
            {
                ShowMenu(globalObj, false);
                return;
            }

            // Global fallback for characters
            var globalChar = game.Characters.Find(c =>
                string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (globalChar is not null)
            {
                ShowMenu(globalChar, false);
                return;
            }
        }


        // ── Action Handlers ───────────────────────────────────────────────────

        private void ExecuteCustomAction(GameObjectData entity, ActionData action, bool forceExecute = false)
        {
            HideMenu();
            var ctx  = GameManager.Instance?.MakeContext(entity);
            var sink = GetComponent<CommandEffectRouter>();
            if (ctx is not null)
            {
                ActionExecutor.Execute(action, ctx, sink, true, forceExecute);
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        public void ExecuteRoomAction(RoomData room, ActionData action, bool forceExecute = false)
        {
            HideMenu();
            var game = GameManager.Instance?.ActiveGame;
            if (game is null) return;
            var ctx = new GameExecutionContext(game, room, null, room);
            var sink = GetComponent<CommandEffectRouter>();
            if (sink is not null)
            {
                ActionExecutor.Execute(action, ctx, sink, true, forceExecute);
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        private void ExecutePlayerAction(PlayerData player, ActionData action, bool forceExecute = false)
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

            var ctx  = new GameExecutionContext(game, room, playerStub, player);
            var sink = GetComponent<CommandEffectRouter>();
            if (sink is not null)
            {
                ActionExecutor.Execute(action, ctx, sink, true, forceExecute);
                UIManager.Instance?.RefreshEntityLists();
            }
        }

        public void ExecuteActionById(string actionId, GameObjectData entity = null, RoomData room = null, bool forceExecute = false)
        {
            if (entity != null)
            {
                var action = entity.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                if (action != null)
                {
                    ExecuteCustomAction(entity, action, forceExecute);
                    return;
                }
            }
            if (room != null)
            {
                var action = room.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                if (action != null)
                {
                    ExecuteRoomAction(room, action, forceExecute);
                    return;
                }
            }
        }

        public void ExecuteHotspotNodes(System.Collections.Generic.List<ActionStepData> nodes, GameObjectData entity = null, RoomData room = null)
        {
            if (nodes == null || nodes.Count == 0) return;
            var game = GameManager.Instance?.ActiveGame;
            if (game == null) return;
            var curRoom = room ?? GameManager.Instance?.CurrentRoom;
            var ctx = new GameExecutionContext(game, curRoom, entity, curRoom);
            var sink = GetComponent<CommandEffectRouter>();
            var tempAction = new ActionData { Name = "HotspotAction", Nodes = nodes, InitallyActive = true };
            ActionExecutor.Execute(tempAction, ctx, sink, true, true);
            UIManager.Instance?.RefreshEntityLists();
        }

        private void ExecuteGlobalActions(string actionId, bool forceExecute = false)
        {
            var game = GameManager.Instance?.ActiveGame;
            if (game != null)
            {
                foreach (var c in game.Characters)
                {
                    var action = c.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                    if (action != null) { ExecuteCustomAction(c, action, forceExecute); return; }
                }
                foreach (var o in game.Objects)
                {
                    var action = o.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                    if (action != null) { ExecuteCustomAction(o, action, forceExecute); return; }
                }
                if (game.Player != null)
                {
                    var action = game.Player.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                    if (action != null) { ExecutePlayerAction(game.Player, action, forceExecute); return; }

                    foreach (var invItem in game.Player.Inventory)
                    {
                        var invAction = invItem.Actions.Find(a => string.Equals(a.Id, actionId, System.StringComparison.OrdinalIgnoreCase));
                        if (invAction != null) { ExecuteCustomAction(invItem, invAction, forceExecute); return; }
                    }
                }
            }
        }



        // ── Menu UI Builder ───────────────────────────────────────────────────

        private void BuildMenuUI(string title, List<(string Label, System.Action Handler)> options)
        {
            if (_menuPanel is null) return;
            _menuPanel.Clear();

            float fontSize = UIManager.Instance != null ? UIManager.Instance.GetScaledFontSize() : 18f;

            var titleLbl = new Label(title);
            titleLbl.AddToClassList("interaction-menu-title");
            titleLbl.style.fontSize = fontSize * 1.05f;
            _menuPanel.Add(titleLbl);

            var grid = new VisualElement();
            grid.AddToClassList("interaction-menu-grid");

            foreach (var (label, handler) in options)
            {
                var btn = new Button(handler);
                btn.text = label;
                btn.AddToClassList("interaction-menu-btn");
                btn.style.fontSize = fontSize * 0.95f;
                btn.style.whiteSpace = WhiteSpace.Normal;
                btn.style.flexShrink = 0;
                btn.clicked += HideMenu;
                RegisterMenuBtnWiggle(btn);
                grid.Add(btn);
            }
            _menuPanel.Add(grid);

            var cancelBtn = new Button(HideMenu) { text = "✕ Cancel" };
            cancelBtn.AddToClassList("interaction-menu-cancel");
            cancelBtn.style.fontSize = fontSize * 0.85f;
            RegisterMenuBtnWiggle(cancelBtn);
            _menuPanel.Add(cancelBtn);
        }

        private void RegisterMenuBtnWiggle(Button btn)
        {
            if (btn is null) return;

            var target = btn.Q<VisualElement>(className: "unity-text-element") ?? (VisualElement)btn;

            btn.RegisterCallback<PointerOverEvent>(evt => {
                if (!btn.enabledSelf) return;

                PrimeTween.Tween.StopAll(target);

                // Quick 0.1s scale to 1.05 with Ease.OutBack
                PrimeTween.Tween.Custom(target.transform.scale.x, 1.05f, duration: 0.1f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    target.transform.scale = new Vector3(val, val, 1f);
                });

                // Subtle Y-axis translation bounce (translate Y by -4f)
                PrimeTween.Tween.Custom(target.transform.position.y, -4f, duration: 0.1f, ease: PrimeTween.Ease.OutBack, onValueChange: val => {
                    target.transform.position = new Vector3(target.transform.position.x, val, 0f);
                });
            });

            btn.RegisterCallback<PointerOutEvent>(evt => {
                PrimeTween.Tween.StopAll(target);

                target.transform.scale = Vector3.one;
                target.transform.position = Vector3.zero;
                target.style.opacity = 1.0f;
            });
        }
    }
}
