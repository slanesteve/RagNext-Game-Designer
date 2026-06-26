using System;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Character in the world. Inherits from GameObject so it can be placed or carried similarly.
    /// </summary>
    public class Character : GameObject
    {
        private bool _isHostile;
        public bool IsHostile { get => _isHostile; set => SetProperty(ref _isHostile, value); }

        private int _health = 100;
        public int Health { get => _health; set => SetProperty(ref _health, value); }

        private List<string> _genders = new() { "Male", "Female", "Non-binary", "Other" };
        public List<string> Genders { get => _genders; set => SetProperty(ref _genders, value); }

        public string Gender
        {
            get => Properties.TryGetValue("Gender", out var g) ? g : "Male";
            set
            {
                Properties["Gender"] = value;
                OnPropertyChanged(nameof(Gender));
            }
        }

        private Room? _startingRoom = null;
        public Room? StartingRoom
        {
            get => _startingRoom;
            set
            {
                if (value == null && _startingRoom != null)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Character.StartingRoom: Ignored null assignment from UI binding initialization.");
                    Console.WriteLine("[DEBUG] Character.StartingRoom: Ignored null assignment from UI binding initialization.");
                    OnPropertyChanged(nameof(StartingRoom));
                    return;
                }
                SetProperty(ref _startingRoom, value);
            }
        }

        public ObservableCollection<GameObject> Inventory { get; set; } = new();

    }
}