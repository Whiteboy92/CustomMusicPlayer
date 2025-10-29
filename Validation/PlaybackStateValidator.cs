namespace MusicPlayer.Validation;

public static class PlaybackStateValidator
{
    public static bool IsValidPlaybackState(string? songPath, double position)
    {
        return StringValidator.HasValue(songPath) && position >= 0;
    }

    public static bool HasPlayablePosition(double position)
    {
        return position > 0;
    }

    public static bool IsValidIndex(int index, int maxCount)
    {
        return index >= 0 && index < maxCount;
    }

    public static bool HasPlaylistItems(int count)
    {
        return count > 0;
    }
}