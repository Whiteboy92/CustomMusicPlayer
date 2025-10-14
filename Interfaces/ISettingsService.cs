namespace MusicPlayer.Interfaces
{
    public interface ISettingsService : IDisposable
    {
        void UpdateFileCount(int count);
        int GetLastPlayedIndex();
        void UpdateLastPlayedIndex(int index);
        void SaveAllDurations(Dictionary<string, string> durations);
        Dictionary<string, string> GetAllDurations();
        int GetPlayCount(string filePath);
        void IncrementPlayCount(string filePath);
        Dictionary<string, int> GetAllPlayCounts();
        void SaveAllPlayCounts(Dictionary<string, int> songPlayCounts);
        void SaveCurrentPlaybackState(string? songPath, double positionSeconds);
        (string? songPath, double positionSeconds) GetCurrentPlaybackState();
        void SaveCurrentQueue(List<string> queuePaths, bool isShuffled);
        (List<string> queuePaths, bool isShuffled) GetCurrentQueue();
        int GetDatabaseSongCount();
        (float band80, float band240, float band750, float band2200, float band6600) GetEqualizerSettings();
        void SaveEqualizerSettings(float band80, float band240, float band750, float band2200, float band6600);
        double GetVolumePercent();
        void SaveVolumePercent(double percent);
        void FlushToDisk();
        
        // Settings page methods
        string GetMusicFolderPath();
        void SaveMusicFolderPath(string path);
        bool GetAutoPlayOnStartup();
        void SaveAutoPlayOnStartup(bool enabled);
        void ClearPlayHistory();
        void ResetDatabase();
        string GetDatabaseFilePath();
        int GetTotalPlayCount();
    }
}

