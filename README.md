# Music Player (WPF, .NET 10)

Lightweight desktop MP3 player. NAudio/WASAPI playback, 5-band EQ, playlists, SQLite-backed persistence, Discord Rich Presence.

## Features

- **Playback** — play/pause/prev/next, seek bar, streams one track at a time (low RAM).
- **Autoplay** — toggle auto-advance to the next track on song end; state persisted.
- **Shuffle** — weighted toward less-played tracks.
- **5-band EQ** — 80 / 240 / 750 / 2200 / 6600 Hz biquad filters.
- **Volume** — perceptual taper curve so the usable range spreads across the slider; saved instantly.
- **Playlists** — create, edit, delete; per-playlist play state.
- **Statistics** — play counts with 🥇🥈🥉 for the top 3, totals, most-played.
- **Resume on reopen** — restores last song, position, and queue (paused).
- **Discord Rich Presence** — shows current track (optional client ID).
- **Library** — scans `*.mp3` in the music folder root; durations cached, missing ones extracted in the background.

## Architecture

- `AudioService` — `AudioFileReader` → EQ → `VolumeSampleProvider` → `WasapiOut`. Raises `MediaOpened` / `PlaybackStopped` / `PlaybackEnded`.
- `SqliteSettingsService` — in-memory cache + per-row dirty tracking. Snapshots under a cache lock, writes off-thread (WAL) so the UI never blocks on disk. 15 s autosave + flush on close. DB: `%LOCALAPPDATA%/MusicPlayer/settings.db`.
- `MusicLoaderService` — root-only `*.mp3` scan; loads cached durations + play counts.
- Views — `MainWindow` (tabbed: Player / Equalizer / Statistics / Playlists), `PlayerControlsView`, `EqualizerView`, `StatisticsView`, `PlaylistsView`. List is UI-virtualized (recycling); polling timers run only during playback.

## Build & run

```powershell
dotnet run
```

Single-file self-contained EXE (win-x64):

```powershell
dotnet publish MusicPlayer.csproj -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=true `
  -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -o "$env:USERPROFILE\Desktop\MusicPlayer_Publish"
```

## Troubleshooting

- No audio — check default output device; try another file.
- No songs — ensure the folder has `.mp3` files at the root.
- Stats not updating — play threshold must be met (default 65%, `SongPercentagePlayed` in `PlayerControlsView`).
- Reset — close app, delete `%LOCALAPPDATA%/MusicPlayer/settings.db`, reopen.
