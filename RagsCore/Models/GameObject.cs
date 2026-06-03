using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Base object that can be placed in rooms or carried by players.
    /// </summary>
    public class GameObject : BaseModel
    {
        // Example: add to any game element (e.g., Player/Room/GameObject)
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        public ObservableCollection<MediaReference> Media { get; set; } = new(); // reference by AssetId
        public ObservableCollection<Action> Actions { get; set; } = new(); // added

        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private bool _isContainer;
        public bool IsContainer { get => _isContainer; set => SetProperty(ref _isContainer, value); }

        private bool _containerOpen;
        public bool ContainerOpen { get => _containerOpen; set => SetProperty(ref _containerOpen, value); }

        public ObservableCollection<Guid> ContainedObjectIds { get; set; } = new();

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private bool _isCollectible = true;
        public bool IsCollectible { get => _isCollectible; set => SetProperty(ref _isCollectible, value); }

        public Dictionary<string, string> Properties { get; set; } = new();

        private string? _portraitImagePath;
        public string? PortraitImagePath
        {
            get => _portraitImagePath;
            set
            {
                if (SetProperty(ref _portraitImagePath, value))
                {
                    OnPropertyChanged(nameof(PortraitImageFileName));
                }
            }
        }

        public string PortraitImageFileName => System.IO.Path.GetFileName(_portraitImagePath ?? string.Empty);
    }
}