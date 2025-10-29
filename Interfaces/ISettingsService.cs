using MusicPlayer.Models;

namespace MusicPlayer.Interfaces;

public interface ISettingsService : IDisposable
{
    void SaveCurrentPlaybackState(string? songPath, double positionSeconds, int? playlistId);
    (string? songPath, double positionSeconds) GetCurrentPlaybackState(int? playlistId);

    void SaveCurrentQueue(List<string> queuePaths, bool isShuffled);
    (List<string> queuePaths, bool isShuffled) GetCurrentQueue();

    void UpdateLastPlayedIndex(int index);
    int GetLastPlayedIndex();

    void SaveCurrentPlaylistId(int? playlistId);
    int? GetCurrentPlaylistId();

    void UpdateFileCount(int count);
    int GetDatabaseSongCount();

    void FlushToDisk();


    void SaveAllDurations(Dictionary<string, string> durations);
    Dictionary<string, string> GetAllDurations();
    void IncrementPlayCountForPlaylist(string filePath, int? playlistId);
    Dictionary<string, int> GetAllPlayCounts();
    Dictionary<string, int> GetPlayCountsForPlaylist(int? playlistId);
    void SaveCumulativePlayedTime(string songPath, double cumulativeSeconds);
    double GetCumulativePlayedTime(string songPath);
    int GetTotalPlayCount();
    double GetVolumePercent();
    void SaveVolumePercent(double percent);

    (float band80, float band240, float band750, float band2200, float band6600) GetEqualizerSettings();
    void SaveEqualizerSettings(float band80, float band240, float band750, float band2200, float band6600);


    string GetMusicFolderPath();
    void SaveMusicFolderPath(string path);

    bool GetAutoPlayOnStartup();
    void SaveAutoPlayOnStartup(bool enabled);

    void ClearPlayHistory();
    void ResetDatabase();
    string GetDatabaseFilePath();


    string? GetDiscordClientId();
    void SaveDiscordClientId(string? clientId);


    string GetSongNameFormat();


    void CreatePlaylist(string name, string? genre, string? tags, List<string> songFilePaths);
    List<Playlist> GetAllPlaylists();
    Playlist? GetPlaylistById(int id);
    void UpdatePlaylist(int id, string name, string? genre, string? tags, List<string> songFilePaths);
    void DeletePlaylist(int id);
}