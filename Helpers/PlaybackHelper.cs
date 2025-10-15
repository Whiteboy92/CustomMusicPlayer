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
		var queuePaths = playlist.Playlist.Where(s => !s.IsCompleted).Select(s => s.FilePath).ToList();
		settings.SaveCurrentQueue(queuePaths, isShuffled);
	}

	public static void RestorePlaybackState(ISettingsService settings, PlaylistView playlist, PlayerControlsView player, Dispatcher dispatcher, Action<MusicFile> onSongRestored)
	{
		var (savedSongPath, savedPosition) = settings.GetCurrentPlaybackState();
		
		if (!StringValidator.HasValue(savedSongPath))
			return;

		var song = playlist.Playlist.FirstOrDefault(s => s.FilePath == savedSongPath);
		if (song == null) return;

		var index = playlist.Playlist.IndexOf(song);
		playlist.SelectedIndex = index;
		
		onSongRestored?.Invoke(song);
		
		bool autoPlay = settings.GetAutoPlayOnStartup();
		
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


