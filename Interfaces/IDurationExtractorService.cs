namespace MusicPlayer.Interfaces
{
    public interface IDurationExtractorService
    {
        Task<TimeSpan?> GetDurationAsync(string filePath);
        string FormatDuration(TimeSpan? duration);
    }
}

