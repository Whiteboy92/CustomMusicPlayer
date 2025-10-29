using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicPlayer.Interfaces;
using MusicPlayer.Windows.CreatePlaylistWindow;

namespace MusicPlayer.Views.Playlists;

public class PlaylistIdToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is -1 ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for one-way binding.");
    }
}

public partial class PlaylistsView
{
    private ISettingsService? settings;
    private IMusicLoaderService? musicLoader;
    public event EventHandler? PlaylistCreated;
    public event EventHandler<int>? PlaylistEdited;
    public event EventHandler? PlaylistDeleted;

    public PlaylistsView()
    {
        InitializeComponent();
    }

    public void Initialize(ISettingsService settingsService, IMusicLoaderService musicLoaderService)
    {
        settings = settingsService;
        musicLoader = musicLoaderService;
        LoadPlaylists();
    }

    public async void LoadPlaylists()
    {
        try
        {
            if (settings == null || musicLoader == null) return;
            
            var playlists = settings.GetAllPlaylists();
            
            var allSongsCount = await musicLoader.GetMusicFileCountAsync();
            
            var displayList = new List<Models.Playlist>();
            
            var defaultQueueSettings = settings.GetPlaylistById(-1);
            
            displayList.Add(new Models.Playlist
            {
                Id = -1,
                Name = defaultQueueSettings?.Name ?? "Default Queue (All Songs)",
                Genre = defaultQueueSettings?.Genre ?? "All",
                Tags = defaultQueueSettings?.Tags ?? "Auto-updated",
                SongFilePaths = [],
                SongCount = allSongsCount,
            });
            
            displayList.AddRange(playlists);
            
            PlaylistsItemsControl.ItemsSource = displayList;

            TxtEmptyState.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading playlists:\n\n{ex.Message}",
                "Load Playlists Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnCreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (settings == null || musicLoader == null) return;
            
        var createWindow = new CreatePlaylistWindow(settings, musicLoader)
        {
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        if (createWindow.ShowDialog() == true)
        {
            LoadPlaylists();
            PlaylistCreated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void BtnEditPlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (settings == null || musicLoader == null) return;
            
        if (sender is Button { Tag: int playlistId })
        {
            var playlist = settings.GetPlaylistById(playlistId);
            if (playlist == null) return;
                
            var editWindow = new CreatePlaylistWindow(settings, musicLoader, playlist)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (editWindow.ShowDialog() == true)
            {
                LoadPlaylists();
                PlaylistEdited?.Invoke(this, playlistId);
                PlaylistCreated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void BtnDeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (settings == null) return;
            
        if (sender is Button { Tag: int playlistId })
        {
            var playlist = settings.GetPlaylistById(playlistId);
            if (playlist == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the playlist '{playlist.Name}'?\n\nThis action cannot be undone.",
                "Delete Playlist",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                settings.DeletePlaylist(playlistId);
                LoadPlaylists();
                PlaylistDeleted?.Invoke(this, EventArgs.Empty);
                    
                MessageBox.Show(
                    $"Playlist '{playlist.Name}' has been deleted.",
                    "Playlist Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}