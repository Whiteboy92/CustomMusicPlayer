using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicPlayer.Interfaces;

namespace MusicPlayer.Windows.CreatePlaylistWindow
{
    public partial class CreatePlaylistWindow
    {
        private readonly ISettingsService settings;
        private readonly IMusicLoaderService musicLoader;
        private readonly ObservableCollection<SelectableSong> allSongs = new();
        private readonly ObservableCollection<SelectableSong> filteredSongs = new();
        private readonly Models.Playlist? existingPlaylist;
        private readonly bool isEditMode;

        public CreatePlaylistWindow(ISettingsService settingsService, IMusicLoaderService musicLoaderService, Models.Playlist? playlistToEdit = null)
        {
            settings = settingsService;
            musicLoader = musicLoaderService;
            existingPlaylist = playlistToEdit;
            isEditMode = playlistToEdit != null;
            
            InitializeComponent();
            
            if (isEditMode && existingPlaylist != null)
            {
                CustomTitleBar.Title = "Edit Playlist";
                TxtPlaylistName.Text = existingPlaylist.Name;
                TxtGenre.Text = existingPlaylist.Genre ?? "";
                TxtTags.Text = existingPlaylist.Tags ?? "";
                BtnSave.Content = "Update";
                
                // If editing default queue, hide song selection UI
                if (existingPlaylist.IsDefaultQueue)
                {
                    SongSelectionSection.Visibility = Visibility.Collapsed;
                    TxtPlaylistName.Focus();
                }
            }
            
            LoadAllSongs();
            SongsItemsControl.ItemsSource = filteredSongs;
        }

