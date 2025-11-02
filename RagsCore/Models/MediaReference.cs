using System;

namespace RagsCore.Models
{
    public enum MediaRole { Default, Thumbnail, Portrait, Background, Video, Audio, Other }

    // Reference to an item in Game.MediaAssets
    public class MediaReference : BaseModel
    {
        public Guid AssetId { get; set; }           // points to Game.MediaAssets[i].Id
        public MediaRole Role { get; set; } = MediaRole.Default;
        public string? Caption { get; set; }
    }
}