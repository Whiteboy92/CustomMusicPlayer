using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Services.Settings;
using MusicPlayer.Validation;

namespace MusicPlayer.Services;

public class SqliteSettingsService : ISettingsService
{
    private readonly string databasePath;
    private readonly object cacheLock = new();
    private readonly Timer saveTimer;

    private bool isDirty;
    private bool disposed;

    private int fileCount;
    private int lastPlayedIndex;
    private int? currentPlaylistId;

    private readonly Dictionary<int, string> playlistCurrentSongs = [];
    private readonly Dictionary<int, double> playlistCurrentPositions = [];
    private List<string> currentQueue = [];
    private bool isQueueShuffled;

    private readonly Dictionary<string, string> songDurations = [];
    private readonly Dictionary<string, int> playCounts = [];
    private readonly Dictionary<string, Dictionary<int, int>> playlistPlayCounts = [];
    private readonly Dictionary<string, double> cumulativePlayedTimes = [];

    // Dirty tracking (P1): only changed rows are written on flush, instead of
    // wiping and re-inserting every table on every save.
    private bool scalarDirty;   // Settings key/value table
    private bool eqDirty;       // EqualizerSettings row
    private bool queueDirty;    // CurrentQueue (order matters -> rewritten wholesale)
    private readonly HashSet<string> dirtyDurations = [];
    private readonly HashSet<string> dirtyPlayCounts = [];
    private readonly HashSet<(string filePath, int playlistId)> dirtyPlaylistPlayCounts = [];
    private readonly HashSet<string> dirtyCumulative = [];
    private readonly HashSet<int> dirtyPlaybackStates = [];

    // Disk I/O runs under saveLock, never under cacheLock. A save snapshots the
    // dirty cache under cacheLock (microseconds) then writes the snapshot under
    // saveLock, so UI-thread setters/flushes never block on the actual DB write
    // (which can take ~1s for the initial duration batch).
    private readonly object saveLock = new();

    private double volumePercent = 3.0; // 0â€“100
    private float band80Hz;
    private float band240Hz;
    private float band750Hz;
    private float band2200Hz;
    private float band6600Hz;

    private string musicFolderPath = string.Empty;
    private bool autoPlayOnStartup;
    private bool autoPlayNext = true;
    private string? discordClientId;
    private string songNameFormat = "SongArtist"; // Default: "Song - Artist"
        
