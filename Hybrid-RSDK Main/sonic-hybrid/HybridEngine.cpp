#include "HybridEngine.hpp"
#include <cstring>

namespace SonicHybrid {
    bool HybridEngine::Initialize() {
        engine = std::make_unique<RetroEngine>();
        stateManager = std::make_unique<StateManager>();
        transitionManager = std::make_unique<TransitionManager>();

        // Initialize RSDK engine
        if (!engine->Initialize()) {
            return false;
        }

        // Initialize state and transition managers
        stateManager->Initialize(engine.get());
        transitionManager->Initialize(engine.get());

        return true;
    }

    void HybridEngine::Update() {
        if (!engine) return;

        // Update RSDK engine
        engine->Update();

        // Check for Sonic 2 completion
        if (CheckSonic2Completion()) {
            HandleGameCompletion();
        }

        // Update managers
        stateManager->Update();
        transitionManager->Update();
    }

    void HybridEngine::Render() {
        if (!engine) return;

        engine->Render();
        transitionManager->Render();
    }

    bool HybridEngine::LoadGame(const std::string& dataPath) {
        if (!engine) return false;

        // Load RSDK data file
        if (!engine->LoadRSDKFile(dataPath)) {
            return false;
        }

        // Initialize game state
        stateManager->ResetState();
        return true;
    }

    bool HybridEngine::CheckSonic2Completion() {
        if (!engine) return false;

        // Check all completion signatures
        for (const auto& sig : SONIC2_COMPLETION_SIGS) {
            if (!ValidateMemorySignature(sig)) {
                return false;
            }
        }

        return true;
    }

    bool HybridEngine::ValidateMemorySignature(const CompletionSignature& sig) {
        // Read memory at signature address
        uint32_t currentValue = 0;
        size_t readSize = sizeof(uint32_t);

        // Use RSDK's memory access functions
        if (!engine->ReadMemory(sig.address, &currentValue, readSize)) {
            return false;
        }

        return currentValue == sig.value;
    }

    void HybridEngine::HandleGameCompletion() {
        // Save final state
        stateManager->SaveState();

        // Start transition effect
        transitionManager->StartTransition();

        // Notify completion
        if (completionCallback) {
            completionCallback();
        }
    }

    void HybridEngine::RegisterCompletionCallback(std::function<void()> callback) {
        completionCallback = callback;
    }

    bool HybridEngine::IsGameComplete() const {
        return CheckSonic2Completion();
    }

    void HybridEngine::Cleanup() {
        if (engine) {
            engine->Cleanup();
        }

        stateManager.reset();
        transitionManager.reset();
        engine.reset();
    }
}
