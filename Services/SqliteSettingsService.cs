using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MusicPlayer.Interfaces;
using MusicPlayer.Services.Settings;
using MusicPlayer.Validation;

namespace MusicPlayer.Services
{
    public class SqliteSettingsService : ISettingsService
    {
        private readonly string databasePath;
        private readonly object cacheLock = new();
        private readonly Timer saveTimer;
        private bool isDirty;
        private bool disposed;
        private int fileCount;
        private int lastPlayedIndex;
        private string? currentSongPath;
        private double currentSongPositionSeconds;
        private List<string> currentQueue = [];
        private bool isQueueShuffled;
        private readonly Dictionary<string, string> songDurations = [];
        private Dictionary<string, int> playCounts = [];
        private double volumePercent = 3.0; // 0-100
        private float band80Hz;
        private float band240Hz;
        private float band750Hz;
        private float band2200Hz;
        private float band6600Hz;
        private string musicFolderPath = string.Empty;
        private bool autoPlayOnStartup;
        private string? discordClientId;
        private string songNameFormat = "SongArtist"; // Default to "Song - Artist" format

        public SqliteSettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicPlayer");

            Directory.CreateDirectory(appDataPath);
            databasePath = Path.Combine(appDataPath, "settings.db");

            InitializeDatabase();
            LoadAllSettingsIntoCache();

            saveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SongDurations (
                    FilePath TEXT PRIMARY KEY,
                    Duration TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PlayCounts (
                    FilePath TEXT PRIMARY KEY,
                    PlayCount INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS CurrentQueue (
                    Position INTEGER PRIMARY KEY,
                    FilePath TEXT NOT NULL,
                    IsCompleted INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_playcounts ON PlayCounts(PlayCount);

                CREATE TABLE IF NOT EXISTS EqualizerSettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Band80Hz REAL NOT NULL DEFAULT 0.0,
                    Band240Hz REAL NOT NULL DEFAULT 0.0,
                    Band750Hz REAL NOT NULL DEFAULT 0.0,
                    Band2200Hz REAL NOT NULL DEFAULT 0.0,
                    Band6600Hz REAL NOT NULL DEFAULT 0.0
                );

                INSERT OR IGNORE INTO EqualizerSettings (Id, Band80Hz, Band240Hz, Band750Hz, Band2200Hz, Band6600Hz) 
                VALUES (1, 0.0, 0.0, 0.0, 0.0, 0.0);
                """;
            command.ExecuteNonQuery();

            var checkColumnCmd = connection.CreateCommand();
            checkColumnCmd.CommandText = "PRAGMA table_info(CurrentQueue)";
            using var reader = checkColumnCmd.ExecuteReader();
            bool hasIsCompletedColumn = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "IsCompleted")
                {
                    hasIsCompletedColumn = true;
                    break;
                }
            }
            reader.Close();
            
            if (!hasIsCompletedColumn)
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE CurrentQueue ADD COLUMN IsCompleted INTEGER NOT NULL DEFAULT 0";
                alterCmd.ExecuteNonQuery();
            }
        }

        private void LoadAllSettingsIntoCache()
        {
            lock (cacheLock)
            {
                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();
                fileCount = GetIntSetting(connection, "FileCount", 0);
                lastPlayedIndex = GetIntSetting(connection, "LastPlayedIndex", 0);
                currentSongPath = GetStringSetting(connection, "CurrentSongPath", null);
                currentSongPositionSeconds = GetDoubleSetting(connection, "CurrentSongPositionSeconds", 0);
                isQueueShuffled = GetBoolSetting(connection, "IsQueueShuffled", false);
                volumePercent = GetDoubleSetting(connection, "VolumePercent", 3.0);
                songDurations.Clear();
                var durationsCommand = connection.CreateCommand();
                durationsCommand.CommandText = "SELECT FilePath, Duration FROM SongDurations";
                using var durationsReader = durationsCommand.ExecuteReader();
                while (durationsReader.Read())
                {
                    songDurations[durationsReader.GetString(0)] = durationsReader.GetString(1);
                }
                playCounts.Clear();
                var countsCommand = connection.CreateCommand();
                countsCommand.CommandText = "SELECT FilePath, PlayCount FROM PlayCounts";
                using var countsReader = countsCommand.ExecuteReader();
                while (countsReader.Read())
                {
                    playCounts[countsReader.GetString(0)] = countsReader.GetInt32(1);
                }
                currentQueue.Clear();
                var queueCommand = connection.CreateCommand();
                queueCommand.CommandText = "SELECT FilePath, IsCompleted FROM CurrentQueue ORDER BY Position";
                using var queueReader = queueCommand.ExecuteReader();
                while (queueReader.Read())
                {
                    var filePath = queueReader.GetString(0);
                    var isCompleted = queueReader.GetInt32(1) == 1;
                    currentQueue.Add($"{filePath}|{isCompleted}");
                }
                var eqCommand = connection.CreateCommand();
                eqCommand.CommandText = "SELECT Band80Hz, Band240Hz, Band750Hz, Band2200Hz, Band6600Hz FROM EqualizerSettings WHERE Id = 1";
                using var eqReader = eqCommand.ExecuteReader();
                if (eqReader.Read())
                {
                    band80Hz = eqReader.GetFloat(0);
                    band240Hz = eqReader.GetFloat(1);
                    band750Hz = eqReader.GetFloat(2);
                    band2200Hz = eqReader.GetFloat(3);
                    band6600Hz = eqReader.GetFloat(4);
                }
                
                musicFolderPath = GetStringSetting(connection, SettingsKeys.MusicFolderPath, string.Empty) ?? string.Empty;
                autoPlayOnStartup = GetBoolSetting(connection, SettingsKeys.AutoPlayOnStartup, false);
                discordClientId = GetStringSetting(connection, SettingsKeys.DiscordClientId, null);
                songNameFormat = GetStringSetting(connection, SettingsKeys.SongNameFormat, "SongArtist") ?? "SongArtist";
            }
        }

        private void AutoSaveCallback(object? state)
        {
            if (isDirty)
            {
                SaveAllSettingsToDatabase();
            }
        }

        private void SaveAllSettingsToDatabase()
        {
            lock (cacheLock)
            {
                if (!isDirty) return;

                try
                {
                    using var connection = new SqliteConnection($"Data Source={databasePath}");
                    connection.Open();

                    using var transaction = connection.BeginTransaction();
                    SetSetting(connection, "FileCount", fileCount.ToString());
                    SetSetting(connection, "LastPlayedIndex", lastPlayedIndex.ToString());
                    SetSetting(connection, "CurrentSongPath", currentSongPath ?? "");
                    SetSetting(connection, "CurrentSongPositionSeconds", currentSongPositionSeconds.ToString(CultureInfo.InvariantCulture));
                    SetSetting(connection, "IsQueueShuffled", isQueueShuffled.ToString());
                    SetSetting(connection, "VolumePercent", volumePercent.ToString(CultureInfo.InvariantCulture));
                    
                    SetSetting(connection, SettingsKeys.MusicFolderPath, musicFolderPath);
                    SetSetting(connection, SettingsKeys.AutoPlayOnStartup, autoPlayOnStartup.ToString());
                    SetSetting(connection, SettingsKeys.DiscordClientId, discordClientId ?? string.Empty);
                    SetSetting(connection, SettingsKeys.SongNameFormat, songNameFormat);
                    var deleteDurationsCmd = connection.CreateCommand();
                    deleteDurationsCmd.CommandText = "DELETE FROM SongDurations";
                    deleteDurationsCmd.ExecuteNonQuery();

                    foreach (var kvp in songDurations)
                    {
                        var insertDurationCmd = connection.CreateCommand();
                        insertDurationCmd.CommandText = "INSERT INTO SongDurations (FilePath, Duration) VALUES (@path, @duration)";
                        insertDurationCmd.Parameters.AddWithValue("@path", kvp.Key);
                        insertDurationCmd.Parameters.AddWithValue("@duration", kvp.Value);
                        insertDurationCmd.ExecuteNonQuery();
                    }
                    var deleteCountsCmd = connection.CreateCommand();
                    deleteCountsCmd.CommandText = "DELETE FROM PlayCounts";
                    deleteCountsCmd.ExecuteNonQuery();

                    foreach (var kvp in playCounts)
                    {
                        var insertCountCmd = connection.CreateCommand();
                        insertCountCmd.CommandText = "INSERT INTO PlayCounts (FilePath, PlayCount) VALUES (@path, @count)";
                        insertCountCmd.Parameters.AddWithValue("@path", kvp.Key);
                        insertCountCmd.Parameters.AddWithValue("@count", kvp.Value);
                        insertCountCmd.ExecuteNonQuery();
                    }
                    var deleteQueueCmd = connection.CreateCommand();
                    deleteQueueCmd.CommandText = "DELETE FROM CurrentQueue";
                    deleteQueueCmd.ExecuteNonQuery();

                    for (int i = 0; i < currentQueue.Count; i++)
                    {
                        var parts = currentQueue[i].Split('|');
                        var filePath = parts[0];
                        var isCompleted = parts.Length > 1 && bool.Parse(parts[1]);
                        
                        var insertQueueCmd = connection.CreateCommand();
                        insertQueueCmd.CommandText = "INSERT INTO CurrentQueue (Position, FilePath, IsCompleted) VALUES (@position, @path, @completed)";
                        insertQueueCmd.Parameters.AddWithValue("@position", i);
                        insertQueueCmd.Parameters.AddWithValue("@path", filePath);
                        insertQueueCmd.Parameters.AddWithValue("@completed", isCompleted ? 1 : 0);
                    insertQueueCmd.ExecuteNonQuery();
                    }
                    var updateEqCmd = connection.CreateCommand();
                    updateEqCmd.CommandText = "UPDATE EqualizerSettings SET Band80Hz = @b80, Band240Hz = @b240, Band750Hz = @b750, Band2200Hz = @b2200, Band6600Hz = @b6600 WHERE Id = 1";
                    updateEqCmd.Parameters.AddWithValue("@b80", band80Hz);
                    updateEqCmd.Parameters.AddWithValue("@b240", band240Hz);
                    updateEqCmd.Parameters.AddWithValue("@b750", band750Hz);
                    updateEqCmd.Parameters.AddWithValue("@b2200", band2200Hz);
                    updateEqCmd.Parameters.AddWithValue("@b6600", band6600Hz);
                    updateEqCmd.ExecuteNonQuery();

                    transaction.Commit();
                    isDirty = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings to database: {ex.Message}");
                }
            }
        }
        
        public void FlushToDisk()
        {
            isDirty = true;
            SaveAllSettingsToDatabase();
        }

        public double GetVolumePercent()
        {
            lock (cacheLock)
            {
                return volumePercent;
            }
        }

        public void SaveVolumePercent(double percent)
        {
            var clamped = Math.Max(0, Math.Min(100, percent));
            lock (cacheLock)
            {
                if (Math.Abs(volumePercent - clamped) > 0.01)
                {
                    volumePercent = clamped;
                    isDirty = true;
                }
            }
        }
        private static int GetIntSetting(SqliteConnection connection, string key, int defaultValue)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            return result != null && int.TryParse(result.ToString(), out var value) ? value : defaultValue;
        }

        private static double GetDoubleSetting(SqliteConnection connection, string key, double defaultValue)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            return result != null && double.TryParse(result.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
        }

        private static string? GetStringSetting(SqliteConnection connection, string key, string? defaultValue)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            return result?.ToString() ?? defaultValue;
        }

        private static bool GetBoolSetting(SqliteConnection connection, string key, bool defaultValue)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
            command.Parameters.AddWithValue("@key", key);
            var result = command.ExecuteScalar();
            return result != null && bool.TryParse(result.ToString(), out var value) ? value : defaultValue;
        }

        private static void SetSetting(SqliteConnection connection, string key, string value)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @value)";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            command.ExecuteNonQuery();
        }

        public void UpdateFileCount(int count)
        {
            lock (cacheLock)
            {
                if (fileCount == count) { return; }
                fileCount = count;
                isDirty = true;
            }
        }

        public int GetLastPlayedIndex()
        {
            lock (cacheLock)
            {
                return lastPlayedIndex;
            }
        }

        public void UpdateLastPlayedIndex(int index)
        {
            lock (cacheLock)
            {
                if (lastPlayedIndex != index)
                {
                    lastPlayedIndex = index;
                    isDirty = true;
                }
            }
        }

        public void SaveAllDurations(Dictionary<string, string> durations)
        {
            lock (cacheLock)
            {
                foreach (var kvp in durations)
                {
                    if (!songDurations.TryGetValue(kvp.Key, out var existing) || existing != kvp.Value)
                    {
                        songDurations[kvp.Key] = kvp.Value;
                        isDirty = true;
                    }
                }
            }
        }

        public Dictionary<string, string> GetAllDurations()
        {
            lock (cacheLock)
            {
                return new Dictionary<string, string>(songDurations);
            }
        }

        public void IncrementPlayCount(string filePath)
        {
            lock (cacheLock)
            {
                if (!playCounts.TryAdd(filePath, 1))
                {
                    playCounts[filePath]++;
                }

                isDirty = true;
            }
        }

        public Dictionary<string, int> GetAllPlayCounts()
        {
            lock (cacheLock)
            {
                return new Dictionary<string, int>(playCounts);
            }
        }

        public void SaveCurrentPlaybackState(string? songPath, double positionSeconds)
        {
            lock (cacheLock)
            {
                currentSongPath = songPath;
                currentSongPositionSeconds = positionSeconds;
                isDirty = true;
            }
        }

        public (string? songPath, double positionSeconds) GetCurrentPlaybackState()
        {
            lock (cacheLock)
            {
                return (currentSongPath, currentSongPositionSeconds);
            }
        }

        public void SaveCurrentQueue(List<string> queuePaths, bool isShuffled)
        {
            lock (cacheLock)
            {
                bool changed = false;
                
                if (isQueueShuffled != isShuffled)
                {
                    isQueueShuffled = isShuffled;
                    changed = true;
                }

                if (!currentQueue.SequenceEqual(queuePaths))
                {
                    currentQueue = new List<string>(queuePaths);
                    changed = true;
                }

                if (changed)
                {
                    isDirty = true;
                }
            }
        }

        public (List<string> queuePaths, bool isShuffled) GetCurrentQueue()
        {
            lock (cacheLock)
            {
                return ([..currentQueue], isQueueShuffled);
            }
        }
        public int GetDatabaseSongCount()
        {
            lock (cacheLock)
            {
                return songDurations.Count;
            }
        }
        public (float band80, float band240, float band750, float band2200, float band6600) GetEqualizerSettings()
        {
            lock (cacheLock)
            {
                return (band80Hz, band240Hz, band750Hz, band2200Hz, band6600Hz);
            }
        }

        public void SaveEqualizerSettings(float band80, float band240, float band750, float band2200, float band6600)
        {
            lock (cacheLock)
            {
                bool changed = false;
                
                if (Math.Abs(band80Hz - band80) > 0.01f)
                {
                    band80Hz = band80;
                    changed = true;
                }
                
                if (Math.Abs(band240Hz - band240) > 0.01f)
                {
                    band240Hz = band240;
                    changed = true;
                }
                
                if (Math.Abs(band750Hz - band750) > 0.01f)
                {
                    band750Hz = band750;
                    changed = true;
                }
                
                if (Math.Abs(band2200Hz - band2200) > 0.01f)
                {
                    band2200Hz = band2200;
                    changed = true;
                }
                
                if (Math.Abs(band6600Hz - band6600) > 0.01f)
                {
                    band6600Hz = band6600;
                    changed = true;
                }
                
                if (changed)
                {
                    isDirty = true;
                }
            }
        }

        public string GetMusicFolderPath()
        {
            lock (cacheLock)
            {
                return musicFolderPath;
            }
        }

        public void SaveMusicFolderPath(string path)
        {
            lock (cacheLock)
            {
                if (musicFolderPath != path)
                {
                    musicFolderPath = path;
                    isDirty = true;
                }
            }
        }

        public bool GetAutoPlayOnStartup()
        {
            lock (cacheLock)
            {
                return autoPlayOnStartup;
            }
        }

        public void SaveAutoPlayOnStartup(bool enabled)
        {
            lock (cacheLock)
            {
                if (autoPlayOnStartup != enabled)
                {
                    autoPlayOnStartup = enabled;
                    isDirty = true;
                }
            }
        }

        public void ClearPlayHistory()
        {
            lock (cacheLock)
            {
                playCounts.Clear();
                isDirty = true;

                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM PlayCounts";
                command.ExecuteNonQuery();
            }
        }

        public void ResetDatabase()
        {
            lock (cacheLock)
            {
                fileCount = 0;
                lastPlayedIndex = -1;
                currentSongPath = null;
                currentSongPositionSeconds = 0;
                currentQueue.Clear();
                isQueueShuffled = false;
                songDurations.Clear();
                playCounts.Clear();
                volumePercent = 50.0;
                band80Hz = 0;
                band240Hz = 0;
                band750Hz = 0;
                band2200Hz = 0;
                band6600Hz = 0;
                autoPlayOnStartup = false;
                
                isDirty = false;

                using var connection = new SqliteConnection($"Data Source={databasePath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = """
                    DROP TABLE IF EXISTS Settings;
                    DROP TABLE IF EXISTS SongDurations;
                    DROP TABLE IF EXISTS PlayCounts;
                    DROP TABLE IF EXISTS CurrentQueue;
                    DROP TABLE IF EXISTS EqualizerSettings;
                    """;
                command.ExecuteNonQuery();

                InitializeDatabase();
                
                if (StringValidator.HasValue(musicFolderPath))
                {
                    SaveMusicFolderPath(musicFolderPath);
                    FlushToDisk();
                }
            }
        }

        public string GetDatabaseFilePath()
        {
            return databasePath;
        }

        public int GetTotalPlayCount()
        {
            lock (cacheLock)
            {
                return playCounts.Values.Sum();
            }
        }

        public string? GetDiscordClientId()
        {
            lock (cacheLock)
            {
                return discordClientId;
            }
        }

        public void SaveDiscordClientId(string? clientId)
        {
            lock (cacheLock)
            {
                if (discordClientId != clientId)
                {
                    discordClientId = clientId;
                    isDirty = true;
                }
            }
        }

        public string GetSongNameFormat()
        {
            lock (cacheLock)
            {
                return songNameFormat;
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
                saveTimer.Dispose();
                FlushToDisk();
                
                disposed = true;
            }
        }
    }
}

