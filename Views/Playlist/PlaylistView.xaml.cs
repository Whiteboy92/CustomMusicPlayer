using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicPlayer.Models;
using MusicPlayer.Validation;

namespace MusicPlayer.Views.Playlist;

public partial class PlaylistView
{
    public ObservableCollection<MusicFile> Playlist { get; } = [];
    private ObservableCollection<MusicFile> DisplayedPlaylist { get; } = [];
    private List<MusicFile> AllVisibleSongs { get; } = [];
    private string currentSearchText = string.Empty;

    public event EventHandler<MusicFile>? SongSelected;

    public PlaylistView()
    {
        InitializeComponent();
        PlaylistBox.ItemsSource = DisplayedPlaylist;
        UpdateEmptyState();
    }

    public void LoadPlaylist(List<MusicFile> songs)
    {
        Playlist.Clear();
        DisplayedPlaylist.Clear();
        AllVisibleSongs.Clear();
            
        var trackNumber = 1;
        foreach (var song in songs)
        {
            song.TrackNumber = trackNumber++;
            Playlist.Add(song);

            if (!song.IsCompleted)
            {
                AllVisibleSongs.Add(song);
                DisplayedPlaylist.Add(song);
            }
        }
            
        ApplySearchFilter();
        UpdateEmptyState();
    }

