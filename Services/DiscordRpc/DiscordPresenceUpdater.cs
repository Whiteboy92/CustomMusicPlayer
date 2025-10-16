using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Views.Player;

namespace MusicPlayer.Services.DiscordRpc;

/// <summary>
/// Handles updating Discord Rich Presence based on current playback state
/// </summary>
public class DiscordPresenceUpdater(IDiscordRpcService discordRpcService, ISettingsService settingsService)
{
    private MusicFile? currentPlayingSong;

    /// <summary>
    /// Sets the currently playing song for Discord presence updates
    /// </summary>
    public void SetCurrentSong(MusicFile? song)
    {
        currentPlayingSong = song;
    }

    /// <summary>
    /// Updates Discord presence with current playback information
    /// </summary>
    public void UpdatePresence(PlayerControlsView playerControls)
    {
        if (currentPlayingSong == null || !playerControls.HasSource)
        {
            return;
        }

        var isPlaying = playerControls.IsPlaying;
        var currentPosition = playerControls.GetCurrentPositionSeconds();
        var fileName = currentPlayingSong.FileName;
        var format = settingsService.GetSongNameFormat();
        var (artist, songName) = DiscordPresenceHelper.ExtractArtistAndSongName(fileName, format);
        var durationSeconds = DiscordPresenceHelper.ParseDurationToSeconds(currentPlayingSong.Duration);

        discordRpcService.UpdatePresence(songName, artist, isPlaying, currentPosition, durationSeconds);
    }

    /// <summary>
    /// Clears the Discord presence (e.g., when app is closing)
    /// </summary>
    public void ClearPresence()
    {
        discordRpcService.ClearPresence();
    }
}

