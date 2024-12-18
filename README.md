# Sonic Hybrid Ultimate

A project to seamlessly integrate multiple Sonic games into a single cohesive experience, combining RSDK and Oxygen Engine games. Acts as a legal alternative to Sonic Origins for fans.

## Project Structure

The project consists of three main components:

1. **Custom Client**: The core integration layer that manages:
   - Engine transitions
   - State preservation
   - Window/rendering management
   - Input handling

2. **Hybrid-RSDK**: Modified version of RSDK that supports:
   - Sonic 1 & 2 (RSDKv4)
   - Sonic CD (RSDKv3 with v4 compatibility)
   - Seamless game transitions
   - State preservation

3. **Sonic 3 AIR/Oxygen**: Integration with Sonic 3 AIR through:
   - Lemonscript integration
   - ROM handling
   - State synchronization

## Current Status

- **Hybrid-RSDK**: 
  - [x] Basic RSDK integration
  - [x] File fetching system
  - [x] Memory management fixes
  - [x] Sonic 2 completion detection
  - [ ] Debugging in progress

- **Custom Client**:
  - [x] Engine interface
  - [x] Transition system
  - [x] State management
  - [x] Window management
  - [x] Input system
  - [x] Death Egg completion detection

- **Sonic 3 AIR Integration**:
  - [x] Basic Oxygen engine wrapper
  - [ ] Lemonscript integration
  - [ ] ROM handling
  - [ ] State synchronization

## Setup Instructions

1. Install required dependencies:
   - Visual Studio 2022 with C++ support
   - .NET 6.0 SDK or later
   - CMake 3.15 or later

2. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/Sonic-Hybrid-Ultimate.git
   cd Sonic-Hybrid-Ultimate
   ```

3. Place the following files in `Hybrid-RSDK Main/Data`:
   - Sonic 1's `Data.rsdk` as `sonic1.rsdk`
   - Sonic 2's `Data.rsdk` as `sonic2.rsdk`
   - Sonic CD's `Data.rsdk` as `soniccd.rsdk`
   - Sonic 3&K ROM as `sonic3.bin`

4. Build the project using the provided build script:
   ```powershell
   # For debug build
   .\build.ps1

   # For release build
   .\build.ps1 -Release

   # To clean and rebuild
   .\build.ps1 -Clean
   ```

5. Run the Custom Client:
   ```powershell
   cd "Custom Client/bin/Debug/net6.0-windows"  # or Release instead of Debug
   ./SonicHybridUltimate.exe
   ```

## Development Status

- **Priority**: Sonic 3 AIR Integration
- **Next Steps**: 
  1. Complete Lemonscript integration
  2. Implement ROM handling
  3. Finalize state synchronization
  4. Polish transition animations

## Features

- **Seamless Transitions**: Smooth transitions between RSDK and Oxygen engines
- **State Preservation**: Maintains game state (rings, score, etc.) across transitions
- **Death Egg Detection**: Accurate detection of Sonic 2's completion
- **Modern UI**: Clean, user-friendly interface with real-time status updates

## Contributing

Help is welcome! Current areas needing attention:
- Sonic 3 AIR integration
- Lemonscript implementation
- Transition animations
- UI/UX improvements

## License

This project is for educational purposes only. All Sonic the Hedgehog properties are owned by SEGA.

## Credits
- RSDK Decompilation by Rubberduckycooly
- Sonic 3 AIR by Eukaryot
- Original games by SEGA
