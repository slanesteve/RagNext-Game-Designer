using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace RagNext.Models
{
    public class EntityFolder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Folder";
        public ObservableCollection<EntityFolder> Children { get; set; } = new();
        public ObservableCollection<Guid> EntityIds { get; set; } = new();
    }

    public class EntityCategoryTree
    {
        public ObservableCollection<EntityFolder> Roots { get; set; } = new();
    }

    public class EntityTreeDocument
    {
        public EntityCategoryTree Rooms { get; set; } = new();
        public EntityCategoryTree Objects { get; set; } = new();
        public EntityCategoryTree Characters { get; set; } = new();
        public EntityCategoryTree Functions { get; set; } = new();
        public EntityCategoryTree Variables { get; set; } = new();
        public EntityCategoryTree Timers { get; set; } = new();
    }
}
