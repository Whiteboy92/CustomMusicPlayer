using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Services;

namespace MusicPlayer.Views.Player
{
    public partial class PlayerControlsView : IDisposable
    {
        private readonly IAudioService audioService;
        private readonly DispatcherTimer timer;
        private bool isUserDraggingSlider;
        private DispatcherTimer? volumePopupTimer;
        private double songStartPosition;
        private double songTotalDuration;
        private const short SongPercentagePlayed = 65;
        private bool disposed;

        public event EventHandler? PlayRequested;
        public event EventHandler? PreviousRequested;
        public event EventHandler? NextRequested;
        public event EventHandler? ShuffleRequested;
        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<bool>? SongFinished;
        public event EventHandler? MediaOpenedEvent;

        public PlayerControlsView() : this(new AudioService())
        {
        }

        public PlayerControlsView(IAudioService audioServiceInstance)
        {
            audioService = audioServiceInstance;
            
            InitializeComponent();

            audioService.PlaybackStopped += AudioService_PlaybackStopped;
            audioService.MediaOpened += AudioService_MediaOpened;
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            timer.Tick += Timer_Tick;
            audioService.Volume = (float)(VolumeSlider.Value / 100.0);
            UpdateVolumeIcon();
        }

        private void UpdateVolumeIcon()
        {
            double volume = VolumeSlider.Value;
            if (volume == 0)
            {
                SpeakerIcon.Text = "🔇";
            }
            else if (volume < 33)
            {
                SpeakerIcon.Text = "🔈";
            }
            else if (volume < 66)
            {
                SpeakerIcon.Text = "🔉";
            }
            else
            {
                SpeakerIcon.Text = "🔊";
            }
        }

        public void SetVolumePercent(double percent)
        {
            var clamped = Math.Max(0, Math.Min(100, percent));
            VolumeSlider.Value = clamped;
            audioService.Volume = (float)(clamped / 100.0);
            TxtVolume.Text = $"{(int)clamped}%";
            UpdateVolumeIcon();
        }

        private void VolumeControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            volumePopupTimer?.Stop();
            VolumePopup.IsOpen = true;
        }

        private void VolumeControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            volumePopupTimer?.Stop();
            volumePopupTimer = null;
            
            volumePopupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            volumePopupTimer.Tick += (_, _) =>
            {
                if (!VolumePopup.IsMouseOver && !VolumeControl.IsMouseOver)
                {
                    VolumePopup.IsOpen = false;
                }
                volumePopupTimer?.Stop();
            };
            volumePopupTimer.Start();
        }

        private void VolumePopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            volumePopupTimer?.Stop();
        }

        private void VolumePopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            VolumePopup.IsOpen = false;
        }

        private bool isCurrentlyPlaying;
        private bool isLoadingNewTrack;
        
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (isUserDraggingSlider || !audioService.HasSource)
            {
                return;
            }
            
            try
            {
                var currentTime = audioService.CurrentTime;
                var totalTime = audioService.TotalTime;
                
                if (totalTime.TotalSeconds > 0)
                {
                    ProgressSlider.Maximum = totalTime.TotalSeconds;
                    ProgressSlider.Value = currentTime.TotalSeconds;
                    TxtCurrentTime.Text = FormatTime(currentTime);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer tick error: {ex.Message}");
            }
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }

        private void ResetProgressBar()
        {
            ProgressSlider.Value = 0;
            TxtCurrentTime.Text = "00:00";
            songStartPosition = 0;
        }

        private void UpdatePlayPauseButtonState()
        {
            BtnPlayPause.Content = isCurrentlyPlaying ? "⏸ Pause" : "▶ Play";
        }

        public bool IsPlaying => audioService.IsPlaying;

        public void LoadSong(MusicFile musicFile)
        {
            try
            {
                timer.Stop();
                ProgressSlider.Value = 0;
                TxtCurrentTime.Text = "00:00";
                
                songStartPosition = 0;
                currentSongPath = musicFile.FilePath;
                audioService.LoadFile(musicFile.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void PlaySong(MusicFile musicFile)
        {
            try
            {
                
                isLoadingNewTrack = true;
                timer.Stop();
                isCurrentlyPlaying = false;
                ResetProgressBar();
                currentSongPath = musicFile.FilePath;
                audioService.LoadFile(musicFile.FilePath);
                audioService.Play();
                isCurrentlyPlaying = true;
                timer.Start();
                
                UpdatePlayPauseButtonState();
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => { isLoadingNewTrack = false; }));
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() =>
                    {
                        if (isCurrentlyPlaying)
                        {
                            if (!timer.IsEnabled)
                            {
                                timer.Start();
                            }
                            UpdatePlayPauseButtonState();
                        }
                    }));
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show($"Error starting playback:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception msgBoxEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting playback and showing message: {ex.Message}, MessageBox error: {msgBoxEx.Message}");
                }
            }
        }

        public void Play()
        {
            if (audioService.HasSource)
            {
                audioService.Play();
                isCurrentlyPlaying = true;
                timer.Start();
                UpdatePlayPauseButtonState();
            }
        }

        public void Pause()
        {
            if (audioService.HasSource)
            {
                audioService.Pause();
                isCurrentlyPlaying = false;
                timer.Stop();
                UpdatePlayPauseButtonState();
            }
        }

        public void Stop()
        {
            audioService.Stop();
            isCurrentlyPlaying = false;
            timer.Stop();
            ResetProgressBar();
            UpdatePlayPauseButtonState();
        }

        public bool HasSource => audioService.HasSource;

        private string? currentSongPath;

        public double GetCurrentPositionSeconds()
        {
            return audioService.CurrentTime.TotalSeconds;
        }

        public void SetPosition(double seconds)
        {
            if (audioService.HasSource)
            {
                audioService.CurrentTime = TimeSpan.FromSeconds(seconds);
                songStartPosition = seconds;
                if (isUserDraggingSlider) { return; }
                ProgressSlider.Value = seconds;
                TxtCurrentTime.Text = FormatTime(TimeSpan.FromSeconds(seconds));
            }
        }

        public string? GetCurrentSongPath()
        {
            return currentSongPath;
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (isCurrentlyPlaying)
            {
                Pause();
            }
            else
            {
                if (audioService.HasSource)
                {
                    Play();
                }
                else
                {
                    PlayRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnShuffle_Click(object sender, RoutedEventArgs e)
        {
            ShuffleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnVolumeDown_Click(object sender, RoutedEventArgs e)
        {
            VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 1);
        }

        private void BtnVolumeUp_Click(object sender, RoutedEventArgs e)
        {
            VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 1);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioService.Volume = (float)(VolumeSlider.Value / 100.0);
            TxtVolume.Text = $"{(int)VolumeSlider.Value}%";
            UpdateVolumeIcon();
            VolumeChanged?.Invoke(this, VolumeSlider.Value);
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            isUserDraggingSlider = true;
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isUserDraggingSlider = false;
            if (audioService.HasSource)
            {
                audioService.CurrentTime = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        private void AudioService_MediaOpened(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressSlider.Maximum = audioService.TotalTime.TotalSeconds;
                TxtTotalTime.Text = FormatTime(audioService.TotalTime);
                songTotalDuration = audioService.TotalTime.TotalSeconds;
                
                // Raise the MediaOpenedEvent for external subscribers
                MediaOpenedEvent?.Invoke(this, EventArgs.Empty);
            });
        }

        private void AudioService_PlaybackStopped(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (isLoadingNewTrack)
                {
                    
                    return;
                }

                isCurrentlyPlaying = false;
                timer.Stop();
                if (audioService.CurrentTime >= audioService.TotalTime - TimeSpan.FromSeconds(1))
                {
                    SongFinished?.Invoke(this, true);
                    NextRequested?.Invoke(this, EventArgs.Empty);
                }
                
                UpdatePlayPauseButtonState();
            });
        }

        public bool WasPlayedEnough()
        {
            if (songTotalDuration <= 0) return false;
            
            var currentPosition = audioService.CurrentTime.TotalSeconds;
            var playedDuration = currentPosition - songStartPosition;
            var percentagePlayed = (playedDuration / songTotalDuration) * 100;
            
            return percentagePlayed >= SongPercentagePlayed;
        }

        public void ResetPlayTracking()
        {
            songStartPosition = 0;
            songTotalDuration = 0;
        }
        public void SetBand80Hz(float gain)
        {
            audioService.Band80Hz = gain;
        }

        public void SetBand240Hz(float gain)
        {
            audioService.Band240Hz = gain;
        }

        public void SetBand750Hz(float gain)
        {
            audioService.Band750Hz = gain;
        }

        public void SetBand2200Hz(float gain)
        {
            audioService.Band2200Hz = gain;
        }

        public void SetBand6600Hz(float gain)
        {
            audioService.Band6600Hz = gain;
        }

        public (float band80, float band240, float band750, float band2200, float band6600) GetEqualizerGains()
        {
            return (audioService.Band80Hz, audioService.Band240Hz, audioService.Band750Hz, 
                    audioService.Band2200Hz, audioService.Band6600Hz);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                timer.Stop();
                volumePopupTimer?.Stop();
                audioService.Dispose();
                disposed = true;
            }
        }
    }
}

