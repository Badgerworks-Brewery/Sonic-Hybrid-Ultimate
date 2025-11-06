#ifndef RSDK_BRIDGE_H
#define RSDK_BRIDGE_H

#include "RetroEngine.hpp"
#include <functional>

namespace RSDK {
    class RSDKBridge {
    public:
        static RSDKBridge& Instance() {
            static RSDKBridge instance;
            return instance;
        }

        bool Initialize(RetroEngine* engine);
        void Update();
        void Cleanup();

        // Memory access
        bool ReadMemory(uint32_t address, void* buffer, size_t size);
        bool WriteMemory(uint32_t address, const void* buffer, size_t size);

        // Death Egg Zone state
        struct DeathEggState {
            bool isActive;          // Currently in Death Egg Zone
            bool robotDefeated;     // Death Egg Robot defeated
            bool cutsceneStarted;   // End cutscene has started
            bool creditsPlaying;    // Credits sequence playing
            int emeraldCount;       // Number of Chaos Emeralds collected
        };
        DeathEggState GetDeathEggState();

        // Event registration
        void RegisterCompletionCallback(std::function<void()> callback);

    private:
        RSDKBridge() = default;
        RetroEngine* rsdkEngine = nullptr;
        std::function<void()> completionCallback;

        // Memory addresses
        static constexpr uint32_t ADDR_CURRENT_ZONE = 0x203A40;
        static constexpr uint32_t ADDR_ROBOT_STATE = 0x203A44;
        static constexpr uint32_t ADDR_CUTSCENE = 0x203A48;
        static constexpr uint32_t ADDR_CREDITS = 0x203A4C;
        static constexpr uint32_t ADDR_EMERALDS = 0x203A50;
    };
}

#endif // RSDK_BRIDGE_H