    public void ClearPlaylist()
    {
        Playlist.Clear();
        DisplayedPlaylist.Clear();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = DisplayedPlaylist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void PlaylistBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistBox.SelectedItem is MusicFile selectedFile)
        {
            ClearSearch();
            SongSelected?.Invoke(this, selectedFile);
        }
    }

    public void ClearSearch()
    {
        if (SearchBox != null)
        {
            SearchBox.Text = string.Empty;
            currentSearchText = string.Empty;
            ApplySearchFilter();
        }
    }

    public int SelectedIndex
    {
        get => PlaylistBox.SelectedIndex;
        set => PlaylistBox.SelectedIndex = value;
    }

    public MusicFile? GetSongAtIndex(int index)
    {
        return PlaybackStateValidator.IsValidIndex(index, DisplayedPlaylist.Count) ? DisplayedPlaylist[index] : null;
    }

    public int GetPlaylistCount() => DisplayedPlaylist.Count;

    public ObservableCollection<MusicFile> GetDisplayedPlaylist() => DisplayedPlaylist;

    public List<MusicFile> GetAllVisibleSongs() => AllVisibleSongs.ToList();

    public void MarkCurrentSongAsCompleted()
    {
        if (!PlaybackStateValidator.IsValidIndex(PlaylistBox.SelectedIndex, DisplayedPlaylist.Count)) 
            return;
            
        var currentIndex = PlaylistBox.SelectedIndex;
        var song = DisplayedPlaylist[currentIndex];

        song.IsCompleted = true;
        AllVisibleSongs.Remove(song);
            
        RefreshDisplayedPlaylist();
            
        if (PlaybackStateValidator.HasPlaylistItems(DisplayedPlaylist.Count))
        {
            if (!PlaybackStateValidator.IsValidIndex(currentIndex, DisplayedPlaylist.Count))
            {
                PlaylistBox.SelectedIndex = DisplayedPlaylist.Count - 1;
            }
            else
            {
                PlaylistBox.SelectedIndex = -1;
                PlaylistBox.SelectedIndex = currentIndex;
            }
        }
        else
        {
            PlaylistBox.SelectedIndex = -1;
        }
    }

    public void RevealPreviousSong()
    {
        MusicFile? currentSong = null;

        if (PlaybackStateValidator.IsValidIndex(PlaylistBox.SelectedIndex, DisplayedPlaylist.Count))
        {
            currentSong = DisplayedPlaylist[PlaylistBox.SelectedIndex];
        }
        else if (PlaybackStateValidator.HasPlaylistItems(DisplayedPlaylist.Count))
        {
            currentSong = DisplayedPlaylist[0];
        }
            
        if (currentSong != null)
        {
        }
            
        var playlistIndex = currentSong != null ? Playlist.IndexOf(currentSong) : -1;
        MusicFile? previousCompletedSong = null;
        var previousCompletedIndex = -1;
            
        if (playlistIndex >= 0)
        {
            for (var i = playlistIndex - 1; i >= 0; i--)
            {
                if (Playlist[i].IsCompleted)
                {
                    previousCompletedSong = Playlist[i];
                    previousCompletedIndex = i;
                    break;
                }
            }
        }

        if (previousCompletedSong != null)
        {
            previousCompletedSong.IsCompleted = false;
                
            var insertIndexInAllVisible = 0;
            for (var i = 0; i < AllVisibleSongs.Count; i++)
            {
                var songIndexInPlaylist = Playlist.IndexOf(AllVisibleSongs[i]);
                if (songIndexInPlaylist > previousCompletedIndex)
                {
                    insertIndexInAllVisible = i;
                    break;
                }
                insertIndexInAllVisible = i + 1;
            }
                
            AllVisibleSongs.Insert(insertIndexInAllVisible, previousCompletedSong);
                
            ClearSearch();
                
            RefreshDisplayedPlaylist();
                
            var newIndex = DisplayedPlaylist.IndexOf(previousCompletedSong);
            if (newIndex >= 0)
            {
                PlaylistBox.SelectedIndex = newIndex;
            }
        }
    }

    public void ScrollToTop()
    {
        if (DisplayedPlaylist.Count > 0)
        {
            PlaylistBox.ScrollIntoView(DisplayedPlaylist[0]);
        }
    }

    public void HideSongsBetween(int currentPlayingIndex, int selectedIndex)
    {
        if (!PlaybackStateValidator.IsValidIndex(currentPlayingIndex, AllVisibleSongs.Count) ||
            !PlaybackStateValidator.IsValidIndex(selectedIndex, AllVisibleSongs.Count))
        {
            return;
        }

        if (selectedIndex > currentPlayingIndex)
        {
            var songsToHide = new List<MusicFile>();
            for (var i = currentPlayingIndex; i < selectedIndex; i++)
            {
                songsToHide.Add(AllVisibleSongs[i]);
            }

            foreach (var song in songsToHide)
            {
                song.IsCompleted = true;
                AllVisibleSongs.Remove(song);
            }

            RefreshDisplayedPlaylist();
        }
    }

    public void HideSongsBefore(int beforeIndex)
    {
        if (!PlaybackStateValidator.IsValidIndex(beforeIndex, AllVisibleSongs.Count) || beforeIndex <= 0)
        {
            return;
        }

        var songsToHide = new List<MusicFile>();
        for (var i = 0; i < beforeIndex; i++)
        {
            songsToHide.Add(AllVisibleSongs[i]);
        }

        foreach (var song in songsToHide)
        {
            song.IsCompleted = true;
            AllVisibleSongs.Remove(song);
        }

        RefreshDisplayedPlaylist();
    }

    private void RefreshDisplayedPlaylist()
    {
        DisplayedPlaylist.Clear();
            
        if (string.IsNullOrWhiteSpace(currentSearchText))
        {
            foreach (var song in AllVisibleSongs)
            {
                DisplayedPlaylist.Add(song);
            }
        }
        else
        {
            var searchText = currentSearchText.Trim().ToLowerInvariant();
            foreach (var song in AllVisibleSongs.Where(song => MatchesSearch(song, searchText)))
            {
                DisplayedPlaylist.Add(song);
            }
        }
            
        UpdateEmptyState();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox searchBox)
        {
            currentSearchText = searchBox.Text;
            ApplySearchFilter();
        }
    }

    private void ApplySearchFilter()
    {
        RefreshDisplayedPlaylist();
    }

    private static bool MatchesSearch(MusicFile song, string searchText)
    {
        var fileName = song.FileName.ToLowerInvariant();
            
        if (fileName.Contains(searchText))
        {
            return true;
        }
            
        var separators = new[] { " - ", " – ", " — ", "-", "–", "—" };
        return (from separator in separators
            where fileName.Contains(separator)
            let parts = fileName.Split([separator], StringSplitOptions.None)
            where parts.Length >= 2
            let artist = parts[0].Trim()
            let songName = parts.Length > 2
                ? string.Join(separator, parts.Skip(1)).Trim()
                : parts[1].Trim()
            where artist.Contains(searchText) || songName.Contains(searchText)
            select artist).Any();
    }
}