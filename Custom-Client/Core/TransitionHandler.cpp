#include "TransitionHandler.hpp"
#include <cmath>

namespace SonicHybrid {
    void TransitionHandler::Initialize() {
        sourceEngine = nullptr;
        targetEngine = nullptr;
        transitionProgress = 0.0f;
        isTransitioning = false;
        currentTrackX = 0.0f;
        currentTrackY = 0.0f;
        
        // Initialize default track curve
        trackControlPoints = {
            {0.0f, 0.0f},    // Start point
            {0.3f, 0.1f},    // Control point 1
            {0.7f, -0.1f},   // Control point 2
            {1.0f, 0.0f}     // End point
        };
    }

    void TransitionHandler::Update() {
        if (!isTransitioning) return;

        // Update transition progress
        transitionProgress += 1.0f / 60.0f; // Assuming 60 FPS
        if (transitionProgress > 1.0f) {
            transitionProgress = 1.0f;
        }

        // Update based on transition type
        switch (currentEffect) {
            case TransitionEffect::TRAIN_TRACK:
                UpdateTrainTrackTransition();
                break;
            case TransitionEffect::FADE:
                UpdateFadeTransition();
                break;
            // Add other transition types
        }

        // Call update callback
        if (onTransitionUpdate) {
            onTransitionUpdate(transitionProgress);
        }

        // Check if transition is complete
        if (transitionProgress >= 1.0f) {
            HandleEngineSwitch();
        }
    }

    void TransitionHandler::StartTransition(GameEngineBase* from, GameEngineBase* to, TransitionEffect effect) {
        if (isTransitioning) return;

        sourceEngine = from;
        targetEngine = to;
        currentEffect = effect;
        transitionProgress = 0.0f;
        isTransitioning = true;

        // Prepare engines for transition
        if (sourceEngine) {
            sourceEngine->PrepareForTransition();
        }
        if (targetEngine) {
            targetEngine->PrepareForTransition();
        }

        // Call start callback
        if (onTransitionStart) {
            onTransitionStart();
        }
    }

    void TransitionHandler::UpdateTrainTrackTransition() {
        // Calculate current position on track curve using Bezier interpolation
        float t = transitionProgress;
        float oneMinusT = 1.0f - t;
        
        // Cubic Bezier curve calculation
        currentTrackX = 0.0f;
        currentTrackY = 0.0f;
        
        for (size_t i = 0; i < trackControlPoints.size(); i++) {
            float bezierCoeff = 0.0f;
            switch (i) {
                case 0: bezierCoeff = oneMinusT * oneMinusT * oneMinusT; break;
                case 1: bezierCoeff = 3.0f * oneMinusT * oneMinusT * t; break;
                case 2: bezierCoeff = 3.0f * oneMinusT * t * t; break;
                case 3: bezierCoeff = t * t * t; break;
            }
            currentTrackX += bezierCoeff * trackControlPoints[i].first;
            currentTrackY += bezierCoeff * trackControlPoints[i].second;
        }

        // Update engine positions based on track coordinates
        if (sourceEngine) {
            // Move source engine out based on track curve
            sourceEngine->HandleTransitionOut();
        }
        if (targetEngine) {
            // Move target engine in based on track curve
            targetEngine->HandleTransitionIn();
        }
    }

    void TransitionHandler::UpdateFadeTransition() {
        if (transitionProgress < 0.5f) {
            // First half: fade out source engine
            if (sourceEngine) {
                sourceEngine->HandleTransitionOut();
            }
        } else {
            // Second half: fade in target engine
            if (targetEngine) {
                targetEngine->HandleTransitionIn();
            }
        }
    }

    void TransitionHandler::HandleEngineSwitch() {
        // Finalize transition
        if (sourceEngine) {
            sourceEngine->Cleanup();
        }
        if (targetEngine) {
            targetEngine->ResumeEngine();
        }

        // Reset transition state
        isTransitioning = false;
        transitionProgress = 0.0f;

        // Call complete callback
        if (onTransitionComplete) {
            onTransitionComplete();
        }
    }

    void TransitionHandler::SetTransitionStartCallback(std::function<void()> callback) {
        onTransitionStart = callback;
    }

    void TransitionHandler::SetTransitionUpdateCallback(std::function<void(float)> callback) {
        onTransitionUpdate = callback;
    }

    void TransitionHandler::SetTransitionCompleteCallback(std::function<void()> callback) {
        onTransitionComplete = callback;
    }

    bool TransitionHandler::IsTransitioning() const {
        return isTransitioning;
    }

    float TransitionHandler::GetTransitionProgress() const {
        return transitionProgress;
    }

    void TransitionHandler::SetTrackCurve(const std::vector<std::pair<float, float>>& controlPoints) {
        if (controlPoints.size() >= 4) {
            trackControlPoints = controlPoints;
        }
    }
}
