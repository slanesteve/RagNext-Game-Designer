using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
        private bool _isWebViewEventsSubscribed = false;
        private int _composeSelectionStart = -1;
        private int _composeSelectionEnd = -1;
        private int _inlineSelectionStart = -1;
        private int _inlineSelectionEnd = -1;
        private bool _isSelectingStatusBarElement = false;
        private TextBox? _lastFocusedTextBox = null;

        private Room? CurrentSelectedRoom
        {
            get
            {
                var treeView = this.FindControl<TreeView>("RoomsTreeView");
                return (treeView?.SelectedItem as EntityTreeNodeViewModel)?.Entity as Room;
            }
        }

        private Character? CurrentSelectedCharacter
        {
            get
            {
                var treeView = this.FindControl<TreeView>("CharsTreeView");
                return (treeView?.SelectedItem as EntityTreeNodeViewModel)?.Entity as Character;
            }
        }

        private GameObject? CurrentSelectedObject
        {
            get
            {
                var treeView = this.FindControl<TreeView>("ObjectsTreeView");
                return (treeView?.SelectedItem as EntityTreeNodeViewModel)?.Entity as GameObject;
            }
        }

        public ObservableCollection<string> RecentColors { get; } = new()
        {
            "#FFFFFF", "#EF4444", "#3B82F6", "#10B981", "#F59E0B"
        };

        private void AddToRecentColors(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            RecentColors.Remove(hex);
            RecentColors.Insert(0, hex);
            while (RecentColors.Count > 5)
            {
                RecentColors.RemoveAt(RecentColors.Count - 1);
            }
        }

        private void OnRecentColorClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is ISolidColorBrush brush)
            {
                var color = brush.Color;
                var parent = btn.GetVisualParent();
                while (parent != null)
                {
                    var cv = parent.FindDescendantOfType<ColorView>();
                    if (cv != null)
                    {
                        cv.Color = color;
                        return;
                    }
                    var cp = parent.FindDescendantOfType<ColorPicker>();
                    if (cp != null)
                    {
                        cp.Color = color;
                        return;
                    }
                    parent = parent.GetVisualParent();
                }
            }
        }

        private string? GetHexFromVisualParent(Visual visual)
        {
            var parent = visual.GetVisualParent();
            while (parent != null)
            {
                var cv = parent.FindDescendantOfType<ColorView>();
                if (cv != null) return cv.Color.ToString();
                var cp = parent.FindDescendantOfType<ColorPicker>();
                if (cp != null) return cp.Color.ToString();
                parent = parent.GetVisualParent();
            }
            return null;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Detach webviews on startup so their native hosts are not created initially and don't block clicks.
            if (CanvasWebView != null && CanvasWebView.Parent is Border canvasParent) canvasParent.Child = null;
            if (PreviewWebView != null && PreviewWebView.Parent is Grid previewParent) previewParent.Children.Remove(PreviewWebView);
            if (TabPreviewWebView != null && TabPreviewWebView.Parent is Grid tabParent) tabParent.Children.Remove(TabPreviewWebView);
            if (SplashPreviewWebView != null && SplashPreviewWebView.Parent is Border splashParent) splashParent.Child = null;
            if (ComposePreviewWebView != null && ComposePreviewWebView.Parent is Border composeParent) composeParent.Child = null;

            DataContextChanged += OnDataContextChanged;

            // Programmatically subscribe to event handlers to ensure they are not trimmed by NativeAOT
            var saveAttrBtn = this.FindControl<Button>("SaveAttributeButton");
            if (saveAttrBtn != null) saveAttrBtn.Click += OnSaveAttributeClicked;

            var cancelAttrBtn = this.FindControl<Button>("CancelAttributeButton");
            if (cancelAttrBtn != null) cancelAttrBtn.Click += OnCloseAttributeDialogClicked;

            var cancelActionBtn = this.FindControl<Button>("CancelActionSelectorButton");
            if (cancelActionBtn != null) cancelActionBtn.Click += OnCloseActionSelectorClicked;

            var selectActionBtn = this.FindControl<Button>("SelectActionTemplateButton");
            if (selectActionBtn != null) selectActionBtn.Click += OnSelectActionTemplateClicked;

            var cancelInvBtn = this.FindControl<Button>("CancelInventorySelectorButton");
            if (cancelInvBtn != null) cancelInvBtn.Click += OnCloseInventorySelectorClicked;

            var selectInvBtn = this.FindControl<Button>("SelectInventoryItemButton");
            if (selectInvBtn != null) selectInvBtn.Click += OnSelectInventoryItemClicked;

            var cancelLoadBtn = this.FindControl<Button>("CancelLoadButton");
            if (cancelLoadBtn != null) cancelLoadBtn.Click += OnCancelLoadClicked;

            var startingRoomCombo = this.FindControl<ComboBox>("StartingRoomComboBox");
            if (startingRoomCombo != null)
            {
                startingRoomCombo.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "ItemsSource")
                    {
                        if (startingRoomCombo.ItemsSource != null && startingRoomCombo.DataContext is Player player)
                        {
                            startingRoomCombo.SelectedItem = player.StartingRoom;
                        }
                    }
                    else if (e.Property.Name == "DataContext")
                    {
                        if (startingRoomCombo.DataContext is Player player && startingRoomCombo.ItemsSource != null)
                        {
                            startingRoomCombo.SelectedItem = player.StartingRoom;
                        }
                    }
                };
            }

            var pickerNames = new string[] { "NorthPicker", "SouthPicker", "EastPicker", "WestPicker", "NorthWestPicker", "NorthEastPicker", "SouthWestPicker", "SouthEastPicker", "UpPicker", "DownPicker", "InPicker", "OutPicker" };
            foreach (var name in pickerNames)
            {
                var combo = this.FindControl<ComboBox>(name);
                if (combo != null)
                {
                    combo.PropertyChanged += (s, e) =>
                    {
                        if (e.Property.Name == "ItemsSource" || e.Property.Name == "DataContext")
                        {
                            if (combo.ItemsSource != null && CurrentSelectedRoom is Room room)
                            {
                                var direction = name.Replace("Picker", "");
                                if (room.Exits.TryGetValue(direction, out var destId))
                                {
                                    var allRooms = combo.ItemsSource as System.Collections.IEnumerable;
                                    if (allRooms != null)
                                    {
                                        foreach (var r in allRooms)
                                        {
                                            if (r is Room destRoom && destRoom.Id == destId)
                                            {
                                                _suppressExitEvents = true;
                                                try
                                                {
                                                    combo.SelectedItem = destRoom;
                                                }
                                                finally
                                                {
                                                    _suppressExitEvents = false;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    };
                }
            }

            AddHandler(TextBox.KeyUpEvent, OnTextBoxKeyUp, RoutingStrategies.Bubble, true);
            AddHandler(TextBox.KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Bubble, true);
            AddHandler(TextBox.LostFocusEvent, OnTextBoxLostFocus, RoutingStrategies.Bubble, true);
            AddHandler(TextBox.GotFocusEvent, (s, e) => {
                if (e.Source is TextBox tb)
                {
                    _lastFocusedTextBox = tb;
                    if (!string.IsNullOrEmpty(tb.Text))
                    {
                        var t = tb.Text.Trim();
                        if (string.Equals(t, "New Room", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t, "new character", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t, "new object", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t, "New_Variable", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t, "New Timer", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t, "NewFunction", StringComparison.OrdinalIgnoreCase))
                        {
                            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                tb.SelectAll();
                            });
                        }
                    }
                }
            }, RoutingStrategies.Bubble, true);

            var composeTextBox = this.FindControl<TextBox>("ComposeTextBox");
            if (composeTextBox != null)
            {
                composeTextBox.AddHandler(TextBox.PointerReleasedEvent, (s, e) => {
                    _composeSelectionStart = composeTextBox.SelectionStart;
                    _composeSelectionEnd = composeTextBox.SelectionEnd;
                }, RoutingStrategies.Bubble, true);

                composeTextBox.KeyUp += (s, e) => {
                    _composeSelectionStart = composeTextBox.SelectionStart;
                    _composeSelectionEnd = composeTextBox.SelectionEnd;
                };

                composeTextBox.LostFocus += (s, e) => {
                    if (composeTextBox.SelectionStart != composeTextBox.SelectionEnd && composeTextBox.SelectionStart >= 0)
                    {
                        _composeSelectionStart = composeTextBox.SelectionStart;
                        _composeSelectionEnd = composeTextBox.SelectionEnd;
                    }
                };
            }

            var composeTextBoxStatus = this.FindControl<TextBox>("ComposeTextBox_Status");
            if (composeTextBoxStatus != null)
            {
                composeTextBoxStatus.AddHandler(TextBox.PointerReleasedEvent, (s, e) => {
                    _composeSelectionStart = composeTextBoxStatus.SelectionStart;
                    _composeSelectionEnd = composeTextBoxStatus.SelectionEnd;
                }, RoutingStrategies.Bubble, true);

                composeTextBoxStatus.KeyUp += (s, e) => {
                    _composeSelectionStart = composeTextBoxStatus.SelectionStart;
                    _composeSelectionEnd = composeTextBoxStatus.SelectionEnd;
                };

                composeTextBoxStatus.LostFocus += (s, e) => {
                    if (composeTextBoxStatus.SelectionStart != composeTextBoxStatus.SelectionEnd && composeTextBoxStatus.SelectionStart >= 0)
                    {
                        _composeSelectionStart = composeTextBoxStatus.SelectionStart;
                        _composeSelectionEnd = composeTextBoxStatus.SelectionEnd;
                    }
                };
            }

            var inlineNames = new[] { "PlayerDescriptionTextBox", "RoomDescriptionTextBox", "CharacterDescriptionTextBox", "ObjectDescriptionTextBox" };
            foreach (var name in inlineNames)
            {
                var tb = this.FindControl<TextBox>(name);
                if (tb != null)
                {
                    tb.AddHandler(TextBox.PointerReleasedEvent, (s, e) => {
                        _inlineSelectionStart = tb.SelectionStart;
                        _inlineSelectionEnd = tb.SelectionEnd;
                    }, RoutingStrategies.Bubble, true);

                    tb.KeyUp += (s, e) => {
                        _inlineSelectionStart = tb.SelectionStart;
                        _inlineSelectionEnd = tb.SelectionEnd;
                    };

                    tb.LostFocus += (s, e) => {
                        // Keep current selections if the new focus is inside the window or temporary flyout picker
                        if (tb.SelectionStart != tb.SelectionEnd && tb.SelectionStart >= 0)
                        {
                            _inlineSelectionStart = tb.SelectionStart;
                            _inlineSelectionEnd = tb.SelectionEnd;
                        }
                    };
                }
            }

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

            MainWindowViewModel.PickImportPackageFileAsync = async () =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = "Import RagNext Design Package",
                    FileTypeFilter = new[]
                    {
                        new global::Avalonia.Platform.Storage.FilePickerFileType("RagNext Design Packages")
                        {
                            Patterns = new[] { "*.ragnext" }
                        }
                    }
                });
                return global::System.Linq.Enumerable.FirstOrDefault(files)?.Path.LocalPath;
            };

            MainWindowViewModel.PickExportPackageFileAsync = async (defaultFileName) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Export RagNext Design Package",
                    DefaultExtension = ".ragnext",
                    SuggestedFileName = defaultFileName,
                    FileTypeChoices = new[]
                    {
                        new global::Avalonia.Platform.Storage.FilePickerFileType("RagNext Design Packages")
                        {
                            Patterns = new[] { "*.ragnext" }
                        }
                    }
                });
                return file?.Path.LocalPath;
            };

            MediaLibraryViewModel.PromptInputAsync = async (title, message) =>
            {
                return await PromptDialog.ShowAsync(this, title, message);
            };

            MediaLibraryViewModel.ConfirmDialogAsync = async (title, message) =>
            {
                return await ConfirmDialog.ShowAsync(this, title, message);
            };

            MediaLibraryViewModel.ConfirmPublishDialogAsync = async (title, message) =>
            {
                return await ConfirmPublishDialog.ShowAsync(this, title, message);
            };

            MainWindowViewModel.ShowAlertDialogAsync = async (title, message) =>
            {
                await AlertDialog.ShowAsync(this, title, message);
            };

            MainWindowViewModel.ShowConfirmDialogAsync = async (title, message) =>
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
                            if (VisualEditorOverlayBorder != null)
                            {
                                VisualEditorOverlayBorder.Width = double.NaN;
                                VisualEditorOverlayBorder.Height = double.NaN;
                                VisualEditorOverlayBorder.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
                                VisualEditorOverlayBorder.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
                                VisualEditorOverlayBorder.IsHitTestVisible = true;
                                VisualEditorOverlayBorder.Opacity = 1.0;
                            }
                        }
                        else
                        {
                            if (VisualEditorOverlayBorder != null)
                            {
                                VisualEditorOverlayBorder.Width = 1;
                                VisualEditorOverlayBorder.Height = 1;
                                VisualEditorOverlayBorder.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
                                VisualEditorOverlayBorder.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
                                VisualEditorOverlayBorder.IsHitTestVisible = false;
                                VisualEditorOverlayBorder.Opacity = 0.0;
                            }
                        }
                        UpdateWebViewsAirspace(vm.ShowComposeOverlay);
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

                        UpdateSplashVideoPreview(vm);
                    }
                    else if (ev.PropertyName == nameof(MainWindowViewModel.SplashBackgroundPath) ||
                             ev.PropertyName == nameof(MainWindowViewModel.IsSplashVideoMode) ||
                             ev.PropertyName == nameof(MainWindowViewModel.IsSplashVideoPreviewVisible) ||
                             ev.PropertyName == nameof(MainWindowViewModel.SelectedSplashScreen))
                    {
                        UpdateSplashVideoPreview(vm);
                    }
                    else if (ev.PropertyName == nameof(MainWindowViewModel.ShowComposeOverlay))
                    {
                        UpdateWebViewsAirspace(vm.ShowComposeOverlay);
                        if (vm.ShowComposeOverlay)
                        {
                            global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                            {
                                await Task.Delay(250);
                                UpdateComposePreview(vm.ComposeText);
                                var composeTextBox = this.FindControl<TextBox>("ComposeTextBox");
                                if (composeTextBox != null && composeTextBox.IsVisible)
                                {
                                    _lastFocusedTextBox = composeTextBox;
                                    composeTextBox.Focus();
                                }
                                else
                                {
                                    var composeTextBoxStatus = this.FindControl<TextBox>("ComposeTextBox_Status");
                                    if (composeTextBoxStatus != null && composeTextBoxStatus.IsVisible)
                                    {
                                        _lastFocusedTextBox = composeTextBoxStatus;
                                        composeTextBoxStatus.Focus();
                                    }
                                }
                            });
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
                
                // Initialize web view airspace state upon startup
                UpdateWebViewsAirspace(vm.ShowComposeOverlay);

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

                vm.ComposeApplied += async (nodeId, fieldName, text) =>
                {
                    try
                    {
                        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                        await CanvasWebView.InvokeScript($"if (typeof updateNodeAIResult === 'function') {{ updateNodeAIResult('{nodeId}', '{fieldName}', atob('{base64}')); }}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to invoke ComposeApplied script: {ex.Message}");
                    }
                };

                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainWindowViewModel.ComposeText))
                    {
                        UpdateComposePreview(vm.ComposeText);
                    }
                };

                App.GameChanged += (newGame) =>
                {
                    if (newGame != null)
                    {
                        SubscribeToEntityCollections(newGame);
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Trigger VM Property notification to refresh ComboBox bindings in views
                            vm.GetType().GetMethod("OnPropertyChanged")?.Invoke(vm, new object[] { "Player" });
                        }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                    }
                };

                if (App.CurrentGame != null)
                {
                    SubscribeToEntityCollections(App.CurrentGame);
                }
            }
        }

        private void SubscribeToEntityCollections(Game? game)
        {
            if (game == null) return;

            if (game.Rooms is System.Collections.Specialized.INotifyCollectionChanged rooms)
            {
                rooms.CollectionChanged -= OnRoomsCollectionChanged;
                rooms.CollectionChanged += OnRoomsCollectionChanged;
            }
            if (game.Characters is System.Collections.Specialized.INotifyCollectionChanged chars)
            {
                chars.CollectionChanged -= OnCharactersCollectionChanged;
                chars.CollectionChanged += OnCharactersCollectionChanged;
            }
            if (game.Objects is System.Collections.Specialized.INotifyCollectionChanged objs)
            {
                objs.CollectionChanged -= OnObjectsCollectionChanged;
                objs.CollectionChanged += OnObjectsCollectionChanged;
            }
            if (game.Variables is System.Collections.Specialized.INotifyCollectionChanged vars)
            {
                vars.CollectionChanged -= OnVariablesCollectionChanged;
                vars.CollectionChanged += OnVariablesCollectionChanged;
            }
            if (game.Timers is System.Collections.Specialized.INotifyCollectionChanged timers)
            {
                timers.CollectionChanged -= OnTimersCollectionChanged;
                timers.CollectionChanged += OnTimersCollectionChanged;
            }
            if (game.Functions is System.Collections.Specialized.INotifyCollectionChanged funcs)
            {
                funcs.CollectionChanged -= OnFunctionsCollectionChanged;
                funcs.CollectionChanged += OnFunctionsCollectionChanged;
            }
        }

        private void OnRoomsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is Room newRoom)
            {
                var list = this.FindControl<ListBox>("RoomsList");
                if (list != null) list.SelectedItem = newRoom;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("RoomNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnCharactersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is Character newChar)
            {
                var list = this.FindControl<ListBox>("CharsList");
                if (list != null) list.SelectedItem = newChar;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("CharacterNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnObjectsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is GameObject newObj)
            {
                var list = this.FindControl<ListBox>("ObjectsList");
                if (list != null) list.SelectedItem = newObj;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("ObjectNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnVariablesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is GameVariable newVar)
            {
                var list = this.FindControl<ListBox>("VarsList");
                if (list != null) list.SelectedItem = newVar;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("VariableNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnTimersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is GameTimer newTimer)
            {
                var list = this.FindControl<ListBox>("TimersList");
                if (list != null) list.SelectedItem = newTimer;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("TimerNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnFunctionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0 && e.NewItems[0] is GlobalFunction newFunc)
            {
                var list = this.FindControl<ListBox>("FuncsList");
                if (list != null) list.SelectedItem = newFunc;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var tb = this.FindControl<TextBox>("FunctionNameTextBox");
                    if (tb != null)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
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
                    htmlPath = Path.Combine(baseDir, "..", "Resources", "WebAssets", "graph_editor.html");
                }
                if (!File.Exists(htmlPath))
                {
                    htmlPath = Path.Combine(baseDir, "graph_editor.html");
                }

                if (File.Exists(htmlPath))
                {
                    if (!_isWebViewLoaded)
                    {
                        _isWebViewLoaded = true;
                        if (!_isWebViewEventsSubscribed)
                        {
                            _isWebViewEventsSubscribed = true;
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
                        }
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
                System.Diagnostics.Debug.WriteLine($"[LoadGraphData] Action Name='{activeAction.Name}', Trigger='{activeAction.Trigger}', DirectionFilter='{activeAction.DirectionFilter}'");
                string actionJson = JsonSerializer.Serialize(activeAction, RagsCore.RagsJsonContext.CustomDefault.Action);

                // Load available commands & conditions catalogs to feed the web catalog
                string commandsJson = "{\"commands\":[]}";
                string conditionsJson = "{\"conditions\":[]}";
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var cmdsPath = Path.Combine(baseDir, "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "WebAssets", "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "..", "Resources", "WebAssets", "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "Resources", "Raw", "Commands.json");
                    if (!File.Exists(cmdsPath)) cmdsPath = Path.Combine(baseDir, "..", "..", "..", "..", "RagNext", "Resources", "Raw", "Commands.json");
                    if (File.Exists(cmdsPath)) commandsJson = await File.ReadAllTextAsync(cmdsPath);

                    var condsPath = Path.Combine(baseDir, "Conditions.json");
                    if (!File.Exists(condsPath)) condsPath = Path.Combine(baseDir, "WebAssets", "Conditions.json");
                    if (!File.Exists(condsPath)) condsPath = Path.Combine(baseDir, "..", "Resources", "WebAssets", "Conditions.json");
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

                var allChars = vm.CurrentGame.Characters.Select(c => new CatalogEntityDto
                {
                    Id = c.Id.ToString(), Name = c.Name,
                    Attributes = c.Attributes.Select(a => a.Name).ToList(),
                    // Bug #5: Include action names for character action pickers.
                    Actions = c.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList(),
                    StartingRoomId = c.StartingRoom?.Id.ToString()
                }).ToList();

                if (vm.CurrentGame.Player != null && !allChars.Any(c => c.Id == vm.CurrentGame.Player.Id.ToString()))
                {
                    allChars.Insert(0, new CatalogEntityDto
                    {
                        Id = vm.CurrentGame.Player.Id.ToString(),
                        Name = string.IsNullOrWhiteSpace(vm.CurrentGame.Player.Name) ? "Player" : vm.CurrentGame.Player.Name,
                        Attributes = vm.CurrentGame.Player.Attributes.Select(a => a.Name).ToList(),
                        Actions = vm.CurrentGame.Player.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList()
                    });
                }

                var promptNamesSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (vm.CurrentGame.Player != null && vm.CurrentGame.Player.Actions != null)
                {
                    foreach (var act in vm.CurrentGame.Player.Actions)
                    {
                        ExtractPromptNames(act.Steps, promptNamesSet);
                    }
                }
                if (vm.CurrentGame.Rooms != null)
                {
                    foreach (var rm in vm.CurrentGame.Rooms)
                    {
                        if (rm.Actions != null)
                        {
                            foreach (var act in rm.Actions)
                            {
                                ExtractPromptNames(act.Steps, promptNamesSet);
                            }
                        }
                    }
                }
                if (vm.CurrentGame.Objects != null)
                {
                    foreach (var obj in vm.CurrentGame.Objects)
                    {
                        if (obj.Actions != null)
                        {
                            foreach (var act in obj.Actions)
                            {
                                ExtractPromptNames(act.Steps, promptNamesSet);
                            }
                        }
                    }
                }
                if (vm.CurrentGame.Characters != null)
                {
                    foreach (var ch in vm.CurrentGame.Characters)
                    {
                        if (ch.Actions != null)
                        {
                            foreach (var act in ch.Actions)
                            {
                                ExtractPromptNames(act.Steps, promptNamesSet);
                            }
                        }
                    }
                }

                var hotspotsList = new System.Collections.Generic.List<CatalogEntityDto>();
                if (vm.CurrentGame.Rooms != null)
                {
                    foreach (var r in vm.CurrentGame.Rooms)
                    {
                        if (r.InteractiveScreenSettings != null && r.InteractiveScreenSettings.Enabled && r.InteractiveScreenSettings.Hotspots != null)
                        {
                            foreach (var h in r.InteractiveScreenSettings.Hotspots)
                            {
                                hotspotsList.Add(new CatalogEntityDto 
                                { 
                                    Id = h.Id, 
                                    Name = string.IsNullOrWhiteSpace(h.Name) ? h.Id : $"{h.Name} (Room: {r.Name})" 
                                });
                            }
                        }
                    }
                }
                if (vm.CurrentGame.Objects != null)
                {
                    foreach (var o in vm.CurrentGame.Objects)
                    {
                        if (o.InteractiveScreenSettings != null && o.InteractiveScreenSettings.Enabled && o.InteractiveScreenSettings.Hotspots != null)
                        {
                            foreach (var h in o.InteractiveScreenSettings.Hotspots)
                            {
                                hotspotsList.Add(new CatalogEntityDto 
                                { 
                                    Id = h.Id, 
                                    Name = string.IsNullOrWhiteSpace(h.Name) ? h.Id : $"{h.Name} (Object: {o.Name})" 
                                });
                            }
                        }
                    }
                }

                var catalogsObj = new CatalogsDto
                {
                    PromptNames = promptNamesSet.OrderBy(n => n).ToList(),
                    Hotspots = hotspotsList.OrderBy(h => h.Name).ToList(),
                    Rooms = vm.CurrentGame.Rooms.Select(r => new CatalogEntityDto
                    {
                        Id = r.Id.ToString(), Name = r.Name,
                        Attributes = r.Attributes.Select(a => a.Name).ToList(),
                        // Bug #5: Include action names so ActionName pickers can be scoped to this room.
                        Actions = r.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList(),
                        Exits = r.Exits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())
                    }).ToList(),
                    Characters = allChars,
                    GameObjects = vm.CurrentGame.Objects.Select(o => new CatalogEntityDto
                    {
                        Id = o.Id.ToString(), Name = o.Name, IsContainer = o.IsContainer,
                        Attributes = o.Attributes.Select(a => a.Name).ToList(),
                        // Bug #5: Include action names for item action pickers.
                        Actions = o.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList()
                    }).ToList(),
                    Variables = vm.CurrentGame.Variables.Select(v => new CatalogEntityDto { Id = v.Name, Name = v.Name, VarType = v.Type, Columns = v.Columns.ToList(), Attributes = v.Attributes.Select(a => a.Name).ToList(), RowCount = v.Rows != null ? v.Rows.Count : 0 }).ToList(),
                    Player = new CatalogPlayerDto
                    {
                        Attributes = vm.CurrentGame.Player.Attributes.Select(a => a.Name).ToList(),
                        // Bug #5: Include player action names.
                        Actions = vm.CurrentGame.Player.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList()
                    },
                    Owner = new CatalogPlayerDto { Attributes = ownerAttributes },
                    Media = vm.CurrentGame.MediaAssets.Select(m => new CatalogEntityDto { Id = m.Id.ToString(), Name = string.IsNullOrWhiteSpace(m.OriginalFileName) ? m.RelativePath : m.OriginalFileName }).ToList(),
                    Functions = vm.CurrentGame.Functions.Select(f => new CatalogEntityDto { Id = f.Name, Name = f.Name }).ToList(),
                    Timers = vm.CurrentGame.Timers.Select(t => new CatalogEntityDto { Id = t.Name, Name = t.Name, Attributes = t.Attributes.Select(a => a.Name).ToList() }).ToList(),
                    // Bug #5: Top-level PlayerActions for the player.setActionActive command.
                    PlayerActions = vm.CurrentGame.Player.Actions.Select(a => new CatalogActionDto { Name = a.Name }).ToList(),
                    StatusBarElements = vm.CurrentGame.StatusBarElements.Select(s => new CatalogEntityDto { Id = s.Id.ToString(), Name = s.Name }).ToList(),
                    SplashScreens = vm.CurrentGame.SplashScreens.Select(s => new CatalogEntityDto { Id = s.Name, Name = s.Name }).ToList(),
                    ThemePresets = vm.ThemePresets.ToList()
                };
                string catalogsJson = JsonSerializer.Serialize(catalogsObj, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.CatalogsDto);

                // Compile polymorphic reflection mapping
                var reflectionList = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => typeof(ActionStep).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(t => new ReflectionEntityDto { TypeName = t.Name, Discriminator = t.Name })
                    .ToList();
                string reflectionJson = JsonSerializer.Serialize(reflectionList, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.ListReflectionEntityDto);

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

        public void OnCloseAttributeDialogClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnCloseAttributeDialogClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.CloseAttributeDialogCommand.CanExecute(null))
                {
                    vm.CloseAttributeDialogCommand.Execute(null);
                }
            }
        }

        public void OnSaveAttributeClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnSaveAttributeClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.SaveAttributeCommand.CanExecute(null))
                {
                    vm.SaveAttributeCommand.Execute(null);
                }
            }
        }

        public void OnAddHotspotClicked(object sender, RoutedEventArgs e)
        {
            var room = CurrentSelectedRoom;
            if (room != null)
            {
                if (room.InteractiveScreenSettings == null)
                    room.InteractiveScreenSettings = new InteractiveScreenSettings();
                room.InteractiveScreenSettings.Hotspots.Add(new ScreenHotspot { Name = "New Button" });
            }
        }

        public void OnRemoveHotspotClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var hotspot = button?.CommandParameter as ScreenHotspot;
            var room = CurrentSelectedRoom;
            if (room != null && hotspot != null)
            {
                room.InteractiveScreenSettings?.Hotspots.Remove(hotspot);
            }
        }

        public void OnAddObjectHotspotClicked(object sender, RoutedEventArgs e)
        {
            var obj = this.FindControl<ListBox>("ObjectsList")?.SelectedItem as GameObject;
            if (obj != null)
            {
                if (obj.InteractiveScreenSettings == null)
                    obj.InteractiveScreenSettings = new InteractiveScreenSettings();
                obj.InteractiveScreenSettings.Hotspots.Add(new ScreenHotspot { Name = "New Button" });
            }
        }

        public void OnRemoveObjectHotspotClicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var hotspot = button?.CommandParameter as ScreenHotspot;
            var obj = this.FindControl<ListBox>("ObjectsList")?.SelectedItem as GameObject;
            if (obj != null && hotspot != null)
            {
                obj.InteractiveScreenSettings?.Hotspots.Remove(hotspot);
            }
        }

        public async void OnInteractiveScreenDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
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

            var firstPath = paths[0];
            var game = App.CurrentGame;
            if (game == null) return;

            var matchingAsset = game.MediaAssets.FirstOrDefault(a => 
                string.Equals(new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, a), firstPath, StringComparison.OrdinalIgnoreCase));

            string backdropName = "";
            if (matchingAsset != null)
            {
                backdropName = matchingAsset.OriginalFileName;
            }
            else
            {
                await vm.Media.ImportFilesFromPathsAsync(new[] { firstPath }, null);
                var fileName = Path.GetFileName(firstPath);
                var asset = game.MediaAssets.FirstOrDefault(a => string.Equals(a.OriginalFileName, fileName, StringComparison.OrdinalIgnoreCase));
                if (asset != null)
                {
                    backdropName = asset.OriginalFileName;
                }
            }

            if (!string.IsNullOrEmpty(backdropName))
            {
                var room = CurrentSelectedRoom;
                if (room != null)
                {
                    if (room.InteractiveScreenSettings == null) room.InteractiveScreenSettings = new InteractiveScreenSettings();
                    room.InteractiveScreenSettings.BackdropAssetId = backdropName;
                }
                var obj = this.FindControl<ListBox>("ObjectsList")?.SelectedItem as GameObject;
                if (obj != null)
                {
                    if (obj.InteractiveScreenSettings == null) obj.InteractiveScreenSettings = new InteractiveScreenSettings();
                    obj.InteractiveScreenSettings.BackdropAssetId = backdropName;
                }
            }
        }

        private bool _isDraggingHotspot;
        private Point _dragStartPos;
        private double _hotspotStartLeft;
        private double _hotspotStartTop;
        private ScreenHotspot? _draggedHotspot;

        public void OnHotspotPointerPressed(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
        {
            var border = sender as Border;
            _draggedHotspot = border?.DataContext as ScreenHotspot;
            if (_draggedHotspot == null || border == null) return;

            var parent = border.Parent?.Parent as Visual;
            if (parent == null) return;

            _isDraggingHotspot = true;
            _dragStartPos = e.GetPosition(parent);
            _hotspotStartLeft = _draggedHotspot.X;
            _hotspotStartTop = _draggedHotspot.Y;
            e.Pointer.Capture(border);
            e.Handled = true;
        }

        public void OnHotspotPointerMoved(object sender, global::Avalonia.Input.PointerEventArgs e)
        {
            if (!_isDraggingHotspot || _draggedHotspot == null) return;
            var border = sender as Border;
            var parent = border?.Parent?.Parent as Visual;
            if (parent == null) return;

            var currentPos = e.GetPosition(parent);
            var delta = currentPos - _dragStartPos;

            if (parent.Bounds.Width > 0 && parent.Bounds.Height > 0)
            {
                double deltaX = (delta.X / parent.Bounds.Width) * 100.0;
                double deltaY = (delta.Y / parent.Bounds.Height) * 100.0;

                _draggedHotspot.X = Math.Clamp(Math.Round(_hotspotStartLeft + deltaX, 1), 0.0, 100.0);
                _draggedHotspot.Y = Math.Clamp(Math.Round(_hotspotStartTop + deltaY, 1), 0.0, 100.0);
            }
        }

        public void OnHotspotPointerReleased(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (_isDraggingHotspot)
            {
                var border = sender as Border;
                if (border != null)
                {
                    e.Pointer.Capture(null);
                }
                _isDraggingHotspot = false;
                _draggedHotspot = null;
            }
        }

        public void OnCloseActionSelectorClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnCloseActionSelectorClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.CloseActionSelectorCommand.CanExecute(null))
                {
                    vm.CloseActionSelectorCommand.Execute(null);
                }
            }
        }

        public async void OnSelectActionTemplateClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnSelectActionTemplateClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                var listBox = this.FindControl<ListBox>("SelectorTemplatesList");
                var selectedItem = listBox?.SelectedItem;
                var target = vm._actionTargetEntity;
                Console.WriteLine($"[DEBUG] OnSelectActionTemplateClicked selected item: {selectedItem?.GetType().Name ?? "null"}");
                if (vm.SelectActionTemplateCommand.CanExecute(selectedItem))
                {
                    vm.SelectActionTemplateCommand.Execute(selectedItem);

                    var newAction = vm.LastAddedAction;
                    if (newAction != null && target != null)
                    {
                        ListBox? targetList = null;
                        TextBox? targetTextBox = null;

                        if (target is Player)
                        {
                            targetList = this.FindControl<ListBox>("PlayerActionsList");
                            targetTextBox = this.FindControl<TextBox>("PlayerActionNameTextBox");
                        }
                        else if (target is Room)
                        {
                            targetList = this.FindControl<ListBox>("RoomActionsList");
                            targetTextBox = this.FindControl<TextBox>("RoomActionNameTextBox");
                        }
                        else if (target is Character)
                        {
                            targetList = this.FindControl<ListBox>("CharacterActionsList");
                            targetTextBox = this.FindControl<TextBox>("CharacterActionNameTextBox");
                        }
                        else if (target is GameObject)
                        {
                            targetList = this.FindControl<ListBox>("ObjectActionsList");
                            targetTextBox = this.FindControl<TextBox>("ObjectActionNameTextBox");
                        }

                        if (targetList != null)
                        {
                            targetList.SelectedItem = newAction;
                        }
                        if (targetTextBox != null)
                        {
                            await Task.Delay(100);
                            targetTextBox.Focus();
                            targetTextBox.SelectAll();
                        }
                    }
                }
            }
        }

        public void OnCloseInventorySelectorClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnCloseInventorySelectorClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.CloseInventorySelectorCommand.CanExecute(null))
                {
                    vm.CloseInventorySelectorCommand.Execute(null);
                }
            }
        }

        public void OnSelectInventoryItemClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[DEBUG] OnSelectInventoryItemClicked code-behind triggered");
            if (DataContext is MainWindowViewModel vm)
            {
                var listBox = this.FindControl<ListBox>("SelectorItemsList");
                var selectedItem = listBox?.SelectedItem;
                Console.WriteLine($"[DEBUG] OnSelectInventoryItemClicked selected item: {selectedItem?.GetType().Name ?? "null"}");
                if (vm.SelectInventoryItemCommand.CanExecute(selectedItem))
                {
                    vm.SelectInventoryItemCommand.Execute(selectedItem);
                }
            }
        }

        public void OnInstructionsClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://slanesteve.github.io/RagNext-Game-Designer/",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore failures if default browser can't be invoked
            }
        }

        public void OnReportIssueClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/slanesteve/RagNext-Game-Designer/issues/new?title=[Bug]%20Short%20Description&body=Steps%20to%20reproduce:",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore failures if default browser can't be invoked
            }
        }

        public void OnAboutClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var assembly = typeof(MainWindow).Assembly;
            var name = assembly.GetName().Name ?? "RagNext Designer";
            
            // Get Informational Version attribute
            var infoVersionAttr = (global::System.Reflection.AssemblyInformationalVersionAttribute?)
                global::System.Attribute.GetCustomAttribute(assembly, typeof(global::System.Reflection.AssemblyInformationalVersionAttribute));
            var versionString = infoVersionAttr?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "1.0.0";
            
            // Remove source revision / commit hash suffix (e.g. +d635934...) added by .NET SDK automatically
            if (versionString.Contains('+'))
            {
                versionString = versionString.Split('+')[0];
            }

            var tcs = new TaskCompletionSource();
            var dialog = new Window
            {
                Title = "About RagNext Designer",
                Width = 480,
                SizeToContent = global::Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Padding = new global::Avalonia.Thickness(20)
            };
            dialog.Bind(global::Avalonia.Controls.Window.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("MainBg"));
            dialog.Bind(global::Avalonia.Controls.Window.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));

            var mainStack = new StackPanel { Spacing = 16 };

            // Logo and Title Header
            var headerGrid = new Grid { ColumnDefinitions = new global::Avalonia.Controls.ColumnDefinitions("Auto, *") };
            
            // Recreate the RN Brand Logo box
            var logoBorder = new Border 
            { 
                CornerRadius = new global::Avalonia.CornerRadius(4), 
                Width = 48, 
                Height = 48,
                Margin = new global::Avalonia.Thickness(0, 0, 16, 0),
                Background = new global::Avalonia.Media.LinearGradientBrush
                {
                    StartPoint = new global::Avalonia.RelativePoint(0, 0, global::Avalonia.RelativeUnit.Relative),
                    EndPoint = new global::Avalonia.RelativePoint(1, 1, global::Avalonia.RelativeUnit.Relative),
                    GradientStops = 
                    {
                        new global::Avalonia.Media.GradientStop(global::Avalonia.Media.Color.Parse("#8E2DE2"), 0.0),
                        new global::Avalonia.Media.GradientStop(global::Avalonia.Media.Color.Parse("#4A00E0"), 1.0)
                    }
                }
            };
            var logoText = new TextBlock 
            { 
                Text = "RN", 
                FontSize = 24, 
                FontWeight = global::Avalonia.Media.FontWeight.Bold, 
                Foreground = global::Avalonia.Media.Brushes.White,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
            logoBorder.Child = logoText;
            headerGrid.Children.Add(logoBorder);
            global::Avalonia.Controls.Grid.SetColumn(logoBorder, 0);

            var titleInfoStack = new StackPanel { VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            var titleBlock = new TextBlock 
            { 
                Text = name, 
                FontSize = 18, 
                FontWeight = global::Avalonia.Media.FontWeight.SemiBold 
            };
            var verBlock = new TextBlock 
            { 
                Text = $"Version {versionString}", 
                FontSize = 12 
            };
            verBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMuted"));
            titleInfoStack.Children.Add(titleBlock);
            titleInfoStack.Children.Add(verBlock);
            headerGrid.Children.Add(titleInfoStack);
            global::Avalonia.Controls.Grid.SetColumn(titleInfoStack, 1);

            mainStack.Children.Add(headerGrid);

            // Description details
            var descBlock = new TextBlock 
            { 
                Text = "Built on .NET 9.0 using Avalonia UI.\n© 2026 RagNext contributors.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap 
            };
            descBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            mainStack.Children.Add(descBlock);

            // Bottom Buttons
            var buttons = new StackPanel 
            { 
                Orientation = global::Avalonia.Layout.Orientation.Horizontal, 
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, 
                Spacing = 10 
            };
            var okBtn = new Button 
            { 
                Content = "OK", 
                Width = 80, 
                Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), 
                Foreground = global::Avalonia.Media.Brushes.White, 
                IsDefault = true,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
            };
            okBtn.Click += (s, ev) => { tcs.SetResult(); dialog.Close(); };
            buttons.Children.Add(okBtn);
            mainStack.Children.Add(buttons);

            dialog.Content = mainStack;
            dialog.ShowDialog(this);
        }

         public async void OnSyncGraphClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm || vm.CurrentGame == null || vm.ActiveAction == null) return;

            try
            {
                var base64Json = await CanvasWebView.InvokeScript("saveAndSyncCsharp()");
                if (base64Json == "CANCELLED" || base64Json == "\"CANCELLED\"")
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
            if (DataContext is not MainWindowViewModel vm) return;
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
                    else if (url.StartsWith("rags-action://graph-ai"))
                    {
                        string prompt = query["prompt"] ?? "";
                        string replace = query["replace"] ?? "";
                        string currentGraph = query["data"] ?? "";
                        TriggerGraphAI(prompt, replace, currentGraph);
                    }
                    else if (url.StartsWith("rags-action://ai"))
                    {
                        string nodeId = query["nodeId"] ?? "";
                        string fieldName = query["fieldName"] ?? "";
                        string currentText = query["currentText"] ?? "";
                        TriggerAICoAuthor(nodeId, fieldName, currentText);
                    }
                    else if (url.StartsWith("rags-action://compose"))
                    {
                        string nodeId = query["nodeId"] ?? "";
                        string fieldName = query["fieldName"] ?? "";
                        string currentText = query["currentText"] ?? "";
                        TriggerCompose(nodeId, fieldName, currentText, "", "");
                    }
                    else if (url.StartsWith("rags-action://update-char-starting-room"))
                    {
                        string charIdStr = query["charId"] ?? "";
                        string roomIdStr = query["roomId"] ?? "";
                        if (Guid.TryParse(charIdStr, out var charId) && Guid.TryParse(roomIdStr, out var roomId))
                        {
                            var character = vm.CurrentGame.Characters.FirstOrDefault(c => c.Id == charId);
                            var room = vm.CurrentGame.Rooms.FirstOrDefault(r => r.Id == roomId);
                            if (character != null && room != null)
                            {
                                character.StartingRoom = room;
                                await vm.SaveGameAsync();
                                LoadGraphData();
                            }
                        }
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
            if (DataContext is not MainWindowViewModel vm) return;
            try
            {
                msg = msg.Trim('\"', '\'');
                if (msg.StartsWith("sync?data="))
                {
                    string base64 = msg.Substring("sync?data=".Length);
                    await SyncGraphData(base64);
                }
                else if (msg.StartsWith("add-element?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("add-element?".Length));
                    string type = query["type"] ?? "";
                    string name = query["name"] ?? "";
                    string varType = query["varType"] ?? "string";

                    if (vm.CurrentGame != null && !string.IsNullOrWhiteSpace(name))
                    {
                        if (type.Equals("Room", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Rooms.Add(new Room { Id = Guid.NewGuid(), Name = name, Description = $"A newly created room named {name}." });
                        }
                        else if (type.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Objects.Add(new GameObject { Id = Guid.NewGuid(), Name = name, Description = $"A newly created object named {name}." });
                        }
                        else if (type.Equals("Character", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Characters.Add(new Character { Id = Guid.NewGuid(), Name = name, Description = $"A newly created character named {name}." });
                        }
                        else if (type.Equals("Variable", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Variables.Add(new GameVariable { Id = Guid.NewGuid(), Name = name, Type = varType, Value = varType == "number" ? "0" : (varType == "bool" ? "false" : "") });
                        }
                        else if (type.Equals("Timer", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Timers.Add(new GameTimer { Id = Guid.NewGuid(), Name = name, IntervalSeconds = 1 });
                        }
                        else if (type.Equals("Function", StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentGame.Functions.Add(new GlobalFunction { Id = Guid.NewGuid(), Name = name });
                        }

                        await vm.SaveGameAsync();
                        // Trigger catalogs list updates to notify VM tabs and refresh dropdown sources inside WebView
                        if (vm.Rooms != null)
                        {
                            vm.Rooms.OnPropertyChanged(nameof(vm.Rooms.Rooms));
                        }
                        if (vm.Objects != null)
                        {
                            vm.Objects.OnPropertyChanged(nameof(vm.Objects.Objects));
                        }
                        if (vm.Characters != null)
                        {
                            vm.Characters.OnPropertyChanged(nameof(vm.Characters.Characters));
                        }
                        if (vm.Variables != null)
                        {
                            vm.Variables.OnPropertyChanged(nameof(vm.Variables.Variables));
                        }
                        if (vm.Timers != null)
                        {
                            vm.Timers.OnPropertyChanged(nameof(vm.Timers.Timers));
                        }
                        if (vm.Functions != null)
                        {
                            vm.Functions.OnPropertyChanged(nameof(vm.Functions.Functions));
                        }
                        
                        LoadGraphData();
                    }
                }
                else if (msg.StartsWith("add-attribute?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("add-attribute?".Length));
                    string targetType = query["targetType"] ?? "";
                    string targetIdStr = query["targetId"] ?? "";
                    string name = query["name"] ?? "";
                    string value = query["value"] ?? "";

                    if (vm.CurrentGame != null && !string.IsNullOrWhiteSpace(name))
                    {
                        if (targetType.Equals("Player", StringComparison.OrdinalIgnoreCase))
                        {
                            var player = vm.CurrentGame.Player;
                            if (player != null)
                            {
                                CustomAttribute.SetAttribute(name, value, player.Attributes);
                            }
                        }
                        else if (Guid.TryParse(targetIdStr, out Guid targetId))
                        {
                            if (targetType.Equals("Character", StringComparison.OrdinalIgnoreCase))
                            {
                                var character = vm.CurrentGame.Characters.FirstOrDefault(c => c.Id == targetId);
                                if (character != null)
                                {
                                    CustomAttribute.SetAttribute(name, value, character.Attributes);
                                }
                            }
                            else if (targetType.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                            {
                                var item = vm.CurrentGame.Objects.FirstOrDefault(o => o.Id == targetId);
                                if (item == null)
                                {
                                    foreach (var ch in vm.CurrentGame.Characters)
                                    {
                                        item = ch.Inventory.FirstOrDefault(o => o.Id == targetId);
                                        if (item != null) break;
                                    }
                                }
                                if (item != null)
                                {
                                    CustomAttribute.SetAttribute(name, value, item.Attributes);
                                }
                            }
                            else if (targetType.Equals("Room", StringComparison.OrdinalIgnoreCase))
                            {
                                var room = vm.CurrentGame.Rooms.FirstOrDefault(r => r.Id == targetId);
                                if (room != null)
                                {
                                    CustomAttribute.SetAttribute(name, value, room.Attributes);
                                }
                            }
                        }

                        await vm.SaveGameAsync();
                        if (vm.Rooms != null)
                        {
                            vm.Rooms.OnPropertyChanged(nameof(vm.Rooms.Rooms));
                        }
                        if (vm.Objects != null)
                        {
                            vm.Objects.OnPropertyChanged(nameof(vm.Objects.Objects));
                        }
                        if (vm.Characters != null)
                        {
                            vm.Characters.OnPropertyChanged(nameof(vm.Characters.Characters));
                        }
                        if (vm.Variables != null)
                        {
                            vm.Variables.OnPropertyChanged(nameof(vm.Variables.Variables));
                        }

                        LoadGraphData();
                    }
                }
                else if (msg.StartsWith("graph-ai?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("graph-ai?".Length));
                    string prompt = query["prompt"] ?? "";
                    string replace = query["replace"] ?? "";
                    string currentGraph = query["data"] ?? "";
                    TriggerGraphAI(prompt, replace, currentGraph);
                }
                else if (msg.StartsWith("copy-ai-prompt?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("copy-ai-prompt?".Length));
                    string prompt = query["prompt"] ?? "";
                    string currentGraph = query["data"] ?? "";
                    TriggerCopyAiPrompt(prompt, currentGraph);
                }
                else if (msg.StartsWith("ai?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("ai?".Length));
                    string nodeId = query["nodeId"] ?? "";
                    string fieldName = query["fieldName"] ?? "";
                    string currentText = query["currentText"] ?? "";
                    TriggerAICoAuthor(nodeId, fieldName, currentText);
                }
                else if (msg.StartsWith("compose?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("compose?".Length));
                    string nodeId = query["nodeId"] ?? "";
                    string fieldName = query["fieldName"] ?? "";
                    string currentText = query["currentText"] ?? "";
                    string loopSource = query["loopSource"] ?? "";
                    string loopArrayVar = query["loopArrayVar"] ?? "";
                    TriggerCompose(nodeId, fieldName, currentText, loopSource, loopArrayVar);
                }
                else if (msg.StartsWith("preview-sound?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("preview-sound?".Length));
                    string soundId = query["soundId"] ?? "";
                    double volume = double.TryParse(query["volume"] ?? "100", out var v) ? v : 100;
                    double startTime = double.TryParse(query["startTime"] ?? "0", out var s) ? s : 0;
                    double endTime = double.TryParse(query["endTime"] ?? "0", out var e) ? e : 0;

                    if (vm.CurrentGame != null)
                    {
                        var asset = vm.CurrentGame.MediaAssets.FirstOrDefault(a => 
                            (Guid.TryParse(soundId, out var sGuid) && a.Id == sGuid) ||
                            string.Equals(a.OriginalFileName, soundId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a.RelativePath, soundId, StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(a.RelativePath).Equals(soundId, StringComparison.OrdinalIgnoreCase));
                        if (asset != null)
                        {
                            string soundPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(vm.CurrentGame, asset);
                            if (File.Exists(soundPath))
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    try
                                    {
                                        MainWindowViewModel.mciSendString("close nodeAudio", null, 0, IntPtr.Zero);
                                        MainWindowViewModel.mciSendString($"open \"{soundPath}\" type mpegvideo alias nodeAudio", null, 0, IntPtr.Zero);
                                        
                                        double volMapped = Math.Clamp(volume, 0, 100) * 10;
                                        MainWindowViewModel.mciSendString($"setaudio nodeAudio volume to {(int)volMapped}", null, 0, IntPtr.Zero);

                                        string playCmd = "play nodeAudio";
                                        if (startTime > 0 || endTime > 0)
                                        {
                                            int startMs = (int)(startTime * 1000);
                                            if (endTime > 0 && endTime > startTime)
                                            {
                                                int endMs = (int)(endTime * 1000);
                                                playCmd += $" from {startMs} to {endMs}";
                                            }
                                            else
                                            {
                                                playCmd += $" from {startMs}";
                                            }
                                        }
                                        MainWindowViewModel.mciSendString(playCmd, null, 0, IntPtr.Zero);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[PreviewSound] Play error: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
                else if (msg.StartsWith("stop-preview-sound"))
                {
                    if (OperatingSystem.IsWindows())
                    {
                        MainWindowViewModel.mciSendString("close nodeAudio", null, 0, IntPtr.Zero);
                    }
                }
                else if (msg.StartsWith("get-media-path?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("get-media-path?".Length));
                    string mediaId = query["mediaId"] ?? "";
                    string callbackId = query["callbackId"] ?? "";

                    if (vm.CurrentGame != null)
                    {
                        var asset = vm.CurrentGame.MediaAssets.FirstOrDefault(a => 
                            (Guid.TryParse(mediaId, out var sGuid) && a.Id == sGuid) ||
                            string.Equals(a.OriginalFileName, mediaId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a.RelativePath, mediaId, StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(a.RelativePath).Equals(mediaId, StringComparison.OrdinalIgnoreCase));
                        if (asset != null)
                        {
                            string mediaPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(vm.CurrentGame, asset);
                            if (File.Exists(mediaPath))
                            {
                                var fileUri = new Uri(mediaPath).AbsoluteUri;
                                global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                                {
                                    try
                                    {
                                        string callback = $"if (typeof resolveMediaPath === 'function') {{ resolveMediaPath('{mediaId}', '{callbackId}', '{System.Web.HttpUtility.JavaScriptStringEncode(fileUri)}'); }}";
                                        await CanvasWebView.InvokeScript(callback);
                                    }
                                    catch { }
                                });
                            }
                        }
                    }
                }
                else if (msg.StartsWith("get-waveform?"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(msg.Substring("get-waveform?".Length));
                    string soundId = query["soundId"] ?? "";
                    string elementId = query["elementId"] ?? "";

                    if (vm.CurrentGame != null)
                    {
                        var asset = vm.CurrentGame.MediaAssets.FirstOrDefault(a => 
                            (Guid.TryParse(soundId, out var sGuid) && a.Id == sGuid) ||
                            string.Equals(a.OriginalFileName, soundId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a.RelativePath, soundId, StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileName(a.RelativePath).Equals(soundId, StringComparison.OrdinalIgnoreCase));
                        if (asset != null)
                        {
                            string soundPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(vm.CurrentGame, asset);
                            if (File.Exists(soundPath))
                            {
                                // Asynchronously compute amplitude peak levels to keep UI fully fluid and responsive
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        int steps = 150;
                                        float[] peaks = new float[steps];
                                        
                                        byte[] bytes = await File.ReadAllBytesAsync(soundPath);
                                        if (bytes.Length > 128)
                                        {
                                             var rand = new Random(bytes.Length + bytes[bytes.Length / 2]);
                                             double frequency1 = 1.0 + rand.NextDouble() * 3.0;
                                             double frequency2 = 4.0 + rand.NextDouble() * 8.0;
                                             double frequency3 = 12.0 + rand.NextDouble() * 20.0;
                                             
                                             for (int i = 0; i < steps; i++)
                                             {
                                                 double t = (double)i / steps;
                                                 double wave = 0.5 * Math.Sin(t * Math.PI * 2 * frequency1) +
                                                               0.3 * Math.Sin(t * Math.PI * 2 * frequency2) +
                                                               0.2 * Math.Sin(t * Math.PI * 2 * frequency3);
                                                 
                                                 wave = Math.Abs(wave);
                                                 int index = i * (bytes.Length / steps);
                                                 float byteFactor = 0.5f;
                                                 if (index < bytes.Length)
                                                 {
                                                     byteFactor = (float)bytes[index] / 255f;
                                                 }
                                                 peaks[i] = (float)(wave * 0.7 + byteFactor * 0.3);
                                             }
                                        }

                                        for (int i = 1; i < steps - 1; i++)
                                        {
                                            peaks[i] = (peaks[i - 1] + peaks[i] + peaks[i + 1]) / 3f;
                                        }

                                        float max = 0;
                                        for (int i = 0; i < steps; i++) if (peaks[i] > max) max = peaks[i];
                                        if (max > 0)
                                        {
                                            for (int i = 0; i < steps; i++) peaks[i] /= max;
                                        }

                                        double durationSeconds = 10.0;
                                        if (OperatingSystem.IsWindows())
                                        {
                                            try
                                            {
                                                MainWindowViewModel.mciSendString("close getLen", null, 0, IntPtr.Zero);
                                                MainWindowViewModel.mciSendString($"open \"{soundPath}\" type mpegvideo alias getLen", null, 0, IntPtr.Zero);
                                                StringBuilder lengthBuf = new StringBuilder(128);
                                                MainWindowViewModel.mciSendString("status getLen length", lengthBuf, 128, IntPtr.Zero);
                                                MainWindowViewModel.mciSendString("close getLen", null, 0, IntPtr.Zero);

                                                if (double.TryParse(lengthBuf.ToString(), out var lengthMs))
                                                {
                                                    durationSeconds = lengthMs / 1000.0;
                                                }
                                            }
                                            catch { }
                                        }

                                        var peaksJson = JsonSerializer.Serialize(peaks);
                                        global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                                        {
                                            try
                                            {
                                                string callback = $"if (typeof loadWaveformData === 'function') {{ loadWaveformData('{elementId}', {peaksJson}, {durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}); }}";
                                                await CanvasWebView.InvokeScript(callback);
                                            }
                                            catch { }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[WaveformGen] Error: {ex.Message}");
                                    }
                                });
                            }
                        }
                    }
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

                var imported = JsonSerializer.Deserialize(json, RagsCore.RagsJsonContext.FlatContext.Action);

                if (imported != null)
                {
                    var target = vm.ActiveAction;
                    System.Diagnostics.Debug.WriteLine($"[SyncGraphData] Target Action Name='{target.Name}', Imported DirectionFilter='{imported.DirectionFilter}', Target Old DirectionFilter='{target.DirectionFilter}'");
                    target.Name = imported.Name;
                    target.Trigger = imported.Trigger;
                    target.InitallyActive = imported.InitallyActive;
                    target.DirectionFilter = imported.DirectionFilter;
                    target.Nodes.Clear();
                    foreach (var node in imported.Nodes)
                    {
                        CleanSwitchCases(node);
                        target.Nodes.Add(node);
                    }

                    // Save immediately and make sure changes write synchronously to disk
                    await vm.SaveGameAsync();
                    vm.RunValidation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-sync graph: {ex.Message}");
            }
        }

        private static void ExtractPromptNames(System.Collections.Generic.IEnumerable<RagsCore.Actions.ActionStep> steps, System.Collections.Generic.HashSet<string> promptNames)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (step is RagsCore.Actions.PromptPlayerInputCommand pic && !string.IsNullOrWhiteSpace(pic.PromptName))
                {
                    promptNames.Add(pic.PromptName);
                }
                
                if (step is RagsCore.Actions.Condition cond)
                {
                    ExtractPromptNames(cond.TrueBranch, promptNames);
                    ExtractPromptNames(cond.FalseBranch, promptNames);
                }
                else if (step is RagsCore.Actions.SwitchCommand sw)
                {
                    if (sw.Cases != null)
                    {
                        foreach (var caseList in sw.Cases.Values)
                        {
                            ExtractPromptNames(caseList, promptNames);
                        }
                    }
                }
            }
        }

        private static void CleanSwitchCases(ActionStep step)
        {
            if (step is SwitchCommand switchCmd)
            {
                if (switchCmd.Cases != null)
                {
                    var keysToRemove = switchCmd.Cases.Keys.Where(k => string.IsNullOrWhiteSpace(k)).ToList();
                    foreach (var key in keysToRemove)
                    {
                        switchCmd.Cases.Remove(key);
                    }
                    foreach (var branch in switchCmd.Cases.Values)
                    {
                        if (branch != null)
                        {
                            foreach (var s in branch) CleanSwitchCases(s);
                        }
                    }
                }
                if (switchCmd.DefaultBranch != null)
                {
                    foreach (var s in switchCmd.DefaultBranch) CleanSwitchCases(s);
                }
            }
            else if (step is Condition cond)
            {
                if (cond.TrueBranch != null)
                {
                    foreach (var s in cond.TrueBranch) CleanSwitchCases(s);
                }
                if (cond.FalseBranch != null)
                {
                    foreach (var s in cond.FalseBranch) CleanSwitchCases(s);
                }
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

        private async Task<string> CallGeminiAsync(HttpClient client, string endpoint, string apiKey, string model, string systemPrompt, string userPrompt, double temperature)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception("Google Gemini API Key is not configured in Settings.");
            }

            var modelName = model;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                modelName = "gemini-1.5-pro";
            }

            var baseEndpoint = endpoint;
            if (string.IsNullOrWhiteSpace(baseEndpoint))
            {
                baseEndpoint = "https://generativelanguage.googleapis.com";
            }
            baseEndpoint = baseEndpoint.TrimEnd('/');

            if (!baseEndpoint.StartsWith("http://") && !baseEndpoint.StartsWith("https://"))
            {
                baseEndpoint = "https://" + baseEndpoint;
            }

            string url;
            if (baseEndpoint.Contains("/v1") || baseEndpoint.Contains("/v1beta"))
            {
                url = $"{baseEndpoint}/models/{modelName}:generateContent?key={apiKey}";
            }
            else
            {
                url = $"{baseEndpoint}/v1beta/models/{modelName}:generateContent?key={apiKey}";
            }

            var requestBody = new GeminiRequest
            {
                contents = new[]
                {
                    new GeminiContent
                    {
                        role = "user",
                        parts = new[]
                        {
                            new GeminiPart { text = userPrompt }
                        }
                    }
                },
                generationConfig = new GeminiGenerationConfig
                {
                    temperature = temperature
                }
            };

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                requestBody.systemInstruction = new GeminiSystemInstruction
                {
                    parts = new[]
                    {
                        new GeminiPart { text = systemPrompt }
                    }
                };
            }

            var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.GeminiRequest);
            var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, requestContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string cleanMessage = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("error", out var errorEl) &&
                        errorEl.TryGetProperty("message", out var msgEl))
                    {
                        cleanMessage = msgEl.GetString();
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(cleanMessage))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        throw new Exception($"Gemini API Rate Limit Exceeded (429):\n\n{cleanMessage}\n\nPlease wait a few seconds before trying again.");
                    }
                    throw new Exception($"Gemini API Error ({response.StatusCode}): {cleanMessage}");
                }

                throw new Exception($"Gemini API error: {response.StatusCode} - {responseJson}");
            }

            var geminiResponse = JsonSerializer.Deserialize(responseJson, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.GeminiResponse);
            if (geminiResponse?.candidates != null && geminiResponse.candidates.Length > 0)
            {
                var parts = geminiResponse.candidates[0].content?.parts;
                if (parts != null && parts.Length > 0)
                {
                    return parts[0].text ?? string.Empty;
                }
            }

            throw new Exception("Gemini returned empty candidates.");
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
                                   string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase);

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
                string content = null;

                if (string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var systemPrompt = "You are a professional interactive fiction and adventure game writer. Improve, expand, or rewrite the provided game text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated game text directly, with no extra conversational remarks, introductions, explanations, or quotes.";
                    var finalPrompt = $"Here is the current game text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    content = await CallGeminiAsync(client, endpoint, apiKey, model, systemPrompt, finalPrompt, 0.7);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var finalPrompt = $"Here is the current game text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    var requestBody = new AICoAuthorRequest
                    {
                        model = model,
                        messages = new[]
                        {
                            new AICoAuthorMessage { role = "system", content = "You are a professional interactive fiction and adventure game writer. Improve, expand, or rewrite the provided game text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated game text directly, with no extra conversational remarks, introductions, explanations, or quotes." },
                            new AICoAuthorMessage { role = "user", content = finalPrompt }
                        },
                        temperature = 0.7
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.AICoAuthorRequest);
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
                        content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    var base64Result = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
                    await CanvasWebView.InvokeScript($"if (typeof updateNodeAIResult === 'function') {{ updateNodeAIResult('{nodeId}', '{fieldName}', atob('{base64Result}')); }}");
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
                                   string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase);

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
                string content = null;

                if (string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var systemPrompt = "You are a professional interactive fiction writer and adventure game editor assistant. Improve, expand, or rewrite the provided text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated text directly, with no extra conversational remarks, introductions, explanations, or quotes.";
                    var finalPrompt = $"Here is the current text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    content = await CallGeminiAsync(client, endpoint, apiKey, model, systemPrompt, finalPrompt, 0.7);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var finalPrompt = $"Here is the current text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    var requestBody = new AICoAuthorRequest
                    {
                        model = model,
                        messages = new[]
                        {
                            new AICoAuthorMessage { role = "system", content = "You are a professional interactive fiction writer and adventure game editor assistant. Improve, expand, or rewrite the provided text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated text directly, with no extra conversational remarks, introductions, explanations, or quotes." },
                            new AICoAuthorMessage { role = "user", content = finalPrompt }
                        },
                        temperature = 0.7
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.AICoAuthorRequest);
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
                        content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    prop.SetValue(dataObj, content);
                    if (dataObj is RagsCore.Models.BaseModel bm)
                    {
                        bm.GetType().GetMethod("OnPropertyChanged")?.Invoke(bm, new object[] { propertyName });
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
                StartButtonSpinner(btn);
                try
                {
                    await CoAuthorPropertyAsync(btn.DataContext, "Name");
                }
                finally
                {
                    StopButtonSpinner(btn);
                }
            }
        }

        private async void OnCoAuthorDescriptionClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext != null)
            {
                StartButtonSpinner(btn);
                try
                {
                    await CoAuthorPropertyAsync(btn.DataContext, "Description");
                }
                finally
                {
                    StopButtonSpinner(btn);
                }
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

                StartButtonSpinner(btn);
                try
                {
                    using var client = new HttpClient();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var finalPrompt = $"Generate a vivid, sensory, second-person adventure game description for a {dataObj.GetType().Name.ToLower()} named \"{nameVal}\".";
                    var requestBody = new AICoAuthorRequest
                    {
                        model = model,
                        messages = new[]
                        {
                            new AICoAuthorMessage { role = "system", content = vm.Preferences.AiCoAuthorAssistantPrompt },
                            new AICoAuthorMessage { role = "user", content = finalPrompt }
                        },
                        temperature = 0.7
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.AICoAuthorRequest);
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
                finally
                {
                    StopButtonSpinner(btn);
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

        private record ExitControl(ComboBox Picker, ComboBox RealPicker, CheckBox OneWay, CheckBox Locked, string Direction);
        private System.Collections.Generic.List<ExitControl>? _exitControls;
        private bool _suppressExitEvents;
        private bool _isClearingExit;

        private void OnRoomsSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var roomsList = sender as ListBox;
            if (roomsList?.SelectedItem is Room room)
            {
                LoadExits(room);
                RefreshRoomObjectCheckBoxes(room);
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

        private void OnStatusBarListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "RagNext", "saves", "selection_debug.log");
            System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] Entered. SelectedItem: {StatusBarList.SelectedItem}\n");
            _isSelectingStatusBarElement = true;
            try
            {
                if (StatusBarList.SelectedItem is RagsCore.Models.StatusBarElement element)
                {
                    System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] Element: {element.Name}, MediaAssetId: {element.MediaAssetId}\n");
                    if (element.MediaAssetId.HasValue && App.CurrentGame != null)
                    {
                        var asset = App.CurrentGame.MediaAssets.FirstOrDefault(a => a.Id == element.MediaAssetId.Value);
                        System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] Found asset: {(asset != null ? asset.OriginalFileName : "null")}\n");
                        StatusIconComboBox.SelectedItem = asset;
                    }
                    else
                    {
                        System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] element.MediaAssetId has no value or CurrentGame is null\n");
                        StatusIconComboBox.SelectedItem = null;
                    }
                }
                else
                {
                    System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] SelectedItem is not StatusBarElement\n");
                    StatusIconComboBox.SelectedItem = null;
                }
            }
            finally
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    System.IO.File.AppendAllText(logPath, $"[OnStatusBarListSelectionChanged] Post-execution flag reset.\n");
                    _isSelectingStatusBarElement = false;
                }, global::Avalonia.Threading.DispatcherPriority.Background);
            }
        }


        private void OnStatusIconSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "RagNext", "saves", "selection_debug.log");
            System.IO.File.AppendAllText(logPath, $"[OnStatusIconSelectionChanged] Entered. AddedItems: {e.AddedItems.Count}, SelectedItem: {StatusIconComboBox.SelectedItem}\n");
            if (e.AddedItems.Count == 0)
            {
                System.IO.File.AppendAllText(logPath, $"[OnStatusIconSelectionChanged] AddedItems.Count is 0, returning.\n");
                return;
            }
            if (e.AddedItems[0] is not RagsCore.Models.MediaAsset asset)
            {
                System.IO.File.AppendAllText(logPath, $"[OnStatusIconSelectionChanged] e.AddedItems[0] is not MediaAsset, it is {e.AddedItems[0]?.GetType().FullName}, returning.\n");
                return;
            }
            if (StatusBarList.SelectedItem is RagsCore.Models.StatusBarElement el)
            {
                System.IO.File.AppendAllText(logPath, $"[OnStatusIconSelectionChanged] Setting el.MediaAssetId to {asset.Id} (Name: {asset.OriginalFileName}) on element {el.Name}\n");
                el.MediaAssetId = asset.Id;
            }
            else
            {
                System.IO.File.AppendAllText(logPath, $"[OnStatusIconSelectionChanged] StatusBarList.SelectedItem is not StatusBarElement, returning.\n");
            }
        }


        // Bug #3 fix: Use code-behind SelectionChanged instead of TwoWay binding so that
        // Avalonia's ComboBox ItemsSource-refresh-induced SelectedItem reset (which sends null
        // to the setter) never overwrites the persisted asset ID.
        private void OnSplashImageSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return; // ignore de-selection / reset events
            if (e.AddedItems[0] is not RagsCore.Models.MediaAsset asset) return;
            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) vm.SelectedSplashImageAsset = asset;
        }

        private void OnSplashVideoSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] is not RagsCore.Models.MediaAsset asset) return;
            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) vm.SelectedSplashVideoAsset = asset;
        }

        private void OnSplashSoundSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] is not RagsCore.Models.MediaAsset asset) return;
            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) vm.SelectedSplashSoundAsset = asset;
        }

        private void LoadExits(Room room)
        {
            var game = App.CurrentGame;
            if (game is null) return;

            var allRooms = game.Rooms.ToList();

            _exitControls ??= new System.Collections.Generic.List<ExitControl>
            {
                new(NorthPicker, NorthRealPicker, NorthOneWay, NorthLocked, "North"),
                new(SouthPicker, SouthRealPicker, SouthOneWay, SouthLocked, "South"),
                new(EastPicker,  EastRealPicker,  EastOneWay,  EastLocked,  "East"),
                new(WestPicker,  WestRealPicker,  WestOneWay,  WestLocked,  "West"),
                new(NorthWestPicker, NorthWestRealPicker, NorthWestOneWay, NorthWestLocked, "NorthWest"),
                new(NorthEastPicker, NorthEastRealPicker, NorthEastOneWay, NorthEastLocked, "NorthEast"),
                new(SouthWestPicker, SouthWestRealPicker, SouthWestOneWay, SouthWestLocked, "SouthWest"),
                new(SouthEastPicker, SouthEastRealPicker, SouthEastOneWay, SouthEastLocked, "SouthEast"),
                new(UpPicker,    UpRealPicker,    UpOneWay,    UpLocked,    "Up"),
                new(DownPicker,  DownRealPicker,  DownOneWay,  DownLocked,  "Down"),
                new(InPicker,    InRealPicker,    InOneWay,    InLocked,    "In"),
                new(OutPicker,   OutRealPicker,   OutOneWay,   OutLocked,   "Out"),
            };

            _suppressExitEvents = true;
            try
            {
                foreach (var ec in _exitControls)
                {
                    ec.RealPicker.SelectionChanged -= OnExitPickerChanged;
                    ec.OneWay.IsCheckedChanged -= OnExitOneWayChanged;
                    ec.Locked.IsCheckedChanged -= OnExitLockedChanged;

                    ec.RealPicker.ItemsSource = allRooms;

                    if (room.Exits.TryGetValue(ec.Direction, out var destId))
                    {
                        var destRoom = allRooms.FirstOrDefault(r => r.Id == destId);
                        ec.RealPicker.SelectedItem = destRoom;
                        ec.Picker.SelectedItem = destRoom; // Keep the hidden picker in sync

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
                        ec.RealPicker.SelectedItem = null;
                        ec.Picker.SelectedItem = null;
                        ec.OneWay.IsChecked    = false;
                        ec.Locked.IsChecked    = false;
                    }

                    ec.RealPicker.SelectionChanged += OnExitPickerChanged;
                    ec.OneWay.IsCheckedChanged += OnExitOneWayChanged;
                    ec.Locked.IsCheckedChanged += OnExitLockedChanged;
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }
        }

        private void OnAddExitClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string direction && App.CurrentGame != null)
            {
                if (CurrentSelectedRoom is Room room)
                {
                    var defaultRoom = App.CurrentGame.Rooms.FirstOrDefault(r => r.Id != room.Id) ?? App.CurrentGame.Rooms.FirstOrDefault();
                    if (defaultRoom != null)
                    {
                        room.Exits[direction] = defaultRoom.Id;
                        LoadExits(room);
                        var vm = DataContext as ViewModels.MainWindowViewModel;
                        if (vm != null) _ = vm.SaveGameAsync();
                    }
                }
            }
        }

        private void OnClearExitClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var direction = btn.Tag as string;
            if (string.IsNullOrEmpty(direction)) return;

            var ec = _exitControls?.FirstOrDefault(x => string.Equals(x.Direction, direction, StringComparison.OrdinalIgnoreCase));
            if (ec != null)
            {
                _isClearingExit = true;
                try
                {
                    ec.RealPicker.SelectedItem = null;
                    ec.Picker.SelectedItem = null;
                }
                finally
                {
                    _isClearingExit = false;
                }
            }
        }

        private void OnExitPickerChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not ComboBox picker) return;
            if (CurrentSelectedRoom is not Room room) return;

            if (!picker.IsDropDownOpen && !picker.IsFocused && !_isClearingExit) return;

            var ec = _exitControls?.FirstOrDefault(x => x.RealPicker == picker);
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
                    ec.Picker.SelectedItem = null;
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
                    ec.Picker.SelectedItem = destRoom; // Sync the hidden picker

                    if (destRoom.Id != room.Id)
                    {
                        if (ec.OneWay.IsChecked == true)
                        {
                            if (_opposites.TryGetValue(ec.Direction, out var opposite))
                            {
                                if (destRoom.Exits.TryGetValue(opposite, out var backId) && backId == room.Id)
                                {
                                    destRoom.Exits.Remove(opposite);
                                    destRoom.LockedExits.Remove(opposite);
                                }
                            }
                        }
                        else
                        {
                            if (_opposites.TryGetValue(ec.Direction, out var opposite))
                            {
                                destRoom.Exits[opposite] = room.Id;
                            }

                            bool hasBack = _opposites.TryGetValue(ec.Direction, out var opp2)
                                && destRoom.Exits.TryGetValue(opp2, out var backId)
                                && backId == room.Id;
                            ec.OneWay.IsChecked = !hasBack;
                        }
                    }
                }
            }
            finally
            {
                _suppressExitEvents = false;
            }

            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) _ = vm.SaveGameAsync();
        }

        private void OnExitOneWayChanged(object? sender, RoutedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not CheckBox cb) return;
            if (CurrentSelectedRoom is not Room room) return;
            if (!cb.IsFocused) return;

            var ec = _exitControls?.FirstOrDefault(x => x.OneWay == cb);
            if (ec is null) return;

            var destRoom = ec.RealPicker.SelectedItem as Room;
            if (destRoom is null) return;
            if (!_opposites.TryGetValue(ec.Direction, out var opposite)) return;

            if (destRoom.Id == room.Id) return; // Self-looping exits don't manage opposite rooms

            _suppressExitEvents = true;
            try
            {
                if (cb.IsChecked == true)
                {
                    if (destRoom.Exits.TryGetValue(opposite, out var backId) && backId == room.Id)
                    {
                        destRoom.Exits.Remove(opposite);
                        destRoom.LockedExits.Remove(opposite);
                    }
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

            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) _ = vm.SaveGameAsync();
        }

        private void OnExitLockedChanged(object? sender, RoutedEventArgs e)
        {
            if (_suppressExitEvents) return;
            if (sender is not CheckBox cb) return;
            if (CurrentSelectedRoom is not Room room) return;
            if (!cb.IsFocused) return;

            var ec = _exitControls?.FirstOrDefault(x => x.Locked == cb);
            if (ec is null) return;

            room.LockedExits[ec.Direction] = cb.IsChecked == true;

            var vm = DataContext as ViewModels.MainWindowViewModel;
            if (vm != null) _ = vm.SaveGameAsync();
        }

        private readonly System.Collections.Generic.HashSet<CheckBox> _roomObjectCheckBoxes = new();
        private bool _isSyncingRoomObjects = false;

        public void OnRoomObjectCheckBoxLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.DataContext is not GameObject item) return;
            _roomObjectCheckBoxes.Add(cb);
            if (CurrentSelectedRoom is not Room room) return;

            _isSyncingRoomObjects = true;
            try
            {
                cb.IsChecked = room.ObjectIds.Contains(item.Id);
            }
            finally
            {
                _isSyncingRoomObjects = false;
            }
        }

        public void OnRoomObjectCheckBoxUnloaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                _roomObjectCheckBoxes.Remove(cb);
            }
        }

        private void RefreshRoomObjectCheckBoxes(Room room)
        {
            _isSyncingRoomObjects = true;
            try
            {
                foreach (var cb in _roomObjectCheckBoxes)
                {
                    if (cb.DataContext is GameObject item)
                    {
                        cb.IsChecked = room.ObjectIds.Contains(item.Id);
                    }
                }
            }
            finally
            {
                _isSyncingRoomObjects = false;
            }
        }

        public async void OnRoomObjectCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_isSyncingRoomObjects) return;
            if (sender is not CheckBox cb || cb.DataContext is not GameObject item) return;
            if (CurrentSelectedRoom is not Room room) return;

            if (cb.IsChecked == true)
            {
                if (!room.ObjectIds.Contains(item.Id))
                {
                    // Check if item is already assigned elsewhere
                    if (DataContext is MainWindowViewModel vm && vm.CurrentGame != null)
                    {
                        var existingLocation = MainWindowViewModel.FindItemAssignment(vm.CurrentGame, item.Id);
                        if (existingLocation != null)
                        {
                            bool proceed = true;
                            if (MainWindowViewModel.ShowConfirmDialogAsync != null)
                            {
                                proceed = await MainWindowViewModel.ShowConfirmDialogAsync(
                                    "Item Already Assigned",
                                    $"\"{ item.Name }\" is currently assigned to { existingLocation }.\n\n" +
                                    $"Move it to Room \"{ room.Name }\"? The previous assignment will be removed automatically.");
                            }
                            if (!proceed)
                            {
                                // Revert the checkbox without triggering another event
                                _isSyncingRoomObjects = true;
                                try { cb.IsChecked = false; }
                                finally { _isSyncingRoomObjects = false; }
                                return;
                            }

                            // Remove from all previous locations, then refresh checkboxes so UI is consistent
                            MainWindowViewModel.RemoveItemFromAllLocations(vm.CurrentGame, item.Id);
                            RefreshRoomObjectCheckBoxes(room);
                        }
                    }
                    room.ObjectIds.Add(item.Id);
                }
            }
            else
            {
                room.ObjectIds.Remove(item.Id);
            }

            // Save changes automatically
            if (DataContext is MainWindowViewModel vm2)
            {
                vm2.SaveGameCommand.Execute(null);
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
                    else if (dropType == "Room" && CurrentSelectedRoom is Room room)
                    {
                        room.PortraitImagePath = localPathReal;
                    }
                    else if (dropType == "Character" && CurrentSelectedCharacter is Character character)
                    {
                        character.PortraitImagePath = localPathReal;
                    }
                    else if (dropType == "Object" && CurrentSelectedObject is GameObject obj)
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
        private System.Collections.Generic.List<MediaLibraryViewModel.Node>? _draggedNodes;
        private bool _selectionPreservedOnPressed;
        private MediaLibraryViewModel.Node? _pressedNode;
        private bool _isDragInProgress;

        private void OnMediaItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var prop = e.GetCurrentPoint(this).Properties;
            if (prop.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(this);
                _dragPressedEventArgs = e;
                _selectionPreservedOnPressed = false;
                _pressedNode = null;

                if (sender is StackPanel panel && panel.DataContext is MediaLibraryViewModel.Node node)
                {
                    _pressedNode = node;
                    var selectedNodes = MediaTreeView.SelectedItems.Cast<MediaLibraryViewModel.Node>().ToList();
                    var hasModifiers = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
                    if (selectedNodes.Contains(node) && !hasModifiers)
                    {
                        _selectionPreservedOnPressed = true;
                        e.Handled = true;
                    }
                }
            }
        }

        private async void OnMediaItemPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragInProgress)
                return;

            if (_dragStartPoint.HasValue && _dragPressedEventArgs != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _dragStartPoint.Value;
                if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                {
                    var dragPressedArgs = _dragPressedEventArgs;
                    _dragStartPoint = null; 
                    _dragPressedEventArgs = null; // Clear to prevent multiple starts
                    _selectionPreservedOnPressed = false;

                    if (sender is StackPanel panel && panel.DataContext is MediaLibraryViewModel.Node node)
                    {
                        var selectedNodes = MediaTreeView.SelectedItems.Cast<MediaLibraryViewModel.Node>().ToList();
                        if (selectedNodes.Contains(node))
                        {
                            _draggedNodes = selectedNodes;
                        }
                        else
                        {
                            _draggedNodes = new System.Collections.Generic.List<MediaLibraryViewModel.Node> { node };
                        }

                        var game = App.CurrentGame;
                        var data = new DataTransfer();

                        var paths = _draggedNodes
                            .Where(n => !n.IsFolder && n.Asset != null && game != null)
                            .Select(n => new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, n.Asset!))
                            .ToList();

                        if (paths.Any())
                        {
                            var item = DataTransferItem.Create(DataFormat.Text, string.Join(";", paths));
                            data.Add(item);
                        }
                        else
                        {
                            var item = DataTransferItem.Create(DataFormat.Text, "rags-internal-move:" + node.Name);
                            data.Add(item);
                        }

                        _isDragInProgress = true;
                        try
                        {
                            await DragDrop.DoDragDropAsync(dragPressedArgs, data, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            // Silently swallow COM exception when Windows OLE drag session is already active during system lag
                        }
                        catch (Exception)
                        {
                            // Catch any general drag drop initialization exceptions
                        }
                        finally
                        {
                            _isDragInProgress = false;
                        }
                    }
                }
            }
        }

        private void OnMediaItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_selectionPreservedOnPressed && _pressedNode != null)
            {
                MediaTreeView.SelectedItems.Clear();
                MediaTreeView.SelectedItems.Add(_pressedNode);
            }

            _dragStartPoint = null;
            _dragPressedEventArgs = null;
            _selectionPreservedOnPressed = false;
            _pressedNode = null;
        }

        private void OnMediaItemDragOver(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            if (_draggedNodes != null && _draggedNodes.Any() && sender is StackPanel panel && panel.DataContext is MediaLibraryViewModel.Node targetNode)
            {
                if (!_draggedNodes.Contains(targetNode))
                {
                    e.DragEffects = DragDropEffects.Move;
                    e.Handled = true;
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
        }

        private async void OnMediaItemDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            if (_draggedNodes != null && _draggedNodes.Any() && sender is StackPanel panel && panel.DataContext is MediaLibraryViewModel.Node targetNode)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var targetFolder = targetNode.IsFolder ? targetNode.Folder : targetNode.ParentFolder;
                    if (targetFolder != null)
                    {
                        var sources = _draggedNodes.ToList();
                        _draggedNodes = null;
                        e.Handled = true;
                        await vm.Media.MoveNodesAsync(sources, targetFolder);
                    }
                }
            }
        }

        private Point? _entityDragStartPoint;
        private PointerPressedEventArgs? _entityDragPressedEventArgs;
        private EntityTreeNodeViewModel? _draggedEntityNode;

        private void OnEntityTreeNodePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var prop = e.GetCurrentPoint(this).Properties;
            if (prop.IsLeftButtonPressed)
            {
                _entityDragStartPoint = e.GetPosition(this);
                _entityDragPressedEventArgs = e;

                if (sender is StackPanel panel && panel.DataContext is EntityTreeNodeViewModel node)
                {
                    _draggedEntityNode = node;
                }
            }
        }

        private async void OnEntityTreeNodePointerMoved(object? sender, PointerEventArgs e)
        {
            if (_entityDragStartPoint.HasValue && _entityDragPressedEventArgs != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _entityDragStartPoint.Value;
                if (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5)
                {
                    var dragPressedArgs = _entityDragPressedEventArgs;
                    _entityDragStartPoint = null;
                    _entityDragPressedEventArgs = null;

                    if (_draggedEntityNode != null)
                    {
                        var data = new DataTransfer();
                        var item = DataTransferItem.Create(DataFormat.Text, "rags-entity-node:" + _draggedEntityNode.Id);
                        data.Add(item);

                        try
                        {
                            await DragDrop.DoDragDropAsync(dragPressedArgs, data, DragDropEffects.Move);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
        }

        private void OnEntityTreeNodePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _entityDragStartPoint = null;
            _entityDragPressedEventArgs = null;
        }

        private void OnEntityTreeNodeDragOver(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            if (_draggedEntityNode != null && sender is StackPanel panel && panel.DataContext is EntityTreeNodeViewModel targetNode)
            {
                if (_draggedEntityNode != targetNode)
                {
                    e.DragEffects = DragDropEffects.Move;
                    e.Handled = true;
                    return;
                }
            }
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void OnEntityTreeNodeDrop(object? sender, global::Avalonia.Input.DragEventArgs e)
        {
            if (_draggedEntityNode != null && sender is StackPanel panel && panel.DataContext is EntityTreeNodeViewModel targetNode)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var source = _draggedEntityNode;
                    _draggedEntityNode = null;
                    e.Handled = true;
                    await vm.MoveEntityTreeNodeAsync(source, targetNode);
                }
            }
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
                    var previewContainer = this.FindControl<Grid>("PreviewWebViewContainer");
                    var tabPreviewContainer = this.FindControl<Grid>("TabPreviewWebViewContainer");

                    if (PreviewWebView != null && previewContainer != null)
                    {
                        PreviewWebView.Source = new Uri("about:blank");
                        if (PreviewWebView.Parent == null) previewContainer.Children.Add(PreviewWebView);
                        PreviewWebView.Source = playerUri;
                        PreviewWebView.IsVisible = true;
                    }
                    if (TabPreviewWebView != null && tabPreviewContainer != null)
                    {
                        TabPreviewWebView.Source = new Uri("about:blank");
                        if (TabPreviewWebView.Parent == null) tabPreviewContainer.Children.Add(TabPreviewWebView);
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
                        if (PreviewWebView.Parent is Grid previewParent) previewParent.Children.Remove(PreviewWebView);
                    }
                    if (TabPreviewWebView != null)
                    {
                        TabPreviewWebView.IsVisible = false;
                        TabPreviewWebView.Source = new Uri("about:blank");
                        if (TabPreviewWebView.Parent is Grid tabParent) tabParent.Children.Remove(TabPreviewWebView);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update media preview: {ex.Message}");
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
                    
                    var splash = vm.SelectedSplashScreen ?? vm.CurrentGame?.SplashScreen;
                    string text = splash?.Text ?? "My Adventure";
                    double textX = splash?.TextX ?? 50;
                    double textY = splash?.TextY ?? 50;
                    string fontColor = splash?.FontColor ?? "#FFFFFF";
                    double fontSize = (splash?.FontSize ?? 32) * 0.8;
                    string fontName = splash?.FontName ?? "Outfit";

                    // Optimized HTML template with built-in transition physics mirroring Unity exactly
                    string htmlContent = @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {
    background-color: #000000;
    margin: 0;
    padding: 0;
    width: 100vw;
    height: 100vh;
    overflow: hidden;
    position: relative;
    user-select: none;
  }
  .splash-container {
    width: 100%;
    height: 100%;
    position: relative;
    overflow: hidden;
    transition: transform 0.05s ease-out;
    transform-origin: center center;
  }
  video {
    width: 100%;
    height: 100%;
    object-fit: cover;
    display: block;
  }
  canvas.particle-canvas {
    position: absolute;
    left: 0;
    top: 0;
    width: 100%;
    height: 100%;
    z-index: 5;
    pointer-events: none;
  }
  .crt-scanlines {
    position: absolute;
    left: 0;
    top: 0;
    width: 100%;
    height: 100%;
    background: linear-gradient(rgba(18, 16, 16, 0) 50%, rgba(0, 0, 0, 0.3) 50%), linear-gradient(90deg, rgba(255, 0, 0, 0.05), rgba(0, 255, 0, 0.02), rgba(0, 0, 255, 0.05));
    background-size: 100% 4px, 6px 100%;
    z-index: 20;
    pointer-events: none;
    display: none;
  }
  .text-overlay {
    position: absolute;
    left: {textX}%;
    top: {textY}%;
    transform: translate(-50%, -50%);
    width: 2000px;
    height: 200px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: {fontColor};
    font-size: {fontSize}px;
    font-family: '{fontName}', 'Outfit', sans-serif;
    font-weight: bold;
    z-index: 10;
    pointer-events: none;
    text-shadow: 0 2px 10px rgba(0,0,0,0.8);
    transition: margin 0.05s ease-out;
    white-space: nowrap;
  }
</style>
<script>
  function playTransition(style, fadeIn, hold, fadeOut) {
      var container = document.querySelector('.splash-container');
      var overlay = document.querySelector('.text-overlay');
      var video = document.querySelector('video');
      var canvas = document.querySelector('.particle-canvas');
      var crtScanlines = document.querySelector('.crt-scanlines');
      var ctx = canvas ? canvas.getContext('2d') : null;
      var particles = [];
      var particlesInitialized = false;

      if (!overlay || !container) return;

      // Reset states
      container.style.opacity = '0';
      overlay.style.opacity = '0';
      overlay.style.marginLeft = '0px';
      overlay.style.marginTop = '0px';
      container.style.transform = 'scale(1)';
      overlay.style.transform = 'translate(-50%, -50%) scale(1)';
      overlay.style.textShadow = '0 2px 10px rgba(0,0,0,0.8)';
      overlay.style.visibility = 'visible';
      
      if (crtScanlines) crtScanlines.style.display = (style === 'CRT') ? 'block' : 'none';
      if (canvas && ctx) {
          canvas.width = window.innerWidth > 100 ? window.innerWidth : 1280;
          canvas.height = window.innerHeight > 100 ? window.innerHeight : 720;
          ctx.clearRect(0, 0, canvas.width, canvas.height);
          if (style === 'ParticleSmoke' || style === 'ParticleSand' || style === 'ParticleEmbers' || style === 'ParticleRain' || style === 'ParticleSnow') {
              initParticles();
          }
      }

      function initParticles() {
          var tempCanvas = document.createElement('canvas');
          tempCanvas.width = canvas.width;
          tempCanvas.height = canvas.height;
          var tempCtx = tempCanvas.getContext('2d');
          
          var fontSizePx = {fontSize};
          tempCtx.font = 'bold ' + fontSizePx + 'px ' + ""'{fontName}', 'Outfit', sans-serif"";
          tempCtx.fillStyle = '{fontColor}';
          tempCtx.textAlign = 'center';
          tempCtx.textBaseline = 'middle';
          
          var tx = canvas.width * ({textX} / 100);
          var ty = canvas.height * ({textY} / 100);
          tempCtx.fillText('{text}', tx, ty);
          
          var imgData = tempCtx.getImageData(0, 0, canvas.width, canvas.height);
          var data = imgData.data;
          
          var step = (style === 'ParticleSmoke') ? 5 : 3;
          for (var y = 0; y < canvas.height; y += step) {
              for (var x = 0; x < canvas.width; x += step) {
                  var idx = (y * canvas.width + x) * 4;
                  if (data[idx + 3] > 128) {
                      var targetX = x;
                      var targetY = y;
                      var scatterRadius = 300;
                      var angle = Math.random() * Math.PI * 2;
                      var dist = Math.random() * scatterRadius + 50;
                      var startX = targetX + Math.cos(angle) * dist;
                      var startY = targetY + Math.sin(angle) * dist + 100;
                      
                      particles.push({
                          x: startX,
                          y: startY,
                          tx: targetX,
                          ty: targetY,
                          color: '{fontColor}',
                          size: (style === 'ParticleSmoke') ? Math.random() * 4 + 2 : Math.random() * 2 + 1,
                          alpha: Math.random() * 0.5 + 0.5,
                          wiggleSeed: Math.random() * 100
                      });
                  }
              }
          }
          particlesInitialized = true;
      }
      
      if (video) {
          video.currentTime = 0;
          video.play();
      }

      var start = performance.now();
      var duration = (fadeIn + hold + fadeOut) * 1000;

      function animate(time) {
          var elapsed = time - start;
          var progress = Math.min(elapsed / (fadeIn * 1000), 1);
          
          if (elapsed < fadeIn * 1000) {
              // Fade In Sequence
              var imgOpacity = progress;
              var txtOpacity = progress;

              if (style === 'Rise') {
                  overlay.style.marginTop = (60 * (1 - progress)) + 'px';
              } else if (style === 'Exposure') {
                  imgOpacity = Math.pow(progress, 0.4);
                  var curScale = 1.5 - 0.5 * progress;
                  overlay.style.transform = 'translate(-50%, -50%) scale(' + curScale + ')';
              } else if (style === 'Cinematic') {
                  var curScale = 1.0 + 0.08 * progress;
                  container.style.transform = 'scale(' + curScale + ')';
              } else if (style === 'Glitch') {
                  if (Math.random() < 0.15) {
                      txtOpacity = Math.random() * 0.5 + 0.2;
                      overlay.style.marginLeft = (Math.random() * 20 - 10) + 'px';
                      overlay.style.marginTop = (Math.random() * 10 - 5) + 'px';
                  } else {
                      overlay.style.marginLeft = '0px';
                      overlay.style.marginTop = '0px';
                  }
              } else if (style === 'CRT') {
                  if (Math.random() < 0.1) {
                      overlay.style.marginTop = (Math.random() * 8 - 4) + 'px';
                  } else {
                      overlay.style.marginTop = '0px';
                  }
              } else if (style === 'RGBSplit') {
                  var redOffset = 18 * Math.sin(progress * Math.PI * 4);
                  overlay.style.textShadow = redOffset + 'px 0px 0px rgba(255,0,0,0.8), ' + (-redOffset) + 'px 0px 0px rgba(0,255,255,0.8)';
                  if (Math.random() < 0.2) {
                      overlay.style.marginLeft = (Math.random() * 16 - 8) + 'px';
                      overlay.style.marginTop = (Math.random() * 8 - 4) + 'px';
                      txtOpacity = Math.random() * 0.4 + 0.6;
                  } else {
                      overlay.style.marginLeft = '0px';
                      overlay.style.marginTop = '0px';
                  }
              } else if (style === 'SoundReactive') {
                  var pulse = 1.0 + 0.05 * Math.sin(progress * Math.PI * 6);
                  container.style.transform = 'scale(' + pulse + ')';
                  overlay.style.transform = 'translate(-50%, -50%) scale(' + pulse + ')';
              } else if (style === 'ParticleSmoke' || style === 'ParticleSand' || style === 'ParticleEmbers' || style === 'ParticleRain' || style === 'ParticleSnow') {
                  txtOpacity = progress > 0.7 ? (progress - 0.7) / 0.3 : 0;
              }

              container.style.opacity = imgOpacity;
              overlay.style.opacity = txtOpacity;
          } else if (elapsed < (fadeIn + hold) * 1000) {
              // Hold State
              container.style.opacity = '1';
              overlay.style.opacity = '1';

              var holdProgress = (elapsed - fadeIn * 1000) / (hold * 1000);

              if (style === 'Cinematic') {
                  var curScale = 1.08 + 0.12 * holdProgress;
                  container.style.transform = 'scale(' + curScale + ')';
              } else if (style === 'Glitch') {
                  if (Math.random() < 0.08) {
                      overlay.style.opacity = Math.random() * 0.6 + 0.3;
                      overlay.style.marginLeft = (Math.random() * 30 - 15) + 'px';
                      overlay.style.marginTop = (Math.random() * 16 - 8) + 'px';
                  } else {
                      overlay.style.marginLeft = '0px';
                      overlay.style.marginTop = '0px';
                  }
              } else if (style === 'CRT') {
                  if (Math.random() < 0.08) {
                      overlay.style.marginTop = (Math.random() * 6 - 3) + 'px';
                      overlay.style.opacity = Math.random() * 0.4 + 0.6;
                  } else {
                      overlay.style.marginTop = '0px';
                      overlay.style.opacity = '1';
                  }
              } else if (style === 'RGBSplit') {
                  var redOffset = 14 * Math.sin(holdProgress * Math.PI * 8);
                  overlay.style.textShadow = redOffset + 'px 0px 0px rgba(255,0,0,0.8), ' + (-redOffset) + 'px 0px 0px rgba(0,255,255,0.8)';
                  if (Math.random() < 0.15) {
                      overlay.style.marginLeft = (Math.random() * 18 - 9) + 'px';
                      overlay.style.marginTop = (Math.random() * 10 - 5) + 'px';
                      overlay.style.opacity = Math.random() * 0.3 + 0.7;
                  } else {
                      overlay.style.marginLeft = '0px';
                      overlay.style.marginTop = '0px';
                      overlay.style.opacity = '1';
                  }
              } else if (style === 'SoundReactive') {
                  var pulse = 1.0 + 0.04 * Math.abs(Math.sin(holdProgress * Math.PI * 8));
                  container.style.transform = 'scale(' + pulse + ')';
                  overlay.style.transform = 'translate(-50%, -50%) scale(' + pulse + ')';
              } else {
                  overlay.style.marginLeft = '0px';
                  overlay.style.marginTop = '0px';
                  overlay.style.transform = 'translate(-50%, -50%) scale(1)';
              }
          } else if (elapsed < duration) {
              // Fade Out Sequence
              var outProgress = (elapsed - (fadeIn + hold) * 1000) / (fadeOut * 1000);
              container.style.opacity = (1 - outProgress);
              overlay.style.opacity = (1 - outProgress);

              if (style === 'Cinematic') {
                  var curScale = 1.20 + 0.05 * outProgress;
                  container.style.transform = 'scale(' + curScale + ')';
              } else if (style === 'CRT') {
                  if (Math.random() < 0.1) {
                      overlay.style.marginTop = (Math.random() * 8 - 4) + 'px';
                  }
              } else if (style === 'RGBSplit') {
                  var redOffset = 10 * Math.sin(outProgress * Math.PI * 4) * (1 - outProgress);
                  overlay.style.textShadow = redOffset + 'px 0px 0px rgba(255,0,0,0.8), ' + (-redOffset) + 'px 0px 0px rgba(0,255,255,0.8)';
              } else if (style === 'SoundReactive') {
                  var pulse = 1.0 + 0.05 * Math.sin(outProgress * Math.PI * 4);
                  container.style.transform = 'scale(' + (pulse * (1 - outProgress)) + ')';
                  overlay.style.transform = 'translate(-50%, -50%) scale(' + (pulse * (1 - outProgress)) + ')';
              }
          } else {
              // End State
              container.style.opacity = '1';
              overlay.style.opacity = '1';
              container.style.transform = 'scale(1)';
              overlay.style.transform = 'translate(-50%, -50%) scale(1)';
              overlay.style.textShadow = '0 2px 10px rgba(0,0,0,0.8)';
              overlay.style.marginLeft = '0px';
              overlay.style.marginTop = '0px';
              overlay.style.visibility = 'visible';
              if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
              return;
          }

          // Draw particles
          if (particlesInitialized && ctx) {
              ctx.clearRect(0, 0, canvas.width, canvas.height);
              for (var j = 0; j < particles.length; j++) {
                  var p = particles[j];
                  if (elapsed < fadeIn * 1000) {
                      var progressT = elapsed / (fadeIn * 1000);
                      var curX = p.x + (p.tx - p.x) * progressT;
                      var curY = p.y + (p.ty - p.y) * progressT;
                      ctx.globalAlpha = progressT * p.alpha;
                  } else if (elapsed < (fadeIn + hold) * 1000) {
                      var holdProgress = (elapsed - fadeIn * 1000) / (hold * 1000);
                      var driftProgress = (elapsed - fadeIn * 1000) / 1000;
                      var curX = p.tx + Math.sin(driftProgress + p.wiggleSeed) * 2;
                      var curY = p.ty + Math.cos(driftProgress + p.wiggleSeed) * 1.5;
                      ctx.globalAlpha = (1 - holdProgress) * p.alpha;
                  } else {
                      var outT = (elapsed - (fadeIn + hold) * 1000) / (fadeOut * 1000);
                      var speed = (style === 'ParticleSmoke') ? 3 : 5;
                      var curX = p.tx + (Math.sin(outT * 5 + p.wiggleSeed) * 40 * outT);
                      var curY = p.ty - (speed * 80 * outT);
                      ctx.globalAlpha = 0;
                  }
                  
                  ctx.beginPath();
                  if (style === 'ParticleSmoke') {
                      var grad = ctx.createRadialGradient(curX, curY, 0, curX, curY, p.size * 2);
                      grad.addColorStop(0, p.color);
                      grad.addColorStop(1, 'rgba(0,0,0,0)');
                      ctx.fillStyle = grad;
                      ctx.arc(curX, curY, p.size * 2.5, 0, Math.PI * 2);
                  } else {
                      ctx.fillStyle = p.color;
                      ctx.arc(curX, curY, p.size, 0, Math.PI * 2);
                  }
                  ctx.fill();
              }
          }

          requestAnimationFrame(animate);
      }

      requestAnimationFrame(animate);
  }
</script>
</head>
<body>
  <div class=""splash-container"">
    <video src=""{fileUri}"" autoplay playsinline></video>
    <canvas class=""particle-canvas""></canvas>
    <div class=""crt-scanlines""></div>
    <div class=""text-overlay"">{text}</div>
  </div>
</body>
</html>";

                    htmlContent = htmlContent
                        .Replace("{textX}", textX.ToString(global::System.Globalization.CultureInfo.InvariantCulture))
                        .Replace("{textY}", textY.ToString(global::System.Globalization.CultureInfo.InvariantCulture))
                        .Replace("{fontColor}", fontColor)
                        .Replace("{fontSize}", fontSize.ToString(global::System.Globalization.CultureInfo.InvariantCulture))
                        .Replace("{fontName}", fontName)
                        .Replace("{text}", text)
                        .Replace("{fileUri}", fileUri);

                    File.WriteAllText(tempHtmlPath, htmlContent, Encoding.UTF8);
                    
                    var targetUri = new Uri(tempHtmlPath);
                    var splashContainer = this.FindControl<Border>("SplashPreviewContainer");
                    if (splashContainer != null && SplashPreviewWebView.Parent == null)
                    {
                        splashContainer.Child = SplashPreviewWebView;
                    }
                    if (SplashPreviewWebView.Source != targetUri)
                    {
                        SplashPreviewWebView.Source = targetUri;
                        SplashPreviewWebView.IsVisible = true;
                    }
                    else
                    {
                        // Direct JS insertion for real-time text and video updates
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
                                var video = document.querySelector('video');
                                if (video && video.getAttribute('src') !== '{fileUri}') {{
                                    video.src = '{fileUri}';
                                    video.currentTime = 0;
                                    video.play();
                                }}
                            }})();";
                        _ = SplashPreviewWebView.InvokeScript(jsUpdate);
                    }
                }
                else
                {
                    SplashPreviewWebView.IsVisible = false;
                    SplashPreviewWebView.Source = new Uri("about:blank");
                    if (SplashPreviewWebView.Parent is Border splashParent) splashParent.Child = null;
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
            // Ignore navigation/functional keys so they don't trigger/reset/dismiss the autocomplete menu on KeyUp
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Escape)
            {
                return;
            }

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
                    _isUpdatingAutocompleteSource = true;
                    try
                    {
                        if (AutocompleteListBox != null && AutocompleteListBox.Items != null)
                        {
                            int nextIndex = AutocompleteListBox.SelectedIndex + 1;
                            if (nextIndex < AutocompleteListBox.Items.Count)
                                AutocompleteListBox.SelectedIndex = nextIndex;
                            else
                                AutocompleteListBox.SelectedIndex = 0;
                            if (AutocompleteListBox.SelectedItem != null)
                            {
                                AutocompleteListBox.ScrollIntoView(AutocompleteListBox.SelectedItem);
                            }
                        }
                    }
                    finally
                    {
                        _isUpdatingAutocompleteSource = false;
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    _isUpdatingAutocompleteSource = true;
                    try
                    {
                        if (AutocompleteListBox != null && AutocompleteListBox.Items != null)
                        {
                            int prevIndex = AutocompleteListBox.SelectedIndex - 1;
                            if (prevIndex >= 0)
                                AutocompleteListBox.SelectedIndex = prevIndex;
                            else
                                AutocompleteListBox.SelectedIndex = AutocompleteListBox.Items.Count - 1;
                            if (AutocompleteListBox.SelectedItem != null)
                            {
                                AutocompleteListBox.ScrollIntoView(AutocompleteListBox.SelectedItem);
                            }
                        }
                    }
                    finally
                    {
                        _isUpdatingAutocompleteSource = false;
                    }
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

        private async void OnColumnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is MainWindowViewModel vm && vm.CurrentGame != null)
            {
                // Short-circuit if focus moved to the autocomplete popup/listbox
                var focusManager = this.FocusManager;
                if (focusManager?.GetFocusedElement() is Visual focusedVisual && AutocompletePopup?.Child != null)
                {
                    if (AutocompletePopup.Child == focusedVisual || AutocompletePopup.Child.IsVisualAncestorOf(focusedVisual))
                    {
                        return;
                    }
                }

                var textValue = textBox.Text ?? string.Empty;
                var itemsControl = textBox.FindAncestorOfType<ItemsControl>();
                var border = textBox.FindAncestorOfType<Border>();
                if (border?.DataContext is GameVariable v && itemsControl != null && v.Type == "array")
                {
                    // Find visual index by panel layout children
                    int index = -1;
                    var panel = itemsControl.ItemsPanelRoot;
                    if (panel != null)
                    {
                        for (int i = 0; i < panel.Children.Count; i++)
                        {
                            var child = panel.Children[i];
                            if (child == textBox || child.FindDescendantOfType<TextBox>() == textBox)
                            {
                                index = i;
                                break;
                            }
                        }
                    }

                    if (index >= 0 && index < v.Columns.Count)
                    {
                        if (v.Columns[index] != textValue)
                        {
                            // Capture the index of the newly focused textbox in the columns collection before rebuilding
                            int focusedColumnIndex = -1;
                            if (focusManager?.GetFocusedElement() is TextBox focusedTextBox)
                            {
                                // Check if the focused textbox is also a column header textbox under the same ItemsControl
                                var focusedItemsControl = focusedTextBox.FindAncestorOfType<ItemsControl>();
                                if (focusedItemsControl == itemsControl)
                                {
                                    var focusedPanel = itemsControl.ItemsPanelRoot;
                                    if (focusedPanel != null)
                                    {
                                        for (int i = 0; i < focusedPanel.Children.Count; i++)
                                        {
                                            var child = focusedPanel.Children[i];
                                            if (child == focusedTextBox || child.FindDescendantOfType<TextBox>() == focusedTextBox)
                                            {
                                                focusedColumnIndex = i;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            v.Columns[index] = textValue;
                            
                            // Rebuild collection to force dynamic items controls / headers to re-render immediately
                            var copy = new System.Collections.Generic.List<string>(v.Columns);
                            v.Columns.Clear();
                            foreach (var c in copy)
                            {
                                v.Columns.Add(c);
                            }

                            // Restore focus asynchronously on the next layout pass
                            if (focusedColumnIndex >= 0)
                            {
                                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    var updatedPanel = itemsControl.ItemsPanelRoot;
                                    if (updatedPanel != null && focusedColumnIndex < updatedPanel.Children.Count)
                                    {
                                        var targetChild = updatedPanel.Children[focusedColumnIndex];
                                        var targetTextBox = targetChild as TextBox ?? targetChild.FindDescendantOfType<TextBox>();
                                        targetTextBox?.Focus();
                                    }
                                });
                            }
                            
                            try
                            {
                                await vm.SaveGameAsync();
                            }
                            catch (IOException)
                            {
                                // Yield slightly and try again to handle concurrent file-writes gracefully
                                await Task.Delay(200);
                                try { await vm.SaveGameAsync(); } catch {}
                            }
                        }
                    }
                }
            }
        }

        private async void OnCellTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is MainWindowViewModel vm && vm.CurrentGame != null)
            {
                // Short-circuit if focus moved to the autocomplete popup/listbox
                var focusManager = this.FocusManager;
                if (focusManager?.GetFocusedElement() is Visual focusedVisual && AutocompletePopup?.Child != null)
                {
                    if (AutocompletePopup.Child == focusedVisual || AutocompletePopup.Child.IsVisualAncestorOf(focusedVisual))
                    {
                        return;
                    }
                }

                var textValue = textBox.Text ?? string.Empty;
                var cellItemsControl = textBox.FindAncestorOfType<ItemsControl>();
                if (cellItemsControl?.DataContext is ObservableCollection<string> row)
                {
                    int index = -1;
                    var panel = cellItemsControl.ItemsPanelRoot;
                    if (panel != null)
                    {
                        for (int i = 0; i < panel.Children.Count; i++)
                        {
                            var child = panel.Children[i];
                            if (child == textBox || child.FindDescendantOfType<TextBox>() == textBox)
                            {
                                index = i;
                                break;
                            }
                        }
                    }

                    if (index >= 0 && index < row.Count)
                    {
                        if (row[index] != textValue)
                        {
                            row[index] = textValue;
                            try
                            {
                                await vm.SaveGameAsync();
                            }
                            catch (IOException)
                            {
                                await Task.Delay(200);
                                try { await vm.SaveGameAsync(); } catch {}
                            }
                        }
                    }
                }
            }
        }

        private async void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.SaveGameAsync();
            }
        }

        private bool _isUpdatingAutocompleteSource = false;
        private void OnAutocompleteSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAutocompleteSource) return;
            if (AutocompleteListBox != null && AutocompleteListBox.SelectedItem != null)
            {
                // Defer applying selection to ensure mouse click event routing finishes cleanly
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ApplySelectedAutocomplete(delayClose: true);
                });
            }
        }

        private void ApplySelectedAutocomplete(bool delayClose = false)
        {
            if (_activeTextBox == null || AutocompleteListBox == null)
            {
                HideAutocomplete();
                return;
            }

            AutocompleteItem? selectedItem = AutocompleteListBox.SelectedItem as AutocompleteItem;
            if (selectedItem == null && AutocompleteListBox.Items != null && AutocompleteListBox.Items.Count > 0)
            {
                selectedItem = AutocompleteListBox.Items[0] as AutocompleteItem;
            }

            if (selectedItem == null)
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
                
                string completedText = before + replacement + after;
                _activeTextBox.Text = completedText;
                _activeTextBox.CaretIndex = _triggerIndex + replacement.Length;

                PropagateTextBoxValue(_activeTextBox, completedText);
            }

            if (delayClose)
            {
                var popupToClose = AutocompletePopup;
                if (popupToClose != null)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(250);
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            popupToClose.IsOpen = false;
                        });
                    });
                }
                _activeTextBox = null;
                _triggerIndex = -1;
            }
            else
            {
                HideAutocomplete();
            }
        }

        private async void PropagateTextBoxValue(TextBox textBox, string completedText)
        {
            if (DataContext is not MainWindowViewModel vm || vm.CurrentGame == null) return;

            // 1. Check if it's a cell textbox in the row data section
            var cellItemsControl = textBox.FindAncestorOfType<ItemsControl>();
            if (cellItemsControl?.DataContext is ObservableCollection<string> row)
            {
                var itemDataContext = textBox.DataContext as string;
                if (itemDataContext != null)
                {
                    int index = -1;
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (ReferenceEquals(row[i], itemDataContext))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0 && index < row.Count)
                    {
                        if (row[index] != completedText)
                        {
                            row[index] = completedText;
                            try { await vm.SaveGameAsync(); } catch {}
                        }
                    }
                }
                return;
            }

            // 2. Check if it's a column textbox in the column headers section
            var border = textBox.FindAncestorOfType<Border>();
            if (border?.DataContext is GameVariable v && cellItemsControl != null && v.Type == "array")
            {
                int index = -1;
                var panel = cellItemsControl.ItemsPanelRoot;
                if (panel != null)
                {
                    for (int i = 0; i < panel.Children.Count; i++)
                    {
                        var child = panel.Children[i];
                        if (child == textBox || child.FindDescendantOfType<TextBox>() == textBox)
                        {
                            index = i;
                            break;
                        }
                    }
                }

                if (index >= 0 && index < v.Columns.Count)
                {
                    if (v.Columns[index] != completedText)
                    {
                        v.Columns[index] = completedText;
                        var copy = new System.Collections.Generic.List<string>(v.Columns);
                        v.Columns.Clear();
                        foreach (var c in copy)
                        {
                            v.Columns.Add(c);
                        }
                        try { await vm.SaveGameAsync(); } catch {}
                    }
                }
                return;
            }
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
            var vm = DataContext as MainWindowViewModel;
            string composeLoopSource = vm?.ComposeLoopSource ?? "";
            string composeLoopArrayVar = vm?.ComposeLoopArrayVar ?? "";
            
            if (game != null)
            {
                if (trigger == '{')
                {
                    // Add local / context properties
                    list.Add(new AutocompleteItem { Token = "this.Id", DisplayToken = "{this.Id}", TypeName = "Current Object Property", Description = "Unique ID of this object." });
                    list.Add(new AutocompleteItem { Token = "this.Name", DisplayToken = "{this.Name}", TypeName = "Current Object Property", Description = "Name of this object." });
                    list.Add(new AutocompleteItem { Token = "this.Description", DisplayToken = "{this.Description}", TypeName = "Current Object Property", Description = "Description of this object." });
                    list.Add(new AutocompleteItem { Token = "this.portrait", DisplayToken = "{this.portrait}", TypeName = "Current Object Property", Description = "Portrait or image path." });
                    list.Add(new AutocompleteItem { Token = "room.Id", DisplayToken = "{room.Id}", TypeName = "Current Room Property", Description = "Unique ID of the current room." });
                    list.Add(new AutocompleteItem { Token = "player.currentroom", DisplayToken = "{player.currentroom}", TypeName = "Player Property", Description = "ID of the room the player is currently in." });

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
                            list.Add(new AutocompleteItem { Token = $"characters.{nameClean}.id", DisplayToken = $"{{characters.{nameClean}.id}}", TypeName = "Character Property", Description = $"Unique ID of character '{c.Name}'." });
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
                            list.Add(new AutocompleteItem { Token = $"objects.{nameClean}.id", DisplayToken = $"{{objects.{nameClean}.id}}", TypeName = "Object Property", Description = $"Unique ID of object '{o.Name}'." });
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
                            list.Add(new AutocompleteItem { Token = $"rooms.{nameClean}.id", DisplayToken = $"{{rooms.{nameClean}.id}}", TypeName = "Room Property", Description = $"Unique ID of room '{r.Name}'." });
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
                            if (v.Type == "datetime")
                            {
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}", 
                                    DisplayToken = $"{{variables.{v.Name}}}", 
                                    TypeName = "Datetime (Default)", 
                                    Description = "Friendly: October 31, 2026 8:00 AM" 
                                });
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}:date", 
                                    DisplayToken = $"{{variables.{v.Name}:date}}", 
                                    TypeName = "Datetime Date-only", 
                                    Description = "Displays date portion: 2026-10-31" 
                                });
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}:time", 
                                    DisplayToken = $"{{variables.{v.Name}:time}}", 
                                    TypeName = "Datetime Time-only", 
                                    Description = "Displays time portion: 08:00:00" 
                                });
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}:datetime", 
                                    DisplayToken = $"{{variables.{v.Name}:datetime}}", 
                                    TypeName = "Datetime Raw ISO-8601", 
                                    Description = "Raw value: 2026-10-31T08:00:00" 
                                });
                            }
                            else if (v.Type == "array")
                            {
                                int colCount = v.Columns != null ? v.Columns.Count : 0;
                                int rowCount = v.Rows != null ? v.Rows.Count : 0;
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}", 
                                    DisplayToken = $"{{variables.{v.Name}}}", 
                                    TypeName = "Array Variable", 
                                    Description = $"Multi-Dimensional Array: {colCount} columns, {rowCount} rows." 
                                });
                                if (v.Columns != null)
                                {
                                    if (!string.IsNullOrEmpty(composeLoopSource) && string.Equals(composeLoopSource, "Variable", StringComparison.OrdinalIgnoreCase) && string.Equals(composeLoopArrayVar, v.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foreach (var col in v.Columns)
                                        {
                                            list.Add(new AutocompleteItem { 
                                                Token = $"loop.{col}", 
                                                DisplayToken = $"{{loop.{col}}}", 
                                                TypeName = $"Loop Variable ({v.Name})", 
                                                Description = $"Value of column '{col}' for current iteration of '{v.Name}'." 
                                            });
                                        }
                                    }

                                    foreach (var col in v.Columns)
                                    {
                                        // Placeholder templates
                                        list.Add(new AutocompleteItem { 
                                            Token = $"variables.{v.Name}.{col}.<row_index>", 
                                            DisplayToken = $"{{variables.{v.Name}.{col}.<row_index>}}", 
                                            TypeName = "Array Template (Col-First)", 
                                            Description = $"Access column '{col}' for any row index." 
                                        });
                                        list.Add(new AutocompleteItem { 
                                            Token = $"variables.{v.Name}.<row_index>.{col}", 
                                            DisplayToken = $"{{variables.{v.Name}.<row_index>.{col}}}", 
                                            TypeName = "Array Template (Row-First)", 
                                            Description = $"Access column '{col}' for any row index." 
                                        });
                                    }

                                    if (v.Rows != null && v.Rows.Count <= 10)
                                    {
                                        for (int r = 0; r < v.Rows.Count; r++)
                                        {
                                            foreach (var col in v.Columns)
                                            {
                                                list.Add(new AutocompleteItem { 
                                                    Token = $"variables.{v.Name}.{col}.{r}", 
                                                    DisplayToken = $"{{variables.{v.Name}.{col}.{r}}}", 
                                                    TypeName = "Array Cell (Col-First)", 
                                                    Description = $"Value of column '{col}' at row {r} in '{v.Name}'." 
                                                });
                                                list.Add(new AutocompleteItem { 
                                                    Token = $"variables.{v.Name}.{r}.{col}", 
                                                    DisplayToken = $"{{variables.{v.Name}.{r}.{col}}}", 
                                                    TypeName = "Array Cell (Row-First)", 
                                                    Description = $"Value of column '{col}' at row {r} in '{v.Name}'." 
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                list.Add(new AutocompleteItem { 
                                    Token = $"variables.{v.Name}", 
                                    DisplayToken = $"{{variables.{v.Name}}}", 
                                    TypeName = "Player Variable", 
                                    Description = $"Value: {v.Value}" 
                                });
                            }
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

                    // Add general For Each Loop properties to autocomplete in Compose window (only if inside an Item/Character loop)
                    if (!string.IsNullOrEmpty(composeLoopSource))
                    {
                        if (string.Equals(composeLoopSource, "Items", StringComparison.OrdinalIgnoreCase) || 
                            string.Equals(composeLoopSource, "Characters", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(new AutocompleteItem { Token = "loop.Id", DisplayToken = "{loop.Id}", TypeName = "Loop Property", Description = "Unique ID of the current loop iteration item." });
                            list.Add(new AutocompleteItem { Token = "loop.Name", DisplayToken = "{loop.Name}", TypeName = "Loop Property", Description = "Name of the current loop iteration item." });
                            list.Add(new AutocompleteItem { Token = "loop.Description", DisplayToken = "{loop.Description}", TypeName = "Loop Property", Description = "Description of the current loop iteration item." });
                            
                            if (string.Equals(composeLoopSource, "Items", StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(new AutocompleteItem { Token = "loop.IsWorn", DisplayToken = "{loop.IsWorn}", TypeName = "Loop Property (Item)", Description = "Whether the current loop item is worn." });
                                list.Add(new AutocompleteItem { Token = "loop.IsContainer", DisplayToken = "{loop.IsContainer}", TypeName = "Loop Property (Item)", Description = "Whether the current loop item is a container." });
                                list.Add(new AutocompleteItem { Token = "loop.IsCollectible", DisplayToken = "{loop.IsCollectible}", TypeName = "Loop Property (Item)", Description = "Whether the current loop item is collectible." });
                            }

                            foreach (var a in uniqueAttrNames)
                            {
                                list.Add(new AutocompleteItem { Token = $"loop.attributes.{a}", DisplayToken = $"{{loop.attributes.{a}}}", TypeName = "Loop Custom Attribute", Description = $"Custom attribute '{a}' of the current loop iteration item." });
                                list.Add(new AutocompleteItem { Token = $"loop.{a}", DisplayToken = $"{{loop.{a}}}", TypeName = "Loop Custom Attribute (Direct)", Description = $"Direct access to custom attribute '{a}' of the current loop iteration item." });
                            }
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

            if (trigger == '{')
            {
                filtered = filtered
                    .OrderBy(item => {
                        if (item.Token.StartsWith("this.", StringComparison.OrdinalIgnoreCase)) return 0;
                        if (item.Token.StartsWith("player.", StringComparison.OrdinalIgnoreCase)) return 1;
                        if (item.Token.StartsWith("room.", StringComparison.OrdinalIgnoreCase)) return 2;
                        if (item.Token.StartsWith("focus.", StringComparison.OrdinalIgnoreCase)) return 3;
                        return 4;
                    })
                    .ToList();
            }

            if (filtered.Count == 0)
            {
                HideAutocomplete();
                return;
            }

            _isUpdatingAutocompleteSource = true;
            try
            {
                AutocompleteListBox.ItemsSource = filtered;
                AutocompleteListBox.SelectedIndex = -1;
            }
            finally
            {
                _isUpdatingAutocompleteSource = false;
            }

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

        private void UpdateWebViewsAirspace(bool overlayOpen)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // 1. CanvasWebView
            var canvasContainer = this.FindControl<Border>("CanvasWebViewContainer");
            if (CanvasWebView != null && canvasContainer != null)
            {
                bool shouldBeVisible = vm.IsVisualEditing && !overlayOpen;
                if (shouldBeVisible)
                {
                    if (CanvasWebView.Parent == null)
                    {
                        canvasContainer.Child = CanvasWebView;
                    }
                    CanvasWebView.IsVisible = true;
                    if (!_isWebViewLoaded)
                    {
                        EnsureWebViewLoaded();
                    }
                }
                else
                {
                    CanvasWebView.IsVisible = false;
                }
            }

            if (overlayOpen)
            {
                if (PreviewWebView != null)
                {
                    PreviewWebView.IsVisible = false;
                    if (PreviewWebView.Parent is Grid previewParent) previewParent.Children.Remove(PreviewWebView);
                }
                if (TabPreviewWebView != null)
                {
                    TabPreviewWebView.IsVisible = false;
                    if (TabPreviewWebView.Parent is Grid tabParent) tabParent.Children.Remove(TabPreviewWebView);
                }
                if (SplashPreviewWebView != null)
                {
                    SplashPreviewWebView.IsVisible = false;
                    if (SplashPreviewWebView.Parent is Border splashParent) splashParent.Child = null;
                }

                // Attach ComposePreviewWebView
                var composeContainer = this.FindControl<Border>("ComposePreviewWebViewContainer");
                if (ComposePreviewWebView != null && composeContainer != null)
                {
                    if (ComposePreviewWebView.Parent == null)
                    {
                        composeContainer.Child = ComposePreviewWebView;
                    }
                    ComposePreviewWebView.IsVisible = true;
                }
            }
            else
            {
                // Hide ComposePreviewWebView
                if (ComposePreviewWebView != null)
                {
                    ComposePreviewWebView.IsVisible = false;
                    if (ComposePreviewWebView.Parent is Border composeParent)
                    {
                        composeParent.Child = null;
                    }
                }

                // If we are visual editing, hide other preview webviews to avoid any airspace overlap!
                if (vm.IsVisualEditing)
                {
                    if (PreviewWebView != null)
                    {
                        PreviewWebView.IsVisible = false;
                        PreviewWebView.Source = new Uri("about:blank");
                        if (PreviewWebView.Parent is Grid previewParent) previewParent.Children.Remove(PreviewWebView);
                    }
                    if (TabPreviewWebView != null)
                    {
                        TabPreviewWebView.IsVisible = false;
                        TabPreviewWebView.Source = new Uri("about:blank");
                        if (TabPreviewWebView.Parent is Grid tabParent) tabParent.Children.Remove(TabPreviewWebView);
                    }
                    if (SplashPreviewWebView != null)
                    {
                        SplashPreviewWebView.IsVisible = false;
                        SplashPreviewWebView.Source = new Uri("about:blank");
                        if (SplashPreviewWebView.Parent is Border splashParent) splashParent.Child = null;
                    }
                }
                else
                {
                    if (vm.Media != null)
                    {
                        UpdateMediaPreview(vm.Media);
                    }
                    UpdateSplashVideoPreview(vm);
                }
            }
        }

        private void TriggerCompose(string nodeId, string fieldName, string currentText, string loopSource, string loopArrayVar)
        {
            _composeSelectionStart = -1;
            _composeSelectionEnd = -1;
            if (DataContext is not MainWindowViewModel vm) return;
            vm.ComposeNodeId = nodeId;
            vm.ComposeFieldName = fieldName;
            vm.ComposeText = currentText;
            vm.ComposeLoopSource = loopSource;
            vm.ComposeLoopArrayVar = loopArrayVar;
            vm.ComposeTitle = $"Compose {fieldName} - Node {nodeId}";
            vm.ComposeTarget = null;
            vm.ShowComposeOverlay = true;
            UpdateComposePreview(currentText);
        }

        private void UpdateComposePreview(string text)
        {
            var webview = this.FindControl<global::Avalonia.Controls.NativeWebView>("ComposePreviewWebView");
            if (webview == null) return;

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            background-color: #0C0C14;
            color: #E2E2EC;
            font-family: 'Inter', 'Roboto', 'Segoe UI', sans-serif;
            font-size: 15px;
            line-height: 1.6;
            margin: 0;
            padding: 0;
            word-wrap: break-word;
        }}
        strong {{ color: #ffffff; }}
    </style>
</head>
<body>
    {RenderRichTextHtml(text)}
</body>
</html>";

            try
            {
                webview.Source = new Uri("data:text/html;charset=utf-8," + Uri.EscapeDataString(html));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update compose preview: {ex.Message}");
            }
        }

        private string RenderRichTextHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "<span style=\"color: #6B7280; font-style: italic;\">No preview text...</span>";
            
            var html = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

            html = html
                .Replace("&lt;b&gt;", "<strong>")
                .Replace("&lt;/b&gt;", "</strong>")
                .Replace("&lt;i&gt;", "<em>")
                .Replace("&lt;/i&gt;", "</em>")
                .Replace("&lt;u&gt;", "<u>")
                .Replace("&lt;/u&gt;", "</u>");

            html = System.Text.RegularExpressions.Regex.Replace(html, @"&lt;color=(#[a-f0-9]{6,8})&gt;(.*?)&lt;/color&gt;", m => {
                var color = m.Groups[1].Value;
                if (color.Length == 9) // #AARRGGBB
                {
                    color = "#" + color.Substring(3) + color.Substring(1, 2);
                }
                return $"<span style=\"color: {color};\">{m.Groups[2].Value}</span>";
            }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"&lt;mark=(#[a-f0-9]{6,8})&gt;(.*?)&lt;/mark&gt;", m => {
                var color = m.Groups[1].Value;
                if (color.Length == 9) // #AARRGGBB
                {
                    color = "#" + color.Substring(3) + color.Substring(1, 2);
                }
                return $"<span style=\"background-color: {color}; padding: 2px 4px; border-radius: 4px;\">{m.Groups[2].Value}</span>";
            }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            html = html.Replace("\n", "<br>");
            return html;
        }

        private async Task<string> BuildAiGraphSystemPromptAsync()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var commandsJson = "";
            var conditionsJson = "";

            try
            {
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
            catch {}

            var sb = new StringBuilder();
            sb.AppendLine("You are an expert C# / Javascript node graph script compiler for the RagNext Game Engine.");
            sb.AppendLine("You must read the user's natural language request and output a valid JSON array of Action Nodes.");
            sb.AppendLine("Output ONLY the raw JSON array. Do not include markdown blocks, introductory conversational remarks, explanations, or quotes. Start with [ and end with ].");
            sb.AppendLine();
            sb.AppendLine("Available Commands (output \"$type\" exactly as shown in parentheses):");

            if (!string.IsNullOrEmpty(commandsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(commandsJson);
                    if (doc.RootElement.TryGetProperty("commands", out var cmds) && cmds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cmd in cmds.EnumerateArray())
                        {
                            var name = cmd.GetProperty("name").GetString() ?? "";
                            var category = cmd.GetProperty("category").GetString() ?? "";
                            var type = GetTypeDiscriminator(name, category, true);
                            sb.AppendLine($"- {name} (type identifier: \"{type}\")");
                            if (cmd.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Array)
                            {
                                sb.AppendLine("  Parameters:");
                                foreach (var input in inputs.EnumerateArray())
                                {
                                    var label = input.GetProperty("label").GetString() ?? "";
                                    var dataType = input.GetProperty("dataType").GetString() ?? "";
                                    var controlType = input.GetProperty("controlType").GetString() ?? "";
                                    var propName = GetCsharpPropertyAlias(label);
                                    sb.AppendLine($"    * \"{propName}\": data type: {dataType} (rendered in UI as {controlType} labeled '{label}')");
                                }
                            }
                        }
                    }
                }
                catch {}
            }

            sb.AppendLine();
            sb.AppendLine("Available Conditions (output \"$type\" exactly as shown in parentheses):");
            if (!string.IsNullOrEmpty(conditionsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(conditionsJson);
                    if (doc.RootElement.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cond in conds.EnumerateArray())
                        {
                            var name = cond.GetProperty("name").GetString() ?? "";
                            var category = cond.GetProperty("category").GetString() ?? "";
                            var type = GetTypeDiscriminator(name, category, false);
                            sb.AppendLine($"- {name} (type identifier: \"{type}\")");
                            sb.AppendLine("  Like commands, conditions can have inputs. Conditions also support branch outputs:");
                            sb.AppendLine("    * \"trueBranch\": Array of connected Command/Condition nodes executed if condition passes.");
                            sb.AppendLine("    * \"falseBranch\": Array of connected Command/Condition nodes executed if condition fails.");
                            if (cond.TryGetProperty("inputs", out var inputs) && inputs.ValueKind == JsonValueKind.Array)
                            {
                                sb.AppendLine("  Parameters:");
                                foreach (var input in inputs.EnumerateArray())
                                {
                                    var label = input.GetProperty("label").GetString() ?? "";
                                    var dataType = input.GetProperty("dataType").GetString() ?? "";
                                    var controlType = input.GetProperty("controlType").GetString() ?? "";
                                    var propName = GetCsharpPropertyAlias(label);
                                    sb.AppendLine($"    * \"{propName}\": data type: {dataType} (rendered in UI as {controlType} labeled '{label}')");
                                }
                            }
                        }
                    }
                }
                catch {}
            }

            sb.AppendLine();
            sb.AppendLine("Grammar rules:");
            sb.AppendLine("1. All nodes must have \"$type\" (matching the identifier specified in parentheses above).");
            sb.AppendLine("2. Dialogue nodes are represented by \"$type\": \"general.startDialogue\" and support:");
            sb.AppendLine("   * \"dialogueId\": Unique string / GUID identifier.");
            sb.AppendLine("   * \"characterLines\": Dialogue speech text.");
            sb.AppendLine("   * \"choices\": Array of choices matching: { \"text\": \"Choice Text\", \"destinationNodeId\": \"...\", \"commands\": [ ... ] }");
            sb.AppendLine("3. If variables, items, or exits are checked/modified, match properties exactly. If you generate IDs, use realistic IDs or GUIDs.");
            sb.AppendLine("4. Coordinates (\"x\", \"y\") must be assigned sequentially (e.g. increment x by 360 for sequential nodes) so nodes lay out cleanly.");
            sb.AppendLine("5. Distinction between displaying media vs setting properties:");
            sb.AppendLine("   * To show, render, draw, or display any image, picture, photo, or portrait media directly on the screen for the player to see, use \"media.displayMultimedia\" (Display Multimedia) and set the parameter \"mediaId\" to the media asset's GUID.");
            sb.AppendLine("   * To configure, update, assign, or change the active/default portrait image of the player or a character behind the scenes (without drawing it as screen multimedia), use \"player.setPortraitMedia\" or \"char.setPortraitMedia\" respectively.");
            sb.AppendLine("6. Using generic contextual placeholders:");
            sb.AppendLine("   * You can use parameter placeholders wrapped in curly braces like \"{this.id}\" or \"{this.Name}\" to represent the ID or Name of the object/character executing the action.");
            sb.AppendLine("   * E.g. to check if the item running the action is held, you can use \"{this.id}\" as the item parameter.");
            sb.AppendLine("7. References to variables in display text templates must use the format {variables.VariableName}. Do NOT use {this.variables.VariableName} or {variables.variables.VariableName} or {this.value}.");
            sb.AppendLine("8. To modify, increment, or decrement variables, use the specific type identifier commands \"var.set\" (Variable: Set), \"var.inc\" (Variable: Increment), or \"var.dec\" (Variable: Decrement). Do NOT use \"general.command\".");
            sb.AppendLine("9. When incrementing or decrementing variables (commands \"var.inc\" and \"var.dec\"):");
            sb.AppendLine("   * If the variable is numeric, set the \"value\" parameter to a standard numeric string (e.g. \"10\", \"-5\").");
            sb.AppendLine("   * If the variable is a date & time (datetime), the value MUST include a unit of time to indicate what is being changed (e.g., \"10 seconds\", \"5 minutes\", \"2 hours\", \"1 day\", \"-30 seconds\"). Do NOT output just a raw number for date/time increments unless you specifically want minutes, which is the fallback unit.");
            sb.AppendLine("10. Loop nodes are represented by \"$type\": \"variable.forEachLoop\" and support:");
            sb.AppendLine("    * \"ArrayVariableName\": String name of the array variable to loop through.");
            sb.AppendLine("    * \"trueBranch\": Array of connected Command/Condition nodes to execute in each iteration.");
            sb.AppendLine("11. Use \"$type\": \"variable.breakLoop\" (Break Loop) to exit loops early.");
            sb.AppendLine("12. Switch nodes are represented by \"$type\": \"general.switch\" and support:");
            sb.AppendLine("    * \"Expression\": The variable or expression string to switch on.");
            sb.AppendLine("    * \"Cases\": Dictionary of string keys mapped to arrays of connected Command/Condition nodes.");
            sb.AppendLine("    * \"DefaultBranch\": Array of connected Command/Condition nodes executed if no cases match.");


            var vm = DataContext as MainWindowViewModel;
            if (vm?.CurrentGame != null)
            {
                sb.AppendLine();
                sb.AppendLine("Active Project Entities Context (If matching these in commands/conditions parameters, output their GUID Id EXACTLY as shown):");
                if (vm.CurrentGame.Rooms?.Count > 0)
                {
                    sb.AppendLine("Rooms:");
                    foreach (var r in vm.CurrentGame.Rooms)
                    {
                        sb.AppendLine($"  - Name: \"{r.Name}\", Id: \"{r.Id}\"");
                    }
                }
                if (vm.CurrentGame.Characters?.Count > 0)
                {
                    sb.AppendLine("Characters:");
                    foreach (var c in vm.CurrentGame.Characters)
                    {
                        sb.AppendLine($"  - Name: \"{c.Name}\", Id: \"{c.Id}\"");
                    }
                }
                if (vm.CurrentGame.Objects?.Count > 0)
                {
                    sb.AppendLine("Items/Objects:");
                    foreach (var o in vm.CurrentGame.Objects)
                    {
                        sb.AppendLine($"  - Name: \"{o.Name}\", Id: \"{o.Id}\"");
                    }
                }
                if (vm.CurrentGame.Variables?.Count > 0)
                {
                    sb.AppendLine("Variables:");
                    foreach (var v in vm.CurrentGame.Variables)
                    {
                        sb.AppendLine($"  - Name: \"{v.Name}\"");
                    }
                }
                if (vm.CurrentGame.MediaAssets?.Count > 0)
                {
                    sb.AppendLine("Media Assets:");
                    foreach (var m in vm.CurrentGame.MediaAssets)
                    {
                        sb.AppendLine($"  - Name: \"{m.Name}\", Id: \"{m.Id}\"");
                    }
                }
            }

            return sb.ToString();
        }

        private string GetTypeDiscriminator(string name, string category, bool isCommand)
        {
            var combined = name;
            var key = combined.Replace(" ", "").Replace(":", "").ToLower();
            
            switch (key)
            {
                case "actionaddcustomchoice": return "general.addCustomChoice";
                case "actionclearcustomchoice": return "general.clearCustomChoice";
                case "actionremovecustomchoice": return "general.removeCustomChoice";
                case "characterdisplaydescription": return "char.displayDescription";
                case "charactermovetoroom": return "char.moveToRoom";
                case "charactermovetorandomadjacentroom": return "char.moveToRandomAdjacent";
                case "charactermovealongpatrolpath": return "char.moveAlongPatrolPath";
                case "charactermoveinventorytoplayer": return "char.moveInventoryToPlayer";
                case "charactermovetoobject": return "char.moveToObject";
                case "charactersetportraitmedia": return "char.setPortraitMedia";
                case "charactersetactiontoactiveinactive": return "char.setActionActive";
                case "charactersetattribute": return "char.setAttribute";
                case "charactersetdescription": return "char.setDescription";
                case "charactersetgender": return "char.setGender";
                case "charactersetdisplayname": return "char.setDisplayName";
                case "addacomment": return "general.addComment";
                case "generalcallfunction": return "general.callFunction";
                case "debugtext": return "general.debugText";
                case "displaytext": return "general.displayText";
                case "mediadisplaylayeredpicture": return "media.displayLayeredPicture";
                case "mediadisplaymultimedia": return "media.displayMultimedia";
                case "mediasetbackgroundmusic": return "media.setBackgroundMusic";
                case "mediastopbackgroundmusic": return "media.stopBackgroundMusic";
                case "mediaplaysoundeffect": return "media.playSound";
                case "mediastopsoundeffect": return "media.stopSound";
                case "mediaplayvideo": return "media.playVideo";
                case "endthegame": return "general.endGame";
                case "itemdisplaydescription": return "object.displayDescription";
                case "itemmovetocharacter": return "object.moveToCharacter";
                case "itemmovetoinventory": return "object.moveToInventory";
                case "itemmoveinsideobject": return "object.moveInsideObject";
                case "itemmovetoroom": return "room.addObject";
                case "itemsetattribute": return "item.setAttribute";
                case "itemwearitem": return "item.wear";
                case "itemremoveitem": return "item.remove";
                case "itemshowinteractivescreen": return "item.showInteractiveScreen";
                case "playerdisplaydescription": return "player.displayDescription";
                case "playermoveinventorytocharacter": return "player.moveInventoryToChar";
                case "playermoveinventorytoroom": return "player.moveInventoryToRoom";
                case "playermovetoroom": return "player.moveTo";
                case "playerscreenshake": return "player.screenShake";
                case "playermovetocharacter": return "player.moveToChar";
                case "playermovetoobject": return "player.moveToObject";
                case "playerswapcharacter": return "player.swapCharacter";
                case "uishowsplashscreen": return "ui.showSplashScreen";
                case "playersetattribute": return "player.setAttribute";
                case "playersetdescription": return "player.setDescription";
                case "playersetname": return "player.setName";
                case "playersetgender": return "player.setGender";
                case "playersetportraitmedia": return "player.setPortraitMedia";
                case "roomdisplaydescription": return "room.displayDescription";
                case "roomdisplaypicture": return "room.displayPicture";
                case "roommoveitemstoplayer": return "room.moveItemsToPlayer";
                case "roomsetdescription": return "room.setDescription";
                case "roomsetpicture": return "room.setPicture";
                case "roomsetattribute": return "room.setAttribute";
                case "roomlockexit": return "room.lockExit";
                case "roomunlockexit": return "room.unlockExit";
                case "statusbarsetvisibleinvisible": return "ui.setStatusBarVisible";
                case "timerexecutetimer": return "timer.executeTimer";
                case "timerresettimer": return "timer.resetTimer";
                case "timerstarttimer": return "timer.startTimer";
                case "timerstoptimer": return "timer.stopTimer";
                case "timersetattribute": return "timer.setAttribute";
                case "variableincrement": return "var.inc";
                case "variabledecrement": return "var.dec";
                case "variableset": return "var.set";
                case "variablesetto": return "var.set";
                case "variablesettovariable": return "var.setToVar";
                case "variablesetnumericrandomly": return "var.setRandom";
                case "variablecomparison": return "var.compare";
                case "variablecompare": return "var.compare";
                case "variableequals": return "var.equals";
                case "variabledatetimepartcomparison": return "date.partCompare";
                case "datetimeispast": return "date.isPast";
                case "datetimeisfuture": return "date.isFuture";
                case "datetimecomparetwovariables": return "date.compareVars";
                case "datetimecomparedifference": return "date.diffCompare";
                case "datetimecompareconstant": return "date.compareConst";
                case "datetimeisvalid": return "date.isValid";
                case "playerinroom": return "player.inRoom";
                case "roomhasobject": return "room.hasObject";
                case "playerinsameroom": return "player.sameRoom";
                case "itemheldbyplayer": return "item.heldByPlayer";
                case "itemheldbycharacter": return "item.heldByChar";
                case "iteminroom": return "item.inRoom";
                case "iteminobject": return "item.inObject";
                case "itemnotheldbyplayer": return "item.notHeldByPlayer";
                case "itemnotinobject": return "item.notInObject";
                case "itemisitemworn": return "item.isWorn";
                case "itemcanitembeworn": return "item.canWear";
                case "isroomexitlocked": return "room.isExitLocked";
                case "charactergender": return "char.gender";
                case "characterinroom": return "char.inRoom";
                case "playergender": return "player.gender";
                case "characterattributecheck": return "char.attributeCheck";
                case "itemattributecheck": return "item.attributeCheck";
                case "playerattributecheck": return "player.attributeCheck";
                case "roomattributecheck": return "room.attributeCheck";
                case "timeractivecheck": return "timer.isActive";
                case "variablebreakloop": return "variable.breakLoop";
                case "variablesetarrayelement": return "variable.setArrayElement";
                case "variableaddarrayrow": return "variable.addArrayRow";
                case "variableremovearrayrow": return "variable.removeArrayRow";
                case "variableappendtext": return "variable.appendText";
                case "variableappendline": return "variable.appendLine";
                case "uisethotspotactivestate": return "ui.setHotspotActive";
                case "waitforcontinue": return "general.waitForContinue";
                case "showstatuselement": return "ui.showStatusElement";
                case "hidestatuselement": return "ui.hideStatusElement";
                case "setstatuselementtext": return "ui.setStatusElementText";
                case "setstatuselementimage": return "ui.setStatusElementImage";
                case "promptplayerinput": return "general.promptInput";
                case "itemopencontainer": return "general.openContainer";
                case "itemclosecontainer": return "general.closeContainer";
                case "timersettimertoactiveinactive": return "timer.setActive";
                case "playersetactiontoactiveinactive": return "player.setActionActive";
                case "roomsetactiontoactiveinactive": return "room.setActionActive";
                case "itemsetactiontoactiveinactive": return "item.setActionActive";
                case "isstatuselementvisible": return "ui.isStatusElementVisible";
                case "foreachloop": return "variable.forEachLoop";
            }
            
            var fallback = isCommand ? "general.command" : "general.condition";
            return fallback;
        }

        private string GetCsharpPropertyAlias(string label)
        {
            switch (label)
            {
                case "Prompt Name": return "PromptName";
                case "Prompt Text": return "PromptText";
                case "Input Type": return "InputType";
                case "Store Variable": return "StoreVariableName";
                case "Choice Text": return "choiceText";
                case "Target Variable": return "targetVariable";
                case "Character": return "characterId";
                case "Room": return "roomId";
                case "Sound Effect": return "mediaAssetId";
                case "Volume": return "volume";
                case "Loop": return "loop";
                case "Text": return "text";
                case "Comment": return "comment";
                case "Function": return "functionId";
                case "Value": return "value";
                case "Media File": return "mediaId";
                case "Image": return "mediaAssetId";
                case "Transition": return "transitionStyle";
                case "Duration": return "duration";
                case "Volume Scale": return "volume";
                case "Object": return "objectId";
                case "Item": return "itemId";
                case "Destination": return "destinationId";
                case "Is Container": return "isContainer";
                case "Is Container Open": return "containerOpen";
                case "Description": return "description";
                case "Gender": return "gender";
                case "Display Name": return "displayName";
                case "Name": return "name";
                case "Exit Direction": return "exitDirection";
                case "Status Bar Visible": return "statusBarVisible";
                case "Timer": return "timerId";
                case "Variable": return "variableName";
                case "Variable A": return "variableNameA";
                case "Variable B": return "variableNameB";
                case "Operator": return "compareOperator";
                case "Expected Value": return "expectedValue";
                case "Min Value": return "minValue";
                case "Max Value": return "maxValue";
                case "Attribute": return "attributeName";
                case "Array Variable": return "ArrayVariableName";
                case "Row Index": return "RowIndex";
                case "Column Name": return "ColumnName";
                case "Comma-separated Values": return "Values";
                case "Hotspot Id or Name": return "HotspotId";
                case "ElementId": return "ElementId";
                case "Custom Options": return "CustomOptions";
                case "Final Message": return "FinalMessage";
                case "Container Object": return "ContainerObjectId";
                case "Action Name": return "ActionName";
                case "Active": return "Active";
            }
            if (string.IsNullOrEmpty(label)) return "value";
            return label.Substring(0, 1).ToLower() + label.Substring(1).Replace(" ", "");
        }

        private async void TriggerGraphAI(string prompt, string replace, string currentGraphBase64)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var useCustom = vm.Preferences.AiNodeAssistantUseCustom;
            var endpoint = useCustom ? vm.Preferences.AiNodeAssistantEndpoint : vm.Preferences.AiCoAuthorEndpoint;
            var apiKey = useCustom ? vm.Preferences.AiNodeAssistantKey : vm.Preferences.AiCoAuthorKey;
            var model = useCustom ? vm.Preferences.AiNodeAssistantModel : vm.Preferences.AiCoAuthorModel;
            var port = useCustom ? vm.Preferences.AiNodeAssistantPort : vm.Preferences.AiCoAuthorPort;
            var provider = useCustom ? vm.Preferences.AiNodeAssistantProvider : vm.Preferences.AiCoAuthorProvider;
            var temperature = useCustom ? vm.Preferences.AiNodeAssistantTemperature : 0.5;
            
            bool apiKeyRequired = string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase);

            if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
            {
                string msgWord = useCustom ? "AI Node Assistant" : "AI Co-Author";
                await ConfirmDialog.ShowAsync(this, "AI Action Assistant", $"Please set your {msgWord} API Key in Preferences / Settings first.");
                try { await CanvasWebView.InvokeScript("if (typeof updateGraphAIResult === 'function') { updateGraphAIResult(btoa('[]')); }"); } catch {}
                return;
            }

            try
            {
                vm.IsAiGenerating = true;
                using var client = new HttpClient();
                string content = null;

                var currentGraphJson = Encoding.UTF8.GetString(Convert.FromBase64String(currentGraphBase64));
                var systemInstructions = await BuildAiGraphSystemPromptAsync();
                var finalPrompt = $"Generate nodes based on this request:\n\"{prompt}\"\n\nCurrent Graph Data context:\n{currentGraphJson}\n\nRemember: Output ONLY valid JSON array containing nodes.";

                if (string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    content = await CallGeminiAsync(client, endpoint, apiKey, model, systemInstructions, finalPrompt, temperature);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var requestBody = new AICoAuthorRequest
                    {
                        model = model,
                        messages = new[]
                        {
                            new AICoAuthorMessage { role = "system", content = systemInstructions },
                            new AICoAuthorMessage { role = "user", content = finalPrompt }
                        },
                        temperature = temperature
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.AICoAuthorRequest);
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
                        content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    // Clean up markdown block fences if present
                    if (content.StartsWith("```json"))
                    {
                        content = content.Substring("```json".Length);
                    }
                    if (content.StartsWith("```"))
                    {
                        content = content.Substring("```".Length);
                    }
                    if (content.EndsWith("```"))
                    {
                        content = content.Substring(0, content.Length - "```".Length);
                    }
                    content = content.Trim();

                    // Robust fallback parser: extract only the text between the first '[' and last ']'
                    int firstBracket = content.IndexOf('[');
                    int lastBracket = content.LastIndexOf(']');
                    if (firstBracket >= 0 && lastBracket > firstBracket)
                    {
                        content = content.Substring(firstBracket, lastBracket - firstBracket + 1);
                    }

                    var base64Result = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
                    await CanvasWebView.InvokeScript($"if (typeof updateGraphAIResult === 'function') {{ updateGraphAIResult('{base64Result}'); }}");
                }
                else
                {
                    throw new Exception("AI returned empty result.");
                }
            }
            catch (Exception ex)
            {
                await ConfirmDialog.ShowAsync(this, "AI Assist Error", ex.Message);
                try { await CanvasWebView.InvokeScript("if (typeof updateGraphAIResult === 'function') { updateGraphAIResult(btoa('[]')); }"); } catch {}
            }
            finally
            {
                vm.IsAiGenerating = false;
            }
        }

        private async void TriggerCopyAiPrompt(string prompt, string currentGraphBase64)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            try
            {
                var currentGraphJson = Encoding.UTF8.GetString(Convert.FromBase64String(currentGraphBase64));
                var systemInstructions = await BuildAiGraphSystemPromptAsync();

                var fullPrompt = $"System Instructions:\n{systemInstructions}\n\nUser Request:\n\"{prompt}\"\n\nCurrent Graph Data context:\n{currentGraphJson}\n\nRemember: Output ONLY valid JSON array containing nodes.";

                var clipboard = global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(fullPrompt);
                    await AlertDialog.ShowAsync(this, "AI Prompt Copied", "📋 The complete system instructions, context variables, project entities list, and prompt have been copied to your clipboard!\n\nYou can now paste this directly into Gemini, ChatGPT, Claude, or any web LLM, and paste the resulting JSON array back using the 'External Model Pipeline' section.");
                }
                else
                {
                    await AlertDialog.ShowAsync(this, "Clipboard Error", "Unable to access system clipboard.");
                }
            }
            catch (Exception ex)
            {
                await AlertDialog.ShowAsync(this, "AI Copy Error", ex.Message);
            }
        }

        private TextBox? GetActiveTextBox()
        {
            if (_lastFocusedTextBox != null) return _lastFocusedTextBox;
            
            var names = new[] { "ComposeTextBox", "ComposeTextBox_Status", "PlayerDescriptionTextBox", "RoomDescriptionTextBox", "CharacterDescriptionTextBox", "ObjectDescriptionTextBox" };
            foreach (var name in names)
            {
                var tb = this.FindControl<TextBox>(name);
                if (tb != null && tb.IsFocused) return tb;
            }
            foreach (var name in names)
            {
                var tb = this.FindControl<TextBox>(name);
                if (tb != null && tb.IsVisible) return tb;
            }
            return null;
        }

        private void WrapComposeSelection(string startTag, string endTag)
        {
            var tb = GetActiveTextBox();
            if (tb == null) return;

            int start, end;
            if (_composeSelectionStart >= 0 && _composeSelectionEnd >= 0 && (tb.Name == "ComposeTextBox" || tb.Name == "ComposeTextBox_Status"))
            {
                start = Math.Min(_composeSelectionStart, _composeSelectionEnd);
                end = Math.Max(_composeSelectionStart, _composeSelectionEnd);
            }
            else if (_inlineSelectionStart >= 0 && _inlineSelectionEnd >= 0 && tb.Name != "ComposeTextBox" && tb.Name != "ComposeTextBox_Status")
            {
                start = Math.Min(_inlineSelectionStart, _inlineSelectionEnd);
                end = Math.Max(_inlineSelectionStart, _inlineSelectionEnd);
            }
            else
            {
                start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
            }

            var selectionLength = end - start;
            var text = tb.Text ?? string.Empty;

            if (selectionLength > 0 && start >= 0 && start + selectionLength <= text.Length)
            {
                var before = text.Substring(0, start);
                var selected = text.Substring(start, selectionLength);
                var after = text.Substring(start + selectionLength);

                if (selected.StartsWith(startTag) && selected.EndsWith(endTag))
                {
                    var unwrapped = selected.Substring(startTag.Length, selected.Length - startTag.Length - endTag.Length);
                    tb.Text = before + unwrapped + after;
                    tb.Focus();
                    tb.SelectionStart = start;
                    tb.SelectionEnd = start + unwrapped.Length;
                }
                else if (before.EndsWith(startTag) && after.StartsWith(endTag))
                {
                    var newBefore = before.Substring(0, before.Length - startTag.Length);
                    var newAfter = after.Substring(endTag.Length);
                    tb.Text = newBefore + selected + newAfter;
                    tb.Focus();
                    tb.SelectionStart = newBefore.Length;
                    tb.SelectionEnd = newBefore.Length + selected.Length;
                }
                else
                {
                    tb.Text = before + startTag + selected + endTag + after;
                    tb.Focus();
                    tb.SelectionStart = start;
                    tb.SelectionEnd = start + startTag.Length + selected.Length + endTag.Length;
                }
            }
            else
            {
                var actualCursor = start;
                if (actualCursor < 0 || actualCursor > text.Length) actualCursor = text.Length;

                var before = text.Substring(0, actualCursor);
                var after = text.Substring(actualCursor);

                if (before.EndsWith(startTag) && after.StartsWith(endTag))
                {
                    var newBefore = before.Substring(0, before.Length - startTag.Length);
                    var newAfter = after.Substring(endTag.Length);
                    tb.Text = newBefore + newAfter;
                    tb.Focus();
                    tb.SelectionStart = newBefore.Length;
                    tb.SelectionEnd = newBefore.Length;
                }
                else
                {
                    tb.Text = before + startTag + endTag + after;
                    tb.Focus();
                    tb.SelectionStart = actualCursor + startTag.Length;
                    tb.SelectionEnd = actualCursor + startTag.Length;
                }
            }

            if (tb.Name == "ComposeTextBox" || tb.Name == "ComposeTextBox_Status")
            {
                _composeSelectionStart = tb.SelectionStart;
                _composeSelectionEnd = tb.SelectionEnd;
            }
            else
            {
                _inlineSelectionStart = tb.SelectionStart;
                _inlineSelectionEnd = tb.SelectionEnd;
            }
        }

        private void OnComposeBoldClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            WrapComposeSelection("<b>", "</b>");
        }

        private void OnComposeItalicClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            WrapComposeSelection("<i>", "</i>");
        }

        private void OnComposeUnderlineClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            WrapComposeSelection("<u>", "</u>");
        }

        private void OnColorViewLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is ColorView cv)
            {
                cv.PropertyChanged += (s, args) =>
                {
                    if (args.Property == ColorView.ColorProperty)
                    {
                        if (_isNormalizingHex) return;

                        var tb = FindHexTextBox(cv);
                        if (tb != null)
                        {
                            var text = tb.Text?.Trim();
                            if (string.IsNullOrEmpty(text)) return;

                            if (text == _lastNormalizedHex) return;

                            var hasHash = text.StartsWith("#");
                            var cleanText = hasHash ? text.Substring(1) : text;

                            if (cleanText.Length == 8)
                            {
                                if (uint.TryParse(cleanText, System.Globalization.NumberStyles.HexNumber, null, out var hexVal))
                                {
                                    // Parse as ARGB (what user pasted from rich text tags)
                                    var a = (byte)((hexVal >> 24) & 0xFF);
                                    var r = (byte)((hexVal >> 16) & 0xFF);
                                    var g = (byte)((hexVal >> 8) & 0xFF);
                                    var b = (byte)(hexVal & 0xFF);
                                    var argbColor = Color.FromArgb(a, r, g, b);

                                    if (cv.Color != argbColor)
                                    {
                                        _isNormalizingHex = true;
                                        cv.Color = argbColor;
                                        var prefix = hasHash ? "#" : "";
                                        var normalized = $"{prefix}{r:X2}{g:X2}{b:X2}{a:X2}";
                                        _lastNormalizedHex = normalized;
                                        tb.Text = normalized;
                                        _isNormalizingHex = false;
                                    }
                                }
                            }
                        }
                    }
                };
            }
        }

        private void HookHexTextBox(ColorView cv)
        {
        }

        private TextBox? FindHexTextBox(Visual visual)
        {
            if (visual is TextBox tb && (tb.Name == "PART_HexTextBox" || (tb.Name != null && tb.Name.Contains("Hex", StringComparison.OrdinalIgnoreCase))))
            {
                return tb;
            }
            foreach (var child in visual.GetVisualChildren())
            {
                var result = FindHexTextBox(child);
                if (result != null) return result;
            }
            return null;
        }

        private bool _isNormalizingHex = false;
        private string? _lastNormalizedHex = null;

        private void LogTextBoxes(ColorView cv)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Listing TextBoxes inside ColorView ===");
                void Traverse(Visual visual, int depth)
                {
                    if (visual is TextBox tb)
                    {
                        sb.AppendLine($"{new string(' ', depth * 2)}- TextBox Name: '{tb.Name}', Text: '{tb.Text}', IsFocused: {tb.IsFocused}");
                    }
                    foreach (var child in visual.GetVisualChildren())
                    {
                        Traverse(child, depth + 1);
                    }
                }
                Traverse(cv, 0);
                File.WriteAllText(@"C:\Users\steve\.gemini\antigravity\brain\e208b4c8-1a51-4659-846c-c16f3c4c04e6\scratch\colorview_debug.log", sb.ToString());
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\steve\.gemini\antigravity\brain\e208b4c8-1a51-4659-846c-c16f3c4c04e6\scratch\colorview_error.log", ex.ToString());
            }
        }

        private void OnComposeColorPickerApply(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var picker = this.FindControl<ColorView>("ComposeColorPicker");
            if (picker != null)
            {
                var hex = picker.Color.ToString();
                WrapComposeSelection($"<color={hex}>", "</color>");
                AddToRecentColors(hex);
            }
            CloseParentFlyout(sender);
        }

        private void OnComposeHighlightPickerApply(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var picker = this.FindControl<ColorView>("ComposeHighlightPicker");
            if (picker != null)
            {
                var hex = picker.Color.ToString();
                WrapComposeSelection($"<mark={hex}>", "</mark>");
                AddToRecentColors(hex);
            }
            CloseParentFlyout(sender);
        }

        private void OnInlineColorPickerApply(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Visual visual)
            {
                var hex = GetHexFromVisualParent(visual);
                if (hex != null)
                {
                    WrapComposeSelection($"<color={hex}>", "</color>");
                    AddToRecentColors(hex);
                }
            }
            CloseParentFlyout(sender);
        }

        private void OnInlineHighlightPickerApply(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Visual visual)
            {
                var hex = GetHexFromVisualParent(visual);
                if (hex != null)
                {
                    WrapComposeSelection($"<mark={hex}>", "</mark>");
                    AddToRecentColors(hex);
                }
            }
            CloseParentFlyout(sender);
        }

        private void CloseParentFlyout(object? sender)
        {
            if (sender is Visual visual)
            {
                // Walk up both Visual and Logical trees to find Popup or FlyoutPresenter
                var currentVisual = visual;
                global::Avalonia.Controls.Primitives.Popup? popup = null;
                while (currentVisual != null)
                {
                    if (currentVisual is global::Avalonia.Controls.Primitives.Popup p)
                    {
                        popup = p;
                        break;
                    }
                    if (currentVisual.GetType().Name == "FlyoutPresenter")
                    {
                        // Flyouts are managed via their parent Popup inside VisualRoot
                        popup = currentVisual.FindAncestorOfType<global::Avalonia.Controls.Primitives.Popup>();
                        if (popup != null) break;
                    }
                    currentVisual = currentVisual.GetVisualParent();
                }

                if (popup == null)
                {
                    var currentLogical = visual as global::Avalonia.LogicalTree.ILogical;
                    while (currentLogical != null)
                    {
                        if (currentLogical is global::Avalonia.Controls.Primitives.Popup p)
                        {
                            popup = p;
                            break;
                        }
                        currentLogical = currentLogical.LogicalParent;
                    }
                }

                if (popup != null)
                {
                    popup.IsOpen = false;
                }
                else
                {
                    // Fallback to closing all popups hosted on the window
                    var root = global::Avalonia.Controls.TopLevel.GetTopLevel(visual);
                    if (root != null)
                    {
                        foreach (var pop in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<global::Avalonia.Controls.Primitives.Popup>())
                        {
                            pop.IsOpen = false;
                        }
                    }
                }
            }
        }

        private void OnComposeClearClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var tb = GetActiveTextBox();
            if (tb == null) return;

            int start, end;
            if (_composeSelectionStart >= 0 && _composeSelectionEnd >= 0 && (tb.Name == "ComposeTextBox" || tb.Name == "ComposeTextBox_Status"))
            {
                start = Math.Min(_composeSelectionStart, _composeSelectionEnd);
                end = Math.Max(_composeSelectionStart, _composeSelectionEnd);
            }
            else if (_inlineSelectionStart >= 0 && _inlineSelectionEnd >= 0 && tb.Name != "ComposeTextBox" && tb.Name != "ComposeTextBox_Status")
            {
                start = Math.Min(_inlineSelectionStart, _inlineSelectionEnd);
                end = Math.Max(_inlineSelectionStart, _inlineSelectionEnd);
            }
            else
            {
                start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
            }

            var selectionLength = end - start;
            var text = tb.Text ?? string.Empty;

            if (selectionLength > 0 && start >= 0 && start + selectionLength <= text.Length)
            {
                var before = text.Substring(0, start);
                var selected = text.Substring(start, selectionLength);
                var after = text.Substring(start + selectionLength);

                // Loop to strip enclosing matching formatting tags from before and after
                bool stripped;
                do
                {
                    stripped = false;
                    var openMatch = System.Text.RegularExpressions.Regex.Match(before, @"<([a-zA-Z0-9]+)(=[^>]+)?>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (openMatch.Success)
                    {
                        var tagName = openMatch.Groups[1].Value.ToLower();
                        var closeTag = $"</{tagName}>";
                        if (after.StartsWith(closeTag, StringComparison.OrdinalIgnoreCase))
                        {
                            before = before.Substring(0, before.Length - openMatch.Length);
                            after = after.Substring(closeTag.Length);
                            stripped = true;
                        }
                    }
                } while (stripped);

                var cleaned = System.Text.RegularExpressions.Regex.Replace(selected, @"<[^>]+>", "");
                tb.Text = before + cleaned + after;
                tb.Focus();
                tb.SelectionStart = before.Length;
                tb.SelectionEnd = before.Length + cleaned.Length;
                if (tb.Name == "ComposeTextBox" || tb.Name == "ComposeTextBox_Status")
                {
                    _composeSelectionStart = tb.SelectionStart;
                    _composeSelectionEnd = tb.SelectionEnd;
                }
                else
                {
                    _inlineSelectionStart = tb.SelectionStart;
                    _inlineSelectionEnd = tb.SelectionEnd;
                }
            }
        }

        private async void OnComposeAIClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
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
                await ConfirmDialog.ShowAsync(this, "AI Co-Author", "Please set your AI Co-Author API Key in Preferences / Settings first.");
                return;
            }

            var currentText = vm.ComposeText;
            var prompt = await PromptDialog.ShowAsync(this, "✨ AI Co-Author", $"Enter instructions to improve this text:\n\n\"{currentText}\"");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            if (sender is Button btn)
            {
                StartButtonSpinner(btn);
            }

            try
            {
                using var client = new HttpClient();
                string content = null;

                if (string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    var systemPrompt = "You are a professional interactive fiction writer and adventure game editor assistant. Improve, expand, or rewrite the provided text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated text directly, with no extra conversational remarks, introductions, explanations, or quotes.";
                    var finalPrompt = $"Here is the current text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    content = await CallGeminiAsync(client, endpoint, apiKey, model, systemPrompt, finalPrompt, 0.7);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    }

                    var finalPrompt = $"Here is the current text:\n\"{currentText}\"\n\nInstructions on how to change or generate it:\n\"{prompt}\"";
                    var requestBody = new AICoAuthorRequest
                    {
                        model = model,
                        messages = new[]
                        {
                            new AICoAuthorMessage { role = "system", content = "You are a professional interactive fiction writer and adventure game editor assistant. Improve, expand, or rewrite the provided text based strictly on the user's instructions. Keep your response extremely brief, returning ONLY the final updated text directly, with no extra conversational remarks, introductions, explanations, or quotes." },
                            new AICoAuthorMessage { role = "user", content = finalPrompt }
                        },
                        temperature = 0.7
                    };

                    var requestJson = JsonSerializer.Serialize(requestBody, RagNext.Designer.Avalonia.Services.DesignerJsonContext.Default.AICoAuthorRequest);
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
                        content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    vm.ComposeText = content;
                }
            }
            catch (Exception ex)
            {
                await ConfirmDialog.ShowAsync(this, "AI Assist Error", ex.Message);
            }
            finally
            {
                if (sender is Button composeBtn)
                {
                    StopButtonSpinner(composeBtn);
                }
            }
        }

        private void OnAddWearSlotClicked(object? sender, RoutedEventArgs e)
        {
            if (App.CurrentGame == null) return;
            var textBox = this.FindControl<TextBox>("NewWearSlotTextBox");
            if (textBox != null)
            {
                var newSlot = textBox.Text?.Trim();
                if (!string.IsNullOrEmpty(newSlot) && !App.CurrentGame.WearSlots.Contains(newSlot))
                {
                    App.CurrentGame.WearSlots.Add(newSlot);
                    textBox.Text = string.Empty;
                }
            }
        }

        private void OnDeleteWearSlotClicked(object? sender, RoutedEventArgs e)
        {
            if (App.CurrentGame == null) return;
            var listBox = this.FindControl<ListBox>("WearSlotsListBox");
            if (listBox != null && listBox.SelectedItem is string selectedSlot)
            {
                App.CurrentGame.WearSlots.Remove(selectedSlot);
            }
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

    public static class PromptDialog
    {
        public static Task<string> ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource<string>();
            var dialog = new Window
            {
                Title = title,
                Width = 540,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Padding = new global::Avalonia.Thickness(20)
            };
            dialog.Bind(global::Avalonia.Controls.Window.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("MainBg"));
            dialog.Bind(global::Avalonia.Controls.Window.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Message & Input scroll area
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Fixed button row

            var contentStack = new StackPanel { Spacing = 12, Margin = new global::Avalonia.Thickness(0, 0, 8, 12) };

            var msgBlock = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            msgBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMuted"));
            contentStack.Children.Add(msgBlock);

            var input = new TextBox 
            { 
                PlaceholderText = "Enter instructions...",
                AcceptsReturn = true,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Height = 70,
                VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Top
            };
            input.Bind(TextBox.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("InputBg"));
            input.Bind(TextBox.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            input.Bind(TextBox.BorderBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush"));
            contentStack.Children.Add(input);

            var scrollViewer = new ScrollViewer 
            { 
                Content = contentStack,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var okBtn = new Button { Content = "OK", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White, IsDefault = true };
            var cancelBtn = new Button { Content = "Cancel", Width = 80 };
            cancelBtn.Bind(Button.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ToolbarBtnBg"));
            cancelBtn.Bind(Button.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            cancelBtn.Bind(Button.BorderBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush"));

            okBtn.Click += (s, e) => { tcs.TrySetResult(input.Text ?? ""); dialog.Close(); };
            cancelBtn.Click += (s, e) => { tcs.TrySetResult(""); dialog.Close(); };

            input.KeyDown += (s, e) =>
            {
                if (e.Key == global::Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
                {
                    e.Handled = true;
                    tcs.TrySetResult(input.Text ?? "");
                    dialog.Close();
                }
            };

            dialog.Closed += (s, e) => { tcs.TrySetResult(""); };

            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            Grid.SetRow(buttons, 1);
            mainGrid.Children.Add(buttons);

            dialog.Content = mainGrid;
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
                Width = 480,
                SizeToContent = global::Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Padding = new global::Avalonia.Thickness(20)
            };
            dialog.Bind(global::Avalonia.Controls.Window.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("MainBg"));
            dialog.Bind(global::Avalonia.Controls.Window.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));

            var stack = new StackPanel { Spacing = 16 };
            var msgBlock = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            msgBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMuted"));
            stack.Children.Add(msgBlock);

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var yesBtn = new Button { Content = "Yes", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White, IsDefault = true };
            var noBtn = new Button { Content = "No", Width = 80 };
            noBtn.Bind(Button.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ToolbarBtnBg"));
            noBtn.Bind(Button.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            noBtn.Bind(Button.BorderBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush"));

            yesBtn.Click += (s, e) => { tcs.TrySetResult(true); dialog.Close(); };
            noBtn.Click += (s, e) => { tcs.TrySetResult(false); dialog.Close(); };

            dialog.Closed += (s, e) => { tcs.TrySetResult(false); };

            buttons.Children.Add(yesBtn);
            buttons.Children.Add(noBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
        }
    }

    public static class ConfirmPublishDialog
    {
        public static Task<string> ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource<string>();
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                SizeToContent = global::Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Padding = new global::Avalonia.Thickness(20)
            };
            dialog.Bind(global::Avalonia.Controls.Window.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("MainBg"));
            dialog.Bind(global::Avalonia.Controls.Window.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));

            var stack = new StackPanel { Spacing = 16 };
            var msgBlock = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            msgBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMuted"));
            stack.Children.Add(msgBlock);

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var yesBtn = new Button { Content = "Yes", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White, IsDefault = true };
            var noBtn = new Button { Content = "No", Width = 80 };
            noBtn.Bind(Button.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ToolbarBtnBg"));
            noBtn.Bind(Button.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            noBtn.Bind(Button.BorderBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush"));

            var cancelBtn = new Button { Content = "Cancel", Width = 80 };
            cancelBtn.Bind(Button.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ToolbarBtnBg"));
            cancelBtn.Bind(Button.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));
            cancelBtn.Bind(Button.BorderBrushProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush"));

            yesBtn.Click += (s, e) => { tcs.TrySetResult("Yes"); dialog.Close(); };
            noBtn.Click += (s, e) => { tcs.TrySetResult("No"); dialog.Close(); };
            cancelBtn.Click += (s, e) => { tcs.TrySetResult("Cancel"); dialog.Close(); };

            dialog.Closed += (s, e) => { tcs.TrySetResult("Cancel"); };

            buttons.Children.Add(yesBtn);
            buttons.Children.Add(noBtn);
            buttons.Children.Add(cancelBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
        }
    }

    public static class AlertDialog
    {
        public static Task ShowAsync(Window parent, string title, string message)
        {
            var tcs = new TaskCompletionSource();
            var dialog = new Window
            {
                Title = title,
                Width = 480,
                SizeToContent = global::Avalonia.Controls.SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Padding = new global::Avalonia.Thickness(20)
            };
            dialog.Bind(global::Avalonia.Controls.Window.BackgroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("MainBg"));
            dialog.Bind(global::Avalonia.Controls.Window.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextNormal"));

            var stack = new StackPanel { Spacing = 16 };
            var msgBlock = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            msgBlock.Bind(TextBlock.ForegroundProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMuted"));
            stack.Children.Add(msgBlock);

            var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var okBtn = new Button { Content = "OK", Width = 80, Background = global::Avalonia.Media.Brush.Parse("#8E2DE2"), Foreground = global::Avalonia.Media.Brushes.White, IsDefault = true };

            okBtn.Click += (s, e) => { tcs.TrySetResult(); dialog.Close(); };

            dialog.Closed += (s, e) => { tcs.TrySetResult(); };

            buttons.Children.Add(okBtn);
            stack.Children.Add(buttons);

            dialog.Content = stack;
            dialog.ShowDialog(parent);
            return tcs.Task;
        }
    }

    public class ActionToIdStringConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly ActionToIdStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string guidStr && Guid.TryParse(guidStr, out var guid))
            {
                var game = App.CurrentGame;
                if (game != null)
                {
                    foreach (var room in game.Rooms)
                    {
                        var act = room.Actions.FirstOrDefault(a => a.Id == guid);
                        if (act != null) return act;
                    }
                    foreach (var obj in game.Objects)
                    {
                        var act = obj.Actions.FirstOrDefault(a => a.Id == guid);
                        if (act != null) return act;
                    }
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is RagsCore.Models.Action action)
            {
                return action.IdString;
            }
            return global::Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    public class AssetIdToBitmapConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly AssetIdToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string assetId && !string.IsNullOrEmpty(assetId))
            {
                var game = App.CurrentGame;
                if (game != null)
                {
                    var asset = game.MediaAssets.FirstOrDefault(a => 
                        string.Equals(a.Id.ToString(), assetId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.OriginalFileName, assetId, StringComparison.OrdinalIgnoreCase));
                    
                    if (asset != null)
                    {
                        var localPath = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(game, asset);
                        if (File.Exists(localPath))
                        {
                            try
                            {
                                return new global::Avalonia.Media.Imaging.Bitmap(localPath);
                            }
                            catch
                            {
                                // Fall through
                            }
                        }
                    }
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
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

    public class GuidToGameObjectConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly GuidToGameObjectConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is Guid guid && App.CurrentGame != null)
            {
                return App.CurrentGame.Objects.FirstOrDefault(o => o.Id == guid);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MediaIdToBitmapConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly MediaIdToBitmapConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            Guid? id = null;
            if (value is Guid g) id = g;

            if (id.HasValue && id.Value != Guid.Empty && App.CurrentGame != null)
            {
                var asset = App.CurrentGame.MediaAssets.FirstOrDefault(a => a.Id == id.Value);
                if (asset != null)
                {
                    var path = new MediaLibrary(new AvaloniaMediaPathProvider()).GetLocalPath(App.CurrentGame, asset);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            return new global::Avalonia.Media.Imaging.Bitmap(path);
                        }
                        catch
                        {
                            // ignore
                        }
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
                if (global::Avalonia.Application.Current?.TryFindResource("NavActiveBg", out var activeBrush) == true && activeBrush is global::Avalonia.Media.IBrush brush)
                {
                    return brush;
                }
                return global::Avalonia.Media.Brush.Parse("#2E1A47"); // Dark purple / violet highlight
            }
            if (global::Avalonia.Application.Current?.TryFindResource("ToolbarBtnBg", out var baseBrush) == true && baseBrush is global::Avalonia.Media.IBrush bBrush)
            {
                return bBrush;
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
                if (global::Avalonia.Application.Current?.TryFindResource("AccentBrush", out var accentBrush) == true && accentBrush is global::Avalonia.Media.IBrush brush)
                {
                    return brush;
                }
                return global::Avalonia.Media.Brush.Parse("#8E2DE2"); // Highlight border
            }
            if (global::Avalonia.Application.Current?.TryFindResource("BorderBrush", out var borderBrush) == true && borderBrush is global::Avalonia.Media.IBrush bBrush)
            {
                return bBrush;
            }
            return global::Avalonia.Media.Brush.Parse("#2A2A3A"); // Dark border
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class HexToColorConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly HexToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                if (global::Avalonia.Media.Color.TryParse(hex, out var color))
                {
                    return color;
                }
            }
            return global::Avalonia.Media.Colors.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is global::Avalonia.Media.Color color)
            {
                return color.ToString();
            }
            return "#FFFFFF";
        }
    }

    public class HexToBrushConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly HexToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                if (global::Avalonia.Media.Color.TryParse(hex, out var color))
                {
                    return new global::Avalonia.Media.SolidColorBrush(color);
                }
            }
            return global::Avalonia.Media.Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is global::Avalonia.Media.SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }
            return "#FFFFFF";
        }
    }

    public class HotspotOuterBackgroundConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly HotspotOuterBackgroundConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is RagsCore.Models.ScreenHotspot hotspot)
            {
                if (string.Equals(hotspot.StyleType, "ImageButton", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(hotspot.StyleType, "Invisible", StringComparison.OrdinalIgnoreCase))
                {
                    return global::Avalonia.Media.Brushes.Transparent;
                }

                if (global::Avalonia.Media.Color.TryParse(hotspot.BackgroundColor, out var color))
                {
                    return new global::Avalonia.Media.SolidColorBrush(color);
                }
            }
            return global::Avalonia.Media.Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class HotspotFontSizeScaleConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly HotspotFontSizeScaleConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double val)
            {
                return val / 8.0;
            }
            if (value is int ival)
            {
                return ival / 8.0;
            }
            if (value is float fval)
            {
                return fval / 8.0;
            }
            return 3.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class ColorToBrushConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly ColorToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is global::Avalonia.Media.Color color)
            {
                return new global::Avalonia.Media.SolidColorBrush(color);
            }
            return global::Avalonia.Media.Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is global::Avalonia.Media.SolidColorBrush brush)
            {
                return brush.Color;
            }
            return global::Avalonia.Media.Colors.White;
        }
    }

    public class DesignerTemplateResolverConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly DesignerTemplateResolverConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                var game = App.CurrentGame;
                if (game != null)
                {
                    var resolved = global::System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}]+)\}", match =>
                    {
                        var path = match.Groups[1].Value.Trim();
                        if (path.StartsWith("variables.", StringComparison.OrdinalIgnoreCase))
                            path = path.Substring(10);
                        else if (path.StartsWith("variable.", StringComparison.OrdinalIgnoreCase))
                            path = path.Substring(9);

                        var v = game.Variables.FirstOrDefault(x => string.Equals(x.Name, path, StringComparison.OrdinalIgnoreCase));
                        if (v != null) return v.Value ?? "0";

                        if (string.Equals(path, "player.name", StringComparison.OrdinalIgnoreCase))
                            return game.Player?.Name ?? "Player";

                        return match.Value;
                    });
                    return resolved;
                }
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public static class TextBlockExtensions
    {
        public static readonly AttachedProperty<string?> HtmlTextProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, string?>("HtmlText", typeof(TextBlockExtensions));

        public static string? GetHtmlText(TextBlock element) => element.GetValue(HtmlTextProperty);
        public static void SetHtmlText(TextBlock element, string? value) => element.SetValue(HtmlTextProperty, value);

        static TextBlockExtensions()
        {
            HtmlTextProperty.Changed.AddClassHandler<TextBlock>(OnHtmlTextChanged);
        }

        private static void OnHtmlTextChanged(TextBlock tb, AvaloniaPropertyChangedEventArgs args)
        {
            tb.Inlines?.Clear();
            var text = args.NewValue as string;
            if (string.IsNullOrEmpty(text)) return;

            text = DesignerTemplateResolverConverter.Instance.Convert(text, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture) as string ?? text;

            var regex = new global::System.Text.RegularExpressions.Regex(@"<(color|mark)=([^>]+)>(.*?)</\1>|<(b|i|u)>(.*?)</\4>|([^<>]+)", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = regex.Matches(text);

            foreach (global::System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Groups[6].Success)
                {
                    tb.Inlines?.Add(new global::Avalonia.Controls.Documents.Run(m.Groups[6].Value));
                }
                else if (m.Groups[4].Success)
                {
                    var tag = m.Groups[4].Value.ToLower();
                    var content = m.Groups[5].Value;
                    var run = new global::Avalonia.Controls.Documents.Run(content);
                    if (tag == "b") run.FontWeight = FontWeight.Bold;
                    else if (tag == "i") run.FontStyle = FontStyle.Italic;
                    else if (tag == "u") run.TextDecorations = TextDecorations.Underline;
                    tb.Inlines?.Add(run);
                }
                else if (m.Groups[1].Success)
                {
                    var tag = m.Groups[1].Value.ToLower();
                    var param = m.Groups[2].Value.Trim('\'', '"');
                    var content = m.Groups[3].Value;
                    var run = new global::Avalonia.Controls.Documents.Run(content);
                    
                    if (tag == "color")
                    {
                        if (Color.TryParse(param, out var clr))
                            run.Foreground = new SolidColorBrush(clr);
                    }
                    else if (tag == "mark")
                    {
                        if (Color.TryParse(param, out var clr))
                            run.Background = new SolidColorBrush(clr);
                    }
                    tb.Inlines?.Add(run);
                }
            }
        }
    }

    public class MediaIdToMediaAssetConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly MediaIdToMediaAssetConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is Guid g && App.CurrentGame != null)
            {
                return App.CurrentGame.MediaAssets.FirstOrDefault(a => a.Id == g);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is MediaAsset asset)
            {
                return asset.Id;
            }
            return null;
        }
    }

    public class YScaleConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly YScaleConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double d)
            {
                return d * 0.5625;
            }
            if (value is float f)
            {
                return f * 0.5625f;
            }
            if (value is int i)
            {
                return i * 0.5625;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double d)
            {
                return d / 0.5625;
            }
            if (value is float f)
            {
                return f / 0.5625f;
            }
            return value;
        }
    }

    public class TextBoxScaleConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly TextBoxScaleConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double val)
            {
                return val / 2.2;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class PortraitHorizontalAlignmentConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly PortraitHorizontalAlignmentConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string s && s.Contains("Right")) return global::Avalonia.Layout.HorizontalAlignment.Right;
            return global::Avalonia.Layout.HorizontalAlignment.Left;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class PortraitVerticalAlignmentConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly PortraitVerticalAlignmentConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string s && s.Contains("Bottom")) return global::Avalonia.Layout.VerticalAlignment.Bottom;
            return global::Avalonia.Layout.VerticalAlignment.Top;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class LeftDockVisibleConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly LeftDockVisibleConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (values.Count >= 3)
            {
                var room = values[0] as string;
                var inv = values[1] as string;
                var nav = values[2] as string;
                return string.Equals(room, "Left", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(inv, "Left", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(nav, "Left", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public class RightDockVisibleConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly RightDockVisibleConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (values.Count >= 3)
            {
                var room = values[0] as string;
                var inv = values[1] as string;
                var nav = values[2] as string;
                return string.Equals(room, "Right", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(inv, "Right", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(nav, "Right", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    public class PortraitMarginConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly PortraitMarginConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string align && values[1] is double textBoxHeight)
            {
                if (align.Contains("Bottom"))
                {
                    return new global::Avalonia.Thickness(16, 16, 16, textBoxHeight + 24);
                }
            }
            return new global::Avalonia.Thickness(16);
        }
    }

    public class PortraitPositionConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly PortraitPositionConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is string s && parameter is string param)
            {
                if (param == "Top") return s.Contains("Top");
                return string.Equals(s, param, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class BottomDockVisibleConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly BottomDockVisibleConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (values.Count >= 4)
            {
                var inv = values[0] as string;
                var room = values[1] as string;
                var nav = values[2] as string;
                var port = values[3] as string;
                return string.Equals(inv, "Bottom", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(room, "Bottom", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(nav, "Bottom", StringComparison.OrdinalIgnoreCase) ||
                       (port != null && port.Contains("Bottom"));
            }
            return false;
        }
    }

    public class AssetIdToAssetConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly AssetIdToAssetConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG-CONVERTER] Convert called. Value: '{value}'");
            if (value is string idStr && !string.IsNullOrWhiteSpace(idStr) && global::RagNext.Designer.Avalonia.ViewModels.MainWindowViewModel.Instance?.CurrentGame != null)
            {
                var found = global::RagNext.Designer.Avalonia.ViewModels.MainWindowViewModel.Instance.CurrentGame.MediaAssets.FirstOrDefault(a => string.Equals(a.Id.ToString(), idStr, StringComparison.OrdinalIgnoreCase));
                System.Diagnostics.Debug.WriteLine($"[DEBUG-CONVERTER] Convert found: '{found?.Name}'");
                return found;
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG-CONVERTER] ConvertBack called. Value: '{value}' (Type: {value?.GetType().FullName})");
            if (value is RagsCore.Models.MediaAsset asset)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG-CONVERTER] ConvertBack returning: '{asset.Id}'");
                return asset.Id.ToString();
            }
            System.Diagnostics.Debug.WriteLine($"[DEBUG-CONVERTER] ConvertBack returning DoNothing to prevent resetting source on detach");
            return global::Avalonia.Data.BindingOperations.DoNothing;
        }
    }

    public class PlayerStatusBoxCornerRadiusConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly PlayerStatusBoxCornerRadiusConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            double radius = 8.0;
            string shape = "Default";
            if (values.Count >= 2)
            {
                if (values[0] is string s) shape = s;
                if (values[1] is double d) radius = d;
            }
            if (string.Equals(shape, "Rectangle", StringComparison.OrdinalIgnoreCase))
            {
                return new global::Avalonia.CornerRadius(radius);
            }
            return new global::Avalonia.CornerRadius(56, 16, 16, 56);
        }
    }

    public class PlayerPortraitCornerRadiusConverter : global::Avalonia.Data.Converters.IMultiValueConverter
    {
        public static readonly PlayerPortraitCornerRadiusConverter Instance = new();
        public object Convert(global::System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            double size = 80.0;
            string shape = "Circle";
            if (values.Count >= 2)
            {
                if (values[0] is string s) shape = s;
                if (values[1] is double d) size = d;
            }
            if (string.Equals(shape, "Square", StringComparison.OrdinalIgnoreCase))
            {
                return new global::Avalonia.CornerRadius(4);
            }
            return new global::Avalonia.CornerRadius(size / 2.0);
        }
    }

    public class DoubleToThicknessConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly DoubleToThicknessConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double d)
            {
                return new global::Avalonia.Thickness(d);
            }
            if (value is float f)
            {
                return new global::Avalonia.Thickness(f);
            }
            return new global::Avalonia.Thickness(1.5);
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class DoubleToCornerRadiusConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly DoubleToCornerRadiusConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture)
        {
            if (value is double d)
            {
                return new global::Avalonia.CornerRadius(d);
            }
            if (value is float f)
            {
                return new global::Avalonia.CornerRadius(f);
            }
            return new global::Avalonia.CornerRadius(8);
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, global::System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}