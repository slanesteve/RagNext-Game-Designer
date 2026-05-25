using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
using RagsCore.Actions;
using RagsCore.Models;
using RagsNextPlayer.Services;

namespace RagsNextPlayer
{
    public partial class MainPage : ContentPage, IGameEventSink
    {
        private Game? _game;
        private Room? _currentRoom;

        public ICommand MoveCommand { get; }

        public MainPage()
        {
            InitializeComponent();

            MoveCommand = new Command<string>(direction =>
            {
                if (_game is null || _currentRoom is null) return;

                var exits = _currentRoom.Exits;
                Guid targetRoomId = Guid.Empty;
                foreach (var pair in exits)
                {
                    if (string.Equals(pair.Key, direction, StringComparison.OrdinalIgnoreCase))
                    {
                        targetRoomId = pair.Value;
                        break;
                    }
                }

                if (targetRoomId != Guid.Empty)
                {
                    MovePlayerToRoom(targetRoomId);
                }
            });

            // Explicitly set DirectionClickedCommand backups to guarantee reliable binding on Windows
            DesktopCompass.DirectionClickedCommand = MoveCommand;
            MobileCompass.DirectionClickedCommand = MoveCommand;

            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await InitializeGameAsync();
        }

        private async Task InitializeGameAsync()
        {
            string loadedFrom = "None (could not locate game.json)";

            // 1. Try loading last active developer state from AppData (legacy path)
            var devSave = Path.Combine(FileSystem.Current.AppDataDirectory, "game.json");
            if (File.Exists(devSave))
            {
                _game = await GameLoader.LoadFromFileAsync(devSave);
                loadedFrom = $"Player AppData ({Path.GetFileName(devSave)})";
            }

            // 1.5 Try loading the latest developer state from the Designer's AppData (extremely helpful fallback)
            if (_game is null)
            {
                var latestDesignerSave = GetLatestDesignerSavePath();
                if (!string.IsNullOrEmpty(latestDesignerSave) && File.Exists(latestDesignerSave))
                {
                    try
                    {
                        _game = await GameLoader.LoadFromFileAsync(latestDesignerSave);
                        loadedFrom = $"Designer Active Save ({Path.GetFileName(latestDesignerSave)})";
                    }
                    catch { }
                }
            }

            // 2. Fallback: check base execution directory
            if (_game is null)
            {
                var baseGame = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game.json");
                if (File.Exists(baseGame))
                {
                    _game = await GameLoader.LoadFromFileAsync(baseGame);
                    loadedFrom = $"Base Execution Directory ({Path.GetFileName(baseGame)})";
                }
            }

            // 3. Fallback: try reading raw resource packaged in binary
            if (_game is null)
            {
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("game.json");
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    _game = System.Text.Json.JsonSerializer.Deserialize<Game>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                        Converters = { new StepDefinitionBaseJsonConverter() }
                    });
                    loadedFrom = "Packaged App Resource (game.json)";
                }
                catch { }
            }

            if (_game is not null)
            {
                App.CurrentGame = _game;
                
                // Initialize default game variables if they don't exist
                EnsurePlayerRoomVariable();

                // Setup Player UI variables
                PlayerNameLabel.Text = _game.Player.Name;
                MobPlayerNameLabel.Text = _game.Player.Name;
                PlayerDetailsLabel.Text = _game.Player.Gender;
                MobPlayerDetailsLabel.Text = _game.Player.Gender;

                if (!string.IsNullOrWhiteSpace(_game.Player.PortraitImagePath) && File.Exists(_game.Player.PortraitImagePath))
                {
                    PlayerPortraitImage.Source = ImageSource.FromFile(_game.Player.PortraitImagePath);
                }

                RefreshRoomContext();

                // Display where the game save file was loaded from in narrative pane
                AppendStatusMessage($"Loaded game data from: {loadedFrom}");
            }
            else
            {
                // If no game loadable, show hub alert or load standard file picker
                await DisplayAlert("Hub Launcher", "No pre-compiled game.json found. Please publish your game from the Designer to autoload here.", "OK");
            }
        }

        private void EnsurePlayerRoomVariable()
        {
            if (_game is null) return;

            var roomVar = _game.Variables.FirstOrDefault(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
            if (roomVar is null)
            {
                Guid startId = Guid.Empty;
                if (_game.Player.StartingRoom is not null)
                {
                    startId = _game.Player.StartingRoom.Id;
                }
                else if (_game.Rooms.Count > 0)
                {
                    startId = _game.Rooms[0].Id;
                }

                _game.Variables.Add(new GameVariable { Name = "player.currentRoomId", Value = startId.ToString() });
            }
        }

        private void RefreshRoomContext()
        {
            if (_game is null) return;

            var roomVar = _game.Variables.FirstOrDefault(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(roomVar, out var roomId))
            {
                _currentRoom = _game.Rooms.FirstOrDefault(r => r.Id == roomId);
            }

            if (_currentRoom is null && _game.Rooms.Count > 0)
            {
                _currentRoom = _game.Rooms[0];
            }

            if (_currentRoom is null) return;

            // 1. Update banners
            RoomNameLabel.Text = _currentRoom.Name;
            AuthorVersionLabel.Text = $"by {_game.Author} - v{_game.Version}";

            // 2. Render Scene Artwork Card
            if (!string.IsNullOrWhiteSpace(_currentRoom.PortraitImagePath) && File.Exists(_currentRoom.PortraitImagePath))
            {
                SceneArtworkImage.Source = ImageSource.FromFile(_currentRoom.PortraitImagePath);
                ArtworkPlaceholder.IsVisible = false;
                SceneArtworkImage.IsVisible = true;
            }
            else
            {
                SceneArtworkImage.Source = null;
                ArtworkPlaceholder.IsVisible = true;
                SceneArtworkImage.IsVisible = false;
            }

            // 3. Update Exit Ring compass
            var exitsCopy = new Dictionary<string, Guid>(_currentRoom.Exits);
            DesktopCompass.Exits = exitsCopy;
            MobileCompass.Exits = exitsCopy;

            // 4. Append room description to the narrative log
            AppendNarrativeEntry(_currentRoom.Name, _currentRoom.Description, isMovement: _hasMovedOnce);
            _hasMovedOnce = true;

            // 5. Populate Room Objects & Characters lists
            PopulateRoomLists();
        }

        private bool _hasMovedOnce = false;

        private void MovePlayerToRoom(Guid targetRoomId)
        {
            if (_game is null) return;

            var roomVar = _game.Variables.FirstOrDefault(v => string.Equals(v.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase));
            if (roomVar is not null)
            {
                roomVar.Value = targetRoomId.ToString();
            }
            else
            {
                _game.Variables.Add(new GameVariable { Name = "player.currentRoomId", Value = targetRoomId.ToString() });
            }

            // Fade out → refresh → fade in the narrative card
            _ = MovePlayerToRoomAnimatedAsync();
        }

        private async Task MovePlayerToRoomAnimatedAsync()
        {
            // Brief fade-out of narrative area
            await NarrativeScrollView.FadeTo(0, 120, Easing.CubicOut);
            RefreshRoomContext();
            await NarrativeScrollView.FadeTo(1, 250, Easing.CubicIn);

            // Auto-scroll log to bottom after a layout pass
            await Task.Delay(50);
            await NarrativeScrollView.ScrollToAsync(0, double.MaxValue, true);
        }

        // --- NARRATIVE LOG (APPEND MODE) ---

        /// <summary>
        /// Appends a new narrative entry (room name header + body text with inline hotlinks)
        /// to the scrolling narrative log. Each entry fades in.
        /// </summary>
        private void AppendNarrativeEntry(string roomName, string rawText, bool isMovement = false)
        {
            if (_game is null) return;

            var resolvedText = string.IsNullOrWhiteSpace(rawText)
                ? "The room is silent..."
                : RagsCore.Services.TemplateResolver.Resolve(rawText, new ActionContext(_game!, _currentRoom));

            var entry = new VerticalStackLayout { Spacing = 6, Opacity = 0 };

            if (isMovement)
            {
                // Thin separator line between rooms
                entry.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#2A2A2A"),
                    Margin = new Thickness(0, 8)
                });
            }

            // Room name header
            entry.Children.Add(new Label
            {
                Text = roomName,
                TextColor = Color.FromArgb("#00BFFF"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Body text with inline hotlinks
            var bodyView = BuildFormattedBody(resolvedText);
            entry.Children.Add(bodyView);

            NarrativeLog.Children.Add(entry);

            // Fade entry in
            entry.FadeTo(1.0, 350, Easing.CubicIn);
        }

        /// <summary>
        /// Appends a plain status/action message to the log (e.g. "You picked up the Lantern").
        /// </summary>
        private async void AppendStatusMessage(string message)
        {
            var label = new Label
            {
                Text = message,
                TextColor = Color.FromArgb("#888888"),
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 4),
                Opacity = 0
            };
            NarrativeLog.Children.Add(label);
            _ = label.FadeTo(1.0, 250, Easing.CubicIn);
            
            // Allow layout pass to compute new height, then scroll
            await Task.Delay(100);
            await NarrativeScrollView.ScrollToAsync(0, double.MaxValue, true);
        }

        private View BuildFormattedBody(string resolvedText)
        {
            var container = new VerticalStackLayout { Spacing = 6 };

            // Split the narrative into paragraphs by newlines
            var paragraphs = resolvedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    // For empty lines, add a small empty spacer
                    container.Children.Add(new BoxView { HeightRequest = 4, Color = Colors.Transparent });
                    continue;
                }

                var flex = new FlexLayout
                {
                    Wrap = FlexWrap.Wrap,
                    Direction = FlexDirection.Row,
                    AlignItems = FlexAlignItems.Start,
                    AlignContent = FlexAlignContent.Start,
                    JustifyContent = FlexJustify.Start,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var matches = Regex.Matches(para, @"\[([^\]]+)\]");
                int lastIndex = 0;

                void AddPlainWords(string plainText)
                {
                    var words = plainText.Split(new[] { ' ' }, StringSplitOptions.None);
                    for (int i = 0; i < words.Length; i++)
                    {
                        var word = words[i];
                        if (i == words.Length - 1 && string.IsNullOrEmpty(word))
                        {
                            // Skip trailing empty splits unless it's the only one
                            if (words.Length > 1) continue;
                        }

                        var label = new Label
                        {
                            Text = word + "\u00A0",
                            TextColor = Color.FromArgb("#D0D0D0"),
                            FontSize = 14,
                            LineBreakMode = LineBreakMode.NoWrap
                        };
                        flex.Children.Add(label);
                    }
                }

                foreach (Match match in matches)
                {
                    if (match.Index > lastIndex)
                    {
                        var plainText = para.Substring(lastIndex, match.Index - lastIndex);
                        AddPlainWords(plainText);
                    }

                    var entityName = match.Groups[1].Value;
                    var hyperLabel = new Label
                    {
                        Text = entityName + "\u00A0",
                        TextColor = Color.FromArgb("#00BFFF"),
                        FontAttributes = FontAttributes.Bold,
                        TextDecorations = TextDecorations.Underline,
                        FontSize = 14,
                        LineBreakMode = LineBreakMode.NoWrap
                    };

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (s, e) => HandleInlineEntityClicked(entityName);
                    hyperLabel.GestureRecognizers.Add(tap);

                    flex.Children.Add(hyperLabel);
                    lastIndex = match.Index + match.Length;
                }

                if (lastIndex < para.Length)
                {
                    var plainText = para.Substring(lastIndex);
                    AddPlainWords(plainText);
                }

                container.Children.Add(flex);
            }

            return container;
        }

        // Keep legacy RenderNarrative for single-use display (e.g. examine entity)
        private void RenderNarrative(string rawText)
        {
            if (_game is null) return;
            var resolvedText = string.IsNullOrWhiteSpace(rawText)
                ? "Nothing to describe."
                : RagsCore.Services.TemplateResolver.Resolve(rawText, new ActionContext(_game!, _currentRoom));

            AppendStatusMessage($"\u00BB {resolvedText}");
        }

        private void HandleInlineEntityClicked(string name)
        {
            if (_game is null || _currentRoom is null) return;

            // 1. Search if it matches a Room Character in the current room
            var ch = _game.Characters.FirstOrDefault(c => _currentRoom.ObjectIds.Contains(c.Id) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (ch is not null)
            {
                ShowEntityInteractionMenu(ch, false);
                return;
            }

            // 2. Search if it matches a Room Object in the current room
            var obj = _game.Objects.FirstOrDefault(o => _currentRoom.ObjectIds.Contains(o.Id) && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (obj is not null)
            {
                ShowEntityInteractionMenu(obj, false);
                return;
            }

            // 3. Search if it matches an item in the Player's inventory
            var invObj = _game.Player.Inventory.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (invObj is not null)
            {
                ShowEntityInteractionMenu(invObj, true);
                return;
            }

            // 4. Search if it matches an Exit in the current room
            var exitPair = _currentRoom.Exits.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
            if (exitPair.Key is not null)
            {
                MovePlayerToRoom(exitPair.Value);
                return;
            }

            // 5. Fallback: Search globally in game objects (so we can at least examine/interact with it)
            var globalObj = _game.Objects.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (globalObj is not null)
            {
                ShowEntityInteractionMenu(globalObj, false);
                return;
            }
        }

        // --- POPULATING GRID CARDS & LISTS ---
        private void PopulateRoomLists()
        {
            if (_game is null || _currentRoom is null) return;

            // 1. Objects in room
            RoomObjectsList.Children.Clear();
            MobRoomObjectsList.Children.Clear();
            var objects = _game.Objects.Where(o => _currentRoom.ObjectIds.Contains(o.Id) && !_game.Characters.Any(c => c.Id == o.Id)).ToList();
            foreach (var obj in objects)
            {
                RoomObjectsList.Children.Add(CreateEntityRow(obj, false));
                MobRoomObjectsList.Children.Add(CreateEntityRow(obj, false));
            }

            // 2. Characters in room
            RoomCharactersList.Children.Clear();
            MobRoomCharactersList.Children.Clear();
            var characters = _game.Characters.Where(c => _currentRoom.ObjectIds.Contains(c.Id)).ToList();
            foreach (var ch in characters)
            {
                RoomCharactersList.Children.Add(CreateEntityRow(ch, false));
                MobRoomCharactersList.Children.Add(CreateEntityRow(ch, false));
            }

            // 3. Player Inventory
            PlayerInventoryList.Children.Clear();
            MobPlayerInventoryList.Children.Clear();
            foreach (var item in _game.Player.Inventory)
            {
                PlayerInventoryList.Children.Add(CreateEntityRow(item, true));
                MobPlayerInventoryList.Children.Add(CreateEntityRow(item, true));
            }
        }

        private View CreateEntityRow(GameObject entity, bool isInventory)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Padding = new Thickness(6, 4),
                Margin = new Thickness(0, 2),
                BackgroundColor = Color.FromArgb("#1E1E1E")
            };

            // Entity icon / visual bullet
            var icon = new Border
            {
                WidthRequest = 8,
                HeightRequest = 8,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Background = entity is Character ? SolidColorBrush.PaleVioletRed : SolidColorBrush.SkyBlue,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            grid.Add(icon, 0);

            var label = new Label
            {
                Text = entity.Name,
                TextColor = Color.FromArgb("#FFFFFF"),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center
            };
            grid.Add(label, 1);

            // Double tap / tap gestures
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => ShowEntityInteractionMenu(entity, isInventory);
            grid.GestureRecognizers.Add(tap);

            return grid;
        }

        private async void ShowEntityInteractionMenu(GameObject entity, bool isInventory)
        {
            if (_game is null || _currentRoom is null) return;

            // Only show actions explicitly defined by the game designer
            var activeActions = entity.Actions.Where(a => a.InitallyActive).ToList();

            if (activeActions.Count == 0)
            {
                AppendStatusMessage($"({entity.Name} has no actions defined.)");
                return;
            }

            var options = activeActions.Select(a => a.Name).ToArray();

            var selection = await DisplayActionSheet(entity.Name, "Cancel", null, options);
            if (string.IsNullOrWhiteSpace(selection) || selection == "Cancel") return;

            var ctx = new ActionContext(_game, _currentRoom, entity);
            var chosen = activeActions.FirstOrDefault(a => string.Equals(a.Name, selection, StringComparison.Ordinal));
            if (chosen is null) return;

            ActionExecutor.Execute(chosen, ctx, this);
        }


        // ── IGameEventSink implementation ──────────────────────────────────────

        /// <summary>
        /// Called by ActionExecutor after every GameCommand completes.
        /// Routes each command type to the correct UI update.
        /// </summary>
        public void OnCommandExecuted(GameCommand cmd, ActionContext ctx)
        {
            // All UI updates must run on the main thread
            MainThread.BeginInvokeOnMainThread(() => HandleCommandEffect(cmd, ctx));
        }

        public void OnConditionEvaluated(RagsCore.Actions.Condition cond, bool result, ActionContext ctx)
        {
            // Conditions are structural — no direct UI feedback needed.
            // Could log "[IF] <TypeName> → true/false" in debug builds.
            System.Diagnostics.Debug.WriteLine($"[Condition] {cond.TypeName} = {result}");
        }

        private void HandleCommandEffect(GameCommand cmd, ActionContext ctx)
        {
            if (_game is null) return;

            switch (cmd)
            {
                case DisplayTextCommand dtc:
                    // Resolve any {variable} tokens before displaying
                    var resolved = RagsCore.Services.TemplateResolver.Resolve(dtc.Text, ctx);
                    AppendStatusMessage(resolved);
                    break;

                case MovePlayerToRoomCommand mpc:
                    // Room Id was already written to player.currentRoomId variable by the command.
                    // Trigger the animated room transition.
                    _ = MovePlayerToRoomAnimatedAsync();
                    break;

                case AddObjectToRoomCommand:
                case RemoveObjectFromRoomCommand:
                    PopulateRoomLists();
                    break;

                case SetVariableCommand svc:
                    // If a script set player.currentRoomId directly, trigger room change
                    if (string.Equals(svc.Name, "player.currentRoomId", StringComparison.OrdinalIgnoreCase))
                        _ = MovePlayerToRoomAnimatedAsync();
                    break;

                case PlayerSetNameCommand:
                    PlayerNameLabel.Text  = _game.Player.Name;
                    MobPlayerNameLabel.Text = _game.Player.Name;
                    break;

                case PlayerSetGenderCommand:
                    PlayerDetailsLabel.Text    = _game.Player.Gender;
                    MobPlayerDetailsLabel.Text = _game.Player.Gender;
                    break;

                case PlayerSetDescriptionCommand:
                    // Optionally show the new description in the log
                    AppendStatusMessage($"[Player] {_game.Player.Description}");
                    break;

                case PlayerSetPortraitMediaCommand:
                    // The command already updated ctx.Player.PortraitImagePath
                    var newPortrait = _game.Player.PortraitImagePath;
                    if (!string.IsNullOrWhiteSpace(newPortrait) && File.Exists(newPortrait))
                        PlayerPortraitImage.Source = ImageSource.FromFile(newPortrait);
                    break;

                case DisplayMultimediaCommand dmc:
                    // Command stored the resolved media ID in a variable; look it up
                    var resolvedMediaId = RagsCore.Services.TemplateResolver.Resolve(dmc.MediaId, ctx);
                    var mediaAsset = _game.MediaAssets.FirstOrDefault(a => a.Id.ToString() == resolvedMediaId);
                    if (mediaAsset is not null && File.Exists(mediaAsset.RelativePath))
                    {
                        SceneArtworkImage.Source     = ImageSource.FromFile(mediaAsset.RelativePath);
                        ArtworkPlaceholder.IsVisible = false;
                        SceneArtworkImage.IsVisible  = true;
                    }
                    break;

                case CharacterDisplayPortraitCommand cdpc:
                    var resolvedPortId = RagsCore.Services.TemplateResolver.Resolve(cdpc.PortraitId, ctx);
                    var portAsset = _game.MediaAssets.FirstOrDefault(a => a.Id.ToString() == resolvedPortId);
                    if (portAsset is not null && File.Exists(portAsset.RelativePath))
                    {
                        SceneArtworkImage.Source     = ImageSource.FromFile(portAsset.RelativePath);
                        ArtworkPlaceholder.IsVisible = false;
                        SceneArtworkImage.IsVisible  = true;
                    }
                    break;

                case PlaySoundEffectCommand psc:
                    // Future: wire to IMediaPlayer service. Log for now.
                    var soundId = RagsCore.Services.TemplateResolver.Resolve(psc.SoundId, ctx);
                    System.Diagnostics.Debug.WriteLine($"[Sound] Play: {soundId} @ {psc.Volume}%");
                    break;

                // All other commands (comments, variable math, etc.) need no direct UI update
                default:
                    break;
            }
        }

        // ── Player Portrait Tap ────────────────────────────────────────────────

        private async void OnPlayerPortraitTapped(object? sender, TappedEventArgs e)
        {
            if (_game is null || _currentRoom is null) return;
            await ShowActionsMenu("Player", _game.Player.Actions,
                new ActionContext(_game, _currentRoom, focusEntity: _game.Player));
        }

        // ── Room Actions Chip ──────────────────────────────────────────────────

        private async void OnRoomActionsChipTapped(object? sender, TappedEventArgs e)
        {
            if (_game is null || _currentRoom is null) return;
            if (_currentRoom.Actions.Count == 0)
            {
                AppendStatusMessage("(No actions defined for this room.)");
                return;
            }
            await ShowActionsMenu(_currentRoom.Name, _currentRoom.Actions,
                new ActionContext(_game, _currentRoom, focusEntity: _currentRoom));
        }

        /// <summary>
        /// Generic action menu: displays a sheet of active actions, runs the selected one.
        /// Works for Player, Room, Object, Character — any entity with an Actions list.
        /// </summary>
        private async Task ShowActionsMenu(
            string entityName,
            IEnumerable<RagsCore.Models.Action> actions,
            ActionContext ctx)
        {
            var activeActions = actions.Where(a => a.InitallyActive).ToList();
            if (activeActions.Count == 0)
            {
                AppendStatusMessage($"({entityName} has no active actions.)");
                return;
            }

            var options = activeActions.Select(a => $"⚡ {a.Name}").ToArray();
            var selection = await DisplayActionSheet(entityName, "Cancel", null, options);

            if (string.IsNullOrWhiteSpace(selection) || selection == "Cancel") return;

            var chosen = activeActions.FirstOrDefault(
                a => string.Equals($"⚡ {a.Name}", selection, StringComparison.Ordinal));
            if (chosen is null) return;

            ActionExecutor.Execute(chosen, ctx, this);
        }

        // --- RESPONSIVE ADAPTIVE RESIZING ---
        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            bool isWide = this.Width > 800;
            DesktopControlDeck.IsVisible = isWide;
            MobileControlDeck.IsVisible = !isWide;

            if (isWide)
            {
                TopGrid.ColumnDefinitions[0].Width = new GridLength(6, GridUnitType.Star);
                TopGrid.ColumnDefinitions[1].Width = new GridLength(4, GridUnitType.Star);
            }
            else
            {
                TopGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                TopGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Absolute);
            }
        }

        // --- MOBILE TABS STATE TOGGLER ---
        private void OnMobileTabClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            var tabName = btn.CommandParameter as string;
            if (string.IsNullOrWhiteSpace(tabName)) return;

            // Reset headers text colors
            TabBtn_Compass.TextColor = Color.FromArgb("#A0A0A0");
            TabBtn_Compass.BackgroundColor = Color.FromArgb("#1A1A1A");
            TabBtn_Room.TextColor = Color.FromArgb("#A0A0A0");
            TabBtn_Room.BackgroundColor = Color.FromArgb("#1A1A1A");
            TabBtn_Inv.TextColor = Color.FromArgb("#A0A0A0");
            TabBtn_Inv.BackgroundColor = Color.FromArgb("#1A1A1A");

            // Reset cards visibility
            MobileCard_Compass.IsVisible = false;
            MobileCard_Room.IsVisible = false;
            MobileCard_Inv.IsVisible = false;

            // Enable selected
            btn.TextColor = Color.FromArgb("#FFFFFF");
            btn.BackgroundColor = Color.FromArgb("#262626");

            if (tabName == "Compass")
            {
                MobileCard_Compass.IsVisible = true;
            }
            else if (tabName == "Room")
            {
                MobileCard_Room.IsVisible = true;
            }
            else if (tabName == "Inventory")
            {
                MobileCard_Inv.IsVisible = true;
            }
        }

        private async void OnGamesHubClicked(object sender, EventArgs e)
        {
            var res = await DisplayAlert("Games Hub", "Open a standalone JSON game package?", "Select File", "Cancel");
            if (!res) return;

            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select Game Story (json)",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".json" } },
                        { DevicePlatform.Android, new[] { "application/json" } },
                        { DevicePlatform.iOS, new[] { "public.json" } }
                    })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    var game = await GameLoader.LoadFromFileAsync(result.FullPath);
                    if (game is not null)
                    {
                        _game = game;
                        App.CurrentGame = _game;
                        EnsurePlayerRoomVariable();

                        PlayerNameLabel.Text = _game.Player.Name;
                        MobPlayerNameLabel.Text = _game.Player.Name;
                        PlayerDetailsLabel.Text = _game.Player.Gender;
                        MobPlayerDetailsLabel.Text = _game.Player.Gender;

                        if (!string.IsNullOrWhiteSpace(_game.Player.PortraitImagePath) && File.Exists(_game.Player.PortraitImagePath))
                        {
                            PlayerPortraitImage.Source = ImageSource.FromFile(_game.Player.PortraitImagePath);
                        }

                        RefreshRoomContext();
                        await DisplayAlert("Loaded", $"'{_game.Title}' loaded successfully!", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Selected file is not a valid RagsNext game JSON package.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("File Picker", $"Failed to load file: {ex.Message}", "OK");
            }
        }

        private string? GetLatestDesignerSavePath()
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                
                // 1. Check unpackaged designer AppData
                var unpackagedDir = Path.Combine(localAppData, "RagNext");
                var candidatePaths = new List<string>();
                
                if (Directory.Exists(unpackagedDir))
                {
                    var gameJson = Path.Combine(unpackagedDir, "game.json");
                    if (File.Exists(gameJson)) candidatePaths.Add(gameJson);
                    
                    var savesDir = Path.Combine(unpackagedDir, "saves");
                    if (Directory.Exists(savesDir))
                    {
                        var files = Directory.GetFiles(savesDir, "*.json");
                        candidatePaths.AddRange(files);
                    }
                }
                
                // 2. Check packaged designer AppData
                var packagesDir = Path.Combine(localAppData, "Packages");
                if (Directory.Exists(packagesDir))
                {
                    var matchingDirs = Directory.GetDirectories(packagesDir, "*RagNext*");
                    foreach (var dir in matchingDirs)
                    {
                        var localState = Path.Combine(dir, "LocalState");
                        if (Directory.Exists(localState))
                        {
                            var gameJson = Path.Combine(localState, "game.json");
                            if (File.Exists(gameJson)) candidatePaths.Add(gameJson);
                            
                            var savesDir = Path.Combine(localState, "saves");
                            if (Directory.Exists(savesDir))
                            {
                                var files = Directory.GetFiles(savesDir, "*.json");
                                candidatePaths.AddRange(files);
                            }
                        }
                    }
                }
                
                if (candidatePaths.Count > 0)
                {
                    return candidatePaths
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to locate developer saves: {ex.Message}");
            }
            
            return null;
        }
    }
}
