namespace MusicPlayer.Interfaces
{
    public interface ISettingsService : IDisposable
    {
        // ──────────────── Playback State ────────────────
        void SaveCurrentPlaybackState(string? songPath, double positionSeconds);
        (string? songPath, double positionSeconds) GetCurrentPlaybackState();

        void SaveCurrentQueue(List<string> queuePaths, bool isShuffled);
        (List<string> queuePaths, bool isShuffled) GetCurrentQueue();

        void UpdateLastPlayedIndex(int index);
        int GetLastPlayedIndex();

        void UpdateFileCount(int count);
        int GetDatabaseSongCount();

        void FlushToDisk();


        // ──────────────── Song Data ────────────────
        void SaveAllDurations(Dictionary<string, string> durations);
        Dictionary<string, string> GetAllDurations();

        void IncrementPlayCount(string filePath);
        Dictionary<string, int> GetAllPlayCounts();

        void SaveCumulativePlayedTime(string songPath, double cumulativeSeconds);
        double GetCumulativePlayedTime(string songPath);

        int GetTotalPlayCount();


        // ──────────────── Audio Settings ────────────────
        double GetVolumePercent();
        void SaveVolumePercent(double percent);

        (float band80, float band240, float band750, float band2200, float band6600) GetEqualizerSettings();
        void SaveEqualizerSettings(float band80, float band240, float band750, float band2200, float band6600);


        // ──────────────── App Settings ────────────────
        string GetMusicFolderPath();
        void SaveMusicFolderPath(string path);

        bool GetAutoPlayOnStartup();
        void SaveAutoPlayOnStartup(bool enabled);

        void ClearPlayHistory();
        void ResetDatabase();
        string GetDatabaseFilePath();


        // ──────────────── Discord RPC ────────────────
        string? GetDiscordClientId();
        void SaveDiscordClientId(string? clientId);


        // ──────────────── Display / Naming ────────────────
        string GetSongNameFormat();
    }
}
