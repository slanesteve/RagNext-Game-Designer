using System;

namespace RagsCore.Models
{
    public class SplashScreenSettings : BaseModel
    {
        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

        private string _mode = "ImageAndText"; // "ImageAndText" or "Video"
        public string Mode { get => _mode; set => SetProperty(ref _mode, value); }

        private string _imageAssetId = string.Empty;
        public string ImageAssetId { get => _imageAssetId; set => SetProperty(ref _imageAssetId, value); }

        private string _soundAssetId = string.Empty;
        public string SoundAssetId { get => _soundAssetId; set => SetProperty(ref _soundAssetId, value); }

        private string _text = "My Adventure";
        public string Text { get => _text; set => SetProperty(ref _text, value); }

        private string _fontName = "Outfit";
        public string FontName { get => _fontName; set => SetProperty(ref _fontName, value); }

        private double _fontSize = 32;
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }

        private string _fontColor = "#FFFFFF";
        public string FontColor { get => _fontColor; set => SetProperty(ref _fontColor, value); }

        private double _textX = 50;
        public double TextX { get => _textX; set => SetProperty(ref _textX, value); }

        private double _textY = 50;
        public double TextY { get => _textY; set => SetProperty(ref _textY, value); }

        private double _fadeInDuration = 1.5;
        public double FadeInDuration { get => _fadeInDuration; set => SetProperty(ref _fadeInDuration, value); }

        private double _displayDuration = 2.5;
        public double DisplayDuration { get => _displayDuration; set => SetProperty(ref _displayDuration, value); }

        private double _fadeOutDuration = 1.0;
        public double FadeOutDuration { get => _fadeOutDuration; set => SetProperty(ref _fadeOutDuration, value); }

        private string _videoAssetId = string.Empty;
        public string VideoAssetId { get => _videoAssetId; set => SetProperty(ref _videoAssetId, value); }

        private string _transitionStyle = "Fade"; // Fade, Rise, Cinematic, Glitch, Exposure
        public string TransitionStyle { get => _transitionStyle; set => SetProperty(ref _transitionStyle, value); }
    }
}
