using System.Windows.Controls;
using MusicPlayer.Interfaces;
using MusicPlayer.Models;

namespace MusicPlayer.Services;

public class PlaylistManager
{
    private readonly ISettingsService settings;

    public PlaylistManager(ISettingsService settingsService)
    {
        settings = settingsService;
    }

    public void PopulateDropdown(ComboBox dropdown)
    {
        var playlists = settings.GetAllPlaylists();
        var defaultQueue = settings.GetPlaylistById(-1);
        
        dropdown.Items.Clear();
        
        dropdown.Items.Add(new PlaylistDropdownItem
        {
            Id = -1,
            PlaylistName = defaultQueue?.Name ?? "Default Queue (All Songs)"
        });
        
        foreach (var playlist in playlists)
        {
            dropdown.Items.Add(new PlaylistDropdownItem
            {
                Id = playlist.Id,
                PlaylistName = playlist.Name
            });
        }
        
        dropdown.DisplayMemberPath = "PlaylistName";
    }

    public void SetDropdownSelection(ComboBox dropdown, int playlistId)
    {
        for (int i = 0; i < dropdown.Items.Count; i++)
        {
            if (dropdown.Items[i] is PlaylistDropdownItem item && item.Id == playlistId)
            {
                dropdown.SelectedIndex = i;
                return;
            }
        }
        
        // If not found, select default queue
        dropdown.SelectedIndex = 0;
    }

    public int? GetSelectedPlaylistId(ComboBox dropdown)
    {
        return dropdown.SelectedItem is PlaylistDropdownItem item ? item.Id : null;
    }

    public (string name, string? genre, string? tags) GetDefaultQueueInfo()
    {
        var defaultQueue = settings.GetPlaylistById(-1);
        return (
            defaultQueue?.Name ?? "Default Queue",
            defaultQueue?.Genre,
            defaultQueue?.Tags
        );
    }
}
