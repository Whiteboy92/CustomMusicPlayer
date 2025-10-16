using System.IO;

namespace MusicPlayer.Services.DiscordRpc;

/// <summary>
/// Helper class for parsing song metadata and formatting Discord presence information
/// </summary>
public static class DiscordPresenceHelper
{
    /// <summary>
    /// Extracts artist and song name from a music file name
    /// Supports formats: "Artist - Song", "Artist – Song", "Artist | Song" (when format is "ArtistSong")
    /// or "Song - Artist", "Song – Artist", "Song | Artist" (when format is "SongArtist")
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
                if (format == "SongArtist")
                {
                    return (parts[1].Trim(), parts[0].Trim()); // artist, songName
                }
                else
                {
                    return (parts[0].Trim(), parts[1].Trim()); // artist, songName
                }
            }
        }
        
        return ("Unknown Artist", nameWithoutExt);
    }

    /// <summary>
    /// Parses a duration string (format: "MM:SS") to total seconds
    /// </summary>
    public static double ParseDurationToSeconds(string duration)
    {
        if (string.IsNullOrEmpty(duration) || duration == "--:--")
        {
            return 0;
        }

        var parts = duration.Split(':');
        if (parts.Length == 2 && 
            int.TryParse(parts[0], out var minutes) && 
            int.TryParse(parts[1], out var seconds))
        {
            return (minutes * 60) + seconds;
        }

        return 0;
    }
}

