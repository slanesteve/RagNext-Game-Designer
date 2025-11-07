using System;
using System.Collections.ObjectModel;

namespace RagNext.Models
{
    // Logical folder that references assets by Id (keeps MediaAsset storage unchanged)
    public class MediaFolder
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = "New Folder";
        public ObservableCollection<MediaFolder> Children { get; set; } = new();
        public ObservableCollection<Guid> AssetIds { get; set; } = new();
    }

    public class MediaTreeDocument
    {
        public ObservableCollection<MediaFolder> Roots { get; set; } = new();
    }
}