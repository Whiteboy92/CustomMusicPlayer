namespace MusicPlayer.Interfaces;

public interface IDiscordRpcService : IDisposable
{
    void Initialize(string clientId);
    void UpdatePresence(string songName, string artist, bool isPlaying);
    void ClearPresence();
}

