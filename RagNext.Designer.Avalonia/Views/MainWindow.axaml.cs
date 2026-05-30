using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using RagNext.Designer.Avalonia.ViewModels;
using RagNext.Designer.Avalonia.Services;
using RagsCore.Models;
using RagsCore.Actions;
using RagNext.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private bool _isWebViewLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            // Setup MediaLibraryViewModel hooks
            MediaLibraryViewModel.PickMultipleFilesAsync = async () =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    AllowMultiple = true,
                    Title = "Import Media Assets"
                });
                return global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select(files, f => f.Path.LocalPath));
            };

            MediaLibraryViewModel.PromptInputAsync = async (title, message) =>
            {
                return await PromptDialog.ShowAsync(this, title, message);
            };

            MediaLibraryViewModel.ConfirmDialogAsync = async (title, message) =>
            {
                return await ConfirmDialog.ShowAsync(this, title, message);
            };
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainWindowViewModel.IsVisualEditing))
                    {
                        if (vm.IsVisualEditing)
                        {
                            EnsureWebViewLoaded();
                        }
                    }
                };

                vm.Media.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MediaLibraryViewModel.SelectedFilePath))
                    {
                        UpdateMediaPreview(vm.Media);
                    }
                };

                // Splash screen 3.5 seconds timer transition
                if (vm.ShowWelcomeOverlay)
                {
                    var timer = new global::Avalonia.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3.5)
                    };
                    timer.Tick += (st, se) =>
                    {
                        vm.ShowSplashOverlay = false;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }

        private void EnsureWebViewLoaded()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var htmlPath = Path.Combine(baseDir, "WebAssets", "graph_editor.html");
                if (!File.Exists(htmlPath))
                {
                    htmlPath = Path.Combine(baseDir, "graph_editor.html");
                }

                if (File.Exists(htmlPath))
                {
                    if (!_isWebViewLoaded)
                    {
                        _isWebViewLoaded = true;
                        CanvasWebView.NavigationStarted += OnWebViewNavigationStarted;
                        CanvasWebView.NavigationCompleted += async (s, e) =>
                        {
                            await Task.Delay(300); // Wait for scripts to settle
                            LoadGraphData();
                        };
                        CanvasWebView.WebMessageReceived += (s, args) =>
                        {
                            try
                            {
                                string? message = null;
                                var type = args.GetType();
                                var bodyProp = type.GetProperty("Body") ?? type.GetProperty("WebMessageAsJson") ?? type.GetProperty("Message");
                                if (bodyProp != null)
                                {
                                    message = bodyProp.GetValue(args) as string;
                                }
                                
                                if (!string.IsNullOrEmpty(message))
                                {
                                    HandleRagsAction(message);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MainWindow] WebMessage callback error: {ex.Message}");
                            }
                        };
                        CanvasWebView.Source = new Uri(htmlPath);
                    }
                    else
                    {
                        // WebView is already loaded; load the active action's graph data directly!
                        LoadGraphData();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load webview canvas: {ex.Message}");
            }
        }

        private async void LoadGraphData()
        {
            if (DataContext is not MainWindowViewModel vm || vm.CurrentGame == null || vm.ActiveAction == null) return;

            try
            {
                var activeAction = vm.ActiveAction;
                var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    ReferenceHandler = ReferenceHandler.Preserve,
                    WriteIndented = false
                };
                settings.Converters.Add(new JsonStringEnumConverter());
                string actionJson = JsonSerializer.Serialize(activeAction, settings);

                // Load available commands & conditions catalogs to feed the web catalog
                string commandsJson = "{\"commands\":[]}";
                string conditionsJson = "{\"conditions\":[]}";
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var cmdsPath = Path.Combine(baseDir, "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "WebAssets", "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "Resources", "Raw", "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "..", "..", "..", "..", "RagNext", "Resources", "Raw", "Commands.json");
                    if (File.Exists(cmdsPath)) commandsJson = await File.ReadAllTextAsync(cmdsPath);

                    var condsPath = Path.Combine(baseDir, "Conditions.json");
                    if (!File.Exists(condsPath)) condsPath = Path.Combine(baseDir, "WebAssets", "Conditions.json");
                    if (!File.Exists(condsPath)) condsPath = Path.Combine(baseDir, "Resources", "Raw", "Conditions.json");
                    if (!File.Exists(condsPath)) condsPath = Path.Combine(baseDir, "..", "..", "..", "..", "RagNext", "Resources", "Raw", "Conditions.json");
                    if (File.Exists(condsPath)) conditionsJson = await File.ReadAllTextAsync(condsPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load catalog files: {ex.Message}");
                }

                // Gather dynamic target options (rooms, characters, objects, variables) for properties autocomplete dropdown mapping
                var catalogsObj = new
                {
                    rooms = vm.CurrentGame.Rooms.Select(r => r.Name).ToList(),
                    characters = vm.CurrentGame.Characters.Select(c => c.Name).ToList(),
                    objects = vm.CurrentGame.Objects.Select(o => o.Name).ToList(),
                    variables = vm.CurrentGame.Variables.Select(v => v.Name).ToList()
                };
                string catalogsJson = JsonSerializer.Serialize(catalogsObj);

                // Compile polymorphic reflection mapping
                var reflectionList = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => typeof(ActionStep).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(t => {
                        return new { TypeName = t.Name, Discriminator = t.Name };
                    }).ToList();
                string reflectionJson = JsonSerializer.Serialize(reflectionList);

                string actionBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(actionJson));
                string commandsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(commandsJson));
                string conditionsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(conditionsJson));
                string catalogsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(catalogsJson));
                string reflectionBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(reflectionJson));

                // Pass loaded catalogs databases to WebView graph catalog parser
                string jsCall = $"if (typeof loadActionGraph === 'function') {{ loadActionGraph(JSON.parse(atob('{actionBase64}')), JSON.parse(atob('{commandsBase64}')), JSON.parse(atob('{conditionsBase64}')), JSON.parse(atob('{catalogsBase64}')), JSON.parse(atob('{reflectionBase64}'))); }}";
                await CanvasWebView.InvokeScript(jsCall);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading graph data: {ex.Message}");
            }
        }

        public void OnCancelLoadClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ShowSavesOverlay = false;
            }
        }

        public async void OnSyncGraphClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm || vm.CurrentGame == null || vm.ActiveAction == null) return;

            try
            {
                var base64Json = await CanvasWebView.InvokeScript("saveAndSyncCsharp()");
                if (string.IsNullOrEmpty(base64Json) || base64Json == "undefined") return;

                SyncGraphData(base64Json);

                // Exit visual editing cleanly and return back to details panel
                vm.IsVisualEditing = false;
                vm.ActiveAction = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to sync canvas: {ex.Message}");
            }
        }

        private void OnWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
        {
            var url = e.Request?.ToString() ?? "";
            if (url.StartsWith("rags-action://"))
            {
                e.Cancel = true; // Cancel navigation to prevent reload!
                try
                {
                    var uri = new Uri(url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    
                    if (url.StartsWith("rags-action://sync"))
                    {
                        string base64 = query["data"] ?? "";
                        if (!string.IsNullOrEmpty(base64))
                        {
                            SyncGraphData(base64);
                        }
                    }
                    else if (url.StartsWith("rags-action://ai"))
                    {
                        string nodeId = query["nodeId"] ?? "";
                        string fieldName = query["fieldName"] ?? "";
                        string currentText = query["currentText"] ?? "";
                        TriggerAICoAuthor(nodeId, fieldName, currentText);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView navigation interception failed: {ex.Message}");
                }
            }
        }

        private void HandleRagsAction(string msg)
        {
            try
            {
                if (msg.StartsWith("sync?data="))
                {
                    string base64 = msg.Substring("sync?data=".Length);
                    SyncGraphData(base64);
                }
                else if (msg.StartsWith("ai?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("ai?".Length));
                    string nodeId = query["nodeId"] ?? "";
                    string fieldName = query["fieldName"] ?? "";
                    string currentText = query["currentText"] ?? "";
                    TriggerAICoAuthor(nodeId, fieldName, currentText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] WebMessage action failed: {ex.Message}");
            }
        }

        private async void SyncGraphData(string base64)
        {
            if (DataContext is not MainWindowViewModel vm || vm.CurrentGame == null || vm.ActiveAction == null) return;

            try
            {
                string cleanBase64 = base64.Trim('\"', '\'');
                if (cleanBase64.StartsWith("data:"))
                {
                    int commaIx = cleanBase64.IndexOf(',');
                    if (commaIx >= 0) cleanBase64 = cleanBase64.Substring(commaIx + 1);
                }

                var bytes = Convert.FromBase64String(cleanBase64);
                string json = Encoding.UTF8.GetString(bytes);

                var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    ReferenceHandler = ReferenceHandler.Preserve
                };
                settings.Converters.Add(new JsonStringEnumConverter());
                var imported = JsonSerializer.Deserialize<RagsCore.Models.Action>(json, settings);

                if (imported != null)
                {
                    var target = vm.ActiveAction;
                    target.Name = imported.Name;
                    target.Trigger = imported.Trigger;
                    target.InitallyActive = imported.InitallyActive;
                    target.Nodes.Clear();
                    foreach (var node in imported.Nodes)
                    {
                        target.Nodes.Add(node);
                    }

                    // Save immediately
                    await GameStorage.SaveAsync(vm.CurrentGame, vm.CurrentGame.Title);
                    vm.RunValidation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-sync graph: {ex.Message}");
            }
        }

        private async void TriggerAICoAuthor(string nodeId, string fieldName, string currentText)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var endpoint = vm.Preferences.AiCoAuthorEndpoint;
            var apiKey = vm.Preferences.AiCoAuthorKey;
            var model = vm.Preferences.AiCoAuthorModel;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                await ConfirmDialog.ShowAsync(this, "AI Dialogue Co-Author", "Please set your AI Co-Author API Key in Preferences / Settings first.");
                return;
            }

            // Ask the user for their prompt instructions using the existing PromptDialog
            var prompt = await PromptDialog.ShowAsync(this, "✨ AI Co-Author", $"Enter instructions to improve this text:\n\n\"{currentText}\"");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            // Notify Javascript to display loading/spinning status on node AI trigger button
            await CanvasWebView.InvokeScript($"if (typeof showNodeAISpinner === 'function') {{ showNodeAISpinner('{nodeId}', '{fieldName}', true); }}");

            try
            {
                using var client = new HttpClient();
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                var finalPrompt = $"Here is the current game text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional interactive fiction and adventure game writer. Improve, expand, or rewrite the provided game text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated game text directly, with no extra conversational remarks, introductions, explanations, or quotes." },
                        new { role = "user", content = finalPrompt }
                    },
                    temperature = 0.7
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                var url = endpoint.TrimEnd('/') + "/chat/completions";
                var response = await client.PostAsync(url, requestContent);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"AI provider error: {response.StatusCode} - {responseJson}");
                }

                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        var base64Result = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
                        await CanvasWebView.InvokeScript($"if (typeof updateNodeAIResult === 'function') {{ updateNodeAIResult('{nodeId}', '{fieldName}', atob('{base64Result}')); }}");
                    }
                }
            }
            catch (Exception ex)
            {
                await ConfirmDialog.ShowAsync(this, "AI Assist Error", ex.Message);
            }
            finally
            {
                await CanvasWebView.InvokeScript($"if (typeof showNodeAISpinner === 'function') {{ showNodeAISpinner('{nodeId}', '{fieldName}', false); }}");
            }
        }

        // Room Exits & Navigation Code-Behind Sync Logic
        private static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> _opposites =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["North"] = "South",
                ["South"] = "North",
                ["East"]  = "West",
                ["West"]  = "East",
                ["NorthWest"] = "SouthEast",
                ["SouthEast"] = "NorthWest",
                ["NorthEast"] = "SouthWest",
                ["SouthWest"] = "NorthEast",
                ["Up"]    = "Down",
                ["Down"]  = "Up",
                ["In"]    = "Out",
                ["Out"]   = "In",
            };

        private record ExitControl(ComboBox Picker, CheckBox OneWay, CheckBox Locked, string Direction);
        private System.Collections.Generic.List<ExitControl>? _exitControls;
        private bool _suppressExitEvents;

        private void OnRoomsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var roomsList = sender as ListBox;
            if (roomsList?.SelectedItem is Room room)
            {
                LoadExits(room);
                LoadRoomObjects(room);
            }
            if (RoomDetailsScrollViewer != null)
            {
                RoomDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
            }
        }

        private void OnCharsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CharDetailsScrollViewer != null)
            {
                CharDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
            }
        }

        private void OnObjectsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ObjectDetailsScrollViewer != null)
            {
                ObjectDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
            }
        }

        private void LoadExits(Room room)
        {
            var game = App.CurrentGame;
            if (game is null) return;

            var allRooms = game.Rooms.ToList();

            _exitControls ??= new System.Collections.Generic.List<ExitControl>
            {
                new(NorthPicker, NorthOneWay, NorthLocked, "North"),
                new(SouthPicker, SouthOneWay, SouthLocked, "South"),
                new(EastPicker,  EastOneWay,  EastLocked,  "East"),
                new(WestPicker,  WestOneWay,  WestLocked,  "West"),
                new(NorthWestPicker, NorthWestOneWay, NorthWestLocked, "NorthWest"),
                new(NorthEastPicker, NorthEastOneWay, NorthEastLocked, "NorthEast"),
                new(SouthWestPicker, SouthWestOneWay, SouthWestLocked, "SouthWest"),
                new(SouthEastPicker, SouthEastOneWay, SouthEastLocked, "SouthEast"),
                new(UpPicker,    UpOneWay,    UpLocked,    "Up"),
                new(DownPicker,  DownOneWay,  DownLocked,  "Down"),
                new(InPicker,    InOneWay,    InLocked,    "In"),
                new(OutPicker,   OutOneWay,   OutLocked,   "Out"),
            };

            _suppressExitEvents = true;
            try
            {
                foreach (var ec in _exitControls)
                {
                    ec.Picker.SelectionChanged -= OnExitPickerChanged;
                    ec.OneWay.IsCheckedChanged -= OnExitOneWayChanged;
                    ec.Locked.IsCheckedChanged -= OnExitLockedChanged;

                    ec.Picker.ItemsSource = allRooms;

                    if (room.Exits.TryGetValue(ec.Direction, out var destId))
                    {
                        var destRoom = allRooms.FirstOrDefault(r => r.Id == destId);
                        ec.Picker.SelectedItem = destRoom;

                        if (_opposites.TryGetValue(ec.Direction, out var opposite))
                        {
                            var hasBackLink = destRoom is not null
                                && destRoom.Exits.TryGetValue(opposite, out var backId)
                                && backId == room.Id;
                            ec.OneWay.IsChecked = !hasBackLink;
                        }
                        else
                        {
                            ec.OneWay.IsChecked = false;
                        }

                        ec.Locked.IsChecked = room.LockedExits.TryGetValue(ec.Direction, out var isLocked) && isLocked;
                    }
                    else
                    {
                        ec.Picker.SelectedItem = null;
                        ec.OneWay.IsChecked    = false;
                        ec.Locked.IsChecked    = false;
                    }

                    ec.Picker.SelectionChanged += OnExitPickerChanged;
                    ec.OneWay.IsCheckedChanged += OnExitOneWayChanged;
                    ec.Locked.IsCheckedChanged += OnExitLockedChanged;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnExitPickerChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not ComboBox picker) return;
            if (RoomsList.SelectedItem is not Room room) return;

            var ec = _exitControls?.FirstOrDefault(x => x.Picker == picker);
            if (ec is null) return;

            var game = App.CurrentGame;
            if (game is null) return;

            var destRoom = picker.SelectedItem as Room;

            _suppressExitEvents = true;
            try
            {
                if (destRoom is null)
                {
                    room.Exits.Remove(ec.Direction);
                    room.LockedExits.Remove(ec.Direction);
                    ec.OneWay.IsChecked = false;
                    ec.Locked.IsChecked = false;

                    if (_opposites.TryGetValue(ec.Direction, out var opp))
                    {
                        foreach (var r in game.Rooms)
                        {
                            if (r.Exits.TryGetValue(opp, out var backId) && backId == room.Id)
                            {
                                r.Exits.Remove(opp);
                                r.LockedExits.Remove(opp);
                            }
                        }
                    }
                }
                else
                {
                    room.Exits[ec.Direction] = destRoom.Id;

                    if (ec.OneWay.IsChecked == false && _opposites.TryGetValue(ec.Direction, out var opposite))
                    {
                        destRoom.Exits[opposite] = room.Id;
                    }

                    bool hasBack = _opposites.TryGetValue(ec.Direction, out var opp2)
                        && destRoom.Exits.TryGetValue(opp2, out var backId)
                        && backId == room.Id;
                    ec.OneWay.IsChecked = !hasBack;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnExitOneWayChanged(object? sender, RoutedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not CheckBox cb) return;
            if (RoomsList.SelectedItem is not Room room) return;

            var ec = _exitControls?.FirstOrDefault(x => x.OneWay == cb);
            if (ec is null) return;

            var destRoom = ec.Picker.SelectedItem as Room;
            if (destRoom is null) return;
            if (!_opposites.TryGetValue(ec.Direction, out var opposite)) return;

            _suppressExitEvents = true;
            try
            {
                if (cb.IsChecked == true)
                {
                    destRoom.Exits.Remove(opposite);
                    destRoom.LockedExits.Remove(opposite);
                }
                else
                {
                    destRoom.Exits[opposite] = room.Id;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnExitLockedChanged(object? sender, RoutedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not CheckBox cb) return;
            if (RoomsList.SelectedItem is not Room room) return;

            var ec = _exitControls?.FirstOrDefault(x => x.Locked == cb);
            if (ec is null) return;

            room.LockedExits[ec.Direction] = cb.IsChecked == true;
        }

        // Room Objects checklist sync logic
        private void LoadRoomObjects(Room room)
        {
            var game = App.CurrentGame;
            if (game?.Objects is null) return;

            var list = new System.Collections.Generic.List<ObjectCheckItem>();
            foreach (var obj in game.Objects)
            {
                bool isChecked = room.ObjectIds.Contains(obj.Id);
                list.Add(new ObjectCheckItem(obj.Id, obj.Name, isChecked));
            }
            ObjectsCheckList.ItemsSource = list;
        }

        private void OnRoomObjectClicked(object? sender, RoutedEventArgs e)
        {
            if (RoomsList.SelectedItem is not Room room) return;
            if (sender is CheckBox cb && cb.DataContext is ObjectCheckItem item)
            {
                if (cb.IsChecked == true)
                {
                    if (!room.ObjectIds.Contains(item.Id))
                    {
                        room.ObjectIds.Add(item.Id);
                    }
                }
                else
                {
                    room.ObjectIds.Remove(item.Id);
                }
            }
        }

        private async void OnDragDropFile(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null || !files.Any()) return;

            var paths = files.Select(f => f.Path.LocalPath).ToArray();
            if (paths.Length == 0) return;

            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            var border = sender as Border;
            bool isPortraitDrop = border != null && border.Tag is string;

            if (isPortraitDrop)
            {
                string dropType = border!.Tag as string ?? "";
                string folderName = dropType switch
                {
                    "Player" => "Players",
                    "Room" => "Rooms",
                    "Character" => "Characters",
                    "Object" => "Objects",
                    _ => "General"
                };

                // Find or create folder in Media Library
                MediaFolder? targetFolder = null;
                var rootNode = vm.Media.Roots.FirstOrDefault();
                if (rootNode != null)
                {
                    var matchNode = rootNode.Children.FirstOrDefault(c => string.Equals(c.Name, folderName, StringComparison.OrdinalIgnoreCase));
                    if (matchNode != null)
                    {
                        targetFolder = matchNode.Folder;
                    }
                    else if (rootNode.Folder != null)
                    {
                        var newFolder = new MediaFolder { Name = folderName };
                        rootNode.Folder.Children.Add(newFolder);
                        targetFolder = newFolder;
                    }
                }

                // Ingest files
                await vm.Media.ImportFilesFromPathsAsync(paths, targetFolder);

                // Assign the first ingested file's local path to the portrait property
                if (targetFolder != null && targetFolder.AssetIds.Any())
                {
                    var game = App.CurrentGame;
                    if (game != null)
                    {
                        var lastAddedAssetId = targetFolder.AssetIds.LastOrDefault();
                        var asset = game.MediaAssets.FirstOrDefault(a => a.Id == lastAddedAssetId);
                        if (asset != null)
                        {
                            var localPathReal = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, asset);

                            if (dropType == "Player")
                            {
                                game.Player.PortraitImagePath = localPathReal;
                            }
                            else if (dropType == "Room" && RoomsList.SelectedItem is Room room)
                            {
                                room.PortraitImagePath = localPathReal;
                            }
                            else if (dropType == "Character" && CharsList.SelectedItem is Character character)
                            {
                                character.PortraitImagePath = localPathReal;
                            }
                            else if (dropType == "Object" && ObjectsList.SelectedItem is GameObject obj)
                            {
                                obj.PortraitImagePath = localPathReal;
                            }
                        }
                    }
                }
            }
            else
            {
                // Ingest files to the currently selected folder in media catalog
                await vm.Media.ImportFilesFromPathsAsync(paths);
            }
        }

        private void UpdateMediaPreview(MediaLibraryViewModel mediaVm)
        {
            try
            {
                if (PreviewWebView == null) return;

                if ((mediaVm.IsSelectedAudio || mediaVm.IsSelectedVideo) && !string.IsNullOrEmpty(mediaVm.SelectedFilePath))
                {
                    var filePath = mediaVm.SelectedFilePath;
                    
                    var tempHtmlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
                    if (!Directory.Exists(tempHtmlDir))
                    {
                        Directory.CreateDirectory(tempHtmlDir);
                    }
                    var tempHtmlPath = Path.Combine(tempHtmlDir, "media_player.html");

                    var fileUri = new Uri(filePath).AbsoluteUri;
                    
                    string tag = mediaVm.IsSelectedAudio 
                        ? $"<audio src=\"{fileUri}\" controls autoplay style=\"width: 100%; outline: none;\"></audio>" 
                        : $"<video src=\"{fileUri}\" controls autoplay style=\"width: 100%; max-height: 100%; border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);\"></video>";

                    string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    background-color: #13131F;
    color: #F0F0F4;
    font-family: 'Segoe UI', -apple-system, sans-serif;
    margin: 0;
    padding: 8px;
    display: flex;
    justify-content: center;
    align-items: center;
    height: calc(100vh - 16px);
    overflow: hidden;
  }}
  .player-container {{
    width: 100%;
    max-width: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
  }}
</style>
</head>
<body>
  <div class=""player-container"">
    {tag}
  </div>
</body>
</html>";

                    File.WriteAllText(tempHtmlPath, htmlContent, Encoding.UTF8);
                    PreviewWebView.Source = new Uri(tempHtmlPath);
                    PreviewWebView.IsVisible = true;
                }
                else
                {
                    PreviewWebView.IsVisible = false;
                    PreviewWebView.Source = new Uri("about:blank");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update media preview: {ex.Message}");
            }
        }
    }

    public class ObjectCheckItem
    {
        public Guid Id { get; }
        public string Name { get; }
        public bool IsChecked { get; set; }
        public ObjectCheckItem(Guid id, string name, bool isChecked)
        {
            Id = id;
            Name = name;
            IsChecked = isChecked;
        }
    }

    public static class PromptDialog
    {
        public static Task<string> ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource<string>();
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = global::Avalonia.Media.Brush.Parse("#14141E"),
                Foreground = global::Avalonia.Media.Brushes.White,
                Padding = new global::Avalonia.Thickness(20)
            };

            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(new TextBlock { Text = message, Foreground = global::Avalonia.Media.Brushes.Gray });
            var input = new TextBox { PlaceholderText = "Enter value...", Background = global::Avalonia.Media.Brush.Parse("#13131F"), Foreground = global::Avalonia.Media.Brushes.White, BorderBrush = global::Avalonia.Media.Brush.Parse("#33334A") };
            stack.Children.Add(input);

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var okBtn = new Button { Content = "OK", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#2B2B3A"), Foreground = global::Avalonia.Media.Brushes.White };

            okBtn.Click += (s, e) => { tcs.SetResult(input.Text ?? ""); dialog.Close(); };
            cancelBtn.Click += (s, e) => { tcs.SetResult(""); dialog.Close(); };

            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
        }
    }

    public static class ConfirmDialog
    {
        public static Task<bool> ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = global::Avalonia.Media.Brush.Parse("#14141E"),
                Foreground = global::Avalonia.Media.Brushes.White,
                Padding = new global::Avalonia.Thickness(20)
            };

            var stack = new StackPanel { Spacing = 16 };
            stack.Children.Add(new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Foreground = global::Avalonia.Media.Brushes.Gray });

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var yesBtn = new Button { Content = "Yes", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White };
            var noBtn = new Button { Content = "No", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#2B2B3A"), Foreground = global::Avalonia.Media.Brushes.White };

            yesBtn.Click += (s, e) => { tcs.SetResult(true); dialog.Close(); };
            noBtn.Click += (s, e) => { tcs.SetResult(false); dialog.Close(); };

            buttons.Children.Add(yesBtn);
            buttons.Children.Add(noBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
        }
    }

    public class ImagePathToBitmapConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly ImagePathToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return new global::Avalonia.Media.Imaging.Bitmap(path);
                    }
                    catch
                    {
                        // Fall through
                    }
                }
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var fullPath = Path.Combine(baseDir, path);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        return new global::Avalonia.Media.Imaging.Bitmap(fullPath);
                    }
                    catch
                    {
                        // Fall through
                    }
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}