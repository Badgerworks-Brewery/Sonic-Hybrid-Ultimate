#ifndef HYBRID_ENGINE_H
#define HYBRID_ENGINE_H

#include "RetroEngine.hpp"
#include "StateManager.hpp"
#include "TransitionManager.hpp"
#include <functional>
#include <vector>
#include <memory>

namespace SonicHybrid {
    class HybridEngine {
    public:
        static HybridEngine& Instance() {
            static HybridEngine instance;
            return instance;
        }

        bool Initialize();
        void Update();
        void Render();
        void Cleanup();

        // Game state management
        bool LoadGame(const std::string& dataPath);
        bool IsGameComplete() const;
        void RegisterCompletionCallback(std::function<void()> callback);

        // Memory signatures for Sonic 2 completion
        struct CompletionSignature {
            uint32_t address;
            uint32_t value;
            std::string description;
        };

    private:
        HybridEngine() = default;
        ~HybridEngine() = default;

        std::unique_ptr<RetroEngine> engine;
        std::unique_ptr<StateManager> stateManager;
        std::unique_ptr<TransitionManager> transitionManager;
        std::function<void()> completionCallback;

        // Sonic 2 completion detection
        const std::vector<CompletionSignature> SONIC2_COMPLETION_SIGS = {
            {0x203A44, 0x0001, "Death Egg Robot defeated"},
            {0x203A48, 0x0001, "Final cutscene started"},
            {0x203A4C, 0x0001, "Credits sequence"}
        };

        bool CheckSonic2Completion();
        void HandleGameCompletion();
        bool ValidateMemorySignature(const CompletionSignature& sig);
    };
}

#endif // HYBRID_ENGINE_H
