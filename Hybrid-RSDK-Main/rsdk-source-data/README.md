# Source RSDK Files

## What Goes Here

Place your legally obtained Sonic game data files here:

- `soniccd.rsdk` - Data.rsdk from Sonic CD (2011 remaster)
- `sonic1.rsdk` - Data.rsdk from Sonic the Hedgehog (2013 mobile remaster)
- `sonic2.rsdk` - Data.rsdk from Sonic the Hedgehog 2 (2013 mobile remaster)

## How to Obtain

These files come from the official mobile/PC releases available on:
- **iOS App Store** - Sonic 1, Sonic 2, Sonic CD
- **Google Play Store** - Sonic 1, Sonic 2, Sonic CD
- **Steam** - Sonic CD

You must own these games legally. The files are copyrighted and cannot be distributed.

### Finding the Data.rsdk Files

**On Android:**
- Navigate to the game's installation directory (usually `/Android/data/com.sega.sonicX/`)
- Copy the `Data.rsdk` file and rename it according to the game

**On iOS:**
- Use a file manager app that can access app data directories
- Find the Sonic game's data folder
- Extract the `Data.rsdk` file

**On PC (Steam - Sonic CD only):**
- Navigate to the game's installation folder
- Copy the `Data.rsdk` file

## What Happens During Build

When you run the build process with these files present:

1. **UnpackScd** - Decrypts and extracts Sonic CD data (RSDKv3 encrypted format)
2. **UnpackS12** - Extracts Sonic 1 and Sonic 2 data (RSDKv4 format)
3. **Generator** - Converts Sonic CD assets to RSDKv4 and merges all three games
4. **Build** - Orchestrates the entire process and creates `../sonic-hybrid/Data.rsdk`

The resulting unified data file allows playing all three games seamlessly in a single continuous experience: **Sonic 1 → Sonic CD → Sonic 2**.

## If You Don't Have These Files

The build will still complete successfully, but:
- The unified hybrid Data.rsdk will NOT be generated
- You can still play games individually by providing their Data.rsdk files separately
- The Custom Client will look for individual game files instead of the hybrid experience

## Build Output

After successful generation, you'll find:

- `../sonic-hybrid/Data.rsdk` - The unified game data (playable with RSDKv4 engine)
- Unpacked intermediate data in subdirectories (soniccd/, sonic1/, sonic2/)

These intermediate files are used during the build process and can be deleted after generation if desired.
