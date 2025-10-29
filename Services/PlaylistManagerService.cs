using System.Windows.Controls;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

public class PlaylistManagerService(ISettingsService settingsService)
{
    public void PopulateDropdown(ComboBox dropdown)
    {
        var playlists = settingsService.GetAllPlaylists();
        var defaultQueue = settingsService.GetPlaylistById(-1);
        
        dropdown.Items.Clear();
        
        dropdown.Items.Add(new PlaylistDropdownItem
        {
            Id = -1,
            PlaylistName = defaultQueue?.Name ?? "Default Queue (All Songs)",
        });
        
        foreach (var playlist in playlists)
        {
            dropdown.Items.Add(new PlaylistDropdownItem
            {
                Id = playlist.Id,
                PlaylistName = playlist.Name,
            });
        }
        
        dropdown.DisplayMemberPath = "PlaylistName";
    }

    public static void SetDropdownSelection(ComboBox dropdown, int playlistId)
    {
        for (var i = 0; i < dropdown.Items.Count; i++)
        {
            if (dropdown.Items[i] is PlaylistDropdownItem item && item.Id == playlistId)
            {
                dropdown.SelectedIndex = i;
                return;
            }
        }
        
        dropdown.SelectedIndex = 0;
    }
    
    public string GetDefaultQueueName()
    {
        var defaultQueue = settingsService.GetPlaylistById(-1);
        return defaultQueue?.Name ?? "Default Queue";
    }
}
