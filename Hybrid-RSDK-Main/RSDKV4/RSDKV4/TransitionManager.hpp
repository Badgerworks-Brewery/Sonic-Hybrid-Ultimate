#ifndef TRANSITION_MANAGER_H
#define TRANSITION_MANAGER_H

#include <functional>
#include <string>

namespace RSDK {
    enum class TransitionType {
        FADE,
        WIPE,
        CROSSFADE,
        CUSTOM
    };

    struct EntityState {
        int entityId;
        float x, y;
        float velocityX, velocityY;
        int animationId;
        int frameId;
        bool active;
        // Add more state variables as needed
    };

    class TransitionManager {
    public:
        static void Initialize();
        
        // Core transition functions
        static void BeginTransition(TransitionType type, float duration);
        static void UpdateTransition();
        static bool IsTransitioning();
        
        // Entity state management
        static void SaveEntityState(const EntityState& state);
        static EntityState LoadEntityState(int entityId);
        static void TranslateCoordinates(float& x, float& y, bool toNewEngine);
        
        // Engine switching
        static void PrepareEngineSwitch();
        static void SwitchToEngine(const std::string& engineName);
        static void FinalizeEngineSwitch();
        
        // Callbacks
        static void SetTransitionStartCallback(std::function<void()> callback);
        static void SetTransitionEndCallback(std::function<void()> callback);
        static void SetCustomTransitionUpdate(std::function<void(float)> callback);
        
        // Train track transition specific
        static void SetTrackPosition(float position);
        static float GetTrackPosition();
        static void UpdateTrackTransition();
        
    private:
        static TransitionType currentType;
        static float transitionProgress;
        static bool isTransitioning;
        static float trackPosition;
        static std::string targetEngine;
    };
}

#endif // TRANSITION_MANAGER_H
