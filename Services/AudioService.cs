using System.IO;
using System.Windows;
using MusicPlayer.Interfaces;
using MusicPlayer.Validation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicPlayer.Services
{
    public class AudioService : IAudioService
    {
        private IWavePlayer? waveOut;
        private AudioFileReader? audioFileReader;
        private EqualizerService? equalizer;
        private VolumeSampleProvider? volumeProvider;
        
        private bool isPlaying;
        private bool disposed;
        private float volume = 0.05f;
        private float band80Hz;
        private float band240Hz;
        private float band750Hz;
        private float band2200Hz;
        private float band6600Hz;

        public event EventHandler? PlaybackStopped;
        public event EventHandler? MediaOpened;

        public bool IsPlaying => isPlaying;
        public bool HasSource => audioFileReader != null;
        
        public int ChannelCount => audioFileReader?.WaveFormat.Channels ?? 0;
        public int SampleRate => audioFileReader?.WaveFormat.SampleRate ?? 0;
        public string AudioFormat => audioFileReader != null 
            ? $"{audioFileReader.WaveFormat.Channels}ch @ {audioFileReader.WaveFormat.SampleRate}Hz" 
            : "No audio loaded";

        public TimeSpan CurrentTime
        {
            get => audioFileReader?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (audioFileReader != null)
                {
                    audioFileReader.CurrentTime = value;
                }
            }
        }

        public TimeSpan TotalTime => audioFileReader?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => volume;
            set
            {
                volume = Math.Clamp(value, 0f, 1f);
                if (volumeProvider != null)
                {
                    volumeProvider.Volume = volume;
                }
            }
        }
        public float Band80Hz
        {
            get => band80Hz;
            set
            {
                band80Hz = Math.Clamp(value, -12f, 12f);
                if (equalizer != null)
                {
                    equalizer.Band80Hz = band80Hz;
                }
            }
        }

        public float Band240Hz
        {
            get => band240Hz;
            set
            {
                band240Hz = Math.Clamp(value, -12f, 12f);
                if (equalizer != null)
                {
                    equalizer.Band240Hz = band240Hz;
                }
            }
        }

        public float Band750Hz
        {
            get => band750Hz;
            set
            {
                band750Hz = Math.Clamp(value, -12f, 12f);
                if (equalizer != null)
                {
                    equalizer.Band750Hz = band750Hz;
                }
            }
        }

        public float Band2200Hz
        {
            get => band2200Hz;
            set
            {
                band2200Hz = Math.Clamp(value, -12f, 12f);
                if (equalizer != null)
                {
                    equalizer.Band2200Hz = band2200Hz;
                }
            }
        }

        public float Band6600Hz
        {
            get => band6600Hz;
            set
            {
                band6600Hz = Math.Clamp(value, -12f, 12f);
                if (equalizer != null)
                {
                    equalizer.Band6600Hz = band6600Hz;
                }
            }
        }

        public void LoadFile(string filePath)
        {
            try
            {
                Stop();
                DisposeAudioResources();

                if (!FileSystemValidator.FileExists(filePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {filePath}");
                }
                audioFileReader = new AudioFileReader(filePath);
                equalizer = new EqualizerService(audioFileReader.ToSampleProvider())
                {
                    Band80Hz = band80Hz,
                    Band240Hz = band240Hz,
                    Band750Hz = band750Hz,
                    Band2200Hz = band2200Hz,
                    Band6600Hz = band6600Hz,
                };
                volumeProvider = new VolumeSampleProvider(equalizer)
                {
                    Volume = volume,
                };
                waveOut = new WasapiOut(
                    NAudio.CoreAudioApi.AudioClientShareMode.Shared,
                    true,
                    100
                );
                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;

                waveOut.Init(volumeProvider);

                MediaOpened?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load audio file:\n\n{ex.Message}", "Audio Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException($"Failed to load audio file: {ex.Message}", ex);
            }
        }

        public void Play()
        {
            if (waveOut != null)
            {
                try
                {
                    if (waveOut.PlaybackState != PlaybackState.Playing)
                    {
                        waveOut.Play();
                    }
                    isPlaying = true;
                }
                catch (Exception ex)
                {
                    isPlaying = false;
                    MessageBox.Show($"Failed to start playback:\n\n{ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void Pause()
        {
            if (waveOut == null) { return; }
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
            }
            isPlaying = false;
        }

        public void Stop()
        {
            if (waveOut != null)
            {
                waveOut.Stop();
                isPlaying = false;
            }

            if (audioFileReader != null)
            {
                audioFileReader.CurrentTime = TimeSpan.Zero;
            }
        }

        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            isPlaying = false;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        private void DisposeAudioResources()
        {
            if (waveOut != null)
            {
                waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                waveOut.Dispose();
                waveOut = null;
            }

            audioFileReader?.Dispose();
            audioFileReader = null;

            equalizer = null;
            volumeProvider = null;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                DisposeAudioResources();
                disposed = true;
            }
        }
    }
}

