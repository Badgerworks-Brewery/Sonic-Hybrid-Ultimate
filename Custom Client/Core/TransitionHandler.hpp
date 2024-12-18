#ifndef TRANSITION_HANDLER_H
#define TRANSITION_HANDLER_H

#include <memory>
#include <functional>
#include "../Engines/GameEngineBase.hpp"

namespace SonicHybrid {
    enum class TransitionEffect {
        FADE,
        WIPE,
        CROSSFADE,
        TRAIN_TRACK,
        CUSTOM
    };

    class TransitionHandler {
    public:
        static TransitionHandler& Instance() {
            static TransitionHandler instance;
            return instance;
        }

        void Initialize();
        void Update();
        
        // Transition control
        void StartTransition(GameEngineBase* fromEngine, GameEngineBase* toEngine, 
                           TransitionEffect effect = TransitionEffect::TRAIN_TRACK);
        void AbortTransition();
        bool IsTransitioning() const;
        float GetTransitionProgress() const;
        
        // Callbacks
        void SetTransitionStartCallback(std::function<void()> callback);
        void SetTransitionUpdateCallback(std::function<void(float)> callback);
        void SetTransitionCompleteCallback(std::function<void()> callback);
        
        // Train track specific
        void UpdateTrackPosition(float progress);
        void SetTrackCurve(const std::vector<std::pair<float, float>>& controlPoints);
        
    private:
        TransitionHandler() = default;
        ~TransitionHandler() = default;
        
        void UpdateTrainTrackTransition();
        void UpdateFadeTransition();
        void HandleEngineSwitch();
        
        GameEngineBase* sourceEngine;
        GameEngineBase* targetEngine;
        TransitionEffect currentEffect;
        float transitionProgress;
        bool isTransitioning;
        
        // Transition curve data
        std::vector<std::pair<float, float>> trackControlPoints;
        float currentTrackX;
        float currentTrackY;
        
        // Callbacks
        std::function<void()> onTransitionStart;
        std::function<void(float)> onTransitionUpdate;
        std::function<void()> onTransitionComplete;
    };
}

#endif // TRANSITION_HANDLER_H
