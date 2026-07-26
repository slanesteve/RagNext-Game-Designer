using System;

namespace RagsCore.Models
{
    public class ThemeSettings : BaseModel
    {
        private string _primaryBgColor = "#1e1e24";
        public string PrimaryBgColor { get => _primaryBgColor; set => SetProperty(ref _primaryBgColor, value); }

        private string _textMainColor = "#ffffff";
        public string TextMainColor { get => _textMainColor; set => SetProperty(ref _textMainColor, value); }

        private string _borderAccentColor = "#4a4a5a";
        public string BorderAccentColor { get => _borderAccentColor; set => SetProperty(ref _borderAccentColor, value); }

        private string _fontName = "Outfit";
        public string FontName { get => _fontName; set => SetProperty(ref _fontName, value); }

        private string _fontAssetId = string.Empty;
        public string FontAssetId { get => _fontAssetId; set => SetProperty(ref _fontAssetId, value); }

        private string _backgroundAssetId = string.Empty;
        public string BackgroundAssetId 
        { 
            get => _backgroundAssetId; 
            set 
            {
                if (SetProperty(ref _backgroundAssetId, value))
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG-SETTER] BackgroundAssetId setter called. Value: '{value}'");
                    OnPropertyChanged(nameof(SelectedBackground));
                }
            }
        }

        private string _frameAssetId = string.Empty;
        public string FrameAssetId 
        { 
            get => _frameAssetId; 
            set 
            {
                if (SetProperty(ref _frameAssetId, value))
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG-SETTER] FrameAssetId setter called. Value: '{value}'");
                    OnPropertyChanged(nameof(SelectedFrame));
                }
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public Func<string, MediaAsset?>? AssetResolver { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsBatchUpdating { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public MediaAsset? SelectedBackground
        {
            get
            {
                var resolved = AssetResolver?.Invoke(BackgroundAssetId);
                System.Diagnostics.Debug.WriteLine($"[DEBUG-MODEL-GET] SelectedBackground get. ID: '{BackgroundAssetId}', Found: '{resolved?.Name}'");
                return resolved;
            }
            set
            {
                if (IsBatchUpdating) return;
                if (value == null)
                {
                    BackgroundAssetId = string.Empty;
                    return;
                }
                BackgroundAssetId = value.Id.ToString();
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public MediaAsset? SelectedFrame
        {
            get
            {
                var resolved = AssetResolver?.Invoke(FrameAssetId);
                System.Diagnostics.Debug.WriteLine($"[DEBUG-MODEL-GET] SelectedFrame get. ID: '{FrameAssetId}', Found: '{resolved?.Name}'");
                return resolved;
            }
            set
            {
                if (IsBatchUpdating) return;
                if (value == null)
                {
                    FrameAssetId = string.Empty;
                    return;
                }
                FrameAssetId = value.Id.ToString();
            }
        }

        public void NotifyThemeProperties()
        {
            OnPropertyChanged(nameof(BackgroundAssetId));
            OnPropertyChanged(nameof(FrameAssetId));
            OnPropertyChanged(nameof(SelectedBackground));
            OnPropertyChanged(nameof(SelectedFrame));
        }

        private string _inventoryDockPosition = "Right"; // "Bottom", "Left", "Right"
        public string InventoryDockPosition { get => _inventoryDockPosition; set => SetProperty(ref _inventoryDockPosition, value); }

        private string _roomItemsDockPosition = "Right"; // "Right", "Left", "Bottom"
        public string RoomItemsDockPosition { get => _roomItemsDockPosition; set => SetProperty(ref _roomItemsDockPosition, value); }

        private string _navigationDockPosition = "Right"; // "Right", "Left", "Bottom"
        public string NavigationDockPosition { get => _navigationDockPosition; set => SetProperty(ref _navigationDockPosition, value); }

        private double _panelPadding = 12;
        public double PanelPadding { get => _panelPadding; set => SetProperty(ref _panelPadding, value); }

        private double _borderRadius = 8;
        public double BorderRadius { get => _borderRadius; set => SetProperty(ref _borderRadius, value); }

        private double _aspectRatio = 1.333; // Scene image aspect ratio
        public double AspectRatio { get => _aspectRatio; set => SetProperty(ref _aspectRatio, value); }

        private string _textBoxAlignment = "Left"; // "Left", "Center", "Right"
        public string TextBoxAlignment { get => _textBoxAlignment; set => SetProperty(ref _textBoxAlignment, value); }

        private double _textBoxWidth = 780; // pixels
        public double TextBoxWidth { get => _textBoxWidth; set => SetProperty(ref _textBoxWidth, value); }

        private double _textBoxHeight = 320; // pixels
        public double TextBoxHeight { get => _textBoxHeight; set => SetProperty(ref _textBoxHeight, value); }

        private string _portraitAlignment = "TopLeft"; // "TopLeft", "TopRight", "BottomLeft", "BottomRight"
        public string PortraitAlignment { get => _portraitAlignment; set => SetProperty(ref _portraitAlignment, value); }

        private double _sidebarWidth = 360; // pixels
        public double SidebarWidth { get => _sidebarWidth; set => SetProperty(ref _sidebarWidth, value); }

        private string _activePreset = "Default";
        public string ActivePreset { get => _activePreset; set => SetProperty(ref _activePreset, value); }

        private double _bottomBarHeight = 220; // pixels
        public double BottomBarHeight { get => _bottomBarHeight; set => SetProperty(ref _bottomBarHeight, value); }

        private double _fontSize = 18; // pixels
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }

        private bool _frameApplyToGameScreen = true;
        public bool FrameApplyToGameScreen { get => _frameApplyToGameScreen; set => SetProperty(ref _frameApplyToGameScreen, value); }

        private bool _frameApplyToMainText = false;
        public bool FrameApplyToMainText { get => _frameApplyToMainText; set => SetProperty(ref _frameApplyToMainText, value); }

        private bool _frameApplyToPopups = false;
        public bool FrameApplyToPopups { get => _frameApplyToPopups; set => SetProperty(ref _frameApplyToPopups, value); }

        private bool _frameApplyToSidebars = false;
        public bool FrameApplyToSidebars { get => _frameApplyToSidebars; set => SetProperty(ref _frameApplyToSidebars, value); }

        private double _borderThickness = 1.5;
        public double BorderThickness { get => _borderThickness; set => SetProperty(ref _borderThickness, value); }

        private string _playerStatusBoxShape = "Default";
        public string PlayerStatusBoxShape { get => _playerStatusBoxShape; set => SetProperty(ref _playerStatusBoxShape, value); }

        private string _playerPortraitShape = "Circle";
        public string PlayerPortraitShape { get => _playerPortraitShape; set => SetProperty(ref _playerPortraitShape, value); }

        private double _portraitSize = 80.0;
        public double PortraitSize { get => _portraitSize; set => SetProperty(ref _portraitSize, value); }

        private string _mapStyle = "Clean"; // "Clean", "SciFi", "Fantasy"
        public string MapStyle { get => _mapStyle; set => SetProperty(ref _mapStyle, value); }
    }
}
