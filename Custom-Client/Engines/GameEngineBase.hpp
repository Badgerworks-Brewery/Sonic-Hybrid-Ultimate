#ifndef GAME_ENGINE_BASE_H
#define GAME_ENGINE_BASE_H

#include <string>
#include <memory>

namespace SonicHybrid {
    class GameEngineBase {
    public:
        virtual ~GameEngineBase() = default;

        // Core engine functions
        virtual bool Initialize(const std::string& dataPath) = 0;
        virtual void Update() = 0;
        virtual void Render() = 0;
        virtual void Cleanup() = 0;

        // State management
        virtual void SaveState() = 0;
        virtual void LoadState() = 0;
        virtual void PauseEngine() = 0;
        virtual void ResumeEngine() = 0;

        // Transition support
        virtual void PrepareForTransition() = 0;
        virtual void HandleTransitionIn() = 0;
        virtual void HandleTransitionOut() = 0;
        virtual bool IsReadyForTransition() const = 0;

        // Scene management
        virtual bool LoadScene(const std::string& sceneName) = 0;
        virtual std::string GetCurrentScene() const = 0;
        virtual bool IsSceneComplete() const = 0;

        // Engine-specific getters
        virtual bool IsRunning() const = 0;
        virtual std::string GetEngineName() const = 0;
        virtual int GetEngineVersion() const = 0;
    };
}

#endif // GAME_ENGINE_BASE_H
