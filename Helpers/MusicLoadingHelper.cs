using System.Windows;
using System.Windows.Threading;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Validation;

namespace MusicPlayer.Helpers
{
    public static class MusicLoadingHelper
    {
        public static bool ValidateMusicFolderExists(IMusicLoaderService musicLoader)
        {
            if (musicLoader.FolderExists())
                return true;

            MessageBox.Show(
                $"Music folder not found:\n{musicLoader.GetFolderPath()}\n\nPlease ensure the folder exists.",
                "Folder Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        public static async Task<List<MusicFile>> LoadAndPrepareSongs(IMusicLoaderService musicLoader)
        {
            return await musicLoader.LoadMusicFromFolderAsync();
        }

        public static Dictionary<string, string> ApplyCachedMetadata(List<MusicFile> songs, ISettingsService settings)
        {
            var cachedDurations = settings.GetAllDurations();
            var cachedPlayCounts = settings.GetAllPlayCounts();
            var updatedDurations = new Dictionary<string, string>();

            foreach (var song in songs)
            {
                if (cachedDurations.TryGetValue(song.FilePath, out var cachedDuration) && 
                    StringValidator.HasValue(cachedDuration) && cachedDuration != "--:--")
                {
                    song.Duration = cachedDuration;
                    updatedDurations[song.FilePath] = cachedDuration;
                }
                else
                {
                    song.Duration = "--:--";
                }

                if (cachedPlayCounts.TryGetValue(song.FilePath, out var playCount))
                {
                    song.PlayCount = playCount;
                }
            }

            return updatedDurations;
        }

        public static (List<MusicFile> songsToDisplay, bool isShuffled) DetermineSongsToDisplay(
            List<MusicFile> allSongs, 
            Dictionary<string, MusicFile> songDict, 
            List<string> savedQueuePaths, 
            bool hasSavedQueue, 
            bool wasShuffled, 
            bool shouldReload)
        {
            if (hasSavedQueue)
            {
                var completionStates = new Dictionary<string, bool>();
                var savedSongOrder = new List<string>();
                
                foreach (var queueEntry in savedQueuePaths)
                {
                    var parts = queueEntry.Split('|');
                    var path = parts[0];
                    var isCompleted = parts.Length > 1 && bool.Parse(parts[1]);
                    completionStates[path] = isCompleted;
                    savedSongOrder.Add(path);
                }
                
                var songsToDisplay = new List<MusicFile>();
                foreach (var path in savedSongOrder)
                {
                    if (songDict.TryGetValue(path, out var song))
                    {
                        song.IsCompleted = completionStates[path];
                        songsToDisplay.Add(song);
                    }
                }

                if (shouldReload)
                {
                    var savedPaths = new HashSet<string>(savedSongOrder);
                    foreach (var song in allSongs)
                    {
                        if (!savedPaths.Contains(song.FilePath))
                        {
                            song.IsCompleted = false;
                            songsToDisplay.Add(song);
                        }
                    }
                    return (songsToDisplay, false);
                }
                
                return (songsToDisplay, wasShuffled);
            }

            return (allSongs, false);
        }

        public static async Task AnalyzeMissingSongDurations(
            List<MusicFile> allSongs, 
            Dictionary<string, string> updatedDurations, 
            int currentFileCount,
            ISettingsService settings,
            IDurationExtractorService durationExtractor,
            Dispatcher dispatcher,
            Action<string> setTitle)
        {
            var songsNeedingDuration = allSongs.Where(s => !updatedDurations.ContainsKey(s.FilePath)).ToList();
            int totalNewSongs = songsNeedingDuration.Count;

            if (totalNewSongs > 0)
            {
                await AnalyzeSongsInParallel(songsNeedingDuration, updatedDurations, totalNewSongs, durationExtractor, dispatcher, setTitle);
                settings.SaveAllDurations(updatedDurations);
                settings.UpdateFileCount(currentFileCount);
                settings.FlushToDisk();
            }
            else
            {
                settings.UpdateFileCount(currentFileCount);
            }
        }

        private static async Task AnalyzeSongsInParallel(
            List<MusicFile> songs, 
            Dictionary<string, string> updatedDurations, 
            int totalNewSongs,
            IDurationExtractorService durationExtractor,
            Dispatcher dispatcher,
            Action<string> setTitle)
        {
            setTitle($"Music Player - Analyzing {totalNewSongs} songs...");
            int maxConcurrency = Math.Clamp(Environment.ProcessorCount * 2, 4, 32);
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var processedCount = 0;
            var lockObj = new object();

            var tasks = songs.Select(async song =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var duration = await durationExtractor.GetDurationAsync(song.FilePath);
                    var formattedDuration = durationExtractor.FormatDuration(duration);
                    await dispatcher.InvokeAsync(() =>
                    {
                        song.Duration = formattedDuration;
                    });

                    lock (lockObj)
                    {
                        updatedDurations[song.FilePath] = formattedDuration;
                        processedCount++;
                        if (processedCount % 20 == 0)
                        {
                            dispatcher.Invoke(() =>
                            {
                                setTitle($"Music Player - Analyzing {processedCount}/{totalNewSongs} songs...");
                            });
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}

