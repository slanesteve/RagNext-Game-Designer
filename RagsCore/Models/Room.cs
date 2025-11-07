using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    public class Room : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        public ObservableCollection<Guid> ObjectIds { get; } = new();
        public Dictionary<string, Guid> Exits { get; } = new();
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        public ObservableCollection<Action> Actions { get; set; } = new(); // was internal
    }
}