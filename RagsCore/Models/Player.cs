using System;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Player model. Holds inventory and a reference to current room by Id to avoid circular object graphs.
    /// </summary>
    public class Player : BaseModel
    {
        private List<string> _genders = new()
        {"Male", "Female","Non-binary", "Other"};
        public List<string> Genders { get => _genders; set => SetProperty(ref _genders, value); }


        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = "Player";
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        private string _description = "The protagonist of this adventure.";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private bool _bPromptForName = false;
        public bool bPromptForName { get => _bPromptForName; set => SetProperty(ref _bPromptForName, value); }
        private string _gender = "Male";
        public string Gender { get => _gender; set => SetProperty(ref _gender, value); }

        private Room? _startingRoom = null;
        public Room? StartingRoom { get => _startingRoom; set => SetProperty(ref _startingRoom, value); }

        public ObservableCollection<GameObject> Inventory { get; set; } = new();
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        public ObservableCollection<Action> Actions { get; set; } = new();

        // Optional portrait image path/URI for binding in editor pages
        private string? _portraitImagePath;
        public string? PortraitImagePath
        {
            get => _portraitImagePath;
            set
            {
                if (SetProperty(ref _portraitImagePath, value))
                {
                    // Also notify file name derived property
                    OnPropertyChanged(nameof(PortraitImageFileName));
                }
            }
        }

        public string PortraitImageFileName => System.IO.Path.GetFileName(_portraitImagePath ?? string.Empty);
    }
}