#ifndef STATE_MANAGER_H
#define STATE_MANAGER_H

// The engine headers come from the RSDKV4-Decompilation submodule, which is what
// CMake actually compiles. They previously pointed at the stale in-tree engine copy
// (deleted) whose headers were placeholders - an ODR violation, since the hybrid
// libraries linked against the submodule while declaring against that other copy.
#include "../RSDKV4-Decompilation/RSDKv4/RetroEngine.hpp"
#include <vector>
#include <string>

namespace SonicHybrid {
    class StateManager {
    public:
        void Initialize(RetroEngine* engine);
        void Update();
        void ResetState();
        void SaveState();
        void LoadState();

        // State data structure
        struct GameState {
            // Player state
            float playerX;
            float playerY;
            int rings;
            int score;
            bool isSuper;

            // Level state
            std::string currentZone;
            int currentAct;
            std::vector<bool> checkpoints;

            // Game progress
            int emeralds;
            bool specialStageAvailable;

            // Engine state
            float cameraX;
            float cameraY;
            float musicVolume;
            float sfxVolume;
        };

    private:
        RetroEngine* rsdkEngine;
        GameState currentState;
        std::vector<GameState> savedStates;

        void ReadPlayerState();
        void WritePlayerState();
        void ReadLevelState();
        void WriteLevelState();
        void ReadGameProgress();
        void WriteGameProgress();
    };
}

#endif // STATE_MANAGER_H
