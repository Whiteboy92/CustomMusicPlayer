using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace MusicPlayer.Views.Settings
{
    public partial class SettingsView
    {
        public event EventHandler? MusicFolderChangeRequested;
        public event EventHandler? DatabaseResetRequested;
        public event EventHandler? PlayHistoryClearRequested;
        public event EventHandler<bool>? AutoPlayOnStartupChanged;
        public event EventHandler<string?>? DiscordClientIdChanged;

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

        public void SetDiscordClientId(string? clientId)
        {
            TxtDiscordClientId.Text = clientId ?? string.Empty;
        }

        private void BtnChangeMusicFolder_Click(object sender, RoutedEventArgs e)
        {
            MusicFolderChangeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnResetDatabase_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "âš ï¸ WARNING âš ï¸\n\n" +
                "This will permanently delete:\n" +
                "â€¢ All play counts and statistics\n" +
                "â€¢ Cached song durations\n" +
                "â€¢ Current queue and playback state\n" +
                "â€¢ All equalizer and volume settings\n\n" +
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
                "â€¢ Reset all play counts to 0\n" +
                "â€¢ Clear statistics\n\n" +
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

        private void TxtDiscordClientId_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var clientId = string.IsNullOrWhiteSpace(TxtDiscordClientId.Text) 
                ? null 
                : TxtDiscordClientId.Text.Trim();
            DiscordClientIdChanged?.Invoke(this, clientId);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true,
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening link:\n\n{ex.Message}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

