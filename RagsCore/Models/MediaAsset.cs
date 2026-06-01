using System;

namespace RagsCore.Models
{
    public enum MediaKind { Image, Video, Audio, Other }

    public class MediaAsset : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string OriginalFileName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonIgnore]
        public string Name => OriginalFileName;

        [System.Text.Json.Serialization.JsonIgnore]
        public string IdString => Id.ToString();

        public string RelativePath { get; set; } = string.Empty; // under the game's Assets folder
        public string ContentType { get; set; } = "application/octet-stream";
        public MediaKind Kind { get; set; } = MediaKind.Other;
        public string Sha256 { get; set; } = string.Empty; // for de-dupe/integrity
        public DateTime AddedAtUtc { get; init; } = DateTime.UtcNow;

        // Optional metadata
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}