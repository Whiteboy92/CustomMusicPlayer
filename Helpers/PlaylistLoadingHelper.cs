using MusicPlayer.Interfaces;
using MusicPlayer.Models;

namespace MusicPlayer.Helpers;

public static class PlaylistLoadingHelper
{
    public static async Task<List<MusicFile>> LoadPlaylistSongsAsync(
        int playlistId,
        IMusicLoaderService musicLoader,
        ISettingsService settings)
    {
        var playlist = settings.GetPlaylistById(playlistId);
        if (playlist == null)
        {
            return new List<MusicFile>();
        }

        var allSongs = await musicLoader.LoadMusicFromFolderAsync();
        var cachedPlayCounts = settings.GetPlayCountsForPlaylist(playlistId);
        var cachedDurations = settings.GetAllDurations();

        List<MusicFile> playlistSongs = new();
        
        foreach (var filePath in playlist.SongFilePaths)
        {
            var song = allSongs.FirstOrDefault(s => s.FilePath == filePath);
            if (song != null)
            {
                if (cachedPlayCounts.TryGetValue(song.FilePath, out var playCount))
                {
                    song.PlayCount = playCount;
                }
                if (cachedDurations.TryGetValue(song.FilePath, out var duration))
                {
                    song.Duration = duration;
                }
                playlistSongs.Add(song);
            }
        }

        return playlistSongs;
    }

    public static async Task<List<MusicFile>> LoadStatisticsSongsAsync(
        int? playlistId,
        IMusicLoaderService musicLoader,
        ISettingsService settings)
    {
        List<MusicFile> songsToAnalyze;
        
        if (playlistId.HasValue && playlistId.Value != -1)
        {
            var playlist = settings.GetPlaylistById(playlistId.Value);
            if (playlist == null)
            {
                return new List<MusicFile>();
            }
            
            var allSongs = await musicLoader.LoadMusicFromFolderAsync();
            
            songsToAnalyze = new();
            foreach (var filePath in playlist.SongFilePaths)
            {
                var song = allSongs.FirstOrDefault(s => s.FilePath == filePath);
                if (song != null)
                {
                    songsToAnalyze.Add(song);
                }
            }
        }
        else
        {
            var allSongs = await musicLoader.LoadMusicFromFolderAsync();
            songsToAnalyze = allSongs;
        }
        
        var playlistCounts = settings.GetPlayCountsForPlaylist(
            playlistId.HasValue && playlistId.Value != -1 ? playlistId.Value : null);
        var cachedDurations = settings.GetAllDurations();
        
        foreach (var song in songsToAnalyze)
        {
            song.PlayCount = playlistCounts.GetValueOrDefault(song.FilePath, 0);

            if (cachedDurations.TryGetValue(song.FilePath, out var duration))
            {
                song.Duration = duration;
            }
        }
        
        return songsToAnalyze;
    }

    public static void ApplyMetadataToSongs(
        List<MusicFile> songs,
        Dictionary<string, int> playCounts,
        Dictionary<string, string> durations)
    {
        foreach (var song in songs)
        {
            song.PlayCount = playCounts.GetValueOrDefault(song.FilePath, 0);

            if (durations.TryGetValue(song.FilePath, out var duration))
            {
                song.Duration = duration;
            }
        }
    }
}

