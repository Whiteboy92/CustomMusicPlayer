using MusicPlayer.Interfaces;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

public class ShuffleService : IShuffleService
{
    public List<MusicFile> CreateShuffledQueue(List<MusicFile> allSongs)
    {
        var random = new Random();
        var shuffledQueue = new List<MusicFile>(allSongs);
        ShuffleList(shuffledQueue, random);
        return shuffledQueue;
    }

    private static void ShuffleList<T>(List<T> list, Random random)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}