namespace MusicPlayer.Interfaces;

public interface IAudioService : IDisposable
{
    event EventHandler? PlaybackStopped;
    event EventHandler? MediaOpened;

    // Raised only when a track reaches its natural end (not on a manual Stop or
    // when loading a new file). Used to drive auto-advance + play-count.
    event EventHandler? PlaybackEnded;

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