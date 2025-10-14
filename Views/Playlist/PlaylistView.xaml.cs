using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicPlayer.Models;
using MusicPlayer.Validation;

namespace MusicPlayer.Views.Playlist
{
    public partial class PlaylistView
    {
        public ObservableCollection<MusicFile> Playlist { get; private set; } = new();
        private ObservableCollection<MusicFile> DisplayedPlaylist { get; set; } = new();

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
            
            int trackNumber = 1;
            foreach (var song in songs)
            {
                song.TrackNumber = trackNumber++;
                song.IsCompleted = false;
                Playlist.Add(song);
                DisplayedPlaylist.Add(song);
            }
            
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
                SongSelected?.Invoke(this, selectedFile);
            }
        }

        public int SelectedIndex
        {
            get => PlaylistBox.SelectedIndex;
            set => PlaylistBox.SelectedIndex = value;
        }

        public MusicFile? GetSongAtIndex(int index)
        {
            if (PlaybackStateValidator.IsValidIndex(index, DisplayedPlaylist.Count))
                return DisplayedPlaylist[index];
            return null;
        }

        public int GetPlaylistCount() => DisplayedPlaylist.Count;

        public void MarkCurrentSongAsCompleted()
        {
            if (!PlaybackStateValidator.IsValidIndex(PlaylistBox.SelectedIndex, DisplayedPlaylist.Count)) 
                return;
            
            int currentIndex = PlaylistBox.SelectedIndex;
            var song = DisplayedPlaylist[currentIndex];
            
            // Mark as completed and remove from displayed list
            song.IsCompleted = true;
            DisplayedPlaylist.RemoveAt(currentIndex);
            
            UpdateEmptyState();
            
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
            var currentDisplayedSong = PlaybackStateValidator.IsValidIndex(PlaylistBox.SelectedIndex, DisplayedPlaylist.Count)
                ? DisplayedPlaylist[PlaylistBox.SelectedIndex] 
                : null;

            int fullPlaylistIndex = -1;
            if (currentDisplayedSong != null)
            {
                fullPlaylistIndex = Playlist.IndexOf(currentDisplayedSong);
            }
            else if (PlaybackStateValidator.HasPlaylistItems(DisplayedPlaylist.Count))
            {
                fullPlaylistIndex = Playlist.IndexOf(DisplayedPlaylist[0]);
            }

            MusicFile? previousCompletedSong = null;
            int previousCompletedIndex = -1;
            
            for (int i = (fullPlaylistIndex > 0 ? fullPlaylistIndex - 1 : Playlist.Count - 1); 
                 i >= 0; 
                 i--)
            {
                if (Playlist[i].IsCompleted)
                {
                    previousCompletedSong = Playlist[i];
                    previousCompletedIndex = i;
                    break;
                }
            }

            if (previousCompletedSong != null)
            {
                previousCompletedSong.IsCompleted = false;
                
                int insertIndex = 0;
                for (int i = 0; i < DisplayedPlaylist.Count; i++)
                {
                    var displayedSongIndex = Playlist.IndexOf(DisplayedPlaylist[i]);
                    if (displayedSongIndex > previousCompletedIndex)
                    {
                        insertIndex = i;
                        break;
                    }
                    insertIndex = i + 1;
                }
                
                DisplayedPlaylist.Insert(insertIndex, previousCompletedSong);
                PlaylistBox.SelectedIndex = insertIndex;
                UpdateEmptyState();
            }
        }
    }
}
