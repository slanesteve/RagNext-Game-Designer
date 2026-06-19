using System;

namespace RagsCore.Models
{
    public class StatusBarElement : BaseModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = "New Element";
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _visualOption = "TextOnly"; // "ImageOnly", "ImageAndText", "TextOnly"
        public string VisualOption 
        { 
            get => _visualOption; 
            set 
            { 
                if (SetProperty(ref _visualOption, value))
                    OnPropertyChanged(nameof(ShowImage));
            } 
        }

        private string _text = "Value";
        public string Text { get => _text; set => SetProperty(ref _text, value); }

        private string _textColor = "#FFFFFF";
        public string TextColor { get => _textColor; set => SetProperty(ref _textColor, value); }

        private Guid? _mediaAssetId;
        public Guid? MediaAssetId 
        { 
            get => _mediaAssetId; 
            set 
            { 
                if (SetProperty(ref _mediaAssetId, value))
                    OnPropertyChanged(nameof(ShowImage));
            } 
        }

        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }

        public bool ShowImage => _visualOption != "TextOnly" && _mediaAssetId.HasValue;
    }
}
