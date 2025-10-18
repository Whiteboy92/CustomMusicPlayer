using MusicPlayer.Models;

namespace MusicPlayer.Interfaces
{
    public interface IMusicLoaderService
    {
        // ──────────────── Folder Management ────────────────
        bool FolderExists();
        string GetFolderPath();
        void SetFolderPath(string path);

        // ──────────────── Music Loading ────────────────
        Task<List<MusicFile>> LoadMusicFromFolderAsync();
        Task<int> GetMusicFileCountAsync();
    }
}