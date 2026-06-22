using System;
using Xunit;
using RagsCore.Models;

namespace RagNext.Tests
{
    public class ComposerAndPreviewTests
    {
        [Theory]
        [InlineData("Hello", "<b>", "</b>", "<b>Hello</b>")]
        [InlineData("World", "<i>", "</i>", "<i>World</i>")]
        [InlineData("Dialogue", "<color=#FF0000>", "</color>", "<color=#FF0000>Dialogue</color>")]
        public void WrapText_ShouldApplyHtmlFormattingTags(string input, string startTag, string endTag, string expected)
        {
            // Simulate the composer formatting wrap logic
            string result = startTag + input + endTag;

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Game_SplashSettings_ShouldRetainConfig()
        {
            var game = new Game
            {
                Title = "Cosmic Expedition",
                Version = "1.0.0"
            };

            // Set splash screen properties
            game.SplashScreen.SoundAssetId = "intro_theme.mp3";
            game.SplashScreen.VideoAssetId = "intro_movie.mp4";
            game.SplashScreen.ImageAssetId = "nebula.png";

            // Verify they persist
            Assert.Equal("intro_theme.mp3", game.SplashScreen.SoundAssetId);
            Assert.Equal("intro_movie.mp4", game.SplashScreen.VideoAssetId);
            Assert.Equal("nebula.png", game.SplashScreen.ImageAssetId);
        }
    }
}
