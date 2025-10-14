using System.Windows.Media;
using MusicPlayer.Interfaces;
using MusicPlayer.Validation;

namespace MusicPlayer.Services
{
    public class DurationExtractorService : IDurationExtractorService
    {
        public async Task<TimeSpan?> GetDurationAsync(string filePath)
        {
            if (!FileSystemValidator.FileExists(filePath))
                return null;

            var tcs = new TaskCompletionSource<TimeSpan?>();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var player = new MediaPlayer
                    {
                        Volume = 0,
                    };

                    player.MediaOpened += (_, _) =>
                    {
                        if (player.NaturalDuration.HasTimeSpan)
                        {
                            tcs.TrySetResult(player.NaturalDuration.TimeSpan);
                        }
                        else
                        {
                            tcs.TrySetResult(null);
                        }
                        player.Close();
                    };

                    player.MediaFailed += (_, _) =>
                    {
                        tcs.TrySetResult(null);
                        player.Close();
                    };

                    player.Open(new Uri(filePath));
                }
                catch
                {
                    tcs.TrySetResult(null);
                }
            });
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return null;
            }

            return await tcs.Task;
        }

        public string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue)
                return "--:--";

            var ts = duration.Value;
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
            }
            else
            {
                return $"{ts.Minutes:00}:{ts.Seconds:00}";
            }
        }
    }
}

