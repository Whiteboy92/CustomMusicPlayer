using System.Windows;
using System.Windows.Threading;
using MusicPlayer.Helpers;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Services;
using MusicPlayer.Services.DiscordRpc;
using MusicPlayer.Validation;

namespace MusicPlayer;

public partial class MainWindow
{
    private readonly IMusicLoaderService musicLoader;
    private readonly ISettingsService settings;
    private readonly IDurationExtractorService durationExtractor;
    private readonly IShuffleService shuffleService;
    private readonly IDiscordRpcService discordRpc;
    private readonly DiscordPresenceUpdater discordPresenceUpdater;
    private readonly PlaybackStatePersistenceService playbackStatePersistenceService;
    private readonly PlaylistManagerService playlistManagerService;
    private readonly DispatcherTimer? savePositionTimer;
    private readonly DispatcherTimer? memoryPositionTimer;
    private readonly DispatcherTimer? discordUpdateTimer;
    private bool isShuffled;
    private string? currentSongPath;
    private double currentSongPosition;
    private bool songJustFinished;
    private int? currentPlaylistId;
    private List<MusicFile> currentPlaylistSongs = [];

    public MainWindow(
        IMusicLoaderService musicLoaderService, 
        ISettingsService settingsService, 
        IDurationExtractorService durationExtractorService,
        IShuffleService shuffleService,
        IDiscordRpcService discordRpcService,
        DiscordPresenceUpdater discordPresenceUpdater,
        PlaybackStatePersistenceService playbackStatePersistenceService,
        PlaylistManagerService playlistManagerService)
    {
        InitializeComponent();

        musicLoader = musicLoaderService;
        settings = settingsService;
        durationExtractor = durationExtractorService;
        this.shuffleService = shuffleService;
        discordRpc = discordRpcService;
        this.discordPresenceUpdater = discordPresenceUpdater;
        this.playbackStatePersistenceService = playbackStatePersistenceService;
        this.playlistManagerService = playlistManagerService;
        
        SubscribeToEvents();
        
        memoryPositionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        
        memoryPositionTimer.Tick += MemoryPositionTimer_Tick;
        memoryPositionTimer.Start();
        
        savePositionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        savePositionTimer.Tick += SavePositionTimer_Tick;
        savePositionTimer.Start();
        
        discordUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        discordUpdateTimer.Tick += DiscordUpdateTimer_Tick;
        discordUpdateTimer.Start();
        
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
        PlayerControlsView.MediaOpenedEvent += PlayerControlsView_MediaOpenedEvent;
        EqualizerView.EqualizerChanged += EqualizerView_EqualizerChanged;
        PlayerControlsView.VolumeChanged += PlayerControlsView_VolumeChanged;
        SettingsView.MusicFolderChangeRequested += SettingsView_MusicFolderChangeRequested;
        SettingsView.DatabaseResetRequested += SettingsView_DatabaseResetRequested;
        SettingsView.PlayHistoryClearRequested += SettingsView_PlayHistoryClearRequested;
        SettingsView.AutoPlayOnStartupChanged += SettingsView_AutoPlayOnStartupChanged;
        SettingsView.DiscordClientIdChanged += SettingsView_DiscordClientIdChanged;
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
        playbackStatePersistenceService.SaveCurrentState(currentSongPath, currentSongPosition, currentPlaylistId, PlayerControlsView);
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
        settings.SaveCurrentPlaylistId(currentPlaylistId);
        DisposeResources();
    }

    private void SaveFinalPlaybackState()
    {
        playbackStatePersistenceService.SaveFinalState(PlayerControlsView, currentPlaylistId);
    }

    private void DisposeResources()
    {
        settings.Dispose();
        PlayerControlsView.Dispose();
        discordRpc.ClearPresence();
        discordRpc.Dispose();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PlaylistsView.Initialize(settings, musicLoader);
        PlaylistsView.PlaylistCreated += PlaylistsView_PlaylistCreated;
        PlaylistsView.PlaylistEdited += PlaylistsView_PlaylistEdited;
        PlaylistsView.PlaylistDeleted += PlaylistsView_PlaylistDeleted;
        
        var playlists = settings.GetAllPlaylists();
        var defaultQueueName = playlistManagerService.GetDefaultQueueName();
        StatisticsView.InitializePlaylistDropdown(playlists, defaultQueueName);
        StatisticsView.PlaylistSelectionChanged += StatisticsView_PlaylistSelectionChanged;
        
        LoadSettingsIntoMusicLoader();
        LoadSettingsIntoUi();
        LoadEqualizerSettings();

        var savedVolume = settings.GetVolumePercent();
        PlayerControlsView.SetVolumePercent(savedVolume);
        
        InitializeDiscordRpc();
        PopulatePlaylistDropdown();
        
        var savedPlaylistId = settings.GetCurrentPlaylistId();
        if (savedPlaylistId is >= -1)
        {
            RestoreSavedPlaylist();
        }
        else
        {
            LoadMusicFromFolder();
        }
    }

