namespace MusicPlayer.Interfaces;

public interface IAudioService : IDisposable
{
    event EventHandler? PlaybackStopped;
    event EventHandler? MediaOpened;

    bool IsPlaying { get; }
    bool HasSource { get; }
    TimeSpan CurrentTime { get; set; }
    TimeSpan TotalTime { get; }
    float Volume { set; }
    float Band80Hz { get; set; }
    float Band240Hz { get; set; }
    float Band750Hz { get; set; }
    float Band2200Hz { get; set; }
    float Band6600Hz { get; set; }

    void LoadFile(string filePath);
    void Play();
    void Pause();
    void Stop();
}