using System.Collections.ObjectModel;
using System.Windows;
using MusicPlayer.Models;

namespace MusicPlayer.Views.Statistics
{
    public class SongStatistic
    {
        public int Rank { get; set; }
        public string RankIcon { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Duration { get; set; } = "";
        public int PlayCount { get; set; }
        public string DisplayRank => string.IsNullOrEmpty(RankIcon) ? Rank.ToString() : RankIcon;
    }

    public partial class StatisticsView
    {
        public ObservableCollection<SongStatistic> Statistics { get; private set; } = new();

        public StatisticsView()
        {
            InitializeComponent();
            StatsList.ItemsSource = Statistics;
            UpdateEmptyState();
        }

        public void LoadStatistics(List<MusicFile> songs)
        {
            Statistics.Clear();
            var playedSongs = songs
                .Where(s => s.PlayCount > 0)
                .OrderByDescending(s => s.PlayCount)
                .ThenBy(s => s.FileName)
                .ToList();

            int rank = 1;
            foreach (var song in playedSongs)
            {
                string rankIcon = rank switch
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

            int totalPlays = playedSongs.Sum(s => s.PlayCount);
            var topSong = playedSongs.First();

            TxtStats.Text = $"Total plays: {totalPlays}  •  Most played: {topSong.FileName} ({topSong.PlayCount} plays)";
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = Statistics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatsList.Visibility = Statistics.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

