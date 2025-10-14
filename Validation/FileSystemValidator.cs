using System.IO;

namespace MusicPlayer.Validation
{
    public static class FileSystemValidator
    {
        public static bool DirectoryExists(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }

        public static bool FileExists(string? path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
    }
}

