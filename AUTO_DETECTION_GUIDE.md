# Auto-Detection and Custom Path Management

## Overview

Sonic Hybrid Ultimate now features **automatic game file detection** that scans your computer for .rsdk and .bin files, eliminating the need to manually copy files to specific folders.

## Features

### 🔍 Automatic Detection

The application automatically scans these locations on startup:

1. **GameData folder** (next to the executable) - Highest priority
2. **User-configured custom paths** - Your specified folders
3. **Current directory** - Where you run the app from
4. **Development paths** - For developers working on the project
5. **User's Documents folder** - Common storage location
6. **User's Desktop** - Where files are often downloaded
7. **User's Downloads folder** - Default download location
8. **Steam installation directories** - Automatically finds Steam games

### 📁 Custom Path Management

Add your own folders where you store game files:

**Via Menu:**
- `Tools` → `Manage Game Locations`
- Click `Browse and Add Folder`
- Select the folder containing your .rsdk or .bin files
- Click `Scan Now` to detect files immediately

**What Gets Detected:**
- `*.rsdk` files (Sonic 1, Sonic 2, Sonic CD)
- `*.bin` files (Sonic 3 & Knuckles ROM)
- `*.md` and `*.gen` files (Genesis ROMs)

### 🎯 Smart File Identification

The application doesn't just find files by name - it **analyzes file content** to identify which game each file belongs to:

- **Sonic 1**: Checks for "GHZ" (Green Hill Zone)
- **Sonic 2**: Checks for "EHZ" (Emerald Hill Zone)
- **Sonic CD**: Checks for "PPZ" (Palmtree Panic Zone)
- **Sonic 3 ROM**: Verifies SEGA header and file size

This means files with generic names like `Data.rsdk` or `game.bin` will still be correctly identified!

### 📥 Auto-Import System

When auto-import is enabled (default):
1. Detected files are **automatically copied** to the GameData folder
2. Files are **renamed to standard names** (sonic1.rsdk, sonic2.rsdk, etc.)
3. Files already in the correct location are **not duplicated**
4. You can **disable auto-import** in the Manage Game Locations dialog

### 💾 Persistent Settings

Your preferences are saved in:
- **Windows**: `%APPDATA%\SonicHybridUltimate\settings.json`
- **Linux**: `~/.config/SonicHybridUltimate/settings.json`
- **macOS**: `~/Library/Application Support/SonicHybridUltimate/settings.json`

Settings include:
- Custom search paths
- Auto-import preferences
- Last used file paths (for quick access)
- Window position and size

## Usage Examples

### Example 1: First-Time User

```
1. Download and extract Sonic Hybrid Ultimate
2. Run SonicHybrid.exe
3. The app scans common locations automatically
4. If files are found:
   ✓ Files are imported to GameData
   ✓ Log shows detected games
   ✓ You can immediately click "Load Sonic 1" to play
5. If files are NOT found:
   → Click "Load Sonic 1"
   → Browse to your Sonic 1 .rsdk file
   → File location is remembered for next time
```

### Example 2: Files on External Drive

```
1. You have game files on D:\Games\Sonic\
2. Run SonicHybrid.exe
3. Tools → Manage Game Locations
4. Browse and Add Folder → Select D:\Games\Sonic\
5. Click "Scan Now"
6. Files are detected and imported
7. Next time the app starts, it remembers this path
```

### Example 3: Steam User

```
If you have Sonic games installed via Steam:
1. Run SonicHybrid.exe
2. The app automatically checks:
   C:\Program Files (x86)\Steam\steamapps\common\
   C:\Program Files\Steam\steamapps\common\
3. Finds your Sonic game installations
4. Extracts .rsdk files if present
5. You're ready to play!
```

### Example 4: Multiple Locations

```
You can have files scattered across:
- Downloads folder (sonic1.rsdk)
- Desktop (sonic2.rsdk)
- External drive (soniccd.rsdk)
- USB stick (sonic3.bin)

The app finds ALL of them and imports them to GameData!
```

## Manual Scanning

If you add new files or connect an external drive:

**Method 1: Via Menu**
- `Tools` → `Scan for Games Now`

**Method 2: Via Dialog**
- `Tools` → `Manage Game Locations`
- Click `Scan Now` button

**Method 3: Restart App**
- The scan runs automatically on every startup

## Troubleshooting

### "No game files found"

