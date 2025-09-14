# Sonic Hybrid Ultimate - Getting Started Guide

## What is Sonic Hybrid Ultimate?

Sonic Hybrid Ultimate is an ambitious project that aims to combine multiple classic Sonic games (Sonic 1, Sonic CD, Sonic 2, and Sonic 3 & Knuckles) into a single unified gaming experience. Think of it as a legal, fan-made alternative to Sonic Origins.

## Project Architecture

The project consists of three main components:

### 1. **Hybrid-RSDK Main**
- Contains the modified RSDK (Retro SDK) engine
- Handles Sonic 1, CD, and 2 gameplay
- Built using C++ and CMake
- Currently has loading issues (backgrounds load but not foreground elements)

### 2. **Sonic 3 AIR Main**
- Contains the Sonic 3 AIR (Angel Island Revisited) / Oxygen Engine
- Handles Sonic 3 & Knuckles gameplay
- Integration is 50% complete

### 3. **Custom Client**
- C# Windows Forms application that manages both engines
- Handles transitions between games
- Provides unified launcher and monitoring
- Currently 0% complete but framework is in place

## Current Status & Issues

### ✅ What's Working:
- Build system is set up and functional
- Custom Client framework is implemented
- Engine wrapper classes are created
- Logging system is working
- Project structure is organized

### ❌ What's Broken:
1. **Hybrid-RSDK Loading Issues**: Games load backgrounds but not foreground elements
2. **Missing Game Data**: You need to provide your own game files
3. **Incomplete Integration**: Sonic 3 AIR integration needs work
4. **Native Dependencies**: Some DLLs may be missing or not built properly

## How to Get Started

### Prerequisites
1. **Visual Studio 2022** with C++ workload
2. **.NET 6.0 SDK**
3. **CMake 3.15+**
4. **Your own game files** (see below)

### Required Game Files
You need to legally obtain these files and place them in `Hybrid-RSDK Main/Data/`:
- `sonic1.rsdk` - Data.rsdk from Sonic 1 (2013 version)
- `sonic2.rsdk` - Data.rsdk from Sonic 2 (2013 version)
- `soniccd.rsdk` - Data.rsdk from Sonic CD
- `sonic3.bin` - ROM file from Sonic 3 & Knuckles

### Building the Project

1. **Clone the repository**
   ```bash
   git clone https://github.com/Badgerworks-Brewery/Sonic-Hybrid-Ultimate.git
   cd Sonic-Hybrid-Ultimate
   ```

2. **Run the build script**
   ```powershell
   .\build.ps1
   ```

   Or for release build:
   ```powershell
   .\build.ps1 -Release
   ```

3. **If you don't have Visual Studio, skip native components**
   ```powershell
   .\build.ps1 -SkipNative
   ```

### Running the Application

After building:
1. Place your game files in `Hybrid-RSDK Main/Data/`
2. Navigate to `Custom Client/bin/Debug/net6.0-windows/` (or Release)
3. Run `SonicHybrid.exe`

## What You Can Do to Help

### Priority 1: Fix Hybrid-RSDK Loading Issues
The biggest blocker is that Hybrid-RSDK only loads backgrounds. This is likely:
- Missing sprite/object data loading
- Incorrect file path resolution
- Memory management issues
- Script execution problems

**Files to investigate:**
- `Hybrid-RSDK Main/RSDKV4/` - Core engine code
- `Hybrid-RSDK Main/SonicHybridRsdk.Build/` - Build tools
- `Hybrid-RSDK Main/CMakeLists.txt` - Build configuration

### Priority 2: Complete Sonic 3 AIR Integration
- Verify Oxygen Engine DLL builds correctly
- Test Sonic 3 AIR loading and execution
- Implement proper game state transitions

### Priority 3: Enhance Custom Client
- Improve error handling and user feedback
- Add configuration options
- Implement save state management
- Create better UI/UX

## Debugging Tips

1. **Check the logs**: The Custom Client creates detailed logs
2. **Use Debug builds**: They provide more information
3. **Test components separately**: Try running RSDK and Oxygen engines independently
4. **Verify file paths**: Make sure all game files are in the correct locations

## Need Help?

This is a complex project that requires knowledge of:
- C++ game engine development
- C# Windows Forms applications
- RSDK file format and scripting
- Sonic 3 AIR modding
- CMake and build systems

The project is ambitious but achievable with the right expertise and dedication!
