#ifndef TRANSITION_MANAGER_H
#define TRANSITION_MANAGER_H

#include "RetroEngine.hpp"
#include <vector>

namespace SonicHybrid {
    class TransitionManager {
    public:
        void Initialize(RetroEngine* engine);
        void Update();
        void Render();

        void StartTransition();
        bool IsTransitioning() const;
        float GetProgress() const;

        // Train track transition data
        struct TrackPoint {
            float x, y;       // Position
            float rotation;   // Rotation in degrees
            float scale;      // Scale factor
        };

    private:
        RetroEngine* rsdkEngine;
        bool transitioning;
        float progress;

        // Transition curve data
        std::vector<TrackPoint> trackPoints;
        TrackPoint currentPoint;

        void UpdateTrackPosition();
        void InterpolateTrackPoints(float t, TrackPoint& result);
        void RenderTrackEffect();
    };
}

#endif // TRANSITION_MANAGER_H
