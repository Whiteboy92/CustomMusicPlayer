using System.Windows;

namespace MusicPlayer.Views.Settings
{
    public partial class SettingsView
    {
        public event EventHandler? MusicFolderChangeRequested;
        public event EventHandler? DatabaseResetRequested;
        public event EventHandler? PlayHistoryClearRequested;
        public event EventHandler<bool>? AutoPlayOnStartupChanged;

        public SettingsView()
        {
            InitializeComponent();
        }

        public void SetMusicFolderPath(string path)
        {
            TxtMusicFolder.Text = path;
        }

        public void SetDatabaseStats(int songCount, int totalPlays, string dbLocation)
        {
            TxtSongCount.Text = songCount.ToString();
            TxtTotalPlays.Text = totalPlays.ToString();
            TxtDatabaseLocation.Text = dbLocation;
        }

        public void SetAutoPlayOnStartup(bool enabled)
        {
            ChkAutoPlayOnStartup.IsChecked = enabled;
        }

        private void BtnChangeMusicFolder_Click(object sender, RoutedEventArgs e)
        {
            MusicFolderChangeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnResetDatabase_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ WARNING ⚠️\n\n" +
                "This will permanently delete:\n" +
                "• All play counts and statistics\n" +
                "• Cached song durations\n" +
                "• Current queue and playback state\n" +
                "• All equalizer and volume settings\n\n" +
                "This action CANNOT be undone!\n\n" +
                "Are you absolutely sure you want to reset the database?",
                "Reset Database - Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                var doubleCheck = MessageBox.Show(
                    "Last chance!\n\nReset database and delete all data?",
                    "Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop,
                    MessageBoxResult.No);

                if (doubleCheck == MessageBoxResult.Yes)
                {
                    DatabaseResetRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all play counts and listening history?\n\n" +
                "This will:\n" +
                "• Reset all play counts to 0\n" +
                "• Clear statistics\n\n" +
                "Cached durations and settings will be preserved.\n\n" +
                "Continue?",
                "Clear Play History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                PlayHistoryClearRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ChkAutoPlayOnStartup_Changed(object sender, RoutedEventArgs e)
        {
            AutoPlayOnStartupChanged?.Invoke(this, ChkAutoPlayOnStartup.IsChecked ?? false);
        }
    }
}

