using Microsoft.Maui.Controls;
using RagNext.ViewModels;
using RagsCore.Models;
using RagNext.Converters;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using System;
using System.Text.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RagNext.Services;

namespace RagNext.Views.Controls
{
    public partial class ActionTreeView : ContentView
    {
        public ActionTreeView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        public static readonly BindableProperty PlayerProperty =
            BindableProperty.Create(nameof(Player), typeof(Player), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty RoomProperty =
            BindableProperty.Create(nameof(Room), typeof(Room), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty GameObjectProperty =
            BindableProperty.Create(nameof(GameObject), typeof(GameObject), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty CharacterProperty =
            BindableProperty.Create(nameof(Character), typeof(Character), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty ActionsProperty =
            BindableProperty.Create(nameof(Actions), typeof(System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>), typeof(ActionTreeView), propertyChanged: OnContextChanged);

        public static readonly BindableProperty HostElementTypeProperty =
            BindableProperty.Create(nameof(HostElementType), typeof(string), typeof(ActionTreeView), defaultValue: "GameObject");

        public Player? Player
        {
            get => (Player?)GetValue(PlayerProperty);
            set => SetValue(PlayerProperty, value);
        }

        public Room? Room
        {
            get => (Room?)GetValue(RoomProperty);
            set => SetValue(RoomProperty, value);
        }

        public GameObject? GameObject
        {
            get => (GameObject?)GetValue(GameObjectProperty);
            set => SetValue(GameObjectProperty, value);
        }

        public Character? Character
        {
            get => (Character?)GetValue(CharacterProperty);
            set => SetValue(CharacterProperty, value);
        }

        public System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>? Actions
        {
            get => (System.Collections.ObjectModel.ObservableCollection<RagsCore.Models.Action>?)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public string HostElementType
        {
            get => (string)GetValue(HostElementTypeProperty);
            set => SetValue(HostElementTypeProperty, value);
        }

        private DateTime _lastContextChangedTime = DateTime.UtcNow;

        private static void OnContextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var self = (ActionTreeView)bindable;
            self._lastContextChangedTime = DateTime.UtcNow;
            if (self.Actions != null)
                self.BindingContext = new ActionLibraryViewModel(self.Actions, self.HostElementType);
            else if (self.Player != null)
                self.BindingContext = new ActionLibraryViewModel(self.Player);
            else if (self.Room != null)
                self.BindingContext = new ActionLibraryViewModel(self.Room);
            else if (self.GameObject != null)
                self.BindingContext = new ActionLibraryViewModel(self.GameObject);
            else if (self.Character != null)
                self.BindingContext = new ActionLibraryViewModel(self.Character);
        }

        private void OnExpanderExpandedChanged(object? sender, ExpandedChangedEventArgs e)
        {
            if (sender is not View view || !e.IsExpanded)
                return;

            // Ignore expander events that fire during initial loading/context binding to prevent scrolling to the bottom on load
            if (DateTime.UtcNow - _lastContextChangedTime < TimeSpan.FromMilliseconds(800))
                return;

            if (BindingContext is ActionLibraryViewModel vm && vm.IsRebuilding)
                return;

            // Delayed execution to allow the expander expansion layout to propagate and update height
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), async () =>
            {
                var scrollView = FindParentScrollView(view);
                if (scrollView != null)
                {
                    // Scroll to position the newly expanded tree node perfectly in view (using MakeVisible to prevent massive layout shifts)
                    await scrollView.ScrollToAsync(view, ScrollToPosition.MakeVisible, true);

                    // If we are close to the bottom, force scroll all the way to the absolute bottom to clear the "scroll for more" warning.
                    var contentHeight = (scrollView.Content as VisualElement)?.Height ?? 0;
                    var viewport = scrollView.Height;
                    var maxScroll = Math.Max(0, contentHeight - viewport);
                    if (maxScroll > 0 && scrollView.ScrollY >= maxScroll - 40)
                    {
                        await scrollView.ScrollToAsync(0, maxScroll, true);
                    }
                }
            });
        }

        private ScrollView? FindParentScrollView(Element? element)
        {
            var parent = element?.Parent;
            while (parent != null)
            {
                if (parent is ScrollView scrollView)
                {
                    return scrollView;
                }
                parent = parent.Parent;
            }
            return null;
        }
        private double _startWidth = 280;

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startWidth = ParentGrid.ColumnDefinitions[0].Width.Value;
                    break;
                case GestureStatus.Running:
                    double newWidth = _startWidth + e.TotalX;
                    newWidth = System.Math.Clamp(newWidth, 180, 700);
                    ParentGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(newWidth));
                    break;
            }
        }

