using System.Windows.Threading;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;
using MusicPlayer.Validation;
using MusicPlayer.Views.Player;
using MusicPlayer.Views.Playlist;

namespace MusicPlayer.Helpers
{
	public static class PlaybackHelper
	{
		public static void SaveCurrentQueue(ISettingsService settings, PlaylistView playlist, bool isShuffled)
		{
			var currentSelectedSong = playlist.SelectedIndex >= 0 && playlist.SelectedIndex < playlist.GetDisplayedPlaylist().Count
				? playlist.GetDisplayedPlaylist()[playlist.SelectedIndex]
				: null;
			
			var queuePaths = playlist.Playlist.Select(s =>
			{
				var isCompleted = s.IsCompleted && s != currentSelectedSong;
				return $"{s.FilePath}|{isCompleted}";
			}).ToList();
			
			settings.SaveCurrentQueue(queuePaths, isShuffled);
		}

		public static void RestorePlaybackState(ISettingsService settings, PlaylistView playlist, PlayerControlsView player, Dispatcher dispatcher, Action<MusicFile> onSongRestored)
		{
			var (savedSongPath, savedPosition) = settings.GetCurrentPlaybackState();
			
			if (!StringValidator.HasValue(savedSongPath))
				return;

			var song = playlist.Playlist.FirstOrDefault(s => s.FilePath == savedSongPath);
			if (song == null) return;

			var displayedIndex = playlist.GetDisplayedPlaylist().IndexOf(song);
			if (displayedIndex >= 0)
			{
				playlist.SelectedIndex = displayedIndex;
			}
			else
			{
				return;
			}
			
			onSongRestored.Invoke(song);
			
			bool autoPlay = settings.GetAutoPlayOnStartup();
			var cumulativePlayedTime = string.IsNullOrEmpty(savedSongPath) ? 0.0 : settings.GetCumulativePlayedTime(savedSongPath);
			
			EventHandler? mediaOpenedHandler = null;
			mediaOpenedHandler = (_, _) =>
			{
				player.MediaOpenedEvent -= mediaOpenedHandler;
				
				dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
				{
					player.Play();
					
					dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
					{
						player.SetPosition(savedPosition);
						player.SetCumulativePlayedTime(cumulativePlayedTime);
						
						if (!autoPlay)
						{
							player.Pause();
						}
					}));
				}));
			};
			
			player.MediaOpenedEvent += mediaOpenedHandler;
			player.LoadSong(song);
		}
	}
}


