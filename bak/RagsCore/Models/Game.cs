using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace RagsCore.Models
{
    /// <summary>
    /// Root game model containing metadata and collections of game entities.
    /// </summary>
    public class Game : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _title = string.Empty;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _author = string.Empty;
        public string Author { get => _author; set => SetProperty(ref _author, value); }

        private string _version = "1.0.0";
        public string Version { get => _version; set => SetProperty(ref _version, value); }

        public Player? Player { get; set; }

        // Make collections settable so System.Text.Json can assign them during deserialization.
        public ObservableCollection<Room> Rooms { get; set; } = new();
        public ObservableCollection<GameObject> Objects { get; set; } = new();
        public ObservableCollection<Character> Characters { get; set; } = new();
        public ObservableCollection<GameVariable> Variables { get; set; } = new();

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        public static Game CreateNew(string title, string author, string version = "1.0.0")
        {
            return new Game
            {
                Title = title,
                Author = author,
                Version = version
            };
        }
    }
}