        private void OnPointerEntered(object sender, PointerEventArgs e)
        {
            HoverSplitterLine.FadeTo(1, 100);
            GripperBadge.ScaleTo(1.15, 100);
        }

        private void OnPointerExited(object sender, PointerEventArgs e)
        {
            HoverSplitterLine.FadeTo(0, 100);
            GripperBadge.ScaleTo(1.0, 100);
        }

        private bool _isGraphMode = false;

        private async void OnToggleModeClicked(object sender, EventArgs e)
        {
            if (_isGraphMode)
            {
                // Warn about unconnected nodes when exiting graph editor
                try
                {
                    var hasUnconnectedJson = await GraphWebView.EvaluateJavaScriptAsync("checkUnconnectedNodes()");
                    if (hasUnconnectedJson == "true")
                    {
                        var page = Shell.Current.CurrentPage;
                        if (page != null)
                        {
                            bool discard = await page.DisplayAlert(
                                "⚠️ Unconnected Drafts",
                                "You have unconnected nodes on the canvas. Switching to List View will discard these drafts. Would you like to discard them and switch?",
                                "Discard & Switch",
                                "Keep Editing");

                            if (!discard)
                            {
                                // Keep editing in graph mode
                                return;
                            }
                        }
                    }

                    // Force sync and save latest connected graph state back to C#
                    var base64Json = await GraphWebView.EvaluateJavaScriptAsync("saveAndSyncCsharp()");
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: base64Json received, length={base64Json?.Length ?? 0}");
                    string? base64 = ExtractBase64(base64Json);
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Extracted base64 length={base64?.Length ?? 0}");
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var bytes = Convert.FromBase64String(base64);
                        string json = System.Text.Encoding.UTF8.GetString(bytes);
                        System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Decoded JSON={json}");

                        var settings = GetJsonOptions();
                        var imported = System.Text.Json.JsonSerializer.Deserialize<RagsCore.Models.Action>(json, settings);
                        System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Deserialized Action={imported?.Name ?? "NULL"}, Nodes={imported?.Nodes?.Count ?? 0}");

                        if (imported != null && BindingContext is ActionLibraryViewModel vm && _activeAction != null)
                        {
                            var target = _activeAction;
                            System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Syncing to backing Target={target.Name}, Current Nodes={target.Nodes.Count}");
                            if (target != null)
                            {
                                target.Name = imported.Name;
                                target.Trigger = imported.Trigger;
                                target.Nodes.Clear();
                                foreach (var node in imported.Nodes)
                                {
                                    target.Nodes.Add(node);
                                }
                                System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Backing Target nodes updated. Count={target.Nodes.Count}");

                                // Force rebuild tree view to reflect newly saved visual node-graph structural changes
                                vm.RebuildTree();
                                System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: vm.RebuildTree() executed.");
                                
                                // Force save game
                                if (App.CurrentGame != null)
                                {
                                    _ = Services.GameStorage.SaveAsync(App.CurrentGame);
                                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: GameStorage.SaveAsync triggered.");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: BindingContext/ActiveAction check failed. Imported={imported != null}, VM={BindingContext is ActionLibraryViewModel}, _activeAction={_activeAction != null}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnToggleModeClicked: Exception={ex.Message}");
                }

                _isGraphMode = false;
                ToggleModeBtn.Text = "🎨 Visual Graph Editor";
                TreeScrollView.IsVisible = true;
                SplitterBar.IsVisible = true;
                EditorScrollView.IsVisible = true;
                GraphWebView.IsVisible = false;
            }
            else
            {
                _isGraphMode = true;
                ToggleModeBtn.Text = "📝 List Tree View";
                TreeScrollView.IsVisible = false;
                SplitterBar.IsVisible = false;
                EditorScrollView.IsVisible = false;
                GraphWebView.IsVisible = true;

                // Sync current Action JSON into WebView
                LoadActionIntoWebView();
            }
        }

        private async void OnUnloaded(object? sender, EventArgs e)
        {
            if (_isGraphMode)
            {
                try
                {
                    // Force a synchronous-style save on unload to guarantee zero data loss
                    var base64Json = await GraphWebView.EvaluateJavaScriptAsync("saveAndSyncCsharp()");
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: base64Json received, length={base64Json?.Length ?? 0}");
                    string? base64 = ExtractBase64(base64Json);
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Extracted base64 length={base64?.Length ?? 0}");
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var bytes = Convert.FromBase64String(base64);
                        string json = System.Text.Encoding.UTF8.GetString(bytes);
                        System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Decoded JSON={json}");

                        var settings = GetJsonOptions();
                        var imported = System.Text.Json.JsonSerializer.Deserialize<RagsCore.Models.Action>(json, settings);
                        System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Deserialized Action={imported?.Name ?? "NULL"}, Nodes={imported?.Nodes?.Count ?? 0}");

                        if (imported != null && BindingContext is ActionLibraryViewModel vm && _activeAction != null)
                        {
                            var target = _activeAction;
                            System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Syncing to backing Target={target.Name}, Current Nodes={target.Nodes.Count}");
                            if (target != null)
                            {
                                target.Name = imported.Name;
                                target.Trigger = imported.Trigger;
                                target.Nodes.Clear();
                                foreach (var node in imported.Nodes)
                                {
                                    target.Nodes.Add(node);
                                }
                                System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Backing Target nodes updated. Count={target.Nodes.Count}");

                                // Force save game
                                if (App.CurrentGame != null)
                                {
                                    await Services.GameStorage.SaveAsync(App.CurrentGame);
                                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: GameStorage.SaveAsync completed.");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: BindingContext/ActiveAction check failed. Imported={imported != null}, VM={BindingContext is ActionLibraryViewModel}, _activeAction={_activeAction != null}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] OnUnloaded: Exception={ex.Message}");
                }
            }
        }

        private RagsCore.Models.Action? _activeAction;

        private async Task<string> ReadPackageFileAsync(string filename)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(filename);
                using var reader = new System.IO.StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read package file {filename}: {ex.Message}");
                return "{}";
            }
        }

        private async void LoadActionIntoWebView()
        {
            if (BindingContext is not ActionLibraryViewModel vm)
            {
                await GraphWebView.EvaluateJavaScriptAsync("loadActionGraph(null)");
                return;
            }

            RagsCore.Models.Action? action = null;
            if (vm.ActionEditor != null)
            {
                action = vm.ActionEditor.GetUnderlyingAction();
            }
            else if (vm.Selected != null)
            {
                var cur = vm.Selected;
                while (cur != null)
                {
                    if (cur.Kind == ActionLibraryViewModel.NodeKind.Action && cur.Model is RagsCore.Models.Action act)
                    {
                        action = act;
                        break;
                    }
                    cur = cur.Parent;
                }
            }

            _activeAction = action;

            // Load dynamically reflected commands and conditions database based strictly on C# implementations!
            string commandsJson = GenerateDynamicCommandsJson(false);
            string conditionsJson = GenerateDynamicCommandsJson(true);

            var game = App.CurrentGame;
            string catalogJson = "{}";
            if (game != null)
            {
                var catalogObj = new
                {
                    Rooms = game.Rooms.Select(r => new { Id = r.Id.ToString(), Name = r.Name }).ToList(),
                    GameObjects = game.Objects.Select(o => new { Id = o.Id.ToString(), Name = o.Name }).ToList(),
                    Characters = game.Characters.Select(c => new { Id = c.Id.ToString(), Name = c.Name }).ToList(),
                    Variables = game.Variables.Select(v => new { Name = v.Name }).ToList(),
                    Media = game.MediaAssets.Select(m => new { Id = m.Id.ToString(), Name = m.Name, Kind = m.Kind.ToString() }).ToList(),
                    Functions = game.Functions.Select(f => new { Id = f.Id.ToString(), Name = f.Name }).ToList(),
                    Timers = game.Timers.Select(t => new { Id = t.Id.ToString(), Name = t.Name }).ToList()
                };
                catalogJson = System.Text.Json.JsonSerializer.Serialize(catalogObj);
            }

            // Generate reflection types map for command discriminator mapping
            var derivedTypes = typeof(RagsCore.Actions.ActionStep).GetCustomAttributes<JsonDerivedTypeAttribute>()
                .Select(attr => new {
                    Type = attr.DerivedType,
                    Discriminator = attr.TypeDiscriminator as string
                })
                .Where(x => x.Discriminator != null)
                .Select(x => {
                    var instance = (RagsCore.Actions.ActionStep)Activator.CreateInstance(x.Type)!;
                    return new {
                        Discriminator = x.Discriminator!,
                        TypeName = instance.TypeName,
                        Kind = instance.Kind.ToString()
                    };
                })
                .ToList();
            string typesMapJson = System.Text.Json.JsonSerializer.Serialize(derivedTypes);

            var settings = GetJsonOptions();

            string actionJson = action != null ? System.Text.Json.JsonSerializer.Serialize(action, settings) : "null";

            string actionBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(actionJson));
            string commandsBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(commandsJson));
            string conditionsBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(conditionsJson));
            string catalogBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(catalogJson));
            string typesMapBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(typesMapJson));

            string jsCall = $"loadActionGraph(" +
                            $"JSON.parse(atob('{actionBase64}')), " +
                            $"JSON.parse(atob('{commandsBase64}')), " +
                            $"JSON.parse(atob('{conditionsBase64}')), " +
                            $"JSON.parse(atob('{catalogBase64}')), " +
                            $"JSON.parse(atob('{typesMapBase64}'))" +
                            $")";

            await GraphWebView.EvaluateJavaScriptAsync(jsCall);
        }

        private async void TriggerAICoAuthor(string nodeId, string fieldName, string currentText)
        {
            var page = Shell.Current.CurrentPage;
            if (page == null) return;

            var ai = App.Current?.Handler?.MauiContext?.Services.GetService<IAIChatService>();
            if (ai == null)
            {
                await page.DisplayAlert("AI Assist Error", "AI Chat Service is not registered or configured.", "OK");
                return;
            }

            // Start popup with empty or helper text rather than slapping entire text, or allow user to replace it.
            // Let's pass the current text so they can see/edit it, but if they change it we process it as instructions.
            var popup = new RagNext.Views.Popups.AIPromptPopup("Ask AI", currentText);
            await page.ShowPopupAsync(popup);

            var prompt = popup.PromptText;
            if (string.IsNullOrWhiteSpace(prompt) || popup.IsCancelled) return;

            // Notify Javascript to display loading/spinning status on node AI trigger button
            await GraphWebView.EvaluateJavaScriptAsync($"showNodeAISpinner('{nodeId}', '{fieldName}', true)");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                string finalPrompt;
                if (prompt.Trim() == currentText.Trim())
                {
                    finalPrompt = $"Here is the current game text to expand/improve:\n\"{(string.IsNullOrWhiteSpace(currentText) ? "(Empty)" : currentText)}\"";
                }
                else
                {
                    finalPrompt = $"Here is the current game text:\n\"{(string.IsNullOrWhiteSpace(currentText) ? "(Empty)" : currentText)}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                }

                var result = await ai.AskAsync(finalPrompt, cts.Token);
                if (string.IsNullOrWhiteSpace(result)) return;

                var base64Result = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result));
                await GraphWebView.EvaluateJavaScriptAsync($"updateNodeAIResult('{nodeId}', '{fieldName}', atob('{base64Result}'))");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("AI Assist Error", ex.Message, "OK");
            }
            finally
            {
                await GraphWebView.EvaluateJavaScriptAsync($"showNodeAISpinner('{nodeId}', '{fieldName}', false)");
            }
        }

        private void OnGraphWebViewNavigating(object sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("rags-action://ai"))
            {
                e.Cancel = true; // Block actual browser navigation
                try
                {
                    var uri = new Uri(e.Url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    string nodeId = query["nodeId"] ?? "";
                    string fieldName = query["fieldName"] ?? "";
                    string currentText = query["currentText"] ?? "";

                    TriggerAICoAuthor(nodeId, fieldName, currentText);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] WebView AI invocation failed: {ex.Message}");
                }
            }
            else if (e.Url.StartsWith("rags-action://sync"))
            {
                e.Cancel = true; // Block actual browser navigation
                try
                {
                    var uri = new Uri(e.Url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    string base64 = query["data"];
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var bytes = Convert.FromBase64String(base64);
                        string json = System.Text.Encoding.UTF8.GetString(bytes);

                        var settings = GetJsonOptions();
                        var imported = System.Text.Json.JsonSerializer.Deserialize<RagsCore.Models.Action>(json, settings);

                        if (imported != null && BindingContext is ActionLibraryViewModel vm && _activeAction != null)
                        {
                            var target = _activeAction;
                            if (target != null)
                            {
                                // Sync properties back
                                target.Name = imported.Name;
                                target.Trigger = imported.Trigger;
                                target.Nodes.Clear();
                                foreach (var node in imported.Nodes)
                                {
                                    target.Nodes.Add(node);
                                }

                                // Force rebuild tree view to reflect newly saved visual node-graph structural changes
                                vm.RebuildTree();
                                
                                // Force save game
                                if (App.CurrentGame != null)
                                {
                                    _ = Services.GameStorage.SaveAsync(App.CurrentGame);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ActionTreeView] WebView message parsing failed: {ex.Message}");
                }
            }
        }


        private static string? ExtractBase64(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "null" || raw == "undefined")
                return null;

            raw = raw.Trim();
            // If it is wrapped in double quotes (standard JSON representation of a string)
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length > 1)
            {
                try
                {
                    // Attempt proper JSON string deserialization first
                    return System.Text.Json.JsonSerializer.Deserialize<string>(raw);
                }
                catch
                {
                    // Fallback to simple quote stripping
                    return raw.Substring(1, raw.Length - 2);
                }
            }
            // If it is wrapped in single quotes
            if (raw.StartsWith("'") && raw.EndsWith("'") && raw.Length > 1)
            {
                return raw.Substring(1, raw.Length - 2);
            }

            return raw;
        }

        private static System.Text.Json.JsonSerializerOptions GetJsonOptions()
        {
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
            opts.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            opts.Converters.Add(new RagsCore.Actions.StepDefinitionBaseJsonConverter());
            return opts;
        }

        private string GenerateDynamicCommandsJson(bool isCondition)
        {
            var baseType = isCondition ? typeof(RagsCore.Actions.Condition) : typeof(RagsCore.Actions.GameCommand);
            var derivedTypes = typeof(RagsCore.Actions.ActionStep).GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>()
                .Where(attr => attr.DerivedType.IsSubclassOf(baseType) && !attr.DerivedType.IsAbstract)
                .Select(attr => {
                    var instance = (RagsCore.Actions.ActionStep)Activator.CreateInstance(attr.DerivedType)!;
                    
                    var props = attr.DerivedType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(p => p.CanWrite && p.Name != "Label" && p.Name != "TrueBranch" && p.Name != "FalseBranch" && p.Name != "X" && p.Name != "Y")
                        .Select(p => {
                            string controlType = GetPropertyControlType(p);
                            string dataType = GetPropertyDataType(p);
                            return new {
                                label = p.Name,
                                controlType = controlType,
                                dataType = dataType
                            };
                        })
                        .ToList();

                    string category = "General";
                    string name = instance.TypeName;
                    if (name.Contains(":"))
                    {
                        var parts = name.Split(':');
                        category = parts[0].Trim();
                        name = parts[1].Trim();
                    }

                    return new {
                        name = instance.TypeName,
                        category = category,
                        inputs = props
                    };
                })
                .ToList();

            var wrapper = isCondition 
                ? (object)new { conditions = derivedTypes } 
                : (object)new { commands = derivedTypes };

            return System.Text.Json.JsonSerializer.Serialize(wrapper);
        }

        private static string GetPropertyControlType(System.Reflection.PropertyInfo p)
        {
            if (p.PropertyType == typeof(bool)) return "Checkbox";
            if (p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(float)) return "Number";
            if (p.PropertyType.IsEnum || p.Name == "StoreVariableName" || p.Name == "InputType" || p.PropertyType == typeof(Guid) || p.Name.Contains("Id")) return "ComboBox";
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName" || p.Name == "SourceName") && 
                (p.DeclaringType != null && (p.DeclaringType.Name.Contains("Variable") || p.DeclaringType.Name.Contains("Random") || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType)))) 
                return "ComboBox"; 
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return "ComboBox";
            if (p.Name.Equals("Gender", StringComparison.OrdinalIgnoreCase)) return "ComboBox";
            if (p.Name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return "ComboBox";
            if (p.Name.Contains("Text", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Description", StringComparison.OrdinalIgnoreCase)) return "TextArea";
            return "Text";
        }

        private static string GetPropertyDataType(System.Reflection.PropertyInfo p)
        {
            if (p.Name == "StoreVariableName") return "Variable";
            if (p.Name.Equals("RoomId", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("DestinationRoomId", StringComparison.OrdinalIgnoreCase)) return "Room";
            if (p.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("ContainerObjectId", StringComparison.OrdinalIgnoreCase)) return "GameObject";
            if (p.Name.Equals("CharacterId", StringComparison.OrdinalIgnoreCase)) return "Character";
            if (p.Name.Equals("ItemId", StringComparison.OrdinalIgnoreCase)) return "Item";
            if (p.Name.Equals("SoundId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("PortraitId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("MediaId", StringComparison.OrdinalIgnoreCase)) return "Media";
            if (p.Name.Equals("Comparison", StringComparison.OrdinalIgnoreCase)) return "Operator";
            if (p.Name.Equals("Direction", StringComparison.OrdinalIgnoreCase)) return "Direction";
            if (p.Name.Equals("FunctionId", StringComparison.OrdinalIgnoreCase)) return "Function";
            if (p.Name.Equals("TimerId", StringComparison.OrdinalIgnoreCase)) return "Timer";
            if ((p.Name == "Name" || p.Name == "NameA" || p.Name == "NameB" || p.Name == "VariableName" || p.Name == "SourceName") && 
                (p.DeclaringType != null && (p.DeclaringType.Name.Contains("Variable") || p.DeclaringType.Name.Contains("Random") || typeof(RagsCore.Actions.Condition).IsAssignableFrom(p.DeclaringType)))) 
                return "Variable";
            return "String";
        }
    }
}