    private void RestoreSavedPlaylist()
    {
        var savedPlaylistId = settings.GetCurrentPlaylistId();
        
        if (savedPlaylistId is >= -1)
        {
            for (var i = 0; i < PlaylistDropdown.Items.Count; i++)
            {
                if (PlaylistDropdown.Items[i] is PlaylistDropdownItem item && item.Id == savedPlaylistId.Value)
                {
                    PlaylistDropdown.SelectedIndex = i;
                    currentPlaylistId = savedPlaylistId.Value;
                    
                    if (savedPlaylistId.Value == -1)
                    {
                        LoadMusicFromFolder();
                    }
                    else
                    {
                        LoadPlaylist(savedPlaylistId.Value);
                    }
                    return;
                }
            }
        }
        
        currentPlaylistId = -1;
        PlaylistDropdown.SelectedIndex = 0;
        LoadMusicFromFolder();
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
        SettingsView.SetDiscordClientId(settings.GetDiscordClientId());
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
        var allSongs = currentPlaylistSongs.ToList();
        var playlistCounts = settings.GetPlayCountsForPlaylist(currentPlaylistId);
        var cachedDurations = settings.GetAllDurations();
        
        PlaylistLoadingHelper.ApplyMetadataToSongs(allSongs, playlistCounts, cachedDurations);
        
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

    private bool restoreSucceeded;

    private void RestorePlaybackState()
    {
        restoreSucceeded = false;
        PlaybackHelper.RestorePlaybackState(settings, PlaylistView, PlayerControlsView, this.Dispatcher, song =>
        {
            restoreSucceeded = true;
            discordPresenceUpdater.SetCurrentSong(song);
        }, currentPlaylistId);
    }

    private async void LoadMusicFromFolder(bool forceReload = false)
    {
        try
        {
            if (!MusicLoadingHelper.ValidateMusicFolderExists(musicLoader))
                return;

            var currentFileCount = await musicLoader.GetMusicFileCountAsync();
            var databaseSongCount = settings.GetDatabaseSongCount();
            var (savedQueuePaths, wasShuffled) = settings.GetCurrentQueue();
            var hasSavedQueue = savedQueuePaths is { Count: > 0 };
            var shouldReload = forceReload || currentFileCount != databaseSongCount || (!hasSavedQueue && PlaylistView.GetPlaylistCount() == 0);

            if (shouldReload || hasSavedQueue)
            {
                var allSongs = await MusicLoadingHelper.LoadAndPrepareSongs(musicLoader);
                if (allSongs.Count == 0)
                    return;

                var songDict = allSongs.ToDictionary(s => s.FilePath, s => s);
                var updatedDurations = MusicLoadingHelper.ApplyCachedMetadata(allSongs, settings);
                var (songsToDisplay, newShuffleState) = MusicLoadingHelper.DetermineSongsToDisplay(
                    allSongs, songDict, savedQueuePaths, hasSavedQueue, wasShuffled, shouldReload);
                
                currentPlaylistSongs = allSongs;
                currentPlaylistId = -1;
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
        StatisticsView.SetSelectedPlaylist(currentPlaylistId);
        LoadStatistics();
        UpdateSettingsStats();
    }

    private void PlaylistView_SongSelected(object? sender, MusicFile musicFile)
    {
        SaveCurrentSongPlayCountIfNeeded();
        
        HandleSongSelection(musicFile);
        
        PlaylistView.ScrollToTop();
        
        PlaySelectedSong(musicFile);
    }

    private void HandleSongSelection(MusicFile selectedSong)
    {
        var allVisibleSongs = PlaylistView.GetAllVisibleSongs();
        var selectedSongIndex = allVisibleSongs.IndexOf(selectedSong);
        
        if (selectedSongIndex < 0)
        {
            return;
        }

        var currentlyPlayingSongPath = PlayerControlsView.GetCurrentSongPath();
        var currentlyPlayingIndex = -1;
        
        if (!string.IsNullOrEmpty(currentlyPlayingSongPath))
        {
            var currentlyPlayingSong = allVisibleSongs.FirstOrDefault(s => s.FilePath == currentlyPlayingSongPath);
            if (currentlyPlayingSong != null)
            {
                currentlyPlayingIndex = allVisibleSongs.IndexOf(currentlyPlayingSong);
            }
        }

        if (currentlyPlayingIndex >= 0 && selectedSongIndex > currentlyPlayingIndex)
        {
            PlaylistView.HideSongsBetween(currentlyPlayingIndex, selectedSongIndex);
        }
        else if (selectedSongIndex > 0)
        {
            PlaylistView.HideSongsBefore(selectedSongIndex);
        }
        
        UpdateSelectionForSong(selectedSong);
    }

    private void UpdateSelectionForSong(MusicFile song)
    {
        var displayedPlaylist = PlaylistView.GetDisplayedPlaylist();
        for (var i = 0; i < displayedPlaylist.Count; i++)
        {
            if (displayedPlaylist[i].FilePath == song.FilePath)
            {
                PlaylistView.SelectedIndex = i;
                return;
            }
        }
    }

    private void PlaySelectedSong(MusicFile musicFile)
    {
        discordPresenceUpdater.SetCurrentSong(musicFile);
        PlayerControlsView.PlaySong(musicFile);
        
        var cumulativeTime = settings.GetCumulativePlayedTime(musicFile.FilePath);
        PlayerControlsView.SetCumulativePlayedTime(cumulativeTime);
        
        settings.UpdateLastPlayedIndex(PlaylistView.SelectedIndex);
    }

    private void PlayerControlsView_SongFinished(object? sender, bool wasPlayedEnough)
    {
        if (wasPlayedEnough && PlaylistView.SelectedIndex >= 0)
        {
            var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
            if (song == null) { return; }
            
            song.PlayCount++;
            settings.IncrementPlayCountForPlaylist(song.FilePath, currentPlaylistId);
            LoadStatistics();
            
            if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
            {
                PlaylistView.MarkCurrentSongAsCompleted();
                SaveCurrentQueue();
                songJustFinished = true;
            }
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
        var startIndex = settings.GetLastPlayedIndex();
        if (!PlaybackStateValidator.IsValidIndex(startIndex, PlaylistView.GetPlaylistCount()))
        {
            startIndex = 0;
        }

        PlaylistView.SelectedIndex = startIndex;
        var song = PlaylistView.GetSongAtIndex(startIndex);
        if (song == null) return;

        discordPresenceUpdater.SetCurrentSong(song);
        PlayerControlsView.PlaySong(song);
        
        var cumulativeTime = settings.GetCumulativePlayedTime(song.FilePath);
        PlayerControlsView.SetCumulativePlayedTime(cumulativeTime);
        
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
        }
        else
        {
            PlaylistView.RevealPreviousSong();
        }

        PlaySelectedSong();
    }

    private void PlaySelectedSong()
    {
        var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
        if (song == null) return;

        PlaySelectedSong(song);
    }

    private void PlayerControlsView_ShuffleRequested(object? sender, EventArgs e)
    {
        try
        {
            if (!PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
            {
                MessageBox.Show("No songs in playlist.", "No Songs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }


            SaveCurrentSongPlayCountIfNeeded();


            var shuffledQueue = shuffleService.CreateShuffledQueue(currentPlaylistSongs);


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
        playbackStatePersistenceService.SaveCurrentSongPlayCount(PlaylistView, PlayerControlsView, currentPlaylistId);
        
        if (PlaybackStateValidator.IsValidIndex(PlaylistView.SelectedIndex, PlaylistView.GetPlaylistCount()))
        {
            var currentSong = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
            if (currentSong != null && PlayerControlsView.WasPlayedEnough())
            {
                LoadStatistics();
            }
        }
    }

    private async void ApplyShuffledQueue(List<MusicFile> shuffledQueue)
    {
        try
        {
            isShuffled = true;

            foreach (var song in shuffledQueue)
            {
                song.IsCompleted = false;
            }

            if (PlaybackStateValidator.HasPlaylistItems(shuffledQueue.Count))
            {
                var firstSong = shuffledQueue[0];

                PlaylistView.LoadPlaylist(shuffledQueue);
                PlaylistView.SelectedIndex = 0;
                SaveCurrentQueue();

                await Dispatcher.InvokeAsync(() =>
                {
                    discordPresenceUpdater.SetCurrentSong(firstSong);
                    PlayerControlsView.PlaySong(firstSong);
                
                    var cumulativeTime = settings.GetCumulativePlayedTime(firstSong.FilePath);
                    PlayerControlsView.SetCumulativePlayedTime(cumulativeTime);
                }, DispatcherPriority.ApplicationIdle);

                settings.UpdateLastPlayedIndex(0);
            }
            else
            {
                PlaylistView.LoadPlaylist(shuffledQueue);
                SaveCurrentQueue();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error applying shuffled queue:\n\n{ex.Message}",
                "Shuffle Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void PlayerControlsView_NextRequested(object? sender, EventArgs e)
    {
        if (!songJustFinished)
        {
            SaveCurrentSongPlayCountIfNeeded();
            
            if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
            {
                PlaylistView.MarkCurrentSongAsCompleted();
                SaveCurrentQueue();
            }
        }
        
        songJustFinished = false;
        
        if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
        {
            PlayNextSong();
        }
        else
        {
            ResetPlaylistAndPlayFromStart();
        }
    }

    private void PlayNextSong()
    {
        var song = PlaylistView.GetSongAtIndex(PlaylistView.SelectedIndex);
        if (song == null) return;
        
        discordPresenceUpdater.SetCurrentSong(song);
        PlayerControlsView.PlaySong(song);
        settings.UpdateLastPlayedIndex(PlaylistView.SelectedIndex);
    }

    private void ResetPlaylistAndPlayFromStart()
    {
        var songs = PlaylistView.Playlist.ToList();
        foreach (var song in songs)
        {
            song.IsCompleted = false;
        }
        
        PlaylistView.LoadPlaylist(songs);
        
        if (PlaybackStateValidator.HasPlaylistItems(PlaylistView.GetPlaylistCount()))
        {
            PlaylistView.SelectedIndex = 0;
            PlayNextSong();
        }
        else
        {
            PlayerControlsView.Stop();
            LoadStatistics();
        }
    }

    private async void SettingsView_MusicFolderChangeRequested(object? sender, EventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Music Folder",
                InitialDirectory = musicLoader.GetFolderPath(),
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
                LoadMusicFromFolder(forceReload: true);
                PlaylistsView.LoadPlaylists();
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
            
            LoadMusicFromFolder(forceReload: true);
            PlaylistsView.LoadPlaylists();
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

    private void SettingsView_DiscordClientIdChanged(object? sender, string? e)
    {
        settings.SaveDiscordClientId(e);
        settings.FlushToDisk();
    }

    private void PopulatePlaylistDropdown()
    {
        PlaylistDropdown.SelectionChanged -= PlaylistDropdown_SelectionChanged;
        
        playlistManagerService.PopulateDropdown(PlaylistDropdown);
        
        if (currentPlaylistId.HasValue)
        {
            PlaylistManagerService.SetDropdownSelection(PlaylistDropdown, currentPlaylistId.Value);
        }
        else
        {
            PlaylistDropdown.SelectedIndex = 0;
        }
        
        PlaylistDropdown.SelectionChanged += PlaylistDropdown_SelectionChanged;
    }

    private void PlaylistDropdown_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PlaylistDropdown.SelectedItem is PlaylistDropdownItem item)
        {
            if (item.Id == -1)
            {
                currentPlaylistId = -1;
                LoadMusicFromFolder(forceReload: true);
            }
            else
            {
                currentPlaylistId = item.Id;
                LoadPlaylist(item.Id);
            }
            
            LoadStatistics();
        }
    }

    private async void LoadPlaylist(int playlistId)
    {
        try
        {
            var playlistSongs = await PlaylistLoadingHelper.LoadPlaylistSongsAsync(playlistId, musicLoader, settings);
        
            if (playlistSongs.Count == 0)
            {
                var playlist = settings.GetPlaylistById(playlistId);
                if (playlist == null)
                {
                    MessageBox.Show("Playlist not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var (savedPathForPlaylist, _) = settings.GetCurrentPlaybackState(playlistId);
            if (!string.IsNullOrEmpty(savedPathForPlaylist))
            {
                foreach (var s in playlistSongs)
                {
                    s.IsCompleted = false;
                }
                var savedIndex = playlistSongs.FindIndex(s => s.FilePath == savedPathForPlaylist);
                if (savedIndex > 0)
                {
                    for (int i = 0; i < savedIndex; i++)
                    {
                        playlistSongs[i].IsCompleted = true;
                    }
                }
            }

            currentPlaylistSongs = playlistSongs;
            isShuffled = false;
            DisplayPlaylist(playlistSongs);
        
            await Task.Delay(100);
            RestorePlaybackState();

            if (!restoreSucceeded && PlaylistView.GetPlaylistCount() > 0)
            {
                PlaylistView.SelectedIndex = 0;
                var firstSong = PlaylistView.GetSongAtIndex(0);
                if (firstSong != null)
                {
                    discordPresenceUpdater.SetCurrentSong(firstSong);
                    PlayerControlsView.PlaySong(firstSong);
                    var cumulativeTime = settings.GetCumulativePlayedTime(firstSong.FilePath);
                    PlayerControlsView.SetCumulativePlayedTime(cumulativeTime);
                    settings.UpdateLastPlayedIndex(0);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading playlist:\n\n{ex.Message}",
                "Load Playlist Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void PlaylistsView_PlaylistCreated(object? sender, EventArgs e)
    {
        PopulatePlaylistDropdown();
        
        var playlists = settings.GetAllPlaylists();
        var defaultQueueName = playlistManagerService.GetDefaultQueueName();
        StatisticsView.InitializePlaylistDropdown(playlists, defaultQueueName);
    }

    private async void PlaylistsView_PlaylistEdited(object? sender, int editedPlaylistId)
    {
        try
        {
            PopulatePlaylistDropdown();
            PlaylistsView.LoadPlaylists();
        
            var playlists = settings.GetAllPlaylists();
            var defaultQueueName = playlistManagerService.GetDefaultQueueName();
            StatisticsView.InitializePlaylistDropdown(playlists, defaultQueueName);
        
            if (currentPlaylistId == editedPlaylistId)
            {
                if (editedPlaylistId == -1)
                {
                    PlaylistManagerService.SetDropdownSelection(PlaylistDropdown, editedPlaylistId);
                    return;
                }
            
                var currentlyPlayingSongPath = PlayerControlsView.GetCurrentSongPath();
            
                var playlist = settings.GetPlaylistById(editedPlaylistId);
                if (playlist == null)
                {
                    currentPlaylistId = -1;
                    PlaylistDropdown.SelectedIndex = 0;
                    LoadMusicFromFolder(forceReload: true);
                    return;
                }
            
                var songStillInPlaylist = !string.IsNullOrEmpty(currentlyPlayingSongPath) && 
                                          playlist.SongFilePaths.Contains(currentlyPlayingSongPath);
            
                await ReloadEditedPlaylist(editedPlaylistId, songStillInPlaylist);

                PlaylistManagerService.SetDropdownSelection(PlaylistDropdown, editedPlaylistId);
            
                LoadStatistics();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error editing playlist:\n\n{ex.Message}",
                "Edit Playlist Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ReloadEditedPlaylist(int playlistId, bool songStillInPlaylist)
    {
        var playlistSongs = await PlaylistLoadingHelper.LoadPlaylistSongsAsync(playlistId, musicLoader, settings);
        
        currentPlaylistSongs = playlistSongs;
        isShuffled = false;
        DisplayPlaylist(playlistSongs);
        
        if (!songStillInPlaylist)
        {
            PlayerControlsView.Stop();
            
            if (PlaylistView.GetPlaylistCount() > 0)
            {
                PlaylistView.SelectedIndex = 0;
                var firstSong = PlaylistView.GetSongAtIndex(0);
                if (firstSong != null)
                {
                    PlayerControlsView.LoadSong(firstSong);
                    discordPresenceUpdater.SetCurrentSong(firstSong);
                    settings.SaveCurrentPlaybackState(firstSong.FilePath, 0, currentPlaylistId);
                }
            }
        }
    }

    private void PlaylistsView_PlaylistDeleted(object? sender, EventArgs e)
    {
        PopulatePlaylistDropdown();
        
        currentPlaylistId = -1;
        PlaylistDropdown.SelectedIndex = 0;
        LoadMusicFromFolder(forceReload: true);
        LoadStatistics();
        
        var playlists = settings.GetAllPlaylists();
        var defaultQueueName = playlistManagerService.GetDefaultQueueName();
        StatisticsView.InitializePlaylistDropdown(playlists, defaultQueueName);
    }

    private async void StatisticsView_PlaylistSelectionChanged(object? sender, int? playlistId)
    {
        try
        {
            var songsToAnalyze = await PlaylistLoadingHelper.LoadStatisticsSongsAsync(playlistId, musicLoader, settings);
            StatisticsView.LoadStatistics(songsToAnalyze);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading statistics:\n\n{ex.Message}",
                "Statistics Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void InitializeDiscordRpc()
    {
        var clientId = settings.GetDiscordClientId();
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            discordRpc.Initialize(clientId);
        }
    }

    private void PlayerControlsView_MediaOpenedEvent(object? sender, EventArgs e)
    {
        UpdateDiscordPresence();
    }

    private void DiscordUpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateDiscordPresence();
    }

    private void UpdateDiscordPresence()
    {
        discordPresenceUpdater.UpdatePresence(PlayerControlsView);
    }
}
