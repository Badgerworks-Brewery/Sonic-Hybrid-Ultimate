# Sonic Hybrid Ultimate - Custom Client

This is the custom client/script executor for Sonic Hybrid Ultimate. It manages the transitions between different Sonic games and handles communication between the RSDK engine and Sonic 3 AIR.

## Features

- Unified launcher for all Sonic games (1, CD, 2, and 3&K)
- Automatic game transitions at specific points:
  - Sonic 2 Death Egg Zone → Sonic 3&K
- Process monitoring and management
- Configuration system for paths and triggers

## Setup

1. Ensure you have the following prerequisites:
   - .NET 6.0 SDK
   - RSDK Content in the Hybrid-RSDK Main directory
   - Sonic 3 AIR in the Sonic3AIR directory

2. Build the client:
   ```bash
   dotnet build
   ```

3. Configure the paths in config.json if needed

## Directory Structure

```
Custom Client/
├── CustomClient.csproj    # Project file
├── Program.cs            # Main client code
├── Config.cs            # Configuration handling
└── README.md            # This file
```

## How it Works

1. The client starts the RSDK engine first (Sonic 1/CD/2)
2. It monitors the game output for specific triggers
3. When a transition trigger is detected:
   - Current game is cleanly shut down
   - Next game is automatically started
   - Game state/progress is preserved

## Development

To add new features or modify transitions:

1. Update the TransitionTriggers in Config.cs
2. Add corresponding handlers in Program.cs
3. Test the transitions with debug logging enabled
