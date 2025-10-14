using MusicPlayer.Interfaces;
using MusicPlayer.Models;

namespace MusicPlayer.Services
{
    public class ShuffleService : IShuffleService
    {
        public List<MusicFile> CreateShuffledQueue(List<MusicFile> allSongs)
        {
            var random = new Random();
            var unplayedSongs = allSongs.Where(s => s.PlayCount == 0).ToList();
            var playedSongs = allSongs.Where(s => s.PlayCount > 0).OrderBy(s => s.PlayCount).ToList();

            var shuffledQueue = new List<MusicFile>();
            
            if (unplayedSongs.Count > 0)
            {
                ShuffleList(unplayedSongs, random);
                shuffledQueue.AddRange(unplayedSongs);
            }
            
            if (playedSongs.Count > 0)
            {
                var weightedPlayedSongs = WeightedShufflePlayedSongs(playedSongs, random);
                shuffledQueue.AddRange(weightedPlayedSongs);
            }

            return shuffledQueue;
        }

        private static void ShuffleList<T>(List<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        private static List<MusicFile> WeightedShufflePlayedSongs(List<MusicFile> songs, Random random)
        {
            var result = new List<MusicFile>();
            var remaining = new List<MusicFile>(songs);

            while (remaining.Count > 0)
            {
                var maxPlayCount = remaining.Max(s => s.PlayCount);
                var weights = remaining.Select(s =>
                {
                    double weight = maxPlayCount - s.PlayCount + 1.0;
                    return weight * weight;
                }).ToList();

                var totalWeight = weights.Sum();
                var randomValue = random.NextDouble() * totalWeight;
                double cumulative = 0;
                int selectedIndex = -1;

                for (int i = 0; i < remaining.Count; i++)
                {
                    cumulative += weights[i];
                    if (randomValue <= cumulative)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                if (selectedIndex == -1)
                {
                    selectedIndex = remaining.Count - 1;
                }

                result.Add(remaining[selectedIndex]);
                remaining.RemoveAt(selectedIndex);
            }

            return result;
        }
    }
}