    private string defaultQueueName = "Default Queue (All Songs)";
    private string? defaultQueueGenre;
    private string? defaultQueueTags;

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

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private void InitializeDatabase()
    {
        using var connection = CreateConnection();

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

                              CREATE TABLE IF NOT EXISTS PlaylistPlayCounts (
                                  FilePath TEXT NOT NULL,
                                  PlaylistId INTEGER,
                                  PlayCount INTEGER NOT NULL DEFAULT 0,
                                  PRIMARY KEY (FilePath, PlaylistId)
                              );

                              CREATE TABLE IF NOT EXISTS CumulativePlayedTimes (
                                  FilePath TEXT PRIMARY KEY,
                                  CumulativeSeconds REAL NOT NULL DEFAULT 0.0
                              );

                              CREATE TABLE IF NOT EXISTS Playlists (
                                  Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  Name TEXT NOT NULL,
                                  Genre TEXT,
                                  Tags TEXT,
                                  CreatedDate TEXT NOT NULL
                              );

                              CREATE TABLE IF NOT EXISTS PlaylistSongs (
                                  PlaylistId INTEGER NOT NULL,
                                  SongFilePath TEXT NOT NULL,
                                  Position INTEGER NOT NULL,
                                  FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                                  PRIMARY KEY (PlaylistId, SongFilePath)
                              );

                              CREATE INDEX IF NOT EXISTS idx_playlist_songs ON PlaylistSongs(PlaylistId);

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

                              CREATE TABLE IF NOT EXISTS PlaylistPlaybackState (
                                  PlaylistId INTEGER PRIMARY KEY,
                                  CurrentSongPath TEXT,
                                  CurrentPosition REAL NOT NULL DEFAULT 0.0
                              );
                              """;
        command.ExecuteNonQuery();

        var checkColumnCmd = connection.CreateCommand();
        checkColumnCmd.CommandText = "PRAGMA table_info(CurrentQueue)";
        using var reader = checkColumnCmd.ExecuteReader();

        var hasIsCompletedColumn = false;
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

        // Normalize legacy default-queue rows (PlaylistId NULL) to -1 so the
        // per-row UPSERT on save has a stable, matchable primary key.
        var migrateCmd = connection.CreateCommand();
        migrateCmd.CommandText = "UPDATE OR IGNORE PlaylistPlayCounts SET PlaylistId = -1 WHERE PlaylistId IS NULL";
        migrateCmd.ExecuteNonQuery();
    }

    private void LoadAllSettingsIntoCache()
    {
        lock (cacheLock)
        {
            using var connection = CreateConnection();

            fileCount = GetIntSetting(connection, "FileCount", 0);
            lastPlayedIndex = GetIntSetting(connection, "LastPlayedIndex", 0);
            currentPlaylistId = GetNullableIntSetting(connection, "CurrentPlaylistId");
            isQueueShuffled = GetBoolSetting(connection, "IsQueueShuffled", false);
            volumePercent = GetDoubleSetting(connection, "VolumePercent", 3.0);

            LoadDurations(connection);
            LoadPlayCounts(connection);
            LoadPlaylistPlayCounts(connection);
            LoadCumulativeTimes(connection);
            LoadPlaylistPlaybackStates(connection);
            LoadQueue(connection);
            LoadEqualizer(connection);

            musicFolderPath = GetStringSetting(connection, SettingsKeys.MusicFolderPath, string.Empty) ?? string.Empty;
            autoPlayOnStartup = GetBoolSetting(connection, SettingsKeys.AutoPlayOnStartup, false);
            autoPlayNext = GetBoolSetting(connection, SettingsKeys.AutoPlayNext, true);
            discordClientId = GetStringSetting(connection, SettingsKeys.DiscordClientId, null);
            songNameFormat = GetStringSetting(connection, SettingsKeys.SongNameFormat, "SongArtist") ?? "SongArtist";
                
            defaultQueueName = GetStringSetting(connection, "DefaultQueueName", "Default Queue (All Songs)") ?? "Default Queue (All Songs)";
            defaultQueueGenre = GetStringSetting(connection, "DefaultQueueGenre", null);
            defaultQueueTags = GetStringSetting(connection, "DefaultQueueTags", null);
        }
    }

    #region Load Helpers
    private void LoadDurations(SqliteConnection connection)
    {
        songDurations.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath, Duration FROM SongDurations";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            songDurations[reader.GetString(0)] = reader.GetString(1);
        }
    }

    private void LoadPlayCounts(SqliteConnection connection)
    {
        playCounts.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath, PlayCount FROM PlayCounts";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            playCounts[reader.GetString(0)] = reader.GetInt32(1);
        }
    }

    private void LoadPlaylistPlayCounts(SqliteConnection connection)
    {
        playlistPlayCounts.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath, PlaylistId, PlayCount FROM PlaylistPlayCounts";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var filePath = reader.GetString(0);
            var playlistId = reader.IsDBNull(1) ? -1 : reader.GetInt32(1);
            var playCount = reader.GetInt32(2);

            if (!playlistPlayCounts.TryGetValue(filePath, out var value))
            {
                value = [];
                playlistPlayCounts[filePath] = value;
            }

            value[playlistId] = playCount;
        }
    }

    private void LoadCumulativeTimes(SqliteConnection connection)
    {
        cumulativePlayedTimes.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath, CumulativeSeconds FROM CumulativePlayedTimes";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cumulativePlayedTimes[reader.GetString(0)] = reader.GetDouble(1);
        }
    }

    private void LoadPlaylistPlaybackStates(SqliteConnection connection)
    {
        playlistCurrentSongs.Clear();
        playlistCurrentPositions.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT PlaylistId, CurrentSongPath, CurrentPosition FROM PlaylistPlaybackState";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var playlistId = reader.GetInt32(0);
            var songPath = reader.IsDBNull(1) ? null : reader.GetString(1);
            var position = reader.GetDouble(2);

            if (songPath != null)
            {
                playlistCurrentSongs[playlistId] = songPath;
                playlistCurrentPositions[playlistId] = position;
            }
        }
    }

    private void LoadQueue(SqliteConnection connection)
    {
        currentQueue.Clear();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT FilePath, IsCompleted FROM CurrentQueue ORDER BY Position";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var filePath = reader.GetString(0);
            var isCompleted = reader.GetInt32(1) == 1;

            currentQueue.Add($"{filePath}|{isCompleted}");
        }
    }

    private void LoadEqualizer(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Band80Hz, Band240Hz, Band750Hz, Band2200Hz, Band6600Hz FROM EqualizerSettings WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            band80Hz = reader.GetFloat(0);
            band240Hz = reader.GetFloat(1);
            band750Hz = reader.GetFloat(2);
            band2200Hz = reader.GetFloat(3);
            band6600Hz = reader.GetFloat(4);
        }
    }
    #endregion

    private void AutoSaveCallback(object? state)
    {
        // Already on a threadpool thread; snapshot + write inline.
        SaveAllSettingsToDatabase();
    }

    public void FlushToDisk()
    {
        // Snapshot is cheap and grabs cacheLock only briefly; the disk write is
        // offloaded so UI-thread callers (volume/autoplay changes) never freeze
        // on a large pending batch. saveLock serializes with the autosave timer.
        var pending = SnapshotPendingWrite();
        if (pending == null) return;

        Task.Run(() => WritePending(pending));
    }

    private static int GetIntSetting(SqliteConnection connection, string key, int defaultValue)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        return result != null && int.TryParse(result.ToString(), out var value) ? value : defaultValue;
    }

    private static int? GetNullableIntSetting(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        if (result != null && int.TryParse(result.ToString(), out var value))
        {
            return value;
        }
        return null;
    }

    private static double GetDoubleSetting(SqliteConnection connection, string key, double defaultValue)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        return result != null && double.TryParse(result.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    private static string? GetStringSetting(SqliteConnection connection, string key, string? defaultValue)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    private static bool GetBoolSetting(SqliteConnection connection, string key, bool defaultValue)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);
        var result = command.ExecuteScalar();
        return result != null && bool.TryParse(result.ToString(), out var value) ? value : defaultValue;
    }

    private static void SetSetting(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @value)";
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    private void SaveAllSettingsToDatabase()
    {
        var pending = SnapshotPendingWrite();
        if (pending == null) return;

        WritePending(pending);
    }

    // Copies every dirty entry out of the cache and clears the dirty flags, all
    // under cacheLock. Returns null if nothing is dirty. The returned snapshot is
    // self-contained, so the disk write can run with no cache access.
    private PendingWrite? SnapshotPendingWrite()
    {
        lock (cacheLock)
        {
            if (!isDirty) return null;

            var pending = new PendingWrite();

            if (scalarDirty)
            {
                pending.Scalars =
                [
                    ("FileCount", fileCount.ToString()),
                    ("LastPlayedIndex", lastPlayedIndex.ToString()),
                    ("CurrentPlaylistId", currentPlaylistId?.ToString() ?? "-1"),
                    ("IsQueueShuffled", isQueueShuffled.ToString()),
                    ("VolumePercent", volumePercent.ToString(CultureInfo.InvariantCulture)),
                    (SettingsKeys.MusicFolderPath, musicFolderPath),
                    (SettingsKeys.AutoPlayOnStartup, autoPlayOnStartup.ToString()),
                    (SettingsKeys.AutoPlayNext, autoPlayNext.ToString()),
                    (SettingsKeys.DiscordClientId, discordClientId ?? string.Empty),
                    (SettingsKeys.SongNameFormat, songNameFormat),
                    ("DefaultQueueName", defaultQueueName),
                    ("DefaultQueueGenre", defaultQueueGenre ?? string.Empty),
                    ("DefaultQueueTags", defaultQueueTags ?? string.Empty),
                ];
                scalarDirty = false;
            }

            foreach (var key in dirtyDurations)
            {
                if (songDurations.TryGetValue(key, out var value))
                    pending.Durations.Add((key, value));
            }
            dirtyDurations.Clear();

            foreach (var key in dirtyPlayCounts)
            {
                if (playCounts.TryGetValue(key, out var value))
                    pending.PlayCounts.Add((key, value));
            }
            dirtyPlayCounts.Clear();

            foreach (var (filePath, pid) in dirtyPlaylistPlayCounts)
            {
                if (playlistPlayCounts.TryGetValue(filePath, out var inner) &&
                    inner.TryGetValue(pid, out var value))
                    pending.PlaylistPlayCounts.Add((filePath, pid, value));
            }
            dirtyPlaylistPlayCounts.Clear();

            foreach (var key in dirtyCumulative)
            {
                if (cumulativePlayedTimes.TryGetValue(key, out var value))
                    pending.Cumulative.Add((key, value));
            }
            dirtyCumulative.Clear();

            foreach (var id in dirtyPlaybackStates)
            {
                if (playlistCurrentSongs.TryGetValue(id, out var songPath) &&
                    playlistCurrentPositions.TryGetValue(id, out var position))
                    pending.PlaybackStates.Add((id, songPath, position));
                else
                    pending.PlaybackStateDeletes.Add(id);
            }
            dirtyPlaybackStates.Clear();

            if (queueDirty)
            {
                pending.Queue = [.. currentQueue];
                queueDirty = false;
            }

            if (eqDirty)
            {
                pending.Equalizer = (band80Hz, band240Hz, band750Hz, band2200Hz, band6600Hz);
                eqDirty = false;
            }

            isDirty = false;
            return pending;
        }
    }

    // Writes a snapshot to disk. Runs under saveLock (serializes the autosave
    // timer, UI flushes, and Dispose) but never under cacheLock, so the UI thread
    // is never blocked by the actual I/O.
    private void WritePending(PendingWrite pending)
    {
        lock (saveLock)
        {
            try
            {
                using var connection = CreateConnection();
                using var transaction = connection.BeginTransaction();

                if (pending.Scalars != null)
                {
                    foreach (var (key, value) in pending.Scalars)
                        SetSetting(connection, key, value);
                }

                if (pending.Durations.Count > 0)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO SongDurations (FilePath, Duration) VALUES (@path, @duration)";
                    var path = cmd.Parameters.Add("@path", SqliteType.Text);
                    var duration = cmd.Parameters.Add("@duration", SqliteType.Text);
                    foreach (var (key, value) in pending.Durations)
                    {
                        path.Value = key;
                        duration.Value = value;
                        cmd.ExecuteNonQuery();
                    }
                }

                if (pending.PlayCounts.Count > 0)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO PlayCounts (FilePath, PlayCount) VALUES (@path, @count)";
                    var path = cmd.Parameters.Add("@path", SqliteType.Text);
                    var count = cmd.Parameters.Add("@count", SqliteType.Integer);
                    foreach (var (key, value) in pending.PlayCounts)
                    {
                        path.Value = key;
                        count.Value = value;
                        cmd.ExecuteNonQuery();
                    }
                }

                if (pending.PlaylistPlayCounts.Count > 0)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO PlaylistPlayCounts (FilePath, PlaylistId, PlayCount) VALUES (@path, @playlistId, @count)";
                    var path = cmd.Parameters.Add("@path", SqliteType.Text);
                    var playlistId = cmd.Parameters.Add("@playlistId", SqliteType.Integer);
                    var count = cmd.Parameters.Add("@count", SqliteType.Integer);
                    foreach (var (filePath, pid, value) in pending.PlaylistPlayCounts)
                    {
                        path.Value = filePath;
                        playlistId.Value = pid;
                        count.Value = value;
                        cmd.ExecuteNonQuery();
                    }
                }

                if (pending.Cumulative.Count > 0)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO CumulativePlayedTimes (FilePath, CumulativeSeconds) VALUES (@path, @cumulative)";
                    var path = cmd.Parameters.Add("@path", SqliteType.Text);
                    var cumulative = cmd.Parameters.Add("@cumulative", SqliteType.Real);
                    foreach (var (key, value) in pending.Cumulative)
                    {
                        path.Value = key;
                        cumulative.Value = value;
                        cmd.ExecuteNonQuery();
                    }
                }

                if (pending.PlaybackStates.Count > 0 || pending.PlaybackStateDeletes.Count > 0)
                {
                    using var upsert = connection.CreateCommand();
                    upsert.CommandText = "INSERT OR REPLACE INTO PlaylistPlaybackState (PlaylistId, CurrentSongPath, CurrentPosition) VALUES (@playlistId, @songPath, @position)";
                    var upId = upsert.Parameters.Add("@playlistId", SqliteType.Integer);
                    var upSong = upsert.Parameters.Add("@songPath", SqliteType.Text);
                    var upPos = upsert.Parameters.Add("@position", SqliteType.Real);
                    foreach (var (id, songPath, position) in pending.PlaybackStates)
                    {
                        upId.Value = id;
                        upSong.Value = songPath;
                        upPos.Value = position;
                        upsert.ExecuteNonQuery();
                    }

                    using var delete = connection.CreateCommand();
                    delete.CommandText = "DELETE FROM PlaylistPlaybackState WHERE PlaylistId = @playlistId";
                    var delId = delete.Parameters.Add("@playlistId", SqliteType.Integer);
                    foreach (var id in pending.PlaybackStateDeletes)
                    {
                        delId.Value = id;
                        delete.ExecuteNonQuery();
                    }
                }

                if (pending.Queue != null)
                {
                    using (var deleteQueueCmd = connection.CreateCommand())
                    {
                        deleteQueueCmd.CommandText = "DELETE FROM CurrentQueue";
                        deleteQueueCmd.ExecuteNonQuery();
                    }

                    using var insertQueueCmd = connection.CreateCommand();
                    insertQueueCmd.CommandText = "INSERT INTO CurrentQueue (Position, FilePath, IsCompleted) VALUES (@position, @path, @completed)";
                    var pos = insertQueueCmd.Parameters.Add("@position", SqliteType.Integer);
                    var path = insertQueueCmd.Parameters.Add("@path", SqliteType.Text);
                    var completed = insertQueueCmd.Parameters.Add("@completed", SqliteType.Integer);
                    for (var i = 0; i < pending.Queue.Count; i++)
                    {
                        var parts = pending.Queue[i].Split('|');
                        var filePath = parts[0];
                        var isCompleted = parts.Length > 1 && bool.Parse(parts[1]);

                        pos.Value = i;
                        path.Value = filePath;
                        completed.Value = isCompleted ? 1 : 0;
                        insertQueueCmd.ExecuteNonQuery();
                    }
                }

                if (pending.Equalizer != null)
                {
                    var (b80, b240, b750, b2200, b6600) = pending.Equalizer.Value;
                    using var updateEqCmd = connection.CreateCommand();
                    updateEqCmd.CommandText = "UPDATE EqualizerSettings SET Band80Hz = @b80, Band240Hz = @b240, Band750Hz = @b750, Band2200Hz = @b2200, Band6600Hz = @b6600 WHERE Id = 1";
                    updateEqCmd.Parameters.AddWithValue("@b80", b80);
                    updateEqCmd.Parameters.AddWithValue("@b240", b240);
                    updateEqCmd.Parameters.AddWithValue("@b750", b750);
                    updateEqCmd.Parameters.AddWithValue("@b2200", b2200);
                    updateEqCmd.Parameters.AddWithValue("@b6600", b6600);
                    updateEqCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings to database: {ex.Message}");
            }
        }
    }

    // Self-contained snapshot of everything dirty at one instant. Built under
    // cacheLock, consumed by WritePending with no further cache access.
    private sealed class PendingWrite
    {
        public (string Key, string Value)[]? Scalars;
        public readonly List<(string Path, string Duration)> Durations = [];
        public readonly List<(string Path, int Count)> PlayCounts = [];
        public readonly List<(string Path, int PlaylistId, int Count)> PlaylistPlayCounts = [];
        public readonly List<(string Path, double Seconds)> Cumulative = [];
        public readonly List<(int Id, string SongPath, double Position)> PlaybackStates = [];
        public readonly List<int> PlaybackStateDeletes = [];
        public List<string>? Queue;
        public (float B80, float B240, float B750, float B2200, float B6600)? Equalizer;
    }
        
    public void Dispose()
    {
        if (disposed) return;

        saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        saveTimer.Dispose();

        // Synchronous on shutdown: snapshot + write inline. WritePending takes
        // saveLock, so this also waits for any in-flight offloaded flush.
        SaveAllSettingsToDatabase();

        disposed = true;
    }


    public void SaveCurrentPlaybackState(string? songPath, double positionSeconds, int? playlistId)
    {
        lock (cacheLock)
        {
            var key = playlistId ?? -1;
                
            if (songPath != null)
            {
                playlistCurrentSongs[key] = songPath;
                playlistCurrentPositions[key] = positionSeconds;
            }
            else
            {
                playlistCurrentSongs.Remove(key);
                playlistCurrentPositions.Remove(key);
            }

            dirtyPlaybackStates.Add(key);
            isDirty = true;
        }
    }

    public (string? songPath, double positionSeconds) GetCurrentPlaybackState(int? playlistId)
    {
        lock (cacheLock)
        {
            var key = playlistId ?? -1;
                
            if (playlistCurrentSongs.TryGetValue(key, out var songPath) &&
                playlistCurrentPositions.TryGetValue(key, out var position))
            {
                return (songPath, position);
            }
                
            return (null, 0);
        }
    }

    public void SaveCurrentQueue(List<string> queuePaths, bool isShuffled)
    {
        lock (cacheLock)
        {
            var changed = false;

            if (isQueueShuffled != isShuffled)
            {
                isQueueShuffled = isShuffled;
                scalarDirty = true;   // IsQueueShuffled lives in the Settings table
                changed = true;
            }

            if (!currentQueue.SequenceEqual(queuePaths))
            {
                currentQueue = [.. queuePaths];
                queueDirty = true;
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

    public void UpdateFileCount(int count)
    {
        lock (cacheLock)
        {
            if (fileCount == count) return;
            fileCount = count;
            scalarDirty = true;
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
                scalarDirty = true;
                isDirty = true;
            }
        }
    }

    public void SaveCurrentPlaylistId(int? playlistId)
    {
        lock (cacheLock)
        {
            if (currentPlaylistId != playlistId)
            {
                currentPlaylistId = playlistId;
                scalarDirty = true;
                isDirty = true;
            }
        }
    }

    public int? GetCurrentPlaylistId()
    {
        lock (cacheLock)
        {
            return currentPlaylistId;
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
                    dirtyDurations.Add(kvp.Key);
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

    private void IncrementPlayCount(string filePath)
    {
        lock (cacheLock)
        {
            if (!playCounts.TryAdd(filePath, 1))
            {
                playCounts[filePath]++;
            }

            dirtyPlayCounts.Add(filePath);
            isDirty = true;
        }
    }

    public void IncrementPlayCountForPlaylist(string filePath, int? playlistId)
    {
        lock (cacheLock)
        {
            IncrementPlayCount(filePath);

            var key = playlistId ?? -1;

            if (!playlistPlayCounts.TryGetValue(filePath, out var value))
            {
                value = [];
                playlistPlayCounts[filePath] = value;
            }

            if (!value.TryAdd(key, 1))
            {
                value[key]++;
            }

            dirtyPlaylistPlayCounts.Add((filePath, key));
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

    public Dictionary<string, int> GetPlayCountsForPlaylist(int? playlistId)
    {
        lock (cacheLock)
        {
            var key = playlistId ?? -1;
                
            Dictionary<string, int> result = [];
            foreach (var fileEntry in playlistPlayCounts)
            {
                if (fileEntry.Value.TryGetValue(key, out var count))
                {
                    result[fileEntry.Key] = count;
                }
            }
            return result;
        }
    }

    public void SaveCumulativePlayedTime(string songPath, double cumulativeSeconds)
    {
        lock (cacheLock)
        {
            cumulativePlayedTimes[songPath] = cumulativeSeconds;
            dirtyCumulative.Add(songPath);
            isDirty = true;
        }
    }

    public double GetCumulativePlayedTime(string songPath)
    {
        lock (cacheLock)
        {
            return cumulativePlayedTimes.GetValueOrDefault(songPath, 0.0);
        }
    }

    public int GetDatabaseSongCount()
    {
        lock (cacheLock)
        {
            return songDurations.Count;
        }
    }

    public int GetTotalPlayCount()
    {
        lock (cacheLock)
        {
            return playCounts.Values.Sum();
        }
    }

    public int GetTotalPlayCountForPlaylist(int? playlistId)
    {
        lock (cacheLock)
        {
            var key = playlistId ?? -1;
                
            var total = 0;
            foreach (var fileEntry in playlistPlayCounts.Values)
            {
                if (fileEntry.TryGetValue(key, out var count))
                {
                    total += count;
                }
            }
            return total;
        }
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
                scalarDirty = true;
                isDirty = true;
            }
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
            var changed = false;
                
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
                eqDirty = true;
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
                scalarDirty = true;
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
                scalarDirty = true;
                isDirty = true;
            }
        }
    }

    public bool GetAutoPlayNext()
    {
        lock (cacheLock)
        {
            return autoPlayNext;
        }
    }

    public void SaveAutoPlayNext(bool enabled)
    {
        lock (cacheLock)
        {
            if (autoPlayNext != enabled)
            {
                autoPlayNext = enabled;
                scalarDirty = true;
                isDirty = true;
            }
        }
    }

    public void ClearPlayHistory()
    {
        lock (cacheLock)
        {
            playCounts.Clear();
            playlistPlayCounts.Clear();
            cumulativePlayedTimes.Clear();

            // Tables are emptied directly here, so drop any pending per-row
            // upserts that would otherwise reference now-removed cache entries.
            dirtyPlayCounts.Clear();
            dirtyPlaylistPlayCounts.Clear();
            dirtyCumulative.Clear();

            using var connection = CreateConnection();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PlayCounts; DELETE FROM PlaylistPlayCounts; DELETE FROM CumulativePlayedTimes;";
            command.ExecuteNonQuery();
        }
    }

    public void ResetDatabase()
    {
        lock (cacheLock)
        {
            // Full factory reset. musicFolderPath and discordClientId are kept
            // (user config, not statistics) and re-persisted after re-init.
            fileCount = 0;
            lastPlayedIndex = -1;
            currentPlaylistId = -1;
            playlistCurrentSongs.Clear();
            playlistCurrentPositions.Clear();
            currentQueue.Clear();
            isQueueShuffled = false;
            songDurations.Clear();
            playCounts.Clear();
            playlistPlayCounts.Clear();
            cumulativePlayedTimes.Clear();
            volumePercent = 50.0;
            band80Hz = 0;
            band240Hz = 0;
            band750Hz = 0;
            band2200Hz = 0;
            band6600Hz = 0;
            autoPlayOnStartup = false;
            autoPlayNext = true;
            songNameFormat = "SongArtist";
            defaultQueueName = "Default Queue (All Songs)";
            defaultQueueGenre = null;
            defaultQueueTags = null;

            // Caches and tables are wiped; drop pending per-row writes and mark
            // the scalar/eq defaults dirty so FlushToDisk persists the reset state.
            dirtyDurations.Clear();
            dirtyPlayCounts.Clear();
            dirtyPlaylistPlayCounts.Clear();
            dirtyCumulative.Clear();
            dirtyPlaybackStates.Clear();
            queueDirty = false;
            scalarDirty = true;
            eqDirty = true;
            isDirty = true;

            using var connection = CreateConnection();

            using var command = connection.CreateCommand();
            command.CommandText = """
                                  DROP TABLE IF EXISTS Settings;
                                  DROP TABLE IF EXISTS SongDurations;
                                  DROP TABLE IF EXISTS PlayCounts;
                                  DROP TABLE IF EXISTS PlaylistPlayCounts;
                                  DROP TABLE IF EXISTS CumulativePlayedTimes;
                                  DROP TABLE IF EXISTS CurrentQueue;
                                  DROP TABLE IF EXISTS EqualizerSettings;
                                  DROP TABLE IF EXISTS PlaylistPlaybackState;
                                  DROP TABLE IF EXISTS PlaylistSongs;
                                  DROP TABLE IF EXISTS Playlists;
                                  """;
            command.ExecuteNonQuery();

            InitializeDatabase();

            if (StringValidator.HasValue(musicFolderPath))
            {
                SaveMusicFolderPath(musicFolderPath);
            }
            FlushToDisk();
        }
    }

    public string GetDatabaseFilePath() => databasePath;

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
                scalarDirty = true;
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

    public void CreatePlaylist(string name, string? genre, string? tags, List<string> songFilePaths)
    {
        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
                INSERT INTO Playlists (Name, Genre, Tags, CreatedDate) 
                VALUES (@name, @genre, @tags, @date);
                SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@name", name);
        insertCmd.Parameters.AddWithValue("@genre", (object?)genre ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@tags", (object?)tags ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("o"));

        var playlistId = Convert.ToInt32(insertCmd.ExecuteScalar());

        for (var i = 0; i < songFilePaths.Count; i++)
        {
            using var songCmd = connection.CreateCommand();
            songCmd.CommandText = @"
                    INSERT INTO PlaylistSongs (PlaylistId, SongFilePath, Position) 
                    VALUES (@playlistId, @filePath, @position)";
            songCmd.Parameters.AddWithValue("@playlistId", playlistId);
            songCmd.Parameters.AddWithValue("@filePath", songFilePaths[i]);
            songCmd.Parameters.AddWithValue("@position", i);
            songCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<Playlist> GetAllPlaylists()
    {
        List<Playlist> playlists = [];
        var byId = new Dictionary<int, Playlist>();

        using var connection = CreateConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT p.Id, p.Name, p.Genre, p.Tags, p.CreatedDate, s.SongFilePath
                          FROM Playlists p
                          LEFT JOIN PlaylistSongs s ON s.PlaylistId = p.Id
                          ORDER BY p.CreatedDate DESC, s.Position
                          """;
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            if (!byId.TryGetValue(id, out var playlist))
            {
                playlist = new Playlist
                {
                    Id = id,
                    Name = reader.GetString(1),
                    Genre = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Tags = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedDate = DateTime.Parse(reader.GetString(4)),
                };
                byId[id] = playlist;
                playlists.Add(playlist);
            }

            if (!reader.IsDBNull(5))
            {
                playlist.SongFilePaths.Add(reader.GetString(5));
            }
        }

