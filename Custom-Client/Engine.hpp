#ifndef ENGINE_H
#define ENGINE_H

#include <string>
#include <memory>
#include <vector>
#include <functional>

namespace SonicHybrid {
    enum class EngineType {
        RSDK_V4,
        OXYGEN,
        NONE
    };

    class Engine {
    public:
        static Engine& Instance() {
            static Engine instance;
            return instance;
        }

        bool Initialize();
        void Update();
        void Render();
        void Cleanup();

        // Engine state
        bool LoadRSDKGame(const std::string& rsdkPath);
        bool LoadSonic3ROM(const std::string& romPath);
        bool IsRunning() const { return engineRunning; }
        
        // Scene management
        bool LoadScene(const std::string& sceneName);
        std::string GetCurrentScene() const;
        
        // Sonic 2 completion detection
        struct DeathEggState {
            bool robotDefeated;
            bool cutsceneStarted;
            bool creditsPlaying;
            bool emeraldsCollected;  // For alternate ending
        };
        void RegisterCompletionCallback(std::function<void()> callback);
        bool IsSonic2Complete() const;

        // Transition handling
        void TransitionToSonic3();
        bool IsTransitioning() const;
        void UpdateTransition();

    private:
        Engine() = default;
        ~Engine() = default;

        // Core state
        bool engineRunning = false;
        EngineType currentEngine = EngineType::NONE;
        bool transitioning = false;
        float transitionProgress = 0.0f;

        // RSDK state
        void* rsdkInstance = nullptr;
        std::string currentRSDKFile;
        std::function<void()> completionCallback;
        DeathEggState deathEggState;

        // Oxygen/S3AIR state
        void* oxygenInstance = nullptr;
        std::string currentROMFile;
        bool lemonscriptInitialized = false;

        // Scene tracking
        struct SceneInfo {
            std::string name;
            bool isEndScene;
            std::vector<uint32_t> completionSignatures;
        };
        std::vector<SceneInfo> sceneList;

        // Internal methods
        void InitializeRSDK();
        void InitializeOxygen();
        void InitializeSceneList();
        void CheckSonic2Completion();
        void HandleTransition();
        void SaveState();
        void RestoreState();

        // Memory addresses for Sonic 2 completion
        const uint32_t ADDR_ROBOT_DEFEATED = 0x203A44;
        const uint32_t ADDR_CUTSCENE_START = 0x203A48;
        const uint32_t ADDR_CREDITS_PLAYING = 0x203A4C;
        const uint32_t ADDR_EMERALDS_COUNT = 0x203A50;
    };
}

#endif // ENGINE_H
