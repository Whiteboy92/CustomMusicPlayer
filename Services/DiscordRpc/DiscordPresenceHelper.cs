using System.IO;

namespace MusicPlayer.Services.DiscordRpc;

/// <summary>
/// Helper class for parsing song metadata and formatting Discord presence information
/// </summary>
public static class DiscordPresenceHelper
{
    /// <summary>
    /// Extracts artist and song name from a music file name
    /// Supports formats: "Artist - Song", "Artist – Song", "Artist | Song" (when a format is "ArtistSong")
    /// or "Song - Artist", "Song – Artist", "Song | Artist" (when a format is "SongArtist")
    /// </summary>
    public static (string artist, string songName) ExtractArtistAndSongName(string fileName, string format = "SongArtist")
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var separators = new[] { " - ", " – ", " | " };
        
        foreach (var separator in separators)
        {
            var parts = nameWithoutExt.Split(separator, 2);
            if (parts.Length == 2)
            {
                return format == "SongArtist" 
                    ? (parts[1].Trim(), parts[0].Trim()) 
                    : (parts[0].Trim(), parts[1].Trim());
            }
        }
        
        return ("Unknown Artist", nameWithoutExt);
    }
}

