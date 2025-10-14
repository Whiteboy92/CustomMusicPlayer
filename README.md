# Music Player (WPF, .NET 8)

A fast, lightweight desktop music player with SQLite-backed persistence, NAudio playback, a 5‑band equalizer, and a clean tabbed UI (Player / Equalizer / Statistics).

## Highlights (What we built)

- Audio engine with NAudio (WASAPI + `AudioFileReader` + `VolumeSampleProvider`)
- Custom 5‑band EQ (80 / 240 / 750 / 2200 / 6600 Hz) using biquad filters
- Shuffle with weighted bias toward less‑played tracks
- Play count tracking and a Statistics page (🥇🥈🥉 medals for the top 3)
- Resume on reopen: last song and timestamp are restored
- Volume persistence (saved instantly, reloaded on startup)
- SQLite settings store with in‑memory cache and 15 s auto‑save
- MP3 folder scan (root only) with cached duration load + background extraction
- Single‑file publish option (one EXE) and custom app icon
- Global exception handling (MessageBox for unhandled errors)

## How it works (Architecture)

### Services
- `SqliteSettingsService`
  - Stores: file count, last index, current song path, current position, queue, shuffle flag, durations, play counts, EQ gains, volume percent
  - In‑memory cache + dirty flag; 15s timer flush; explicit `FlushToDisk()` on close
  - DB file: `%LOCALAPPDATA%/MusicPlayer/settings.db`

- `AudioService`
  - Chain: `AudioFileReader` → EQ → `VolumeSampleProvider` → `WasapiOut`
  - Preserves stereo, low latency (event sync, 100 ms)
  - Raises `MediaOpened`, `PlaybackStopped`

- `MusicLoaderService`
  - Scans only `*.mp3` in the root of the configured music folder
  - `FileName` displayed without `.mp3` extension
  - Loads cached durations and play counts; extracts missing durations in background

### Views / UI
- `MainWindow` uses a tabbed layout (Player / Equalizer / Statistics)
- `PlayerControlsView`
  - Progress and time, Play/Pause/Prev/Next/Shuffle
  - Volume panel with +/− buttons (square) and percentage display
  - Raises `VolumeChanged` so volume persists via SQLite instantly
  - Robust timer and state sync with the audio engine
- `EqualizerView`
  - Five vertical sliders, centered panel, consistent spacing between bands
- `StatisticsView`
  - Ranks with medals (🥇🥈🥉) and aligned numbers (4+)
  - Totals and most‑played summary

### Persistence & Resume
- Autosave every 2 s (position/queue) and also on close
- On launch: loads queue, restores last song and seeks to the saved position (paused)
- Volume and EQ are loaded from SQLite and applied to the audio engine

### Shuffle & Play Counts
- Shuffle reorders all songs; on shuffle/next it counts the previous song as played if it reached the threshold (configurable in `PlayerControlsView` via `songPercentagePlayed`)
- Statistics updates immediately after increments

## Configuration

- Music folder path: see `Services/MusicLoaderService.cs` → `MusicFolderPath` constant
- App icon: place your ICO at `Assets/appicon.ico` (see `MusicPlayer.csproj` → `<ApplicationIcon>...`)

## Building & Running

### Run (dev)
```powershell
cd MusicPlayer
dotnet run
```

### Publish a single EXE (win‑x64)
Outputs to your Desktop under `MusicPlayer_Publish`.
```powershell
cd MusicPlayer
$out="$env:USERPROFILE\Desktop\MusicPlayer_Publish"
dotnet publish MusicPlayer.csproj -c Release -r win-x64 \
  -p:PublishSingleFile=true -p:SelfContained=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true \
  -o "$out"
```

## Troubleshooting

- No audio: verify your default output device; try another file
- No songs listed: ensure the folder exists and contains `.mp3` files at the root
- Stats not updating: ensure the play threshold is met (default 65%)
- Reset settings: close the app, delete `%LOCALAPPDATA%/MusicPlayer/settings.db`, reopen

## Project Structure (key parts)

```
MusicPlayer/
├── Services/
│   ├── AudioService.cs
│   ├── MusicLoaderService.cs
│   └── SqliteSettingsService.cs
├── Views/
│   ├── Player/
│   │   ├── PlayerControlsView.xaml / .cs
│   ├── Equalizer/
│   │   ├── EqualizerView.xaml / .cs
│   └── Statistics/
│       ├── StatisticsView.xaml / .cs
├── Helpers/
│   └── PlaybackHelper.cs
├── Assets/
│   └── appicon.ico
└── App.xaml / App.xaml.cs
```
