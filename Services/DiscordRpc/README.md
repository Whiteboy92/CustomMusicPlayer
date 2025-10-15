# Discord RPC Module

This folder contains all Discord Rich Presence related logic, organized into separate, focused classes.

## Structure

### 📁 Files

#### `DiscordRpcService.cs`
**Purpose:** Core service for Discord RPC integration  
**Responsibilities:**
- Initializes Discord RPC client
- Manages connection state
- Sends presence updates to Discord
- Handles timestamps for play/pause states
- Cleans up resources on disposal

**Key Methods:**
- `Initialize(string clientId)` - Starts Discord RPC with app ID
- `UpdatePresence(...)` - Updates what's shown on Discord profile
- `ClearPresence()` - Removes presence from Discord
- `Dispose()` - Cleans up resources

**What Discord Shows:**
- **Song Name** (top line - "Details")
- **Artist Name** (bottom line - "State")
- **Play/Pause Icon** (small icon - play ▶️ or pause ⏸️)
- **Activity Timer** ("Playing for X minutes" - managed by Discord)

**Smart Update System:**
- Tracks presence state to avoid unnecessary updates
- Only sends updates when song changes or play/pause state changes
- Preserves Discord's "Playing for X minutes" timer by minimizing updates
- No song position timer - keeps the display clean and simple

---

#### `DiscordPresenceUpdater.cs`
**Purpose:** High-level coordinator for Discord presence updates  
**Responsibilities:**
- Tracks currently playing song
- Coordinates between player controls and Discord service
- Extracts and formats song metadata
- Updates presence based on playback state

**Key Methods:**
- `SetCurrentSong(MusicFile song)` - Sets the active song
- `UpdatePresence(PlayerControlsView)` - Sends update to Discord
- `ClearPresence()` - Clears Discord presence

**Used by:** `MainWindow.xaml.cs` to manage Discord updates

---

#### `DiscordPresenceHelper.cs`
**Purpose:** Utility methods for parsing and formatting  
**Responsibilities:**
- Extracts artist and song name from filenames
- Parses duration strings into seconds
- Pure utility functions with no state

**Key Methods:**
- `ExtractArtistAndSongName(string fileName)` - Parses "Artist - Song.mp3" format
- `ParseDurationToSeconds(string duration)` - Converts "MM:SS" to seconds

**Static class** - no instantiation needed

---

## Design Pattern

This module follows the **Single Responsibility Principle**:

```
DiscordRpcService          → Low-level Discord API communication
       ↑
DiscordPresenceUpdater     → Business logic & coordination
       ↑
DiscordPresenceHelper      → Data parsing & formatting
```

## Integration

### Dependency Injection Setup (`App.xaml.cs`)
```csharp
services.AddSingleton<IDiscordRpcService, DiscordRpcService>();
services.AddSingleton<DiscordPresenceUpdater>();
```

### Usage in MainWindow
```csharp
// Set current song when playback changes
discordPresenceUpdater.SetCurrentSong(musicFile);

// Update Discord every 5 seconds
discordPresenceUpdater.UpdatePresence(PlayerControlsView);

// Clear on app close
discordPresenceUpdater.ClearPresence();
```

## Configuration

Users configure Discord RPC through:
1. Settings UI → Discord Rich Presence section
2. Enter Discord Application ID
3. Restart app for changes to take effect

See `DISCORD_RPC_SETUP.md` in the project root for full setup instructions.

