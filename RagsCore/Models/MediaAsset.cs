using System.Text.Json.Serialization;

namespace RagsCore.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<MediaKind>))]
    public enum MediaKind { Image, Video, Audio, Other }

    public class MediaAsset : BaseModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OriginalFileName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonIgnore]
        public string Name => OriginalFileName;

        public override string ToString()
        {
            return OriginalFileName;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string IdString => Id.ToString();

        public override bool Equals(object? obj)
        {
            if (obj is MediaAsset other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        private string _relativePath = string.Empty;
        public string RelativePath 
        { 
            get => _relativePath; 
            set => _relativePath = value?.Replace('\\', '/') ?? string.Empty; 
        } // under the game's Assets folder
        public string ContentType { get; set; } = "application/octet-stream";
        public MediaKind Kind { get; set; } = MediaKind.Other;
        public string Sha256 { get; set; } = string.Empty; // for de-dupe/integrity
        public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

        // Optional metadata
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}