using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    public class Room : BaseModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        public ObservableCollection<Guid> ObjectIds { get; set; } = new();
        public Dictionary<string, Guid> Exits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> LockedExits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        public ObservableCollection<Action> Actions { get; set; } = new(); // was internal

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

        public override bool Equals(object? obj)
        {
            if (obj is Room other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}