namespace MusicPlayer.Validation
{
    public static class StringValidator
    {
        public static bool HasValue(string? value)
        {
            return !string.IsNullOrEmpty(value);
        }

        public static bool HasValueWithContent(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}

