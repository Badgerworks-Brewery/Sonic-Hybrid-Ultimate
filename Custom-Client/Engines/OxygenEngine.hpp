#ifndef OXYGEN_ENGINE_H
#define OXYGEN_ENGINE_H

#include "GameEngineBase.hpp"
#include <vector>

namespace SonicHybrid {
    class OxygenEngine : public GameEngineBase {
    public:
        OxygenEngine();
        ~OxygenEngine() override;

        // GameEngineBase implementation
        bool Initialize(const std::string& romPath) override;
        void Update() override;
        void Render() override;
        void Cleanup() override;

        void SaveState() override;
        void LoadState() override;
        void PauseEngine() override;
        void ResumeEngine() override;

        void PrepareForTransition() override;
        void HandleTransitionIn() override;
        void HandleTransitionOut() override;
        bool IsReadyForTransition() const override;

        bool LoadScene(const std::string& sceneName) override;
        std::string GetCurrentScene() const override;
        bool IsSceneComplete() const override;

        bool IsRunning() const override;
        std::string GetEngineName() const override { return "Oxygen"; }
        int GetEngineVersion() const override { return 1; }

        // Oxygen/S3AIR specific functions
        bool InitializeLemonscript();
        bool LoadROM(const std::string& romPath);
        void ExecuteLemonscript(const std::string& scriptName);
        bool StartAngelIsland();
        
    private:
        void* oxygenInstance;  // Pointer to Oxygen Engine instance
        void* lemonscriptContext;  // Pointer to Lemonscript context
        std::string currentROM;
        std::string currentScene;
        bool engineRunning;
        bool lemonscriptInitialized;
        
        // Scene management
        struct ZoneInfo {
            std::string name;
            int actNumber;
            std::vector<std::string> scripts;
        };
        std::vector<ZoneInfo> zoneList;
        
        // State preservation
        struct EngineState {
            // Add relevant state variables
            float playerX, playerY;
            float cameraX, cameraY;
            int currentZone;
            int currentAct;
            int rings;
            int score;
            // Add more as needed
        };
        EngineState savedState;
        
        void InitializeZoneList();
        void SetupAngelIsland();
        bool CompileLemonscript(const std::string& scriptPath);
        void TranslateRSDKState(const void* rsdkState);
    };
}

#endif // OXYGEN_ENGINE_H
