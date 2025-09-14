# Troubleshooting Guide

This guide covers common issues and their solutions when building and running Sonic Hybrid Ultimate.

## Build Issues

### CMake Cache Error

**Error**: `CMake Error: The current CMakeCache.txt directory ... is different than the directory ... where CMakeCache.txt was created`

**Solution**:
```bash
rm -rf "Hybrid-RSDK Main/build"
mkdir -p "Hybrid-RSDK Main/build"
cd "Hybrid-RSDK Main/build"
cmake ..
```

### Missing Dependencies

**Error**: `Could not find SDL2` or similar dependency errors

**Solution**:
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install cmake build-essential pkg-config libsdl2-dev libgl1-mesa-dev libglew-dev libvorbis-dev

# macOS
brew install cmake sdl2 glew libvorbis pkg-config
```

### .NET Build Errors

**Error**: `The specified framework 'Microsoft.NETCore.App', version '6.0.0' was not found`

**Solution**: Install .NET 6.0 SDK from https://dotnet.microsoft.com/download/dotnet/6.0

### Missing RSDK Decompilations

**Error**: Build fails because RSDKv3 or RSDKv5 directories don't exist

**Solution**:
```bash
./fetch_rsdkv3.sh
./fetch_rsdkv5.sh
```

## Runtime Issues

### Game Data Files Not Found

**Error**: `RSDK file not found` or similar errors when loading games

**Solution**: Ensure you have placed the correct data files:
```
Hybrid-RSDK Main/rsdk-source-data/
├── sonic1.rsdk    # From Sonic 1 (2013 version)
├── sonic2.rsdk    # From Sonic 2 (2013 version)
├── soniccd.rsdk   # From Sonic CD
└── sonic3.bin     # From Sonic 3 & Knuckles ROM
```

### Graphics Corruption

**Issue**: Graphics appear corrupted or missing

**Possible Solutions**:
1. Ensure you're using the correct RSDK data files (2013 versions for Sonic 1 & 2)
2. Check that your graphics drivers are up to date
3. Try running in software rendering mode if available

### Audio Issues

**Issue**: No sound or audio crackling

**Possible Solutions**:
1. Check that Vorbis libraries are properly installed
2. Ensure audio drivers are up to date
3. Try adjusting audio buffer settings if available

## Platform-Specific Issues

### Windows
- Ensure Visual Studio Build Tools or Visual Studio 2019+ is installed
- Make sure Windows SDK is installed

### macOS
- Install Xcode Command Line Tools: `xcode-select --install`
- Use Homebrew for dependency management

### Linux
- Ensure development packages are installed (build-essential, cmake, etc.)
- Some distributions may require additional OpenGL development packages

## Getting Help

If you encounter issues not covered here:

1. Check the [GitHub Issues](https://github.com/Badgerworks-Brewery/Sonic-Hybrid-Ultimate/issues)
2. Create a new issue with:
   - Your operating system and version
   - Complete error messages
   - Steps to reproduce the problem
   - Build logs if applicable
