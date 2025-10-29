using MusicPlayer.Interfaces;
using MusicPlayer.Validation;
using MusicPlayer.Views.Player;
using MusicPlayer.Views.Playlist;

namespace MusicPlayer.Services;

public class PlaybackStatePersistenceService(ISettingsService settingsService)
{
    public void SaveCurrentState(
        string? currentSongPath,
        double currentSongPosition,
        int? currentPlaylistId,
        PlayerControlsView playerControls)
    {
        if (PlaybackStateValidator.IsValidPlaybackState(currentSongPath, currentSongPosition) && 
            PlaybackStateValidator.HasPlayablePosition(currentSongPosition))
        {
            settingsService.SaveCurrentPlaybackState(currentSongPath, currentSongPosition, currentPlaylistId);

            if (!string.IsNullOrEmpty(currentSongPath))
            {
                var cumulativeTime = playerControls.GetCumulativePlayedTime();
                settingsService.SaveCumulativePlayedTime(currentSongPath, cumulativeTime);
            }
            
            settingsService.FlushToDisk();
        }
    }

    public void SaveFinalState(
        PlayerControlsView playerControls,
        int? currentPlaylistId)
    {
        var currentSongPath = playerControls.GetCurrentSongPath();
        var currentSongPosition = playerControls.GetCurrentPositionSeconds();

        if (StringValidator.HasValue(currentSongPath))
        {
            settingsService.SaveCurrentPlaybackState(currentSongPath, currentSongPosition, currentPlaylistId);
            settingsService.FlushToDisk();
        }
    }

    public void SaveCurrentSongPlayCount(
        PlaylistView playlistView,
        PlayerControlsView playerControls,
        int? currentPlaylistId)
    {
        if (PlaybackStateValidator.IsValidIndex(playlistView.SelectedIndex, playlistView.GetPlaylistCount()))
        {
            var currentSong = playlistView.GetSongAtIndex(playlistView.SelectedIndex);
            
            if (currentSong == null)
            {
                return;
            }
            
            var cumulativeTime = playerControls.GetCumulativePlayedTime();
            settingsService.SaveCumulativePlayedTime(currentSong.FilePath, cumulativeTime);

            if (playerControls.WasPlayedEnough())
            {
                currentSong.PlayCount++;
                settingsService.IncrementPlayCountForPlaylist(currentSong.FilePath, currentPlaylistId);
                settingsService.SaveCumulativePlayedTime(currentSong.FilePath, 0);
            }

            settingsService.FlushToDisk();
        }
    }
}

