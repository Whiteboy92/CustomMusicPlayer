using System.IO;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Validation;

namespace MusicPlayer.Services;

public class MusicLoaderService : IMusicLoaderService
{
    private const string DefaultMusicFolderPath = @"C:\Whiteboy\Admin\Music Mp3";
    private string musicFolderPath;

    public MusicLoaderService()
    {
        musicFolderPath = DefaultMusicFolderPath;
    }

    public async Task<List<MusicFile>> LoadMusicFromFolderAsync()
    {
        return await Task.Run(() =>
        {
            var musicFiles = new List<MusicFile>();

            if (!FileSystemValidator.DirectoryExists(musicFolderPath))
            {
                return musicFiles;
            }
            var files = Directory.GetFiles(musicFolderPath, "*.mp3", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName)
                .ToList();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - 4);
                }
                musicFiles.Add(new MusicFile
                {
                    FilePath = file,
                    FileName = name,
                });
            }

            return musicFiles;
        });
    }

    public async Task<int> GetMusicFileCountAsync()
    {
        return await Task.Run(() => 
            !FileSystemValidator.DirectoryExists(musicFolderPath) 
                ? 0 
                : Directory.GetFiles(musicFolderPath, "*.mp3", SearchOption.TopDirectoryOnly).Length);
    }

    public bool FolderExists()
    {
        return FileSystemValidator.DirectoryExists(musicFolderPath);
    }

    public string GetFolderPath()
    {
        return musicFolderPath;
    }

    public void SetFolderPath(string path)
    {
        if (StringValidator.HasValueWithContent(path))
        {
            musicFolderPath = path;
        }
    }
}