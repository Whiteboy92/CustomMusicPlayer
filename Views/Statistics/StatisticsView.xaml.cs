using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MusicPlayer.Models;

namespace MusicPlayer.Views.Statistics;

public class SongStatistic
{
    public int Rank { get; init; }
    public string RankIcon { get; set; } = "";
    public string FileName { get; init; } = "";
    public string FilePath { get; set; } = "";
    public string Duration { get; init; } = "";
    public int PlayCount { get; init; }
}

public partial class StatisticsView
{
    private ObservableCollection<SongStatistic> Statistics { get; } = [];
    public event EventHandler<int?>? PlaylistSelectionChanged;

    public StatisticsView()
    {
        InitializeComponent();
        StatsList.ItemsSource = Statistics;
        UpdateEmptyState();
    }

    public void InitializePlaylistDropdown(List<Models.Playlist> playlists, string defaultQueueName = "Default Queue")
    {
        PlaylistDropdown.Items.Clear();
        PlaylistDropdown.Items.Add(new ComboBoxItem { Content = defaultQueueName, Tag = (int?)-1 });
            
        foreach (var playlist in playlists)
        {
            PlaylistDropdown.Items.Add(new ComboBoxItem { Content = playlist.Name, Tag = (int?)playlist.Id });
        }
            
        PlaylistDropdown.SelectedIndex = 0;
    }

    public void SetSelectedPlaylist(int? playlistId)
    {
        var targetTag = playlistId ?? -1;
        for (int i = 0; i < PlaylistDropdown.Items.Count; i++)
        {
            if (PlaylistDropdown.Items[i] is ComboBoxItem { Tag: int tagValue } && tagValue == targetTag)
            {
                PlaylistDropdown.SelectedIndex = i;
                return;
            }
        }
    }

    private void PlaylistDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistDropdown.SelectedItem is not ComboBoxItem { Tag: int tagValue }) { return; }
        int? playlistId = tagValue == -1 ? null : tagValue;
        PlaylistSelectionChanged?.Invoke(this, playlistId);
    }

    public void LoadStatistics(List<MusicFile> songs)
    {
        Statistics.Clear();
        var playedSongs = songs
            .Where(s => s.PlayCount > 0)
            .OrderByDescending(s => s.PlayCount)
            .ThenBy(s => s.FileName)
            .ToList();

        var rank = 1;
        foreach (var song in playedSongs)
        {
            var rankIcon = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => "",
            };

            Statistics.Add(new SongStatistic
            {
                Rank = rank,
                RankIcon = rankIcon,
                FileName = song.FileName,
                FilePath = song.FilePath,
                Duration = song.Duration,
                PlayCount = song.PlayCount,
            });

            rank++;
        }

        UpdateEmptyState();
        UpdateStatsSummary(playedSongs);
    }

    private void UpdateStatsSummary(List<MusicFile> playedSongs)
    {
        if (playedSongs.Count == 0)
        {
            TxtStats.Text = "Total plays: 0  •  Most played song: None";
            return;
        }

        var totalPlays = playedSongs.Sum(s => s.PlayCount);
        var topSong = playedSongs.First();

        TxtStats.Text = $"Total plays: {totalPlays}  •  Most played: {topSong.FileName}";
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = Statistics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatsList.Visibility = Statistics.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}