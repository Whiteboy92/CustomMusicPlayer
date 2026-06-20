using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Services;

namespace MusicPlayer.Views.Player;

public partial class PlayerControlsView : IDisposable
{
    private readonly IAudioService audioService;
    private readonly DispatcherTimer timer;
    private DispatcherTimer? volumePopupTimer;

    private bool isUserDraggingSlider;
    private bool isCurrentlyPlaying;
    private bool isLoadingNewTrack;
    private bool disposed;
    private bool autoPlayEnabled = true;
    private bool suppressAutoPlayEvent;

    private double songTotalDuration;
    private double cumulativePlayedSeconds;
    private double lastTickPosition;

    private string? currentSongPath;
    private const short SongPercentagePlayed = 65;

    // Perceptual volume taper. The slider value (0-100) is what the user sees and
    // what gets stored; actual audio gain follows this power curve so the usable
    // listening range is spread across the slider instead of crammed into 0-2%.
    // Anchored so slider 50% -> 0.02 gain (0.5^VolumeCurveExponent == 0.02).
    private const double VolumeCurveExponent = 5.64;

    private static float SliderToGain(double sliderPercent)
    {
        var normalized = Math.Clamp(sliderPercent / 100.0, 0.0, 1.0);
        return (float)Math.Pow(normalized, VolumeCurveExponent);
    }

    public event EventHandler? PlayRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? ShuffleRequested;
    public event EventHandler<double>? VolumeChanged;
    public event EventHandler<bool>? SongFinished;
    public event EventHandler? MediaOpenedEvent;
    public event EventHandler<bool>? AutoPlayChanged;

    public PlayerControlsView() : this(new AudioService()) { }

    private PlayerControlsView(IAudioService audioServiceInstance)
    {
        audioService = audioServiceInstance;

        InitializeComponent();

        audioService.PlaybackStopped += AudioService_PlaybackStopped;
        audioService.PlaybackEnded += AudioService_PlaybackEnded;
        audioService.MediaOpened += AudioService_MediaOpened;

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += Timer_Tick;

        audioService.Volume = SliderToGain(VolumeSlider.Value);
        UpdateVolumeIcon();
    }

    public void SetVolumePercent(double percent)
    {
        var clamped = Math.Max(0, Math.Min(100, percent));
        VolumeSlider.Value = clamped;
        audioService.Volume = SliderToGain(clamped);
        TxtVolume.Text = $"{(int)clamped}%";
        UpdateVolumeIcon();
    }

    private void UpdateVolumeIcon()
    {
        var volume = VolumeSlider.Value;
        SpeakerIcon.Text = volume switch
        {
            0 => "🔇",
            < 33 => "🔈",
            < 66 => "🔉",
            _ => "🔊",
        };
    }

