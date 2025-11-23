# Sonic 3 AIR Setup Guide

This guide explains how to set up Sonic 3 AIR integration for Sonic Hybrid Ultimate.

## What You Need

To play Sonic 3 & Knuckles in Sonic Hybrid Ultimate, you need **TWO** things:

1. **Sonic 3 AIR executable** - The actual Sonic 3 AIR application
2. **Sonic 3 & Knuckles ROM file** - Your legally obtained ROM

## Step 1: Download Sonic 3 AIR

1. Go to [https://sonic3air.org/](https://sonic3air.org/)
2. Download the latest version of Sonic 3 AIR
3. Extract the downloaded archive

## Step 2: Install Sonic 3 AIR

You have several options for where to place Sonic 3 AIR:

### Option A: Project Directory (Recommended)
1. Create a folder called `Sonic 3 AIR Main` in your Sonic Hybrid Ultimate directory
2. Copy the entire Sonic 3 AIR installation into this folder
3. Make sure `sonic3air.exe` is located at: `Sonic 3 AIR Main/sonic3air.exe`

### Option B: System Installation
1. Install Sonic 3 AIR to `C:\Program Files\Sonic 3 AIR\`
2. Make sure `sonic3air.exe` is located at: `C:\Program Files\Sonic 3 AIR\sonic3air.exe`

### Option C: Same Directory
1. Copy `sonic3air.exe` to the same directory as your Sonic Hybrid Ultimate executable

## Step 3: Obtain ROM File

You need a legally obtained Sonic 3 & Knuckles ROM file. Common names include:
- `sonic3.bin`
- `Sonic_Knuckles_Wii_VC.bin`
- `s3k.bin`

**Note**: We cannot provide ROM files. You must obtain them legally from your own cartridge or legitimate sources.

## Step 4: Test the Setup

1. Launch Sonic Hybrid Ultimate
2. Click "Load Sonic 3 & Knuckles"
3. If prompted, select your ROM file
4. The application should launch Sonic 3 AIR with your ROM

## Troubleshooting

### Error: "Sonic 3 AIR executable not found"

This means the application cannot find `sonic3air.exe`. Check that:
- Sonic 3 AIR is installed in one of the expected locations
- The executable is named `sonic3air.exe` (Windows) or `sonic3air` (Linux)
- You have the correct file permissions

### Error: "ROM file not found or not readable"

This means there's an issue with your ROM file. Check that:
- The ROM file exists at the specified path
- The file is not corrupted
- You have read permissions for the file
- The file size is reasonable (should be several MB)

### Error: "OxygenEngine native library is not available"

This means the native libraries weren't built. To fix this:
1. Run `build_native_libs.sh` (Linux/Mac) or `build_native_libs.ps1` (Windows)
2. Or build the project with CMake
3. Make sure `OxygenEngine.dll` is in the application directory

## Directory Structure

Your final directory structure should look like this:

```
Sonic-Hybrid-Ultimate/
├── Custom-Client/
│   └── bin/
│       └── Release/
│           └── net6.0-windows/
│               ├── SonicHybridUltimate.exe
│               ├── OxygenEngine.dll
│               └── RSDKv4.dll
├── Sonic 3 AIR Main/
│   ├── sonic3air.exe
│   ├── data/
│   └── ... (other Sonic 3 AIR files)
└── sonic3.bin (or your ROM file)
```

## Advanced Configuration

### Environment Variables

You can set the `SONIC3AIR_PATH` environment variable to specify a custom location for Sonic 3 AIR:

```bash
export SONIC3AIR_PATH="/path/to/your/sonic3air"
```

### Command Line Arguments

Sonic 3 AIR supports various command line arguments. The integration automatically passes:
- `--rom="path/to/your/rom.bin"` - Specifies the ROM file to use

## Support

If you're still having issues:

1. Check the application log for detailed error messages
2. Verify that Sonic 3 AIR works standalone with your ROM
3. Make sure all native libraries are built and present
4. Check file permissions and paths

For more help, refer to the main project documentation or the Sonic 3 AIR documentation at [https://sonic3air.org/](https://sonic3air.org/).