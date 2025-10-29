using MusicPlayer.Interfaces;
using MusicPlayer.Validation;
using NAudio.Wave;

namespace MusicPlayer.Services;

public class DurationExtractorService : IDurationExtractorService
{
    public Task<TimeSpan?> GetDurationAsync(string filePath)
    {
        // Runs on a threadpool thread (no UI dispatcher), so the bounded-parallel
        // analyze loop actually runs in parallel. NAudio reads the duration from
        // the MP3 frame index without decoding audio.
        return Task.Run<TimeSpan?>(() =>
        {
            if (!FileSystemValidator.FileExists(filePath))
                return null;

            try
            {
                using var reader = new Mp3FileReader(filePath);
                return reader.TotalTime;
            }
            catch
            {
                try
                {
                    using var reader = new AudioFileReader(filePath);
                    return reader.TotalTime;
                }
                catch
                {
                    return null;
                }
            }
        });
    }

    public string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "--:--";

        var ts = duration.Value;

        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:00}:{ts.Seconds:00}";
    }
}
