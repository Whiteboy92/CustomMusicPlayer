using MusicPlayer.Models;

namespace MusicPlayer.Interfaces;

public interface IMusicLoaderService
{
    bool FolderExists();
    string GetFolderPath();
    void SetFolderPath(string path);

    Task<List<MusicFile>> LoadMusicFromFolderAsync();
    Task<int> GetMusicFileCountAsync();
}