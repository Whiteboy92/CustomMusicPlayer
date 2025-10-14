using System.Windows;
using System.Windows.Threading;
using MusicPlayer.Helpers;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Validation;

namespace MusicPlayer;

public partial class MainWindow
{
    private readonly IMusicLoaderService musicLoader;
    private readonly ISettingsService settings;
    private readonly IDurationExtractorService durationExtractor;
    private readonly IShuffleService shuffleService;
    private readonly DispatcherTimer? savePositionTimer;
    private readonly DispatcherTimer? memoryPositionTimer;
    private bool isShuffled;
    private string? currentSongPath;
    private double currentSongPosition;

    public MainWindow(
        IMusicLoaderService musicLoaderService, 
        ISettingsService settingsService, 
        IDurationExtractorService durationExtractorService,
        IShuffleService shuffleService)
    {
        InitializeComponent();

        musicLoader = musicLoaderService;
        settings = settingsService;
        durationExtractor = durationExtractorService;
        this.shuffleService = shuffleService;
        SubscribeToEvents();
        
        memoryPositionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        
        memoryPositionTimer.Tick += MemoryPositionTimer_Tick;
        memoryPositionTimer.Start();
        
        savePositionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        savePositionTimer.Tick += SavePositionTimer_Tick;
        savePositionTimer.Start();
        
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void SubscribeToEvents()
    {
        PlaylistView.SongSelected += PlaylistView_SongSelected;
        PlayerControlsView.PlayRequested += PlayerControlsView_PlayRequested;
        PlayerControlsView.PreviousRequested += PlayerControlsView_PreviousRequested;
        PlayerControlsView.NextRequested += PlayerControlsView_NextRequested;
        PlayerControlsView.ShuffleRequested += PlayerControlsView_ShuffleRequested;
        PlayerControlsView.SongFinished += PlayerControlsView_SongFinished;
        EqualizerView.EqualizerChanged += EqualizerView_EqualizerChanged;
        PlayerControlsView.VolumeChanged += PlayerControlsView_VolumeChanged;
        SettingsView.MusicFolderChangeRequested += SettingsView_MusicFolderChangeRequested;
        SettingsView.DatabaseResetRequested += SettingsView_DatabaseResetRequested;
        SettingsView.PlayHistoryClearRequested += SettingsView_PlayHistoryClearRequested;
        SettingsView.AutoPlayOnStartupChanged += SettingsView_AutoPlayOnStartupChanged;
    }

    private void PlayerControlsView_VolumeChanged(object? sender, double e)
    {
        settings.SaveVolumePercent(e);
        settings.FlushToDisk();
    }

    private void MemoryPositionTimer_Tick(object? sender, EventArgs e)
    {
        var path = PlayerControlsView.GetCurrentSongPath();
        var position = PlayerControlsView.GetCurrentPositionSeconds();
        
        if (PlaybackStateValidator.IsValidPlaybackState(path, position) && 
            PlaybackStateValidator.HasPlayablePosition(position))
        {
            currentSongPath = path;
            currentSongPosition = position;
        }
    }

    private void SavePositionTimer_Tick(object? sender, EventArgs e)
    {
        SaveCurrentPlaybackState();
        SaveCurrentQueue();
    }

    private void SaveCurrentPlaybackState()
    {
        if (PlaybackStateValidator.IsValidPlaybackState(currentSongPath, currentSongPosition) && 
            PlaybackStateValidator.HasPlayablePosition(currentSongPosition))
        {
            settings.SaveCurrentPlaybackState(currentSongPath, currentSongPosition);
            settings.FlushToDisk();
        }
    }

    private void SaveCurrentQueue()
    {
        PlaybackHelper.SaveCurrentQueue(settings, PlaylistView, isShuffled);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentSongPlayCountIfNeeded();
        SaveFinalPlaybackState();
        SaveCurrentQueue();
        DisposeResources();
    }

    private void SaveFinalPlaybackState()
    {
        currentSongPath = PlayerControlsView.GetCurrentSongPath();
        currentSongPosition = PlayerControlsView.GetCurrentPositionSeconds();

        if (StringValidator.HasValue(currentSongPath))
        {
            settings.SaveCurrentPlaybackState(currentSongPath, currentSongPosition);
            settings.FlushToDisk();
        }
    }

    private void DisposeResources()
    {
        settings.Dispose();
        PlayerControlsView.Dispose();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettingsIntoMusicLoader();
        LoadSettingsIntoUi();
        LoadMusicFromFolder();
        LoadEqualizerSettings();

        var savedVolume = settings.GetVolumePercent();
        PlayerControlsView.SetVolumePercent(savedVolume);
    }

    private void LoadSettingsIntoMusicLoader()
    {
        var savedMusicFolder = settings.GetMusicFolderPath();
        if (StringValidator.HasValue(savedMusicFolder))
        {
            musicLoader.SetFolderPath(savedMusicFolder);
        }
        else
        {
            settings.SaveMusicFolderPath(musicLoader.GetFolderPath());
        }
    }

    private void LoadSettingsIntoUi()
    {
        SettingsView.SetMusicFolderPath(musicLoader.GetFolderPath());
        SettingsView.SetAutoPlayOnStartup(settings.GetAutoPlayOnStartup());
        UpdateSettingsStats();
    }

    private void UpdateSettingsStats()
    {
        var songCount = settings.GetDatabaseSongCount();
        var totalPlays = settings.GetTotalPlayCount();
        var dbPath = settings.GetDatabaseFilePath();
        SettingsView.SetDatabaseStats(songCount, totalPlays, dbPath);
    }
    
    private void LoadEqualizerSettings()
    {
        var (band80, band240, band750, band2200, band6600) = settings.GetEqualizerSettings();
        EqualizerView.SetEqualizerValues(band80, band240, band750, band2200, band6600);
        PlayerControlsView.SetBand80Hz(band80);
        PlayerControlsView.SetBand240Hz(band240);
        PlayerControlsView.SetBand750Hz(band750);
        PlayerControlsView.SetBand2200Hz(band2200);
        PlayerControlsView.SetBand6600Hz(band6600);
    }
    
    private void LoadStatistics()
    {
        var allSongs = PlaylistView.Playlist.ToList();
        StatisticsView.LoadStatistics(allSongs);
    }
    
    private void EqualizerView_EqualizerChanged(object? sender, (float band80, float band240, float band750, float band2200, float band6600) e)
    {
        PlayerControlsView.SetBand80Hz(e.band80);
        PlayerControlsView.SetBand240Hz(e.band240);
        PlayerControlsView.SetBand750Hz(e.band750);
        PlayerControlsView.SetBand2200Hz(e.band2200);
        PlayerControlsView.SetBand6600Hz(e.band6600);
        settings.SaveEqualizerSettings(e.band80, e.band240, e.band750, e.band2200, e.band6600);
    }

    private void RestorePlaybackState()
    {
        PlaybackHelper.RestorePlaybackState(settings, PlaylistView, PlayerControlsView, this.Dispatcher);
    }

    private async void LoadMusicFromFolder()
    {
        try
        {
            if (!MusicLoadingHelper.ValidateMusicFolderExists(musicLoader))
                return;

            int currentFileCount = await musicLoader.GetMusicFileCountAsync();
            int databaseSongCount = settings.GetDatabaseSongCount();
            var (savedQueuePaths, wasShuffled) = settings.GetCurrentQueue();
            bool hasSavedQueue = savedQueuePaths is { Count: > 0 };
            bool shouldReload = (currentFileCount != databaseSongCount) || (!hasSavedQueue && PlaylistView.GetPlaylistCount() == 0);

            if (shouldReload || hasSavedQueue)
            {
                var allSongs = await MusicLoadingHelper.LoadAndPrepareSongs(musicLoader);
                if (allSongs.Count == 0)
                    return;

                var songDict = allSongs.ToDictionary(s => s.FilePath, s => s);
                var updatedDurations = MusicLoadingHelper.ApplyCachedMetadata(allSongs, settings);
                var (songsToDisplay, newShuffleState) = MusicLoadingHelper.DetermineSongsToDisplay(
                    allSongs, songDict, savedQueuePaths, hasSavedQueue, wasShuffled, shouldReload);
                
                isShuffled = newShuffleState;
                DisplayPlaylist(songsToDisplay);
                
                await MusicLoadingHelper.AnalyzeMissingSongDurations(
                    allSongs, updatedDurations, currentFileCount, settings, durationExtractor, Dispatcher, title => Title = title);
                await Task.Delay(300);
                RestorePlaybackState();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading music folder:\n\n{ex.Message}",
                "Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DisplayPlaylist(List<MusicFile> songs)
    {
        PlaylistView.LoadPlaylist(songs);
        LoadStatistics();
        UpdateSettingsStats();
    }

    private void PlaylistView_SongSelected(object? sender, MusicFile musicFile)
    {
        PlayerControlsView.PlaySong(musicFile);
        settings.UpdateLastPlayedIndex(PlaylistView.SelectedIndex);
    }

    private void PlayerControlsView_SongFinished(object? sender, bool wasPlayedEnough)
    {
        if (wasPlayedEnough && PlaylistView.SelectedIndex >= 0)
        {
            var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
            if (song == null) { return; }
            
            song.PlayCount++;
            settings.IncrementPlayCount(song.FilePath);
            LoadStatistics();
        }
    }

    private void PlayerControlsView_PlayRequested(object? sender, EventArgs e)
    {
        if (PlayerControlsView.HasSource)
        {
            PlayerControlsView.Play();
            return;
        }
        
        if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
        {
            PlayFromSavedOrFirstSong();
        }
        else
        {
            ShowNoSongsMessage();
        }
    }

    private void PlayFromSavedOrFirstSong()
    {
        int startIndex = settings.GetLastPlayedIndex();
        if (!PlaybackStateValidator.IsValidIndex(startIndex, PlaylistView.GetPlaylistCount()))
        {
            startIndex = 0;
        }

        PlaylistView.SelectedIndex = startIndex;
        var song = PlaylistView.GetSongAtIndex(startIndex);
        if (song == null) return;

        PlayerControlsView.PlaySong(song);
        settings.UpdateLastPlayedIndex(startIndex);
    }

    private void ShowNoSongsMessage()
    {
        MessageBox.Show(
            "No songs in playlist.\n\nPlease add music files to:\n" + musicLoader.GetFolderPath(),
            "No Songs",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PlayerControlsView_PreviousRequested(object? sender, EventArgs e)
    {
        if (PlaylistView.SelectedIndex > 0)
        {
            PlaylistView.SelectedIndex--;
            PlaySelectedSong();
        }
        else
        {
            PlaylistView.RevealPreviousSong();
            PlaySelectedSong();
        }
    }

    private void PlaySelectedSong()
    {
        var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
        if (song == null) return;

        PlayerControlsView.PlaySong(song);
        settings.UpdateLastPlayedIndex(PlaylistView.SelectedIndex);
    }

    private async void PlayerControlsView_ShuffleRequested(object? sender, EventArgs e)
    {
        try
        {
            if (!PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
            {
                MessageBox.Show("No songs in playlist.", "No Songs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            SaveCurrentSongPlayCountIfNeeded();
            
            var allSongs = await LoadAllSongsWithMetadata();
            var shuffledQueue = shuffleService.CreateShuffledQueue(allSongs);
            
            ApplyShuffledQueue(shuffledQueue);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error during shuffle:\n\n{ex.Message}", 
                "Shuffle Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private void SaveCurrentSongPlayCountIfNeeded()
    {
        if (PlaybackStateValidator.IsValidIndex(PlaylistView.SelectedIndex, PlaylistView.GetPlaylistCount()))
        {
            var currentSong = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
            if (currentSong != null && PlayerControlsView.WasPlayedEnough())
            {
                currentSong.PlayCount++;
                settings.IncrementPlayCount(currentSong.FilePath);
                settings.FlushToDisk();
                LoadStatistics();
            }
        }
    }

    private async Task<List<MusicFile>> LoadAllSongsWithMetadata()
    {
        var allSongs = await musicLoader.LoadMusicFromFolderAsync();
        var cachedPlayCounts = settings.GetAllPlayCounts();
        var cachedDurations = settings.GetAllDurations();

        foreach (var song in allSongs)
        {
            if (cachedPlayCounts.TryGetValue(song.FilePath, out var playCount))
            {
                song.PlayCount = playCount;
            }
            if (cachedDurations.TryGetValue(song.FilePath, out var duration))
            {
                song.Duration = duration;
            }
        }

        return allSongs;
    }

    private async void ApplyShuffledQueue(List<MusicFile> shuffledQueue)
    {
        isShuffled = true;

        if (PlaybackStateValidator.HasPlaylistItems(shuffledQueue.Count))
        {
            var firstSong = shuffledQueue[0];

            PlaylistView.LoadPlaylist(shuffledQueue);
            PlaylistView.SelectedIndex = 0;
            SaveCurrentQueue();

            await Dispatcher.InvokeAsync(() =>
            {
                PlayerControlsView.PlaySong(firstSong);
            }, DispatcherPriority.ApplicationIdle);

            settings.UpdateLastPlayedIndex(0);
        }
        else
        {
            PlaylistView.LoadPlaylist(shuffledQueue);
            SaveCurrentQueue();
        }
    }
    
    private void PlayerControlsView_NextRequested(object? sender, EventArgs e)
    {
        SaveCurrentSongPlayCountIfNeeded();
        
        if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
        {
            PlaylistView.MarkCurrentSongAsCompleted();
            SaveCurrentQueue();
        }
        
        if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
        {
            PlayNextSong();
        }
        else
        {
            PlayerControlsView.Stop();
            LoadStatistics();
        }
    }

    private void PlayNextSong()
    {
        var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
        if (song == null) return;
        
        PlayerControlsView.PlaySong(song);
        settings.UpdateLastPlayedIndex(PlaylistView.SelectedIndex);
    }

    private async void SettingsView_MusicFolderChangeRequested(object? sender, EventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Music Folder",
                InitialDirectory = musicLoader.GetFolderPath()
            };

            if (dialog.ShowDialog() == true)
            {
                var newPath = dialog.FolderName;
            
                musicLoader.SetFolderPath(newPath);
                settings.SaveMusicFolderPath(newPath);
                settings.FlushToDisk();
            
                SettingsView.SetMusicFolderPath(newPath);
                MessageBox.Show(
                    $"Music folder changed to:\n{newPath}\n\nReloading library...",
                    "Folder Changed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            
                await Task.Delay(300);
                LoadMusicFromFolder();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error changing music folder:\n\n{ex.Message}",
                "Folder Change Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SettingsView_DatabaseResetRequested(object? sender, EventArgs e)
    {
        try
        {
            PlayerControlsView.Stop();
            settings.ResetDatabase();
            PlaylistView.ClearPlaylist();
            LoadSettingsIntoUi();
            LoadStatistics();
            
            MessageBox.Show(
                "Database has been reset successfully!\n\nAll play counts, statistics, and settings have been cleared.",
                "Reset Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            LoadMusicFromFolder();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error resetting database:\n\n{ex.Message}",
                "Reset Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SettingsView_PlayHistoryClearRequested(object? sender, EventArgs e)
    {
        try
        {
            settings.ClearPlayHistory();
            settings.FlushToDisk();
            
            foreach (var song in PlaylistView.Playlist)
            {
                song.PlayCount = 0;
            }
            
            LoadStatistics();
            UpdateSettingsStats();
            
            MessageBox.Show(
                "Play history cleared successfully!\n\nAll play counts have been reset to 0.",
                "History Cleared",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error clearing play history:\n\n{ex.Message}",
                "Clear Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SettingsView_AutoPlayOnStartupChanged(object? sender, bool e)
    {
        settings.SaveAutoPlayOnStartup(e);
        settings.FlushToDisk();
    }
}
