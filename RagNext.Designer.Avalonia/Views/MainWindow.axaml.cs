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

            AddHandler(TextBox.KeyUpEvent, OnTextBoxKeyUp, RoutingStrategies.Bubble, true);
            AddHandler(TextBox.KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Bubble, true);

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

            MainWindowViewModel.PickFolderAsync = async () =>
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "Select Standalone Publish Export Directory"
                });
                var folder = global::System.Linq.Enumerable.FirstOrDefault(folders);
                return folder?.Path.LocalPath ?? string.Empty;
            };

            MediaLibraryViewModel.PromptInputAsync = async (title, message) =>
            {
                return await PromptDialog.ShowAsync(this, title, message);
            };

            MediaLibraryViewModel.ConfirmDialogAsync = async (title, message) =>
            {
                return await ConfirmDialog.ShowAsync(this, title, message);
            };

            // Responsive sizing subscription to prevent native airspace overlap/spillout
            SplashPreviewParentPanel.SizeChanged += OnSplashPreviewParentPanelSizeChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PlaySplashVideoPreviewTransition = async (style, fadeIn, hold, fadeOut) =>
                {
                    if (SplashPreviewWebView != null)
                    {
                        await SplashPreviewWebView.InvokeScript($"if (typeof playTransition === 'function') {{ playTransition('{style}', {fadeIn}, {hold}, {fadeOut}); }}");
                    }
                };

                vm.StopSplashVideoPreview = () =>
                {
                    if (SplashPreviewWebView != null)
                    {
                        _ = SplashPreviewWebView.InvokeScript("var video = document.querySelector('video'); if (video) { video.pause(); video.currentTime = 0; }");
                    }
                };

                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainWindowViewModel.IsVisualEditing))
                    {
                        if (vm.IsVisualEditing)
                        {
                            EnsureWebViewLoaded();
                        }
                    }
                    else if (ev.PropertyName == nameof(MainWindowViewModel.ActiveView))
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (PlayerDetailsScrollViewer != null) PlayerDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (RoomDetailsScrollViewer != null) RoomDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (CharDetailsScrollViewer != null) CharDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (ObjectDetailsScrollViewer != null) ObjectDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                        }, global::Avalonia.Threading.DispatcherPriority.Background);

                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (PlayerDetailsScrollViewer != null) PlayerDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (RoomDetailsScrollViewer != null) RoomDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (CharDetailsScrollViewer != null) CharDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                            if (ObjectDetailsScrollViewer != null) ObjectDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0);
                        }, global::Avalonia.Threading.DispatcherPriority.Loaded);

                        UpdateSplashVideoPreview(vm);
                    }
                    else if (ev.PropertyName == nameof(MainWindowViewModel.SplashBackgroundPath) ||
                             ev.PropertyName == nameof(MainWindowViewModel.IsSplashVideoMode) ||
                             ev.PropertyName == nameof(MainWindowViewModel.IsSplashVideoPreviewVisible))
                    {
                        UpdateSplashVideoPreview(vm);
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

                // Find owner context attributes
                System.Collections.Generic.List<string> ownerAttributes = new();
                if (vm.CurrentGame.Player.Actions.Any(a => a.Id == activeAction.Id))
                {
                    ownerAttributes = vm.CurrentGame.Player.Attributes.Select(a => a.Name).ToList();
                }
                else
                {
                    var ownerRoom = vm.CurrentGame.Rooms.FirstOrDefault(r => r.Actions.Any(a => a.Id == activeAction.Id));
                    if (ownerRoom != null)
                    {
                        ownerAttributes = ownerRoom.Attributes.Select(a => a.Name).ToList();
                    }
                    else
                    {
                        var ownerObj = vm.CurrentGame.Objects.FirstOrDefault(o => o.Actions.Any(a => a.Id == activeAction.Id));
                        if (ownerObj != null)
                        {
                            ownerAttributes = ownerObj.Attributes.Select(a => a.Name).ToList();
                        }
                        else
                        {
                            var ownerChar = vm.CurrentGame.Characters.FirstOrDefault(c => c.Actions.Any(a => a.Id == activeAction.Id));
                            if (ownerChar != null)
                            {
                                ownerAttributes = ownerChar.Attributes.Select(a => a.Name).ToList();
                            }
                        }
                    }
                }

                // Gather dynamic target options (rooms, characters, objects, variables) for properties autocomplete dropdown mapping
                var catalogsObj = new
                {
                    Rooms = vm.CurrentGame.Rooms.Select(r => new { Id = r.Id.ToString(), Name = r.Name, Attributes = r.Attributes.Select(a => a.Name).ToList() }).ToList(),
                    Characters = vm.CurrentGame.Characters.Select(c => new { Id = c.Id.ToString(), Name = c.Name, Attributes = c.Attributes.Select(a => a.Name).ToList() }).ToList(),
                    GameObjects = vm.CurrentGame.Objects.Select(o => new { Id = o.Id.ToString(), Name = o.Name, IsContainer = o.IsContainer, Attributes = o.Attributes.Select(a => a.Name).ToList() }).ToList(),
                    Variables = vm.CurrentGame.Variables.Select(v => new { Id = v.Name, Name = v.Name, Attributes = v.Attributes.Select(a => a.Name).ToList() }).ToList(),
                    Player = new { Attributes = vm.CurrentGame.Player.Attributes.Select(a => a.Name).ToList() },
                    Owner = new { Attributes = ownerAttributes },
                    Media = vm.CurrentGame.MediaAssets.Select(m => new { Id = m.RelativePath, Name = string.IsNullOrWhiteSpace(m.OriginalFileName) ? m.RelativePath : m.OriginalFileName }).ToList(),
                    Functions = vm.CurrentGame.Functions.Select(f => new { Id = f.Name, Name = f.Name }).ToList(),
                    Timers = vm.CurrentGame.Timers.Select(t => new { Id = t.Name, Name = t.Name, Attributes = t.Attributes.Select(a => a.Name).ToList() }).ToList()
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
                if (base64Json == "CANCELLED")
                {
                    return; // User cancelled saving! Keep visual editor open!
                }

                if (!string.IsNullOrEmpty(base64Json) && base64Json != "undefined")
                {
                    await SyncGraphData(base64Json);
                }
                else
                {
                    // Delay slightly to allow the webview's async window.location / rags-action sync interceptor to finish
                    await Task.Delay(250);
                }

                // Exit visual editing cleanly and return back to details panel
                vm.IsVisualEditing = false;
                vm.ActiveAction = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to sync canvas: {ex.Message}");
                vm.IsVisualEditing = false;
                vm.ActiveAction = null;
            }
        }

        private async void OnWebViewNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
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
                            await SyncGraphData(base64);
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

        private async void HandleRagsAction(string msg)
        {
            try
            {
                if (msg.StartsWith("sync?data="))
                {
                    string base64 = msg.Substring("sync?data=".Length);
                    await SyncGraphData(base64);
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

        private async Task SyncGraphData(string base64)
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
                json = ActionStep.NormalizeLegacyDiscriminators(json);

                var settings = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    ReferenceHandler = ReferenceHandler.Preserve
                };
                settings.Converters.Add(new JsonStringEnumConverter());
                settings.Converters.Add(new StepDefinitionBaseJsonConverter());
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

                    // Save immediately and make sure changes write synchronously to disk
                    await GameStorage.SaveAsync(vm.CurrentGame, vm.CurrentGame.Title, false);
                    vm.RunValidation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-sync graph: {ex.Message}");
            }
        }

        private string AppendPortToEndpoint(string endpoint, string port)
        {
            if (string.IsNullOrWhiteSpace(port)) return endpoint;
            if (string.IsNullOrWhiteSpace(endpoint)) return endpoint;

            endpoint = endpoint.Trim();
            if (endpoint.Contains(":" + port)) return endpoint;

            try
            {
                var uri = new Uri(endpoint);
                var builder = new UriBuilder(uri);
                if (int.TryParse(port, out int portNum))
                {
                    builder.Port = portNum;
                    return builder.Uri.ToString().TrimEnd('/');
                }
            }
            catch
            {
                if (endpoint.Contains("://"))
                {
                    var parts = endpoint.Split(new[] { "://" }, 2, StringSplitOptions.None);
                    var scheme = parts[0];
                    var remainder = parts[1];
                    var firstSlash = remainder.IndexOf('/');
                    if (firstSlash >= 0)
                    {
                        var host = remainder.Substring(0, firstSlash);
                        var path = remainder.Substring(firstSlash);
                        if (!host.Contains(":")) host = host + ":" + port;
                        return scheme + "://" + host + path;
                    }
                    else
                    {
                        if (!remainder.Contains(":")) remainder = remainder + ":" + port;
                        return scheme + "://" + remainder;
                    }
                }
                else
                {
                    if (!endpoint.Contains(":")) return endpoint + ":" + port;
                }
            }

            return endpoint;
        }

        private string GetAiUrl(string endpoint, string port, string provider)
        {
            var resolvedEndpoint = endpoint;
            if (!string.IsNullOrWhiteSpace(port) && port != "0")
            {
                resolvedEndpoint = AppendPortToEndpoint(endpoint, port);
            }
            if (provider != null && provider.ToUpper() == "LMSTUDIO")
            {
                return resolvedEndpoint.TrimEnd('/') + "/v1/chat/completions";
            }
            return resolvedEndpoint.TrimEnd('/') + "/chat/completions";
        }

        private async void TriggerAICoAuthor(string nodeId, string fieldName, string currentText)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var endpoint = vm.Preferences.AiCoAuthorEndpoint;
            var apiKey = vm.Preferences.AiCoAuthorKey;
            var model = vm.Preferences.AiCoAuthorModel;
            var port = vm.Preferences.AiCoAuthorPort;

            var provider = vm.Preferences.AiCoAuthorProvider;
            bool apiKeyRequired = string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase);

            if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
            {
                await ConfirmDialog.ShowAsync(this, "AI Dialogue Co-Author", "Please set your AI Co-Author API Key in Preferences / Settings first.");
                return;
            }

            // Ask the user for their prompt instructions using the existing PromptDialog
            var prompt = await PromptDialog.ShowAsync(this, "✨ AI Co-Author", $"Enter instructions to improve this text:\n\n\"{currentText}\"");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            try
            {
                try
                {
                    // Notify Javascript to display loading/spinning status on node AI trigger button
                    await CanvasWebView.InvokeScript($"if (typeof showNodeAISpinner === 'function') {{ showNodeAISpinner('{nodeId}', '{fieldName}', true); }}");
                }
                catch {}

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

                var url = GetAiUrl(endpoint, port, provider);
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
                try
                {
                    await CanvasWebView.InvokeScript($"if (typeof showNodeAISpinner === 'function') {{ showNodeAISpinner('{nodeId}', '{fieldName}', false); }}");
                }
                catch {}
            }
        }

        private async Task CoAuthorPropertyAsync(object dataObj, string propertyName)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var prop = dataObj.GetType().GetProperty(propertyName);
            if (prop == null) return;

            var currentText = prop.GetValue(dataObj) as string ?? string.Empty;

            var endpoint = vm.Preferences.AiCoAuthorEndpoint;
            var apiKey = vm.Preferences.AiCoAuthorKey;
            var model = vm.Preferences.AiCoAuthorModel;
            var port = vm.Preferences.AiCoAuthorPort;
            var provider = vm.Preferences.AiCoAuthorProvider;
            bool apiKeyRequired = string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase);

            if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
            {
                await ConfirmDialog.ShowAsync(this, "AI Co-Author", "Please set your AI Co-Author API Key in Preferences / Settings first.");
                return;
            }

            var prompt = await PromptDialog.ShowAsync(this, "✨ AI Co-Author", $"Enter instructions to improve this {propertyName.ToLower()}:\n\n\"{currentText}\"");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            try
            {
                using var client = new HttpClient();
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                var finalPrompt = $"Here is the current text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional interactive fiction writer and adventure game editor assistant. Improve, expand, or rewrite the provided text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated text directly, with no extra conversational remarks, introductions, explanations, or quotes." },
                        new { role = "user", content = finalPrompt }
                    },
                    temperature = 0.7
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var url = GetAiUrl(endpoint, port, provider);
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
                        prop.SetValue(dataObj, content);
                        if (dataObj is RagsCore.Models.BaseModel bm)
                        {
                            bm.GetType().GetMethod("OnPropertyChanged")?.Invoke(bm, new object[] { propertyName });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ConfirmDialog.ShowAsync(this, "AI Assist Error", ex.Message);
            }
        }

        private async void OnCoAuthorNameClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                await CoAuthorPropertyAsync(btn.DataContext, "Name");
            }
        }

        private async void OnCoAuthorDescriptionClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                await CoAuthorPropertyAsync(btn.DataContext, "Description");
            }
        }

        private async void OnSuggestDescriptionClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                var dataObj = btn.DataContext;
                var propName = dataObj.GetType().GetProperty("Name");
                var nameVal = propName?.GetValue(dataObj) as string ?? "Unnamed";
                
                if (DataContext is not MainWindowViewModel vm) return;

                var endpoint = vm.Preferences.AiCoAuthorEndpoint;
                var apiKey = vm.Preferences.AiCoAuthorKey;
                var model = vm.Preferences.AiCoAuthorModel;
                var port = vm.Preferences.AiCoAuthorPort;

                var provider = vm.Preferences.AiCoAuthorProvider;
                bool apiKeyRequired = string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase);

                if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
                {
                    await ConfirmDialog.ShowAsync(this, "AI Co-Author", "Please set your AI Co-Author API Key in Preferences / Settings first.");
                    return;
                }

                try
                {
                    using var client = new HttpClient();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var finalPrompt = $"Generate a vivid, sensory, second-person adventure game description for a {dataObj.GetType().Name.ToLower()} named \"{nameVal}\".";
                    var requestBody = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = vm.Preferences.AiCoAuthorAssistantPrompt },
                            new { role = "user", content = finalPrompt }
                        },
                        temperature = 0.7
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    var url = GetAiUrl(endpoint, port, provider);
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
                            var descProp = dataObj.GetType().GetProperty("Description");
                            descProp?.SetValue(dataObj, content);
                            if (dataObj is RagsCore.Models.BaseModel bm)
                            {
                                bm.GetType().GetMethod("OnPropertyChanged")?.Invoke(bm, new object[] { "Description" });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await ConfirmDialog.ShowAsync(this, "AI Assist Error", ex.Message);
                }
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
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { RoomDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0); }, global::Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private void OnCharsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CharDetailsScrollViewer != null)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { CharDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0); }, global::Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private void OnObjectsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ObjectDetailsScrollViewer != null)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { ObjectDetailsScrollViewer.Offset = new global::Avalonia.Vector(0, 0); }, global::Avalonia.Threading.DispatcherPriority.Background);
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

                // Save changes automatically
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.SaveGameCommand.Execute(null);
                }
            }
        }

        private async void OnDragDropFile(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            string[]? paths = null;
            var files = e.DataTransfer.TryGetFiles();
            if (files != null && files.Any())
            {
                paths = files.Select(f => f.Path.LocalPath).ToArray();
            }
            else
            {
                var textData = e.DataTransfer.TryGetText();
                if (!string.IsNullOrWhiteSpace(textData))
                {
                    if (File.Exists(textData))
                    {
                        paths = new[] { textData };
                    }
                }
            }

            if (paths == null || paths.Length == 0) return;

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

                var game = App.CurrentGame;
                if (game == null) return;

                string? localPathReal = null;
                var firstPath = paths[0];

                var matchingAsset = game.MediaAssets.FirstOrDefault(a => 
                    string.Equals(new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, a), firstPath, StringComparison.OrdinalIgnoreCase));

                if (matchingAsset != null)
                {
                    localPathReal = firstPath;
                }
                else
                {
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

                    if (targetFolder != null && targetFolder.AssetIds.Any())
                    {
                        var lastAddedAssetId = targetFolder.AssetIds.LastOrDefault();
                        var asset = game.MediaAssets.FirstOrDefault(a => a.Id == lastAddedAssetId);
                        if (asset != null)
                        {
                            localPathReal = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, asset);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(localPathReal))
                {
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
                    await vm.SaveGameAsync();
                }
            }
            else
            {
                // Ingest files to the currently selected folder in media catalog
                await vm.Media.ImportFilesFromPathsAsync(paths);
            }
        }

        private Point? _dragStartPoint;
        private PointerPressedEventArgs? _dragPressedEventArgs;

        private void OnMediaItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var prop = e.GetCurrentPoint(this).Properties;
            if (prop.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(this);
                _dragPressedEventArgs = e;
            }
        }

        private async void OnMediaItemPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragStartPoint.HasValue && _dragPressedEventArgs != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _dragStartPoint.Value;
                if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                {
                    var dragPressedArgs = _dragPressedEventArgs;
                    _dragStartPoint = null; 
                    _dragPressedEventArgs = null; // Clear to prevent multiple starts

                    if (sender is StackPanel panel && panel.DataContext is MediaLibraryViewModel.Node node && node.Asset != null)
                    {
                        var game = App.CurrentGame;
                        if (game != null)
                        {
                            var localPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, node.Asset);
                            var item = new DataTransferItem();
                            item.Set(DataFormat.Text, localPath);
                            var data = new DataTransfer();
                            data.Add(item);
                            await DragDrop.DoDragDropAsync(dragPressedArgs, data, DragDropEffects.Copy | DragDropEffects.Link);
                        }
                    }
                }
            }
        }

        private void OnMediaItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragStartPoint = null;
            _dragPressedEventArgs = null;
        }

        private readonly System.Collections.Generic.Dictionary<Button, object> _originalButtonContents = new();

        private void StartButtonSpinner(Button btn)
        {
            if (btn == null) return;
            if (!_originalButtonContents.ContainsKey(btn))
            {
                _originalButtonContents[btn] = btn.Content ?? "";
            }
            
            var spinnerTextBlock = new TextBlock
            {
                Text = "⟳",
                Classes = { "spinner" },
                RenderTransform = new RotateTransform(),
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Content = spinnerTextBlock;
            btn.IsEnabled = false;
        }

        private void StopButtonSpinner(Button btn)
        {
            if (btn == null) return;
            if (_originalButtonContents.TryGetValue(btn, out var originalContent))
            {
                btn.Content = originalContent;
                _originalButtonContents.Remove(btn);
            }
            btn.IsEnabled = true;
        }

        private async void OnGeneratePortraitClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string dropType = btn.Tag as string ?? "";
            if (string.IsNullOrWhiteSpace(dropType)) return;

            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            var promptResult = await GenerateImageDialog.ShowAsync(this, "🎨 Generate Portrait with AI", $"Enter a visual prompt for the {dropType.ToLower()}:");
            if (promptResult == null || promptResult.IsCancelled || string.IsNullOrWhiteSpace(promptResult.Prompt)) return;

            string prompt = promptResult.Prompt;
            int width = promptResult.Width;
            int height = promptResult.Height;

            string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
            try
            {
                StartButtonSpinner(btn);

                var provider = vm.Preferences.AiImageGenProvider;
                var endpoint = vm.Preferences.AiImageGenEndpoint;
                var apiKey = vm.Preferences.AiImageGenKey;
                var model = vm.Preferences.AiImageGenModel;
                var host = vm.Preferences.AiImageGenHost;
                var port = vm.Preferences.AiImageGenPort;

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(3);

                byte[]? imageBytes = null;

                if (string.Equals(provider, "Pollinations.ai", StringComparison.OrdinalIgnoreCase))
                {
                    var encodedPrompt = Uri.EscapeDataString(prompt);
                    var url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&model={Uri.EscapeDataString(model)}&nologo=true&enhance=true";
                    imageBytes = await client.GetByteArrayAsync(url);
                }
                else if (string.Equals(provider, "Local Stable Diffusion", StringComparison.OrdinalIgnoreCase))
                {
                    var resolvedEndpoint = endpoint;
                    if (!string.IsNullOrWhiteSpace(port) && port != "0")
                    {
                        resolvedEndpoint = AppendPortToEndpoint(endpoint, port);
                    }
                    var url = resolvedEndpoint.TrimEnd('/') + "/sdapi/v1/txt2img";

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var requestBody = new
                    {
                        prompt = prompt,
                        width = width,
                        height = height,
                        steps = 20
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, requestContent);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Image generation failed: {response.StatusCode} - {responseJson}");
                    }

                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("images", out var imagesArray) && imagesArray.ValueKind == JsonValueKind.Array && imagesArray.GetArrayLength() > 0)
                    {
                        var base64Str = imagesArray[0].GetString();
                        if (string.IsNullOrWhiteSpace(base64Str))
                        {
                            throw new Exception("No image data returned from local Stable Diffusion.");
                        }
                        imageBytes = Convert.FromBase64String(base64Str);
                    }
                    else
                    {
                        throw new Exception("Invalid local Stable Diffusion API response structure.");
                    }
                }
                else
                {
                    var resolvedEndpoint = endpoint;
                    if (!string.IsNullOrWhiteSpace(port) && port != "0")
                    {
                        resolvedEndpoint = AppendPortToEndpoint(endpoint, port);
                    }
                    var url = resolvedEndpoint.TrimEnd('/') + "/images/generations";

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var requestBody = new
                    {
                        prompt = prompt,
                        model = model,
                        n = 1,
                        size = $"{width}x{height}"
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, requestContent);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Image generation failed: {response.StatusCode} - {responseJson}");
                    }

                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array && dataArray.GetArrayLength() > 0)
                    {
                        var imgUrl = dataArray[0].GetProperty("url").GetString();
                        if (string.IsNullOrWhiteSpace(imgUrl))
                        {
                            throw new Exception("No image URL returned from API.");
                        }
                        imageBytes = await client.GetByteArrayAsync(imgUrl);
                    }
                    else
                    {
                        throw new Exception("Invalid API response structure.");
                    }
                }

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    throw new Exception("Failed to retrieve image data.");
                }

                await File.WriteAllBytesAsync(tempFilePath, imageBytes);

                string folderName = dropType switch
                {
                    "Player" => "Players",
                    "Room" => "Rooms",
                    "Character" => "Characters",
                    "Object" => "Objects",
                    _ => "General"
                };

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

                await vm.Media.ImportFilesFromPathsAsync(new[] { tempFilePath }, targetFolder);

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

                            await vm.SaveGameAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ConfirmDialog.ShowAsync(this, "AI Image Generation Error", ex.Message);
            }
            finally
            {
                StopButtonSpinner(btn);
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch {}
            }
        }

        private void UpdateMediaPreview(MediaLibraryViewModel mediaVm)
        {
            try
            {
                if (PreviewWebView == null && TabPreviewWebView == null) return;

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
                    
                    var playerUri = new Uri(tempHtmlPath);
                    if (PreviewWebView != null)
                    {
                        PreviewWebView.Source = playerUri;
                        PreviewWebView.IsVisible = true;
                    }
                    if (TabPreviewWebView != null)
                    {
                        TabPreviewWebView.Source = playerUri;
                        TabPreviewWebView.IsVisible = true;
                    }
                }
                else
                {
                    if (PreviewWebView != null)
                    {
                        PreviewWebView.IsVisible = false;
                        PreviewWebView.Source = new Uri("about:blank");
                    }
                    if (TabPreviewWebView != null)
                    {
                        TabPreviewWebView.IsVisible = false;
                        TabPreviewWebView.Source = new Uri("about:blank");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update media preview: {ex.Message}");
            }
        }

        private bool _isSyncingContainedObjects = false;

        public void OnContainedObjectCheckBoxLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.DataContext is not GameObject item) return;
            if (ObjectsList.SelectedItem is not GameObject container) return;

            _isSyncingContainedObjects = true;
            try
            {
                cb.IsEnabled = item.Id != container.Id;
                cb.IsChecked = container.ContainedObjectIds.Contains(item.Id);
            }
            finally
            {
                _isSyncingContainedObjects = false;
            }
        }

        public void OnContainedObjectCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_isSyncingContainedObjects) return;
            if (sender is not CheckBox cb || cb.DataContext is not GameObject item) return;
            if (ObjectsList.SelectedItem is not GameObject container) return;

            if (cb.IsChecked == true)
            {
                if (!container.ContainedObjectIds.Contains(item.Id))
                {
                    container.ContainedObjectIds.Add(item.Id);
                }
            }
            else
            {
                container.ContainedObjectIds.Remove(item.Id);
            }

            // Save changes automatically
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SaveGameCommand.Execute(null);
            }
        }

        private void UpdateSplashVideoPreview(MainWindowViewModel vm)
        {
            try
            {
                if (SplashPreviewWebView == null) return;

                if (vm.IsSplashVideoPreviewVisible && !string.IsNullOrEmpty(vm.SplashBackgroundPath))
                {
                    var filePath = vm.SplashBackgroundPath;
                    
                    var tempHtmlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebAssets");
                    if (!Directory.Exists(tempHtmlDir))
                    {
                        Directory.CreateDirectory(tempHtmlDir);
                    }
                    var tempHtmlPath = Path.Combine(tempHtmlDir, "splash_video_preview.html");

                    var fileUri = new Uri(filePath).AbsoluteUri;
                    
                    var splash = vm.CurrentGame?.SplashScreen;
                    string text = splash?.Text ?? "My Adventure";
                    double textX = splash?.TextX ?? 50;
                    double textY = splash?.TextY ?? 50;
                    string fontColor = splash?.FontColor ?? "#FFFFFF";
                    double fontSize = (splash?.FontSize ?? 32) * 2.4;
                    string fontName = splash?.FontName ?? "Outfit";

                    // Optimized HTML template with built-in transition physics mirroring Unity exactly
                    string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    background-color: #000000;
    margin: 0;
    padding: 0;
    width: 100vw;
    height: 100vh;
    overflow: hidden;
    position: relative;
    user-select: none;
    transition: transform 0.05s ease-out;
  }}
  video {{
    width: 100%;
    height: 100%;
    object-fit: cover;
    display: block;
  }}
  .text-overlay {{
    position: absolute;
    left: {textX}%;
    top: {textY}%;
    transform: translate(0, 0);
    color: {fontColor};
    font-size: {fontSize}px;
    font-family: '{fontName}', 'Outfit', sans-serif;
    font-weight: bold;
    z-index: 10;
    pointer-events: none;
    text-shadow: 0 2px 10px rgba(0,0,0,0.8);
    transition: transform 0.05s ease-out;
  }}
</style>
<script>
  function playTransition(style, fadeIn, hold, fadeOut) {{
      var body = document.body;
      var overlay = document.querySelector('.text-overlay');
      var video = document.querySelector('video');

      if (!overlay || !body) return;

      // Reset states
      body.style.opacity = '0';
      overlay.style.opacity = '0';
      overlay.style.transform = 'translate(0,0)';
      body.style.transform = 'scale(1)';
      
      if (video) {{
          video.currentTime = 0;
          video.play();
      }}

      var start = performance.now();
      var duration = (fadeIn + hold + fadeOut) * 1000;

      function animate(time) {{
          var elapsed = time - start;
          var progress = Math.min(elapsed / (fadeIn * 1000), 1);
          
          if (elapsed < fadeIn * 1000) {{
              // Fade In Sequence
              var imgOpacity = progress;
              var txtOpacity = progress;

              if (style === 'Rise') {{
                  overlay.style.transform = 'translate(0, ' + (60 * (1 - progress)) + 'px)';
              }} else if (style === 'Exposure') {{
                  imgOpacity = Math.pow(progress, 0.4);
              }} else if (style === 'Cinematic') {{
                  var curScale = 1.0 + 0.02 * progress;
                  body.style.transform = 'scale(' + curScale + ')';
              }} else if (style === 'Glitch') {{
                  if (Math.random() < 0.15) {{
                      txtOpacity = Math.random() * 0.5 + 0.2;
                      overlay.style.transform = 'translate(' + (Math.random() * 20 - 10) + 'px, ' + (Math.random() * 10 - 5) + 'px)';
                  }} else {{
                      overlay.style.transform = 'translate(0, 0)';
                  }}
              }}

              body.style.opacity = imgOpacity;
              overlay.style.opacity = txtOpacity;
          }} else if (elapsed < (fadeIn + hold) * 1000) {{
              // Hold State
              body.style.opacity = '1';
              overlay.style.opacity = '1';

              var holdProgress = (elapsed - fadeIn * 1000) / (hold * 1000);

              if (style === 'Cinematic') {{
                  var curScale = 1.02 + 0.03 * holdProgress;
                  body.style.transform = 'scale(' + curScale + ')';
              }} else if (style === 'Glitch') {{
                  if (Math.random() < 0.08) {{
                      overlay.style.opacity = Math.random() * 0.6 + 0.3;
                      overlay.style.transform = 'translate(' + (Math.random() * 30 - 15) + 'px, ' + (Math.random() * 16 - 8) + 'px)';
                  }} else {{
                      overlay.style.transform = 'translate(0, 0)';
                  }}
              }} else {{
                  overlay.style.transform = 'translate(0, 0)';
              }}
          }} else if (elapsed < duration) {{
              // Fade Out Sequence
              var outProgress = (elapsed - (fadeIn + hold) * 1000) / (fadeOut * 1000);
              body.style.opacity = (1 - outProgress);
              overlay.style.opacity = (1 - outProgress);

              if (style === 'Cinematic') {{
                  var curScale = 1.05 + 0.02 * outProgress;
                  body.style.transform = 'scale(' + curScale + ')';
              }}
          }} else {{
              // End State
              body.style.opacity = '1';
              overlay.style.opacity = '1';
              body.style.transform = 'scale(1)';
              overlay.style.transform = 'translate(0, 0)';
              return;
          }}

          requestAnimationFrame(animate);
      }}

      requestAnimationFrame(animate);
  }}
</script>
</head>
<body>
  <video src=""{fileUri}"" autoplay loop playsinline></video>
  <div class=""text-overlay"">{text}</div>
</body>
</html>";

                    File.WriteAllText(tempHtmlPath, htmlContent, Encoding.UTF8);
                    
                    var targetUri = new Uri(tempHtmlPath);
                    if (SplashPreviewWebView.Source != targetUri)
                    {
                        SplashPreviewWebView.Source = targetUri;
                        SplashPreviewWebView.IsVisible = true;
                    }
                    else
                    {
                        // Direct JS insertion for real-time text updates
                        string jsUpdate = $@"
                            (function() {{
                                var overlay = document.querySelector('.text-overlay');
                                if (overlay) {{
                                    overlay.innerText = `{text.Replace("`","\\`").Replace("$","\\$")}`;
                                    overlay.style.left = '{textX}%';
                                    overlay.style.top = '{textY}%';
                                    overlay.style.color = '{fontColor}';
                                    overlay.style.fontSize = '{fontSize}px';
                                    overlay.style.fontFamily = `'{fontName}', 'Outfit', sans-serif`;
                                }}
                            }})();";
                        _ = SplashPreviewWebView.InvokeScript(jsUpdate);
                    }
                }
                else
                {
                    SplashPreviewWebView.IsVisible = false;
                    SplashPreviewWebView.Source = new Uri("about:blank");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Splash video preview error: {ex.Message}");
            }
        }

        private void OnSplashPreviewParentPanelSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            try
            {
                double availableWidth = e.NewSize.Width;
                double availableHeight = e.NewSize.Height;

                if (availableWidth <= 0 || availableHeight <= 0) return;

                // Calculate maximum size that fits 16:9 ratio
                double targetWidth = availableWidth;
                double targetHeight = availableWidth * 9.0 / 16.0;

                if (targetHeight > availableHeight)
                {
                    targetHeight = availableHeight;
                    targetWidth = availableHeight * 16.0 / 9.0;
                }

                // Limit to maximum dimensions for design aesthetics (e.g. 640x360)
                if (targetWidth > 640)
                {
                    targetWidth = 640;
                    targetHeight = 360;
                }

                if (SplashPreviewContainer != null)
                {
                    SplashPreviewContainer.Width = targetWidth;
                    SplashPreviewContainer.Height = targetHeight;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Sizing error: {ex.Message}");
            }
        }

        private bool _isSavingAndClosing = false;
        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (_isSavingAndClosing)
            {
                base.OnClosing(e);
                return;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                e.Cancel = true; // Cancel standard closing to allow save to finish
                _isSavingAndClosing = true;

                try
                {
                    await vm.SaveGameAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Save on close failed: {ex.Message}");
                }

                Close(); // Re-trigger close which will pass through the first branch
            }
            else
            {
                base.OnClosing(e);
            }
        }

        private TextBox? _activeTextBox;
        private char _triggerChar;
        private int _triggerIndex = -1;

        private void OnTextBoxKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Source is TextBox textBox)
            {
                string text = textBox.Text ?? "";
                int caret = textBox.CaretIndex;
                if (caret > 0 && caret <= text.Length)
                {
                    int openBraceIndex = text.LastIndexOf('{', caret - 1);
                    int openBracketIndex = text.LastIndexOf('[', caret - 1);
                    
                    int triggerIdx = Math.Max(openBraceIndex, openBracketIndex);
                    if (triggerIdx >= 0)
                    {
                        char trigger = text[triggerIdx];
                        string sub = text.Substring(triggerIdx + 1, caret - (triggerIdx + 1));
                        
                        char close = trigger == '{' ? '}' : ']';
                        if (!sub.Contains(close) && (trigger == '[' || !sub.Contains(' ')))
                        {
                            _activeTextBox = textBox;
                            _triggerChar = trigger;
                            _triggerIndex = triggerIdx;
                            ShowAutocomplete(textBox, trigger, sub);
                            return;
                        }
                    }
                }
                HideAutocomplete();
            }
        }

        private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (_activeTextBox != null && AutocompletePopup != null && AutocompletePopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    int nextIndex = AutocompleteListBox.SelectedIndex + 1;
                    if (nextIndex < AutocompleteListBox.Items.Count)
                        AutocompleteListBox.SelectedIndex = nextIndex;
                    else
                        AutocompleteListBox.SelectedIndex = 0;
                    AutocompleteListBox.ScrollIntoView(AutocompleteListBox.SelectedItem);
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    int prevIndex = AutocompleteListBox.SelectedIndex - 1;
                    if (prevIndex >= 0)
                        AutocompleteListBox.SelectedIndex = prevIndex;
                    else
                        AutocompleteListBox.SelectedIndex = AutocompleteListBox.Items.Count - 1;
                    AutocompleteListBox.ScrollIntoView(AutocompleteListBox.SelectedItem);
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    e.Handled = true;
                    ApplySelectedAutocomplete();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    HideAutocomplete();
                }
            }
        }

        private void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
        }

        private void OnAutocompleteListBoxTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
        {
            ApplySelectedAutocomplete();
        }

        private void ApplySelectedAutocomplete()
        {
            if (_activeTextBox == null || AutocompleteListBox == null || AutocompleteListBox.SelectedItem is not AutocompleteItem selectedItem)
            {
                HideAutocomplete();
                return;
            }

            string text = _activeTextBox.Text ?? "";
            int caret = _activeTextBox.CaretIndex;
            if (_triggerIndex >= 0 && _triggerIndex < text.Length)
            {
                char close = _triggerChar == '{' ? '}' : ']';
                string replacement = _triggerChar + selectedItem.Token + close;
                
                string before = text.Substring(0, _triggerIndex);
                string after = text.Substring(caret);
                
                _activeTextBox.Text = before + replacement + after;
                _activeTextBox.CaretIndex = _triggerIndex + replacement.Length;
            }
            HideAutocomplete();
        }



        private void ShowAutocomplete(TextBox textBox, char trigger, string query)
        {
            if (AutocompleteListBox == null || AutocompletePopup == null) return;
            
            // Caret positioning approximation relative to top-left of the target TextBox
            int lineIndex = 0;
            int colIndex = 0;
            try
            {
                string text = textBox.Text ?? "";
                int caret = Math.Min(textBox.CaretIndex, text.Length);
                if (caret >= 0)
                {
                    string prefix = text.Substring(0, caret);
                    string[] lines = prefix.Split('\n');
                    lineIndex = lines.Length - 1;
                    colIndex = lines[lineIndex].Length;
                }
            }
            catch {}

            AutocompletePopup.HorizontalOffset = Math.Max(0, colIndex * 7.2 - 10);
            AutocompletePopup.VerticalOffset = Math.Max(0, lineIndex * 18 + 20);

            var list = new System.Collections.Generic.List<AutocompleteItem>();
            var game = App.CurrentGame;
            
            if (game != null)
            {
                if (trigger == '{')
                {
                    // Add local / context properties
                    list.Add(new AutocompleteItem { Token = "this.Name", DisplayToken = "{this.Name}", TypeName = "Current Object Property", Description = "Name of this object." });
                    list.Add(new AutocompleteItem { Token = "this.Description", DisplayToken = "{this.Description}", TypeName = "Current Object Property", Description = "Description of this object." });
                    list.Add(new AutocompleteItem { Token = "this.portrait", DisplayToken = "{this.portrait}", TypeName = "Current Object Property", Description = "Portrait or image path." });

                    // Add local attributes on the current context object (only attributes belonging to "this" context)
                    var attrProp = textBox.DataContext?.GetType().GetProperty("Attributes");
                    if (attrProp != null && attrProp.GetValue(textBox.DataContext) is System.Collections.IEnumerable attributesList)
                    {
                        foreach (var attrObj in attributesList)
                        {
                            var nameProp = attrObj.GetType().GetProperty("Name");
                            var nameVal = nameProp?.GetValue(attrObj) as string;
                            if (!string.IsNullOrEmpty(nameVal))
                            {
                                list.Add(new AutocompleteItem { Token = $"this.attributes.{nameVal}", DisplayToken = $"{{this.attributes.{nameVal}}}", TypeName = "Context Custom Attribute", Description = $"Context object custom attribute '{nameVal}'." });
                            }
                        }
                    }

                    list.Add(new AutocompleteItem { Token = "player.Name", DisplayToken = "{player.Name}", TypeName = "Player Property", Description = "Name of the protagonist." });
                    list.Add(new AutocompleteItem { Token = "player.Description", DisplayToken = "{player.Description}", TypeName = "Player Property", Description = "Description of the protagonist." });
                    list.Add(new AutocompleteItem { Token = "player.Gender", DisplayToken = "{player.Gender}", TypeName = "Player Property", Description = "Gender of the protagonist." });
                    list.Add(new AutocompleteItem { Token = "player.portrait", DisplayToken = "{player.portrait}", TypeName = "Player Property", Description = "Protagonist image portrait path." });

                    list.Add(new AutocompleteItem { Token = "room.Name", DisplayToken = "{room.Name}", TypeName = "Room Property", Description = "Name of current room." });
                    list.Add(new AutocompleteItem { Token = "room.Description", DisplayToken = "{room.Description}", TypeName = "Room Property", Description = "Description of current room." });
                    list.Add(new AutocompleteItem { Token = "room.portrait", DisplayToken = "{room.portrait}", TypeName = "Room Property", Description = "Image path of current room." });

                    list.Add(new AutocompleteItem { Token = "focus.Name", DisplayToken = "{focus.Name}", TypeName = "Focus Object Property", Description = "Name of current focus object." });
                    list.Add(new AutocompleteItem { Token = "focus.Description", DisplayToken = "{focus.Description}", TypeName = "Focus Object Property", Description = "Description of current focus object." });

                    // Dynamically scan and insert created custom attributes
                    var uniqueAttrNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (game.Player?.Attributes != null)
                    {
                        foreach (var a in game.Player.Attributes)
                        {
                            uniqueAttrNames.Add(a.Name);
                            list.Add(new AutocompleteItem { Token = $"player.attributes.{a.Name}", DisplayToken = $"{{player.attributes.{a.Name}}}", TypeName = "Player Custom Attribute", Description = $"Custom attribute '{a.Name}' on player." });
                        }
                    }

                    if (game.Characters != null)
                    {
                        foreach (var c in game.Characters)
                        {
                            string nameClean = c.Name.Replace(" ", "");
                            if (c.Attributes != null)
                            {
                                foreach (var a in c.Attributes)
                                {
                                    uniqueAttrNames.Add(a.Name);
                                    list.Add(new AutocompleteItem { Token = $"characters.{nameClean}.attributes.{a.Name}", DisplayToken = $"{{characters.{nameClean}.attributes.{a.Name}}}", TypeName = "Character Custom Attribute", Description = $"Custom attribute '{a.Name}' on character '{c.Name}'." });
                                }
                            }
                        }
                    }

                    if (game.Objects != null)
                    {
                        foreach (var o in game.Objects)
                        {
                            string nameClean = o.Name.Replace(" ", "");
                            if (o.Attributes != null)
                            {
                                foreach (var a in o.Attributes)
                                {
                                    uniqueAttrNames.Add(a.Name);
                                    list.Add(new AutocompleteItem { Token = $"objects.{nameClean}.attributes.{a.Name}", DisplayToken = $"{{objects.{nameClean}.attributes.{a.Name}}}", TypeName = "Object Custom Attribute", Description = $"Custom attribute '{a.Name}' on object '{o.Name}'." });
                                }
                            }
                        }
                    }

                    if (game.Rooms != null)
                    {
                        foreach (var r in game.Rooms)
                        {
                            string nameClean = r.Name.Replace(" ", "");
                            if (r.Attributes != null)
                            {
                                foreach (var a in r.Attributes)
                                {
                                    uniqueAttrNames.Add(a.Name);
                                    list.Add(new AutocompleteItem { Token = $"rooms.{nameClean}.attributes.{a.Name}", DisplayToken = $"{{rooms.{nameClean}.attributes.{a.Name}}}", TypeName = "Room Custom Attribute", Description = $"Custom attribute '{a.Name}' on room '{r.Name}'." });
                                }
                            }
                        }
                    }

                    if (game.Timers != null)
                    {
                        foreach (var t in game.Timers)
                        {
                            string nameClean = t.Name.Replace(" ", "");
                            if (t.Attributes != null)
                            {
                                foreach (var a in t.Attributes)
                                {
                                    uniqueAttrNames.Add(a.Name);
                                    list.Add(new AutocompleteItem { Token = $"timers.{nameClean}.attributes.{a.Name}", DisplayToken = $"{{timers.{nameClean}.attributes.{a.Name}}}", TypeName = "Timer Custom Attribute", Description = $"Custom attribute '{a.Name}' on timer '{t.Name}'." });
                                }
                            }
                        }
                    }



                    if (game.Variables != null)
                    {
                        foreach (var v in game.Variables)
                        {
                            list.Add(new AutocompleteItem { 
                                Token = $"variables.{v.Name}", 
                                DisplayToken = $"{{variables.{v.Name}}}", 
                                TypeName = "Player Variable", 
                                Description = $"Value: {v.Value}" 
                            });
                        }
                    }

                    if (game.Characters != null)
                    {
                        foreach (var c in game.Characters)
                        {
                            string nameClean = c.Name.Replace(" ", "");
                            list.Add(new AutocompleteItem { Token = $"characters.{nameClean}.Name", DisplayToken = $"{{characters.{nameClean}.Name}}", TypeName = "Character Property", Description = $"Name of character '{c.Name}'." });
                            list.Add(new AutocompleteItem { Token = $"characters.{nameClean}.Description", DisplayToken = $"{{characters.{nameClean}.Description}}", TypeName = "Character Property", Description = $"Description of character '{c.Name}'." });
                            list.Add(new AutocompleteItem { Token = $"characters.{nameClean}.Health", DisplayToken = $"{{characters.{nameClean}.Health}}", TypeName = "Character Property", Description = $"Health of character '{c.Name}'." });
                        }
                    }

                    if (game.Objects != null)
                    {
                        foreach (var o in game.Objects)
                        {
                            string nameClean = o.Name.Replace(" ", "");
                            list.Add(new AutocompleteItem { Token = $"objects.{nameClean}.Name", DisplayToken = $"{{objects.{nameClean}.Name}}", TypeName = "Object Property", Description = $"Name of object '{o.Name}'." });
                            list.Add(new AutocompleteItem { Token = $"objects.{nameClean}.Description", DisplayToken = $"{{objects.{nameClean}.Description}}", TypeName = "Object Property", Description = $"Description of object '{o.Name}'." });
                        }
                    }
                }
                else if (trigger == '[')
                {
                    var directions = new[] { "North", "South", "East", "West", "Up", "Down", "In", "Out" };
                    foreach (var dir in directions)
                    {
                        list.Add(new AutocompleteItem { Token = dir, DisplayToken = $"[{dir}]", TypeName = "Exit Direction", Description = "Clickable exit shortcut in player navigation." });
                    }

                    if (game.Rooms != null)
                    {
                        foreach (var r in game.Rooms)
                        {
                            list.Add(new AutocompleteItem { Token = r.Name, DisplayToken = $"[{r.Name}]", TypeName = "Room Link", Description = $"Navigation link to '{r.Name}'." });
                        }
                    }

                    if (game.Characters != null)
                    {
                        foreach (var c in game.Characters)
                        {
                            list.Add(new AutocompleteItem { Token = c.Name, DisplayToken = $"[{c.Name}]", TypeName = "Character Link", Description = $"Interactive inline link to character '{c.Name}'." });
                        }
                    }

                    if (game.Objects != null)
                    {
                        foreach (var o in game.Objects)
                        {
                            list.Add(new AutocompleteItem { Token = o.Name, DisplayToken = $"[{o.Name}]", TypeName = "Object Link", Description = $"Interactive inline link to object '{o.Name}'." });
                        }
                    }
                }
            }

            var filtered = new System.Collections.Generic.List<AutocompleteItem>();
            foreach (var item in list)
            {
                if (string.IsNullOrEmpty(query) || item.Token.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(item);
                }
            }

            if (filtered.Count == 0)
            {
                HideAutocomplete();
                return;
            }

            AutocompleteListBox.ItemsSource = filtered;
            AutocompleteListBox.SelectedIndex = 0;

            AutocompletePopup.PlacementTarget = textBox;
            AutocompletePopup.IsOpen = true;
        }

        private void HideAutocomplete()
        {
            if (AutocompletePopup != null)
            {
                AutocompletePopup.IsOpen = false;
            }
            _activeTextBox = null;
            _triggerIndex = -1;
        }
    }

    public class AutocompleteItem
    {
        public string Token { get; set; } = "";
        public string DisplayToken { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string Description { get; set; } = "";
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
    public class GenerateImageResult
    {
        public bool IsCancelled { get; set; }
        public string Prompt { get; set; } = "";
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
    }

    public static class GenerateImageDialog
    {
        public static Task<GenerateImageResult> ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource<GenerateImageResult>();
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = global::Avalonia.Media.Brush.Parse("#14141E"),
                Foreground = global::Avalonia.Media.Brushes.White,
                Padding = new global::Avalonia.Thickness(20)
            };

            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(new TextBlock { Text = message, Foreground = global::Avalonia.Media.Brushes.Gray });

            var input = new TextBox
            {
                PlaceholderText = "Enter visual prompt (e.g. realistic warrior, dark fantasy)...",
                Background = global::Avalonia.Media.Brush.Parse("#13131F"),
                Foreground = global::Avalonia.Media.Brushes.White,
                BorderBrush = global::Avalonia.Media.Brush.Parse("#33334A")
            };
            stack.Children.Add(input);

            // Size Selector Stack
            var sizeStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };
            sizeStack.Children.Add(new TextBlock { Text = "Size:", VerticalAlignment = VerticalAlignment.Center, Foreground = global::Avalonia.Media.Brushes.Gray });

            var sizeCombo = new ComboBox
            {
                Background = global::Avalonia.Media.Brush.Parse("#13131F"),
                Foreground = global::Avalonia.Media.Brushes.White,
                BorderBrush = global::Avalonia.Media.Brush.Parse("#33334A"),
                Width = 200
            };

            var sizes = new[]
            {
                "512 x 512 (Square)",
                "1024 x 1024 (HD Square)",
                "768 x 512 (Landscape)",
                "512 x 768 (Portrait)",
                "1280 x 720 (HD Landscape)",
                "720 x 1280 (HD Portrait)",
                "Custom..."
            };
            sizeCombo.ItemsSource = sizes;
            sizeCombo.SelectedIndex = 0;
            sizeStack.Children.Add(sizeCombo);
            stack.Children.Add(sizeStack);

            // Custom Size Inputs (Grid)
            var customSizeGrid = new Grid
            {
                IsVisible = false,
                ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto,*"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var wLabel = new TextBlock { Text = "Width:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,5,0), Foreground = global::Avalonia.Media.Brushes.Gray };
            var wInput = new NumericUpDown { Value = 512, Minimum = 64, Maximum = 2048, Increment = 64, Width = 110, Margin = new Thickness(0,0,15,0) };
            var hLabel = new TextBlock { Text = "Height:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,5,0), Foreground = global::Avalonia.Media.Brushes.Gray };
            var hInput = new NumericUpDown { Value = 512, Minimum = 64, Maximum = 2048, Increment = 64, Width = 110 };

            Grid.SetColumn(wLabel, 0);
            Grid.SetColumn(wInput, 1);
            Grid.SetColumn(hLabel, 2);
            Grid.SetColumn(hInput, 3);

            customSizeGrid.Children.Add(wLabel);
            customSizeGrid.Children.Add(wInput);
            customSizeGrid.Children.Add(hLabel);
            customSizeGrid.Children.Add(hInput);

            stack.Children.Add(customSizeGrid);

            // Event to show/hide custom sizes
            sizeCombo.SelectionChanged += (s, e) =>
            {
                bool isCustom = sizeCombo.SelectedItem as string == "Custom...";
                customSizeGrid.IsVisible = isCustom;
                dialog.Height = isCustom ? 300 : 240;
            };

            // Buttons
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            var okBtn = new Button { Content = "OK", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#2B2B3A"), Foreground = global::Avalonia.Media.Brushes.White };

            void Submit()
            {
                var promptVal = input.Text ?? "";
                int w = 512;
                int h = 512;

                var selectedOption = sizeCombo.SelectedItem as string;
                if (selectedOption == "512 x 512 (Square)") { w = 512; h = 512; }
                else if (selectedOption == "1024 x 1024 (HD Square)") { w = 1024; h = 1024; }
                else if (selectedOption == "768 x 512 (Landscape)") { w = 768; h = 512; }
                else if (selectedOption == "512 x 768 (Portrait)") { w = 512; h = 768; }
                else if (selectedOption == "1280 x 720 (HD Landscape)") { w = 1280; h = 720; }
                else if (selectedOption == "720 x 1280 (HD Portrait)") { w = 720; h = 1280; }
                else if (selectedOption == "Custom...")
                {
                    w = (int)(wInput.Value ?? 512);
                    h = (int)(hInput.Value ?? 512);
                }

                tcs.SetResult(new GenerateImageResult { IsCancelled = false, Prompt = promptVal, Width = w, Height = h });
                dialog.Close();
            }

            okBtn.Click += (s, e) => Submit();
            cancelBtn.Click += (s, e) => { tcs.SetResult(new GenerateImageResult { IsCancelled = true }); dialog.Close(); };

            input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    Submit();
                }
            };

            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
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

            input.KeyDown += (s, e) =>
            {
                if (e.Key == global::Avalonia.Input.Key.Enter)
                {
                    e.Handled = true;
                    tcs.SetResult(input.Text ?? "");
                    dialog.Close();
                }
            };

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

    public class SelectedToBrushConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly SelectedToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is bool selected && selected)
            {
                return global::Avalonia.Media.Brush.Parse("#2E1A47"); // Dark purple / violet highlight
            }
            return global::Avalonia.Media.Brush.Parse("#13131F"); // Base button color
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class SelectedToBorderBrushConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly SelectedToBorderBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is bool selected && selected)
            {
                return global::Avalonia.Media.Brush.Parse("#8E2DE2"); // Highlight border
            }
            return global::Avalonia.Media.Brush.Parse("#2A2A3A"); // Dark border
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}