    private void VolumeControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        volumePopupTimer?.Stop();
        VolumePopup.IsOpen = true;
    }

    private void VolumeControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        volumePopupTimer?.Stop();
        volumePopupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

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

    private void VolumePopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
        volumePopupTimer?.Stop();

    private void VolumePopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        VolumePopup.IsOpen = false;

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        audioService.Volume = SliderToGain(VolumeSlider.Value);
        TxtVolume.Text = $"{(int)VolumeSlider.Value}%";
        UpdateVolumeIcon();
        VolumeChanged?.Invoke(this, VolumeSlider.Value);
    }

    private void BtnVolumeDown_Click(object sender, RoutedEventArgs e) =>
        VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 1);

    private void BtnVolumeUp_Click(object sender, RoutedEventArgs e) =>
        VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 1);

    public bool IsPlaying => audioService.IsPlaying;
    public bool HasSource => audioService.HasSource;

    public void LoadSong(MusicFile musicFile)
    {
        try
        {
            timer.Stop();
            ResetProgressBar();

            currentSongPath = musicFile.FilePath;
            audioService.LoadFile(musicFile.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading file:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            cumulativePlayedSeconds = 0;
            lastTickPosition = 0;

            currentSongPath = musicFile.FilePath;
            audioService.LoadFile(musicFile.FilePath);
            audioService.Play();

            isCurrentlyPlaying = true;
            timer.Start();
            UpdatePlayPauseButtonState();

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => isLoadingNewTrack = false));
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (isCurrentlyPlaying && !timer.IsEnabled)
                    timer.Start();
                UpdatePlayPauseButtonState();
            }));
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show($"Error starting playback:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception msgBoxEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting playback and showing message: {ex.Message}, MessageBox error: {msgBoxEx.Message}");
            }
        }
    }

    public void Play()
    {
        if (!audioService.HasSource) return;
        audioService.Play();
        isCurrentlyPlaying = true;
        timer.Start();
        UpdatePlayPauseButtonState();
    }

    private void Pause()
    {
        if (!audioService.HasSource) return;
        audioService.Pause();
        isCurrentlyPlaying = false;
        timer.Stop();
        UpdatePlayPauseButtonState();
    }

    public void Stop()
    {
        audioService.Stop();
        isCurrentlyPlaying = false;
        timer.Stop();
        ResetProgressBar();
        UpdatePlayPauseButtonState();
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (isCurrentlyPlaying)
            Pause();
        else if (audioService.HasSource)
            Play();
        else
            PlayRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e) =>
        PreviousRequested?.Invoke(this, EventArgs.Empty);

    private void BtnNext_Click(object sender, RoutedEventArgs e) =>
        NextRequested?.Invoke(this, EventArgs.Empty);

    private void BtnShuffle_Click(object sender, RoutedEventArgs e) =>
        ShuffleRequested?.Invoke(this, EventArgs.Empty);

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (isUserDraggingSlider || !audioService.HasSource) return;

        try
        {
            var currentTime = audioService.CurrentTime;
            var totalTime = audioService.TotalTime;

            if (totalTime.TotalSeconds <= 0) return;

            ProgressSlider.Maximum = totalTime.TotalSeconds;
            ProgressSlider.Value = currentTime.TotalSeconds;
            TxtCurrentTime.Text = FormatTime(currentTime);

            if (!isCurrentlyPlaying) return;

            var currentPosition = currentTime.TotalSeconds;
            if (lastTickPosition > 0 && currentPosition > lastTickPosition)
            {
                var deltaTime = currentPosition - lastTickPosition;
                if (deltaTime is > 0 and < 2.0)
                    cumulativePlayedSeconds += deltaTime;
            }

            lastTickPosition = currentPosition;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Timer tick error: {ex.Message}");
        }
    }

    private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e) =>
        isUserDraggingSlider = true;

    private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        isUserDraggingSlider = false;
        if (audioService.HasSource)
            audioService.CurrentTime = TimeSpan.FromSeconds(ProgressSlider.Value);
    }

    private void ResetProgressBar()
    {
        ProgressSlider.Value = 0;
        TxtCurrentTime.Text = "00:00";
        lastTickPosition = 0;
    }

    private static string FormatTime(TimeSpan time) =>
        $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

    // Filled play triangle / two pause bars, swapped in place so the button keeps
    // its layout and the icon stays consistent with the other vector controls.
    private static readonly Geometry PlayIconGeometry = Geometry.Parse("M7,4 L7,20 L20,12 Z");
    private static readonly Geometry PauseIconGeometry = Geometry.Parse("M7,5 L7,19 L11,19 L11,5 Z M14,5 L14,19 L18,19 L18,5 Z");

    private void UpdatePlayPauseButtonState()
    {
        PlayPauseIcon.Data = isCurrentlyPlaying ? PauseIconGeometry : PlayIconGeometry;
        PlayPauseLabel.Text = isCurrentlyPlaying ? "Pause" : "Play";
    }

    private void AudioService_MediaOpened(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressSlider.Maximum = audioService.TotalTime.TotalSeconds;
            TxtTotalTime.Text = FormatTime(audioService.TotalTime);
            songTotalDuration = audioService.TotalTime.TotalSeconds;
            MediaOpenedEvent?.Invoke(this, EventArgs.Empty);
        });
    }

    private void AudioService_PlaybackStopped(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Loading a new track stops the old one, which raises this
            // asynchronously. Ignore it mid-load so it doesn't clobber the
            // already-updated "now playing" UI state.
            if (isLoadingNewTrack) return;

            isCurrentlyPlaying = false;
            timer.Stop();
            UpdatePlayPauseButtonState();
        });
    }

    private void AudioService_PlaybackEnded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // A new track being loaded triggers a Stop first; guard against
            // treating that as a finished song.
            if (isLoadingNewTrack) return;

            SongFinished?.Invoke(this, true);

            if (autoPlayEnabled)
            {
                // Defer: NextRequested loads a new track, which disposes the
                // device that is still on the stack inside this PlaybackStopped
                // callback. Re-initializing NAudio from there can silently fail,
                // so let the handler unwind first.
                Dispatcher.BeginInvoke(new Action(() => NextRequested?.Invoke(this, EventArgs.Empty)));
            }
        });
    }

    private void BtnAutoplay_Checked(object sender, RoutedEventArgs e)
    {
        autoPlayEnabled = true;
        if (suppressAutoPlayEvent) return;
        AutoPlayChanged?.Invoke(this, true);
    }

    private void BtnAutoplay_Unchecked(object sender, RoutedEventArgs e)
    {
        autoPlayEnabled = false;
        if (suppressAutoPlayEvent) return;
        AutoPlayChanged?.Invoke(this, false);
    }

    // Restoring the persisted value on startup; don't echo it straight back as a
    // user-initiated change (which would re-save + flush for nothing).
    public void SetAutoPlay(bool enabled)
    {
        suppressAutoPlayEvent = true;
        autoPlayEnabled = enabled;
        BtnAutoplay.IsChecked = enabled;
        suppressAutoPlayEvent = false;
    }

    public double GetCurrentPositionSeconds() => audioService.CurrentTime.TotalSeconds;

    public void SetPosition(double seconds)
    {
        if (!audioService.HasSource) return;
        audioService.CurrentTime = TimeSpan.FromSeconds(seconds);
        lastTickPosition = seconds;

        if (isUserDraggingSlider) return;

        ProgressSlider.Value = seconds;
        TxtCurrentTime.Text = FormatTime(TimeSpan.FromSeconds(seconds));
    }

    public string? GetCurrentSongPath() => currentSongPath;

    public bool WasPlayedEnough()
    {
        if (songTotalDuration <= 0) return false;
        var percentagePlayed = cumulativePlayedSeconds / songTotalDuration * 100;
        return percentagePlayed >= SongPercentagePlayed;
    }

    public void SetCumulativePlayedTime(double seconds) => cumulativePlayedSeconds = seconds;
    public double GetCumulativePlayedTime() => cumulativePlayedSeconds;

    public void SetBand80Hz(float gain) => audioService.Band80Hz = gain;
    public void SetBand240Hz(float gain) => audioService.Band240Hz = gain;
    public void SetBand750Hz(float gain) => audioService.Band750Hz = gain;
    public void SetBand2200Hz(float gain) => audioService.Band2200Hz = gain;
    public void SetBand6600Hz(float gain) => audioService.Band6600Hz = gain;

    public void Dispose()
    {
        if (disposed) return;

        timer.Stop();
        volumePopupTimer?.Stop();
        audioService.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }
}