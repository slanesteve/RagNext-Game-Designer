using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        public string AmbientOverlay
        {
            get => CustomAttribute.GetAttribute("Weather", Attributes) ?? "None";
            set
            {
                if (value == "None")
                {
                    var weatherAttr = Attributes.FirstOrDefault(a => string.Equals(a.Name, "Weather", StringComparison.OrdinalIgnoreCase));
                    if (weatherAttr != null) Attributes.Remove(weatherAttr);
                    var atmosAttr = Attributes.FirstOrDefault(a => string.Equals(a.Name, "Atmosphere", StringComparison.OrdinalIgnoreCase));
                    if (atmosAttr != null) Attributes.Remove(atmosAttr);
                }
                else
                {
                    CustomAttribute.SetAttribute("Weather", value, Attributes);
                }
                OnPropertyChanged(nameof(AmbientOverlay));
            }
        }

        public string ScreenEffect
        {
            get
            {
                var shakeVal = CustomAttribute.GetAttribute("Shake", Attributes);
                return string.IsNullOrEmpty(shakeVal) ? "None" : shakeVal;
            }
            set
            {
                if (value == "None" || string.IsNullOrEmpty(value))
                {
                    var shakeAttr = Attributes.FirstOrDefault(a => string.Equals(a.Name, "Shake", StringComparison.OrdinalIgnoreCase));
                    if (shakeAttr != null) Attributes.Remove(shakeAttr);
                }
                else
                {
                    CustomAttribute.SetAttribute("Shake", value, Attributes);
                }
                OnPropertyChanged(nameof(ScreenEffect));
            }
        }

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

        private InteractiveScreenSettings _interactiveScreenSettings = new();
        public InteractiveScreenSettings InteractiveScreenSettings { get => _interactiveScreenSettings; set => SetProperty(ref _interactiveScreenSettings, value); }

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