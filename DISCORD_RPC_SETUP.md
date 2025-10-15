# Discord Rich Presence Setup

This guide will help you set up Discord Rich Presence for your Music Player application.

## What is Discord Rich Presence?

Discord Rich Presence allows your Discord profile to display what song you're currently listening to in the Music Player, including:
- Song name
- Artist name
- Play/pause status
- Elapsed time and remaining time

## Prerequisites

- A Discord account
- Discord Desktop application installed and running

## Setup Instructions

### Step 1: Create a Discord Application

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications)
2. Click the **"New Application"** button in the top right
3. Enter a name for your application (e.g., "My Music Player")
4. Click **"Create"**

### Step 2: Get Your Application ID

1. After creating your application, you'll be on the "General Information" page
2. Find the **"APPLICATION ID"** field near the top of the page
3. Click the **"Copy"** button next to the Application ID
4. Save this ID - you'll need it in the next step

### Step 3: (Optional) Add Rich Presence Assets

If you want custom icons to appear on your Discord profile:

1. In the Discord Developer Portal, navigate to **"Rich Presence" → "Art Assets"**
2. Upload images for:
   - **Large Image Key**: `music_note` (recommended size: 1024x1024px)
   - **Small Image Key**: `play` (for playing status)
   - **Small Image Key**: `pause` (for paused status)

**Note:** If you don't upload these images, Discord will show a default icon.

### Step 4: Configure the Music Player

1. Open the Music Player application
2. Navigate to **Settings** (click the ⚙️ icon in the sidebar)
3. Scroll down to the **"💬 Discord Rich Presence"** section
4. Paste your Application ID into the **"Discord Application ID"** text field
5. The ID will save automatically
6. **Restart the application** for changes to take effect

> **Note:** The Application ID is a numeric value (typically 18 digits), not a hash or public key.

### Step 5: Test the Integration

1. Make sure Discord is running
2. Open the Music Player application
3. Play a song
4. Check your Discord profile - you should see:
   - The song name as the "Details"
   - The artist name as the "State"
   - A music note icon (if you uploaded assets)
   - Elapsed and remaining time for the current song

## Troubleshooting

### Rich Presence Not Showing

- **Discord not running**: Make sure Discord Desktop is open and running
- **Wrong Application ID**: Verify you copied the correct Application ID
- **Application needs restart**: Close and reopen the Music Player after setting the Application ID
- **Privacy settings**: Check Discord's Activity Privacy settings:
  - Go to Discord → User Settings → Activity Privacy
  - Enable "Display current activity as a status message"

### "Unknown Artist" Displayed

The Music Player extracts artist and song names from your filename. For best results, name your files in one of these formats:
- `Artist - Song Name.mp3`
- `Artist – Song Name.mp3`
- `Artist | Song Name.mp3`

If your files don't follow this format, the entire filename will be shown as the song name with "Unknown Artist".

### Icons Not Showing

- Make sure you uploaded the images in the Discord Developer Portal
- Use the exact key names: `music_note`, `play`, `pause`
- Wait a few minutes after uploading - Discord assets can take time to propagate

## Privacy Note

Discord Rich Presence only shows information on your Discord profile. Only people who can see your Discord profile will see what you're listening to. You can disable this at any time by:
1. Removing the Application ID from the Music Player settings
2. Turning off "Display current activity" in Discord's Activity Privacy settings

## Need Help?

If you encounter any issues not covered in this guide, please check:
- [Discord Developer Documentation](https://discord.com/developers/docs/rich-presence/how-to)
- Music Player application logs (if available)
- Discord application status (check if Discord is having issues)