        return playlists;
    }

    public Playlist? GetPlaylistById(int id)
    {
        // Special handling for default queue
        if (id == -1)
        {
            lock (cacheLock)
            {
                return new Playlist
                {
                    Id = -1,
                    Name = defaultQueueName,
                    Genre = defaultQueueGenre,
                    Tags = defaultQueueTags,
                    CreatedDate = DateTime.Now,
                    SongFilePaths = [],
                };
            }
        }
            
        using var connection = CreateConnection();

        using var playlistCmd = connection.CreateCommand();
        playlistCmd.CommandText = "SELECT Id, Name, Genre, Tags, CreatedDate FROM Playlists WHERE Id = @id";
        playlistCmd.Parameters.AddWithValue("@id", id);
        using var reader = playlistCmd.ExecuteReader();

        if (!reader.Read())
            return null;

        var playlist = new Playlist
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Genre = reader.IsDBNull(2) ? null : reader.GetString(2),
            Tags = reader.IsDBNull(3) ? null : reader.GetString(3),
            CreatedDate = DateTime.Parse(reader.GetString(4)),
        };

        using var songCmd = connection.CreateCommand();
        songCmd.CommandText = "SELECT SongFilePath FROM PlaylistSongs WHERE PlaylistId = @id ORDER BY Position";
        songCmd.Parameters.AddWithValue("@id", id);
        using var songReader = songCmd.ExecuteReader();

        while (songReader.Read())
        {
            playlist.SongFilePaths.Add(songReader.GetString(0));
        }

        return playlist;
    }

    public void UpdatePlaylist(int id, string name, string? genre, string? tags, List<string> songFilePaths)
    {
        // Special handling for default queue
        if (id == -1)
        {
            lock (cacheLock)
            {
                defaultQueueName = name;
                defaultQueueGenre = genre;
                defaultQueueTags = tags;
                scalarDirty = true;
                isDirty = true;
            }
            return;
        }
            
        using var connection = CreateConnection();

        using var transaction = connection.BeginTransaction();

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = @"
                UPDATE Playlists 
                SET Name = @name, Genre = @genre, Tags = @tags 
                WHERE Id = @id";
        updateCmd.Parameters.AddWithValue("@id", id);
        updateCmd.Parameters.AddWithValue("@name", name);
        updateCmd.Parameters.AddWithValue("@genre", (object?)genre ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@tags", (object?)tags ?? DBNull.Value);
        updateCmd.ExecuteNonQuery();

        using var deleteCmd = connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM PlaylistSongs WHERE PlaylistId = @id";
        deleteCmd.Parameters.AddWithValue("@id", id);
        deleteCmd.ExecuteNonQuery();

        for (var i = 0; i < songFilePaths.Count; i++)
        {
            using var songCmd = connection.CreateCommand();
            songCmd.CommandText = @"
                    INSERT INTO PlaylistSongs (PlaylistId, SongFilePath, Position) 
                    VALUES (@playlistId, @filePath, @position)";
            songCmd.Parameters.AddWithValue("@playlistId", id);
            songCmd.Parameters.AddWithValue("@filePath", songFilePaths[i]);
            songCmd.Parameters.AddWithValue("@position", i);
            songCmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeletePlaylist(int id)
    {
        using var connection = CreateConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Playlists WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
            
    }
}