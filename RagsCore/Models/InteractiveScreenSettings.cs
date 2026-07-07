using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    public class InteractiveScreenSettings : BaseModel
    {
        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

        private string _backdropAssetId = string.Empty;
        public string BackdropAssetId { get => _backdropAssetId; set => SetProperty(ref _backdropAssetId, value); }

        public ObservableCollection<ScreenHotspot> Hotspots { get; set; } = new();
    }
}
