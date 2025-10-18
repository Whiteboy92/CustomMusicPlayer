namespace MusicPlayer.Models
{
    public class Playlist
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Genre { get; set; }
        public string? Tags { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> SongFilePaths { get; set; } = new List<string>();

        private int? songCount;
        public int SongCount
        {
            get => songCount ?? SongFilePaths.Count;
            set => songCount = value;
        }

        public bool IsDefaultQueue => Id == -1;
    }
}

