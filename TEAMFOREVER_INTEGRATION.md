# Team Forever RSDKv4 Integration

This document describes the integration of Team Forever QOL updates into Sonic Hybrid Ultimate.

## What Was Integrated

The following enhancements from Team Forever's RSDKv4 decompilation have been integrated:

### Core Features
1. **Mod Loader API** (`ModAPI.cpp`, `ModAPI.hpp`)
   - Built-in mod support with mod loading and management
   - Custom achievements system
   - XML GameConfig data support
   - Save file redirection per mod

2. **Networking System** (`Networking.cpp`, `Networking.hpp`)
   - Multiplayer networking for Sonic 2 VS mode
   - Server hosting and joining capabilities
   - Custom multiplayer menu system

3. **Script Compiler**
   - Built-in script compiler similar to RSDKv3
   - Supports RSDKv4 script syntax
   - Allows runtime script compilation

4. **Enhanced Debug Features**
   - F12: Pause
   - F11: Step over & fast forward
   - F1: Load first scene (title screen)
   - F2/F3: Load previous/next scene
   - F5: Reload current scene and assets
   - F8/F9: Visualize hitboxes
   - F10: Palette overlay
   - ESC: Access dev menu from anywhere

5. **Settings System**
   - `settings.ini` file for game configuration
   - Similar to Sonic Mania's settings system
   - Persistent settings across sessions

6. **Idle Screen Dimming**
   - Feature from Sonic Mania Plus
   - Configurable timeout
   - Can be disabled in settings

### Enhanced Native Objects
Updated native objects with improved functionality:
- ModsMenu, ModsButton, ModInfoButton
- MultiplayerScreen, MultiplayerButton, MultiplayerHandler
- Enhanced OptionsMenu, SettingsScreen
- Improved AboutScreen, AchievementsMenu
- Better PlayerSelectScreen, SaveSelect

### Updated Core Systems
- **Enhanced Renderer** with improved performance
- **Improved Audio System** with better streaming
- **Better Input Handling** for multiple controllers
- **Enhanced Drawing** with more rendering options
- **Improved Script Engine** with additional opcodes

## Source Repositories

The enhancements were integrated from:
- https://github.com/ElspethThePict/TeamForever-v4-1.3 (main source)
- https://github.com/ElspethThePict/S1Forever (S1-specific features)
- https://github.com/ElspethThePict/S2Absolute (S2-specific features)

## Files Modified

All core RSDKv4 files were updated with Team Forever enhancements:
- Core engine files (RetroEngine, Scene, Script, etc.)
- All subsystems (Audio, Video, Input, Drawing, etc.)
- All NativeObjects for menus and UI
- Helper files (fcaseopen, resource headers)

## Build Notes

The integration maintains compatibility with the existing build system.
The mod loader and networking features are enabled by default via:
- `RETRO_USE_MOD_LOADER = 1`
- `RETRO_USE_NETWORKING = 1`

These can be disabled in RetroEngine.hpp if needed.

## Credits

- Team Forever RSDKv4 decompilation by Rubberduckycooly and contributors
- S1Forever, TeamForever-v4-1.3, S2Absolute by ElspethThePict
- Original RSDKv4 decompilation by Rubberduckycooly
- Integration by Copilot for Sonic Hybrid Ultimate
