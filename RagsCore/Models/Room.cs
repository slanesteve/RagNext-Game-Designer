using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Room model. Exits map a direction to another Room's Id.
    /// </summary>
    public class Room : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        // Items present in the room (by object id or embedded objects)
        public ObservableCollection<Guid> ObjectIds { get; } = new();

        // Simple mapping of exits, e.g. "north" => roomId
        public Dictionary<string, Guid> Exits { get; } = new();
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        internal ObservableCollection<Action> Actions { get; set; } = new();

    }
}