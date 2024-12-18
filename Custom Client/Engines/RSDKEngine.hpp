#ifndef RSDK_ENGINE_H
#define RSDK_ENGINE_H

#include "GameEngineBase.hpp"
#include <vector>

namespace SonicHybrid {
    class RSDKEngine : public GameEngineBase {
    public:
        RSDKEngine();
        ~RSDKEngine() override;

        // GameEngineBase implementation
        bool Initialize(const std::string& dataPath) override;
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
        std::string GetEngineName() const override { return "RSDK"; }
        int GetEngineVersion() const override { return 4; }

        // RSDK specific functions
        bool LoadRSDKFile(const std::string& rsdkPath);
        bool InitializeRSDKv4();
        void UpdateRSDKState();
        bool IsSonic2Complete() const;

    private:
        void* rsdkInstance;  // Pointer to RSDK instance
        std::string currentRSDKFile;
        std::string currentScene;
        bool engineRunning;
        bool transitionReady;
        
        // Scene completion detection
        struct SceneInfo {
            std::string name;
            bool isComplete;
            std::vector<std::string> nextScenes;
        };
        std::vector<SceneInfo> sceneList;
        
        // State preservation
        struct EngineState {
            // Add relevant state variables
            float playerX, playerY;
            float cameraX, cameraY;
            int currentAct;
            int score;
            // Add more as needed
        };
        EngineState savedState;
        
        void InitializeSceneList();
        void CheckSceneCompletion();
        void SaveEngineState();
        void RestoreEngineState();
    };
}

#endif // RSDK_ENGINE_H
