#include "RSDKBridge.hpp"

namespace RSDK {
    bool RSDKBridge::Initialize(RetroEngine* engine) {
        if (!engine) return false;
        rsdkEngine = engine;
        return true;
    }

    void RSDKBridge::Update() {
        if (!rsdkEngine) return;

        // Check Death Egg state
        auto state = GetDeathEggState();
        if (state.isActive && state.robotDefeated && state.cutsceneStarted && state.creditsPlaying) {
            if (completionCallback) {
                completionCallback();
            }
        }
    }

    bool RSDKBridge::ReadMemory(uint32_t address, void* buffer, size_t size) {
        if (!rsdkEngine || !buffer) return false;

        // Validate memory range
        if (address < 0x200000 || address + size > 0x300000) return false;

        // Read from RSDK memory
        memcpy(buffer, reinterpret_cast<void*>(address), size);
        return true;
    }

    bool RSDKBridge::WriteMemory(uint32_t address, const void* buffer, size_t size) {
        if (!rsdkEngine || !buffer) return false;

        // Validate memory range
        if (address < 0x200000 || address + size > 0x300000) return false;

        // Write to RSDK memory
        memcpy(reinterpret_cast<void*>(address), buffer, size);
        return true;
    }

    RSDKBridge::DeathEggState RSDKBridge::GetDeathEggState() {
        DeathEggState state = {};
        
        if (!rsdkEngine) return state;

        // Read current zone
        uint8_t currentZone = 0;
        ReadMemory(ADDR_CURRENT_ZONE, &currentZone, sizeof(currentZone));
        state.isActive = (currentZone == 0x0B); // Death Egg Zone ID

        // Read completion flags
        uint8_t robotState = 0, cutsceneState = 0, creditsState = 0, emeralds = 0;
        ReadMemory(ADDR_ROBOT_STATE, &robotState, sizeof(robotState));
        ReadMemory(ADDR_CUTSCENE, &cutsceneState, sizeof(cutsceneState));
        ReadMemory(ADDR_CREDITS, &creditsState, sizeof(creditsState));
        ReadMemory(ADDR_EMERALDS, &emeralds, sizeof(emeralds));

        state.robotDefeated = (robotState != 0);
        state.cutsceneStarted = (cutsceneState != 0);
        state.creditsPlaying = (creditsState != 0);
        state.emeraldCount = emeralds;

        return state;
    }

    void RSDKBridge::RegisterCompletionCallback(std::function<void()> callback) {
        completionCallback = callback;
    }
}
