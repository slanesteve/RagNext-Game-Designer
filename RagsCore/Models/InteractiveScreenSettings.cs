using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    public class InteractiveScreenSettings : BaseModel
    {
        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

        private string _backdropAssetId = string.Empty;
        public string BackdropAssetId { get => _backdropAssetId; set => SetProperty(ref _backdropAssetId, value); }

        private bool _showCloseButton = true;
        public bool ShowCloseButton { get => _showCloseButton; set => SetProperty(ref _showCloseButton, value); }

        private string _onCloseActionId = string.Empty;
        public string OnCloseActionId { get => _onCloseActionId; set => SetProperty(ref _onCloseActionId, value); }

        public ObservableCollection<ScreenHotspot> Hotspots { get; set; } = new();
    }
}
