using System;
using System.Collections.Generic;

namespace RagsCore.Models
{
    /// <summary>
    /// Represents a promotional, social, or fundraising link defined by the game designer.
    /// Supports preset platforms (Patreon, Ko-fi, Kickstarter, Discord, Steam, YouTube, Twitch, Website, Custom)
    /// with platform-matched icons and brand colors.
    /// </summary>
    public class PromotionalLink : BaseModel
    {
        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _url = string.Empty;
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        private string _platform = "Patreon";
        public string Platform
        {
            get => _platform;
            set => SetProperty(ref _platform, value);
        }

        /// <summary>
        /// Available platform icon presets.
        /// </summary>
        public static readonly List<string> AvailablePlatforms = new()
        {
            "Patreon",
            "Ko-fi",
            "Kickstarter",
            "Discord",
            "Steam",
            "YouTube",
            "Twitch",
            "Website",
            "Custom"
        };
    }
}
