#include "StateManager.hpp"

namespace SonicHybrid {
    void StateManager::Initialize(RSDK::RetroEngine* engine) {
        rsdkEngine = engine;
        ResetState();
    }

    void StateManager::Update() {
        if (!rsdkEngine) return;

        // Read current state from engine
        ReadPlayerState();
        ReadLevelState();
        ReadGameProgress();
    }

    void StateManager::ResetState() {
        currentState = GameState();
        savedStates.clear();
    }

    void StateManager::SaveState() {
        if (!rsdkEngine) return;

        // Read current state
        ReadPlayerState();
        ReadLevelState();
        ReadGameProgress();

        // Save state
        savedStates.push_back(currentState);
    }

    void StateManager::LoadState() {
        if (savedStates.empty()) return;

        // Load last saved state
        currentState = savedStates.back();
        savedStates.pop_back();

        // Write state back to engine
        WritePlayerState();
        WriteLevelState();
        WriteGameProgress();
    }

    void StateManager::ReadPlayerState() {
        // Read player position
        rsdkEngine->ReadMemory(0x20A100, &currentState.playerX, sizeof(float));
        rsdkEngine->ReadMemory(0x20A104, &currentState.playerY, sizeof(float));

        // Read player stats
        rsdkEngine->ReadMemory(0x20A200, &currentState.rings, sizeof(int));
        rsdkEngine->ReadMemory(0x20A204, &currentState.score, sizeof(int));

        // Read super state
        uint8_t superState;
        rsdkEngine->ReadMemory(0x20A208, &superState, sizeof(uint8_t));
        currentState.isSuper = superState != 0;
    }

    void StateManager::WritePlayerState() {
        // Write player position
        rsdkEngine->WriteMemory(0x20A100, &currentState.playerX, sizeof(float));
        rsdkEngine->WriteMemory(0x20A104, &currentState.playerY, sizeof(float));

        // Write player stats
        rsdkEngine->WriteMemory(0x20A200, &currentState.rings, sizeof(int));
        rsdkEngine->WriteMemory(0x20A204, &currentState.score, sizeof(int));

        // Write super state
        uint8_t superState = currentState.isSuper ? 1 : 0;
        rsdkEngine->WriteMemory(0x20A208, &superState, sizeof(uint8_t));
    }

    void StateManager::ReadLevelState() {
        // Read zone info
        char zoneName[32];
        rsdkEngine->ReadMemory(0x20B000, zoneName, sizeof(zoneName));
        currentState.currentZone = zoneName;
        
        // Read act number
        rsdkEngine->ReadMemory(0x20B100, &currentState.currentAct, sizeof(int));

        // Read checkpoint data
        uint32_t checkpointData;
        rsdkEngine->ReadMemory(0x20B200, &checkpointData, sizeof(uint32_t));
        currentState.checkpoints.clear();
        for (int i = 0; i < 32; i++) {
            currentState.checkpoints.push_back((checkpointData & (1 << i)) != 0);
        }
    }

    void StateManager::WriteLevelState() {
        // Write zone info
        rsdkEngine->WriteMemory(0x20B000, currentState.currentZone.c_str(), 
                               currentState.currentZone.length() + 1);
        
        // Write act number
        rsdkEngine->WriteMemory(0x20B100, &currentState.currentAct, sizeof(int));

        // Write checkpoint data
        uint32_t checkpointData = 0;
        for (size_t i = 0; i < currentState.checkpoints.size() && i < 32; i++) {
            if (currentState.checkpoints[i]) {
                checkpointData |= (1 << i);
            }
        }
        rsdkEngine->WriteMemory(0x20B200, &checkpointData, sizeof(uint32_t));
    }

    void StateManager::ReadGameProgress() {
        // Read emerald count
        rsdkEngine->ReadMemory(0x20C000, &currentState.emeralds, sizeof(int));

        // Read special stage availability
        uint8_t specialStage;
        rsdkEngine->ReadMemory(0x20C100, &specialStage, sizeof(uint8_t));
        currentState.specialStageAvailable = specialStage != 0;

        // Read camera position
        rsdkEngine->ReadMemory(0x20C200, &currentState.cameraX, sizeof(float));
        rsdkEngine->ReadMemory(0x20C204, &currentState.cameraY, sizeof(float));

        // Read audio settings
        rsdkEngine->ReadMemory(0x20C300, &currentState.musicVolume, sizeof(float));
        rsdkEngine->ReadMemory(0x20C304, &currentState.sfxVolume, sizeof(float));
    }

    void StateManager::WriteGameProgress() {
        // Write emerald count
        rsdkEngine->WriteMemory(0x20C000, &currentState.emeralds, sizeof(int));

        // Write special stage availability
        uint8_t specialStage = currentState.specialStageAvailable ? 1 : 0;
        rsdkEngine->WriteMemory(0x20C100, &specialStage, sizeof(uint8_t));

        // Write camera position
        rsdkEngine->WriteMemory(0x20C200, &currentState.cameraX, sizeof(float));
        rsdkEngine->WriteMemory(0x20C204, &currentState.cameraY, sizeof(float));

        // Write audio settings
        rsdkEngine->WriteMemory(0x20C300, &currentState.musicVolume, sizeof(float));
        rsdkEngine->WriteMemory(0x20C304, &currentState.sfxVolume, sizeof(float));
    }
}
