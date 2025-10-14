using MusicPlayer.Models;

namespace MusicPlayer.Interfaces
{
    public interface IShuffleService
    {
        List<MusicFile> CreateShuffledQueue(List<MusicFile> allSongs);
    }
}