        private async void LoadAllSongs()
        {
            try
            {
                var musicFiles = await musicLoader.LoadMusicFromFolderAsync();
                var durations = settings.GetAllDurations();
                var songs = new List<SelectableSong>();

                foreach (var musicFile in musicFiles)
                {
                    if (!File.Exists(musicFile.FilePath))
                        continue;

                    var filename = Path.GetFileNameWithoutExtension(musicFile.FilePath);
                    
                    var parts = filename.Split([" - "], 2, StringSplitOptions.None);
                    string title, artist;
                    
                    if (parts.Length == 2)
                    {
                        title = parts[0].Trim();
                        artist = parts[1].Trim();
                    }
                    else
                    {
                        title = filename;
                        artist = "Unknown Artist";
                    }

                    songs.Add(new SelectableSong
                    {
                        FilePath = musicFile.FilePath,
                        Title = title,
                        Artist = artist,
                        DisplayName = $"{title} - {artist}",
                        Duration = GetDurationString(musicFile.FilePath, durations)
                    });
                }

                var sortedSongs = songs.OrderBy(s => s.Artist).ThenBy(s => s.Title);

                allSongs.Clear();
                foreach (var song in sortedSongs)
                {
                    if (isEditMode && existingPlaylist != null)
                    {
                        song.IsSelected = existingPlaylist.SongFilePaths.Contains(song.FilePath);
                    }
                    
                    allSongs.Add(song);
                    song.PropertyChanged += Song_PropertyChanged;
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading songs:\n\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Song_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableSong.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            var count = allSongs.Count(s => s.IsSelected);
            TxtSelectedCount.Text = $"{count} song{(count != 1 ? "s" : "")} selected";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private string GetDurationString(string filePath, Dictionary<string, string> durations)
        {
            try
            {
                if (durations.TryGetValue(filePath, out var durationStr) && !string.IsNullOrEmpty(durationStr))
                {
                    if (durationStr != "--:--" && durationStr != "0:00")
                    {
                        return durationStr;
                    }
                }
                
                var caseInsensitiveMatch = durations.Keys.FirstOrDefault(k => 
                    string.Equals(k, filePath, StringComparison.OrdinalIgnoreCase));
                
                if (caseInsensitiveMatch != null && durations.TryGetValue(caseInsensitiveMatch, out var caseDurationStr) && 
                    !string.IsNullOrEmpty(caseDurationStr) && caseDurationStr != "--:--" && caseDurationStr != "0:00")
                {
                    return caseDurationStr;
                }
                
                var normalizedPath = filePath.Replace('\\', '/');
                var normalizedMatch = durations.Keys.FirstOrDefault(k => 
                    k.Replace('\\', '/') == normalizedPath);
                
                if (normalizedMatch != null && durations.TryGetValue(normalizedMatch, out var normDurationStr) && 
                    !string.IsNullOrEmpty(normDurationStr) && normDurationStr != "--:--" && normDurationStr != "0:00")
                {
                    return normDurationStr;
                }
                
                return "--:--";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting duration for {filePath}: {ex.Message}");
                return "--:--";
            }
        }

        private void ApplyFilter()
        {
            var searchText = TxtSearch.Text.Trim().ToLower();

            filteredSongs.Clear();

            foreach (var song in allSongs)
            {
                if (string.IsNullOrEmpty(searchText) ||
                    song.Title.ToLower().Contains(searchText) ||
                    song.Artist.ToLower().Contains(searchText))
                {
                    filteredSongs.Add(song);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var playlistName = TxtPlaylistName.Text.Trim();

            if (string.IsNullOrEmpty(playlistName))
            {
                MessageBox.Show("Please enter a playlist name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPlaylistName.Focus();
                return;
            }

            try
            {
                var genre = string.IsNullOrWhiteSpace(TxtGenre.Text) ? null : TxtGenre.Text.Trim();
                var tags = string.IsNullOrWhiteSpace(TxtTags.Text) ? null : TxtTags.Text.Trim();

                if (isEditMode && existingPlaylist != null)
                {
                    // Special handling for default queue - only update metadata, not songs
                    if (existingPlaylist.IsDefaultQueue)
                    {
                        settings.UpdatePlaylist(existingPlaylist.Id, playlistName, genre, tags, existingPlaylist.SongFilePaths);
                        
                        var summary = $"Playlist Name: {playlistName}\n";

                        if (!string.IsNullOrEmpty(genre))
                            summary += $"Genre: {genre}\n";

                        if (!string.IsNullOrEmpty(tags))
                            summary += $"Tags: {tags}\n";

                        ShowSuccessDialog($"Playlist updated successfully!\n\n{summary}");
                    }
                    else
                    {
                        // Normal playlist - allow editing songs
                        var selectedSongs = allSongs.Where(s => s.IsSelected).ToList();

                        if (selectedSongs.Count == 0)
                        {
                            var result = MessageBox.Show(
                                "No songs selected. Do you want to save an empty playlist?",
                                "No Songs Selected",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result != MessageBoxResult.Yes)
                                return;
                        }

                        var songFilePaths = selectedSongs.Select(s => s.FilePath).ToList();
                        settings.UpdatePlaylist(existingPlaylist.Id, playlistName, genre, tags, songFilePaths);
                        
                        var summary = $"Playlist Name: {playlistName}\n" +
                                     $"Songs: {selectedSongs.Count}\n";

                        if (!string.IsNullOrEmpty(genre))
                            summary += $"Genre: {genre}\n";

                        if (!string.IsNullOrEmpty(tags))
                            summary += $"Tags: {tags}\n";

                        ShowSuccessDialog($"Playlist updated successfully!\n\n{summary}");
                    }
                }
                else
                {
                    // Creating new playlist
                    var selectedSongs = allSongs.Where(s => s.IsSelected).ToList();

                    if (selectedSongs.Count == 0)
                    {
                        var result = MessageBox.Show(
                            "No songs selected. Do you want to save an empty playlist?",
                            "No Songs Selected",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                            return;
                    }

                    var songFilePaths = selectedSongs.Select(s => s.FilePath).ToList();
                    settings.CreatePlaylist(playlistName, genre, tags, songFilePaths);
                    
                    var summary = $"Playlist Name: {playlistName}\n" +
                                 $"Songs: {selectedSongs.Count}\n";

                    if (!string.IsNullOrEmpty(genre))
                        summary += $"Genre: {genre}\n";

                    if (!string.IsNullOrEmpty(tags))
                        summary += $"Tags: {tags}\n";

                    ShowSuccessDialog($"Playlist created successfully!\n\n{summary}");
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving playlist:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSuccessDialog(string message)
        {
            var dialog = new Window
            {
                Title = "Success",
                Width = 450,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize
            };

            var outerBorder = new Border
            {
                Background = (Brush)FindResource("BackgroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Height = 40
            };

            var titleBarGrid = new Grid();
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBarText = new TextBlock
            {
                Text = "Success",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(15, 0, 0, 0)
            };

            var closeButton = new Button
            {
                Content = "✕",
                Width = 40,
                Height = 40,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = Cursors.Hand
            };

            closeButton.Click += (_, _) => dialog.Close();

            closeButton.MouseEnter += (_, _) => closeButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            closeButton.MouseLeave += (_, _) => closeButton.Background = Brushes.Transparent;

            titleBarGrid.Children.Add(titleBarText);
            Grid.SetColumn(titleBarText, 0);
            titleBarGrid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 1);

            titleBar.Child = titleBarGrid;
            titleBar.MouseLeftButtonDown += (_, _) => dialog.DragMove();

            var contentStackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(30, 20, 30, 20)
            };

            var messageText = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = (Brush)FindResource("TextBrush"),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            contentStackPanel.Children.Add(messageText);

            var buttonBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 40,
                Background = (Brush)FindResource("AccentBrush"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };

            var buttonStyle = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var templateBorder = new FrameworkElementFactory(typeof(Border));
            templateBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            templateBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            templateBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            
            templateBorder.AppendChild(contentPresenter);
            template.VisualTree = templateBorder;
            buttonStyle.Setters.Add(new Setter(TemplateProperty, template));
            
            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(OpacityProperty, 0.9));
            buttonStyle.Triggers.Add(hoverTrigger);
            
            okButton.Style = buttonStyle;
            okButton.Click += (_, _) => dialog.Close();

            buttonBorder.Child = okButton;

            Grid.SetRow(titleBar, 0);
            Grid.SetRow(contentStackPanel, 1);
            Grid.SetRow(buttonBorder, 2);

            rootGrid.Children.Add(titleBar);
            rootGrid.Children.Add(contentStackPanel);
            rootGrid.Children.Add(buttonBorder);

            outerBorder.Child = rootGrid;
            dialog.Content = outerBorder;

            dialog.ShowDialog();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class SelectableSong : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