**Solution**:
1. Check the log window for scan results
2. Verify files are actually .rsdk or .bin format
3. Add the folder containing your files:
   - Tools → Manage Game Locations
   - Browse and Add Folder
4. Try manual file selection:
   - Click "Load Sonic 1"
   - Browse to the file

### "File detected but not working"

**Possible causes**:
- File is corrupted
- File is from an incompatible version
- File is encrypted (Sonic CD needs to be decrypted first)

**Solution**:
- Check log for specific error messages
- Ensure files are from official releases
- For Sonic CD, make sure it's the Data.rsdk from the mobile/remastered version

### "Auto-import not working"

**Solution**:
1. Open Tools → Manage Game Locations
2. Check "Automatically import detected games" is enabled
3. Make sure GameData folder is writable
4. Run the app as administrator if on Windows

### "Custom path not remembered"

**Solution**:
1. Make sure you click "Browse and Add Folder" (not just browsing)
2. Check %APPDATA%\SonicHybridUltimate\ exists and is writable
3. Verify settings.json is created in that folder

## Advanced: Settings File Format

The `settings.json` file looks like this:

```json
{
  "CustomSearchPaths": [
    "D:\\Games\\Sonic",
    "E:\\Retro Games"
  ],
  "LastUsedPaths": {
    "sonic1": "D:\\Games\\Sonic\\sonic1.rsdk",
    "sonic2": "D:\\Games\\Sonic\\sonic2.rsdk"
  },
  "AutoImportDetectedGames": true,
  "ShowStartupScan": true,
  "Window": {
    "X": 100,
    "Y": 100,
    "Width": 1024,
    "Height": 768
  }
}
```

You can manually edit this file to:
- Add paths without using the UI
- Disable auto-import
- Reset window position

## Technical Details

### File Identification Algorithm

```
1. Check filename for game indicators
   - sonic1, sonic_1 → Sonic 1
   - sonic2, sonic_2 → Sonic 2
   - soniccd, sonic_cd → Sonic CD
   - sonic3, s3k → Sonic 3

2. If filename is generic (Data.rsdk):
   - Read first 4KB of file
   - Search for level names:
     * GHZ, Green Hill → Sonic 1
     * EHZ, Emerald Hill → Sonic 2
     * PPZ, Palmtree Panic → Sonic CD

3. For ROM files:
   - Check file size (2-8 MB range)
   - Verify SEGA header at offset 0x100
   - Validate as Genesis ROM format
```

### Search Performance

- Scans are **non-blocking** (doesn't freeze UI)
- Uses **parallel file searching** for speed
- Caches results to avoid repeated scans
- Only scans **top-level directories** (doesn't recurse deeply)

### Privacy

The auto-detection feature:
- ✓ Only reads file headers (first few KB)
- ✓ Does NOT upload any data
- ✓ Does NOT send telemetry
- ✓ Stores paths locally only
- ✓ No network access required

## Future Enhancements

Planned features:
- [ ] Real-time file watcher (detect new files immediately)
- [ ] Drag-and-drop support
- [ ] Automatic Steam library detection
- [ ] Cloud storage integration (OneDrive, Dropbox)
- [ ] First-time setup wizard
- [ ] File integrity verification
- [ ] Automatic updates for game files

## FAQ

**Q: Will this work with ROM hacks?**
A: Yes! As long as the file format is compatible, modified ROMs and RSDK files should be detected.

**Q: Can I have multiple versions of the same game?**
A: The system keeps the first one found. To use a different version, remove it from GameData and re-scan.

**Q: Does this work offline?**
A: Yes, completely offline. No internet connection needed.

**Q: Can I disable auto-detection?**
A: Yes, just remove all custom paths and don't place files in common locations. The app will ask you to browse for files manually.

**Q: Is my file system scanned constantly?**
A: No, scanning only happens:
  - On application startup
  - When you click "Scan Now"
  - When you add a new custom path

## Support

If you encounter issues with auto-detection:

1. **Enable detailed logging**
   - Check the log window for scan results
   - Look for "Auto-detecting game files..." section

2. **Verify file formats**
   - .rsdk files should be from official mobile/remaster versions
   - .bin files should be valid Genesis ROMs

3. **Check permissions**
   - Make sure the app can read your folders
   - On Windows, try running as administrator

4. **Report bugs**
   - Open an issue on GitHub with:
     * Your file locations
     * Log output
     * Steps to reproduce

---

**Enjoy the convenience of automatic game file detection!** 🎮✨
