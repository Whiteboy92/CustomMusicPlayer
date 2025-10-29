namespace MusicPlayer.Models;

public class Playlist
{
    public int Id { get; set; }
    public string Name { get; init; } = string.Empty;
    public string? Genre { get; init; }
    public string? Tags { get; init; }
    public DateTime CreatedDate { get; set; }
    public List<string> SongFilePaths { get; init; } = [];

    private int? songCount;
    public int SongCount
    {
        get => songCount ?? SongFilePaths.Count;
        set => songCount = value;
    }

    public bool IsDefaultQueue => Id == -1;
}