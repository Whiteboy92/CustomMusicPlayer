using MusicPlayer.Models;

namespace MusicPlayer.Interfaces
{
    public interface IMusicLoaderService
    {
        Task<List<MusicFile>> LoadMusicFromFolderAsync();
        Task<int> GetMusicFileCountAsync();
        bool FolderExists();
        string GetFolderPath();
        void SetFolderPath(string path);
    }
}

