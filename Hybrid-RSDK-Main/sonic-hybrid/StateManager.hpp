#ifndef STATE_MANAGER_H
#define STATE_MANAGER_H

#include "../RSDKV4/RSDKV4/RetroEngine.hpp"
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
