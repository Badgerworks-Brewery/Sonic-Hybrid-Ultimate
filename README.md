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
  - [ ] Memory management fixes
  - [ ] Debugging in progress

- **Custom Client**:
  - [x] Engine interface
  - [x] Transition system
  - [x] State management
  - [ ] Window management
  - [ ] Input system

- **Sonic 3 AIR Integration**:
  - [x] Basic Oxygen engine wrapper
  - [ ] Lemonscript integration
  - [ ] ROM handling
  - [ ] State synchronization

## Setup Instructions

1. Clone the repository
2. Place the following files in `Hybrid-RSDK Main/Data`:
   - Sonic 1's `Data.rsdk` as `sonic1.rsdk`
   - Sonic 2's `Data.rsdk` as `sonic2.rsdk`
   - Sonic CD's `Data.rsdk` as `soniccd.rsdk`
   - Sonic 3&K ROM as `sonic3.bin`

3. Install dependencies:
   - Visual Studio 2022 with C++ support
   - .NET 6.0 SDK
   - CMake (latest version)

4. Build the project:
```bash
# Build Custom Client
cd "Custom Client"
dotnet build

# Build Hybrid-RSDK
cd "../Hybrid-RSDK Main"
cmake -B build
cmake --build build
```

5. Run the Custom Client:
```bash
cd "Custom Client"
dotnet run
```

## Development Status

- **Priority**: Debugging Hybrid-RSDK integration
- **Next Steps**: 
  1. Complete memory management fixes
  2. Implement Sonic 2 completion detection
  3. Finalize transition system
  4. Integrate Lemonscript

## Contributing

Help is welcome! Current areas needing attention:
- Hybrid-RSDK debugging
- Memory management improvements
- Transition system refinements
- Lemonscript integration

## License

This project is for educational purposes only. All Sonic the Hedgehog properties are owned by SEGA.

## Credits
* Decompilation by Rubberduckycooly.
* Hybrid-RSDK by Xeeynamo.
* Sonic 3 AIR by Eukaryot.
* Conceived by Chaphidoesstuff aka @CCIGAMES.
* Main Development By Pixel-1 Games, SomeRandomPerson_, twanvanb1, FGSOFTWARE1 and more.
