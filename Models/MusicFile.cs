using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicPlayer.Models;

public class MusicFile : INotifyPropertyChanged
{
    private string duration = "--:--";
    private int playCount;
    private bool isCompleted;
        
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public int TrackNumber { get; set; }
        
    public string Duration
    {
        get => duration;
        set
        {
            if (duration != value)
            {
                duration = value;
                OnPropertyChanged();
            }
        }
    }
        
    public int PlayCount
    {
        get => playCount;
        set
        {
            if (playCount != value)
            {
                playCount = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => isCompleted;
        set
        {
            if (isCompleted != value)
            {
                isCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}