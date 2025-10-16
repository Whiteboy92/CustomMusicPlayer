using DiscordRPC;
using DiscordRPC.Logging;
using MusicPlayer.Interfaces;

namespace MusicPlayer.Services.DiscordRpc;

/// <summary>
/// Service for managing Discord Rich Presence integration
/// </summary>
public class DiscordRpcService : IDiscordRpcService
{
    private DiscordRpcClient? client;
    private bool isInitialized;
    private readonly object lockObject = new();
    private DateTime? activityStartTime;
    
    private string? lastSongName;
    private string? lastArtist;
    private bool lastIsPlaying;

    /// <summary>
    /// Initializes the Discord RPC client with the given application ID
    /// </summary>
    public void Initialize(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        lock (lockObject)
        {
            if (isInitialized)
            {
                return;
            }

            try
            {
                client = new DiscordRpcClient(clientId);
                client.Logger = new ConsoleLogger { Level = LogLevel.Warning };
                
                client.OnReady += (_, e) =>
                {
                    Console.WriteLine($"Discord RPC Ready - User: {e.User.Username}");
                };

                client.OnError += (_, e) =>
                {
                    Console.WriteLine($"Discord RPC Error: {e.Message}");
                };

                client.Initialize();
                isInitialized = true;
                activityStartTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Discord RPC: {ex.Message}");
                client = null;
                isInitialized = false;
            }
        }
    }

    /// <summary>
    /// Updates Discord presence with song information and playback state
    /// </summary>
    public void UpdatePresence(string songName, string artist, bool isPlaying, double currentPosition, double duration)
    {
        if (!isInitialized || client is not { IsInitialized: true })
        {
            return;
        }

        try
        {
            bool songChanged = lastSongName != songName || lastArtist != artist;
            bool playStateChanged = lastIsPlaying != isPlaying;
            bool shouldUpdate = songChanged || playStateChanged;
            
            if (!shouldUpdate)
            {
                return;
            }
            
            lastSongName = songName;
            lastArtist = artist;
            lastIsPlaying = isPlaying;

            var presence = new RichPresence
            {
                Details = songName,
                State = artist,
                Assets = new Assets
                {
                    LargeImageKey = "music_note",
                    LargeImageText = "Listening to music",
                    SmallImageKey = isPlaying ? "play" : "pause",
                    SmallImageText = isPlaying ? "Playing" : "Paused",
                },
                Timestamps = activityStartTime.HasValue 
                    ? new Timestamps { Start = activityStartTime.Value }
                    : null,
            };

            client.SetPresence(presence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update Discord presence: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the Discord Rich Presence
    /// </summary>
    public void ClearPresence()
    {
        if (!isInitialized || client is not { IsInitialized: true })
        {
            return;
        }

        try
        {
            client.ClearPresence();
            
            activityStartTime = null;
            lastSongName = null;
            lastArtist = null;
            lastIsPlaying = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear Discord presence: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the Discord RPC client and clears resources
    /// </summary>
    public void Dispose()
    {
        lock (lockObject)
        {
            if (client != null)
            {
                try
                {
                    ClearPresence();
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing Discord RPC client: {ex.Message}");
                }
                finally
                {
                    client = null;
                    isInitialized = false;
                    activityStartTime = null;
                }
            }
        }
    }
}

