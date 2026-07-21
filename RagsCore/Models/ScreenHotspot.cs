using System;

namespace RagsCore.Models
{
    public class ScreenHotspot : BaseModel
    {
        private string _id = Guid.NewGuid().ToString();
        public string Id { get => _id; set => SetProperty(ref _id, value); }

        private string _name = "New Button";
        public string Name 
        { 
            get => _name; 
            set 
            {
                if (SetProperty(ref _name, value))
                    OnPropertyChanged(nameof(DisplayName));
            }
        }

        private double _x = 50;
        public double X { get => _x; set => SetProperty(ref _x, value); }

        private double _y = 50;
        public double Y { get => _y; set => SetProperty(ref _y, value); }

        private double _width = 10;
        public double Width { get => _width; set => SetProperty(ref _width, value); }

        private double _height = 10;
        public double Height { get => _height; set => SetProperty(ref _height, value); }

        private string _styleType = "Invisible";
        public string StyleType { get => _styleType; set => SetProperty(ref _styleType, value); }

        private string _labelText = string.Empty;
        public string LabelText 
        { 
            get => _labelText; 
            set 
            {
                if (SetProperty(ref _labelText, value))
                    OnPropertyChanged(nameof(DisplayName));
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(LabelText) ? Name : LabelText;

        private string _fontColor = "#FFFFFF";
        public string FontColor { get => _fontColor; set => SetProperty(ref _fontColor, value); }

        private double _fontSize = 14;
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }

        private string _backgroundColor = "#1A1A1A";
        public string BackgroundColor { get => _backgroundColor; set => SetProperty(ref _backgroundColor, value); }

        private string _imageAssetId = string.Empty;
        public string ImageAssetId { get => _imageAssetId; set => SetProperty(ref _imageAssetId, value); }

        private string _linkedActionId = string.Empty;
        public string LinkedActionId { get => _linkedActionId; set => SetProperty(ref _linkedActionId, value); }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    }
}
