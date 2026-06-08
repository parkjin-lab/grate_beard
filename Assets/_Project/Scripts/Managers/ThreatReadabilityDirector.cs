using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public sealed class ThreatReadabilityDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private PlayerVisibilitySource visibilitySource;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private FogOfWarSystem fogOfWar;
        [SerializeField] private StagePressureDirector stagePressureDirector;
        [SerializeField] private MapTuningDebugController mapTuning;
        [SerializeField] private MapSystem mapSystem;

        [Header("Flow")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField, Min(0.02f)] private float updateInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float referenceResolveRetryInterval = 0.75f;
        [SerializeField, Min(0.1f)] private float responseSmoothing = 4.7f;
        [SerializeField] private bool logPressureChanges;

        [Header("Threat Evaluation")]
        [SerializeField, Min(2f)] private float threatRange = 13f;
        [SerializeField, Range(0f, 1f)] private float nearbyThreatWeight = 0.68f;
        [SerializeField, Range(0f, 1f)] private float stagePressureWeight = 0.32f;
        [SerializeField, Range(0f, 1f)] private float suspicionWeight = 0.35f;

        [Header("Threat State Weights")]
        [SerializeField, Range(0f, 1f)] private float idleWeight = 0.16f;
        [SerializeField, Range(0f, 1f)] private float suspicionStateWeight = 0.42f;
        [SerializeField, Range(0f, 1f)] private float investigateWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] private float chaseWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float searchWeight = 0.58f;
        [SerializeField, Range(0f, 1f)] private float returnWeight = 0.28f;
        [SerializeField, Range(0f, 1f)] private float stunnedWeight = 0.08f;

        [Header("Map Preset Bias")]
        [SerializeField] private bool useMapPresetBias = true;
        [SerializeField, Range(0.7f, 1.3f)] private float compactPressureBias = 0.93f;
        [SerializeField, Range(0.7f, 1.3f)] private float standardPressureBias = 1f;
        [SerializeField, Range(0.7f, 1.3f)] private float expansivePressureBias = 1.08f;

        [Header("Camera Tuning")]
        [SerializeField, Min(0f)] private float maxCameraZoomOut = 0.55f;
        [SerializeField, Min(1f)] private float minimumCameraOrthoSize = 3.75f;
        [SerializeField, Min(1f)] private float maximumCameraOrthoSize = 5.85f;
        [SerializeField, Min(0f)] private float maxCloseThreatZoomIn = 0.52f;
        [SerializeField, Range(0f, 1f)] private float closeThreatTunnelStart = 0.24f;
        [SerializeField, Range(0f, 1f)] private float closeThreatTunnelFull = 0.78f;
        [SerializeField, Range(0f, 1f)] private float closeThreatSuppressZoomOut = 0.72f;
        [SerializeField, Range(0.5f, 2f)] private float minLookAheadMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxLookAheadMultiplier = 1.35f;
        [SerializeField, Range(0.5f, 2f)] private float minSmoothMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxSmoothMultiplier = 1.25f;
        [SerializeField, Range(0.5f, 2f)] private float minLookAheadSmoothingMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxLookAheadSmoothingMultiplier = 1.18f;

        [Header("Art Grade")]
        [SerializeField] private bool enableRuntimeArtGrade = true;
        [SerializeField, Min(0.1f)] private float cameraBackgroundLerpSpeed = 5.4f;
        [SerializeField] private Color calmCameraBackgroundColor = new(0.018f, 0.024f, 0.034f, 1f);
        [SerializeField] private Color dangerCameraBackgroundColor = new(0.052f, 0.006f, 0.014f, 1f);
        [SerializeField] private Color calmFogTint = new(0.019f, 0.025f, 0.038f, 1f);
        [SerializeField] private Color dangerFogTint = new(0.033f, 0.006f, 0.012f, 1f);
        [SerializeField, Range(0.65f, 1.35f)] private float calmFogHiddenAlphaMultiplier = 1.08f;
        [SerializeField, Range(0.65f, 1.35f)] private float dangerFogHiddenAlphaMultiplier = 1.28f;
        [SerializeField, Range(0.4f, 1.3f)] private float calmFogVisibleAlphaMultiplier = 1.08f;
        [SerializeField, Range(0.4f, 1.3f)] private float dangerFogVisibleAlphaMultiplier = 0.82f;
        [SerializeField] private bool enableThreatPulseImpulse = true;
        [SerializeField, Range(0f, 1f)] private float pulsePressureThreshold = 0.78f;
        [SerializeField, Range(0f, 1f)] private float pulsePressureDeltaThreshold = 0.12f;
        [SerializeField, Min(0.05f)] private float pulseCooldownSeconds = 1.25f;
        [SerializeField, Min(0f)] private float pulseImpulseAmplitude = 0.14f;
        [SerializeField, Min(0.05f)] private float pulseImpulseDuration = 0.18f;
        [SerializeField] private bool enablePressureWaveFeedback = true;
        [SerializeField] private bool pressureWaveRaisesRuntimeEvent = true;
        [SerializeField, Min(0.1f)] private float pressureWaveEventCooldownSeconds = 5.5f;
        [SerializeField] private Color pressureWaveColor = new(0.82f, 0.08f, 0.14f, 0.36f);
        [SerializeField, Min(0.1f)] private float pressureWaveMinRadius = 2.4f;
        [SerializeField, Min(0.1f)] private float pressureWaveMaxRadius = 4.2f;
        [SerializeField, Min(0.1f)] private float pressureWaveDuration = 1.65f;
        [SerializeField, Range(1, 4)] private int pressureWaveRingCount = 3;
        [SerializeField, Min(0f)] private float pressureWaveRingInterval = 0.34f;
        [SerializeField] private int pressureWaveSortingOrder = 42;

        [Header("Dread Beat")]
        [SerializeField] private bool enableDreadBeat = true;
        [SerializeField, Range(0f, 1f)] private float dreadBeatPressureThreshold = 0.34f;
        [SerializeField, Min(0.1f)] private float maxDreadBeatInterval = 4.5f;
        [SerializeField, Min(0.1f)] private float minDreadBeatInterval = 2.05f;
        [SerializeField, Min(0f)] private float minDreadBeatImpulse = 0.025f;
        [SerializeField, Min(0f)] private float maxDreadBeatImpulse = 0.085f;
        [SerializeField, Min(0.05f)] private float dreadBeatImpulseDuration = 0.28f;
        [SerializeField, Range(0f, 0.75f)] private float dreadBeatIntervalJitter = 0.28f;
        [SerializeField] private bool enableDreadBreathTint = true;
        [SerializeField] private Color dreadBreathTint = new(0.13f, 0.01f, 0.024f, 1f);
        [SerializeField, Min(0.05f)] private float dreadBreathSpeed = 0.72f;
        [SerializeField, Range(0f, 0.45f)] private float dreadBreathTintStrength = 0.18f;

        [Header("Phantom Cues")]
        [SerializeField] private bool enablePhantomCues = true;
        [SerializeField, Range(0f, 1f)] private float phantomCuePressureThreshold = 0.42f;
        [SerializeField, Min(0.5f)] private float maxPhantomCueInterval = 9f;
        [SerializeField, Min(0.5f)] private float minPhantomCueInterval = 4.1f;
        [SerializeField, Range(0f, 2f)] private float phantomCueIntervalJitter = 0.65f;
        [SerializeField, Min(0.5f)] private float phantomCueMinDistance = 4.6f;
        [SerializeField, Min(0.5f)] private float phantomCueMaxDistance = 9.4f;
        [SerializeField, Range(0f, 1f)] private float phantomCueHiddenFogThreshold = 0.55f;
        [SerializeField] private Color phantomCueColor = new(0.2f, 0.58f, 1f, 0.34f);
        [SerializeField, Min(0.1f)] private float phantomCueRadius = 3.35f;
        [SerializeField, Min(0.1f)] private float phantomCueDuration = 2.85f;
        [SerializeField, Range(1, 4)] private int phantomCueRingCount = 2;
        [SerializeField, Min(0f)] private float phantomCueRingInterval = 0.42f;
        [SerializeField] private int phantomCueSortingOrder = 32;
        [SerializeField] private bool phantomCueEmitsNoise = true;
        [SerializeField, Min(0f)] private float phantomCueNoiseLoudness = 0.72f;
        [SerializeField, Min(0f)] private float phantomCueNoiseRadius = 4.8f;
        [SerializeField] private bool enablePhantomCueAudio = true;
        [SerializeField, Range(0f, 1f)] private float phantomCueAudioVolume = 0.18f;
        [SerializeField, Min(0.05f)] private float phantomCueAudioDuration = 1.45f;
        [SerializeField, Range(30f, 220f)] private float phantomCueAudioFrequency = 54f;
        [SerializeField, Range(0f, 1f)] private float phantomCueAudioSpatialBlend = 0.55f;

        [Header("Close Stalker Cues")]
        [SerializeField] private bool enableCloseStalkerCues = true;
        [SerializeField, Range(0f, 1f)] private float closeStalkerPressureThreshold = 0.46f;
        [SerializeField, Range(0f, 1f)] private float closeStalkerNearbyThreatThreshold = 0.32f;
        [SerializeField, Min(0.5f)] private float closeStalkerTriggerDistance = 5.8f;
        [SerializeField, Min(0.2f)] private float closeStalkerCueTowardEnemyDistance = 1.85f;
        [SerializeField, Min(0f)] private float closeStalkerCueSideJitter = 1.15f;
        [SerializeField, Min(0.5f)] private float closeStalkerMaxInterval = 6.6f;
        [SerializeField, Min(0.5f)] private float closeStalkerMinInterval = 2.95f;
        [SerializeField, Range(0f, 1f)] private float closeStalkerCueChance = 0.78f;
        [SerializeField] private Color closeStalkerCueColor = new(0.86f, 0.05f, 0.12f, 0.34f);
        [SerializeField, Min(0.1f)] private float closeStalkerCueRadius = 1.55f;
        [SerializeField, Min(0.1f)] private float closeStalkerCueDuration = 1.55f;
        [SerializeField, Range(1, 4)] private int closeStalkerCueRingCount = 2;
        [SerializeField, Min(0f)] private float closeStalkerCueRingInterval = 0.24f;
        [SerializeField] private int closeStalkerCueSortingOrder = 37;
        [SerializeField] private bool enableCloseStalkerCueAudio = true;
        [SerializeField, Range(0f, 1f)] private float closeStalkerCueAudioVolume = 0.22f;
        [SerializeField, Min(0.05f)] private float closeStalkerCueAudioDuration = 0.62f;
        [SerializeField, Range(25f, 180f)] private float closeStalkerCueAudioFrequency = 48f;
        [SerializeField, Range(0f, 1f)] private float closeStalkerCueAudioSpatialBlend = 0.72f;
        [SerializeField] private bool closeStalkerCueCameraImpulse = true;
        [SerializeField, Min(0f)] private float closeStalkerCueImpulseAmplitude = 0.045f;
        [SerializeField, Min(0.05f)] private float closeStalkerCueImpulseDuration = 0.16f;

        [Header("Threat Cue Budget")]
        [SerializeField] private bool enableThreatCueBudget = true;
        [SerializeField, Min(0.25f)] private float majorThreatCueCooldownSeconds = 3f;
        [SerializeField, Min(0.05f)] private float minorThreatCueCooldownSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float deniedThreatCueRetrySeconds = 0.45f;

        [Header("Escape Relief Reward")]
        [SerializeField] private bool enableEscapeReliefReward = true;
        [SerializeField, Min(0.1f)] private float minEscapeReliefChaseSeconds = 2.4f;
        [SerializeField, Min(0.5f)] private float escapeReliefRewardCooldownSeconds = 8.5f;
        [SerializeField, Min(0f)] private float escapeReliefStaminaRecover = 0.82f;
        [SerializeField, Min(0f)] private float escapeReliefPressureStaminaBonus = 0.38f;
        [SerializeField] private bool escapeReliefRevealFog = true;
        [SerializeField, Min(0.1f)] private float escapeReliefRevealRadius = 2.6f;
        [SerializeField, Min(0f)] private float escapeReliefRevealSoftnessBoost = 0.68f;
        [SerializeField] private Color escapeReliefPulseColor = new(0.22f, 1f, 0.78f, 0.42f);
        [SerializeField, Min(0.1f)] private float escapeReliefPulseRadius = 2.15f;
        [SerializeField, Min(0.1f)] private float escapeReliefPulseDuration = 1.42f;
        [SerializeField, Range(1, 4)] private int escapeReliefPulseRingCount = 2;
        [SerializeField, Min(0f)] private float escapeReliefPulseRingInterval = 0.28f;
        [SerializeField] private int escapeReliefPulseSortingOrder = 38;
        [SerializeField] private bool enableEscapeReliefAudio = true;
        [SerializeField, Range(0f, 1f)] private float escapeReliefAudioVolume = 0.2f;
        [SerializeField, Min(0.05f)] private float escapeReliefAudioDuration = 0.82f;
        [SerializeField, Range(40f, 260f)] private float escapeReliefAudioFrequency = 118f;
        [SerializeField, Range(0f, 1f)] private float escapeReliefAudioSpatialBlend = 0.2f;
        [SerializeField] private bool enableEscapeReliefCalmWindow = true;
        [SerializeField, Min(0.1f)] private float escapeReliefCalmSeconds = 2.8f;
        [SerializeField, Range(0f, 0.75f)] private float escapeReliefPressureDip = 0.28f;
        [SerializeField, Range(0f, 1f)] private float escapeReliefCueSuppression = 0.86f;
        [SerializeField] private bool enableEscapeReliefEnemyTrail = true;
        [SerializeField, Min(0.2f)] private float escapeReliefTrailMaxDistance = 8.5f;
        [SerializeField, Min(0.1f)] private float escapeReliefTrailDuration = 1.65f;
        [SerializeField, Min(0.01f)] private float escapeReliefTrailWidth = 0.07f;
        [SerializeField, Min(0f)] private float escapeReliefTrailWaver = 0.42f;
        [SerializeField] private Color escapeReliefTrailColor = new(0.18f, 1f, 0.78f, 0.38f);
        [SerializeField] private int escapeReliefTrailSortingOrder = 37;
        [SerializeField] private bool enableEscapeReliefQuietBreath = true;
        [SerializeField, Min(0.1f)] private float escapeReliefQuietBreathSeconds = 2.6f;
        [SerializeField, Range(0.2f, 1f)] private float escapeReliefFootstepNoiseMultiplier = 0.46f;
        [SerializeField, Range(0.2f, 1f)] private float escapeReliefSprintNoiseMultiplier = 0.62f;
        [SerializeField] private bool enableEscapeReliefBreathSnap = true;
        [SerializeField, Min(0.05f)] private float escapeReliefBreathSnapStrainSeconds = 0.48f;
        [SerializeField, Min(0.5f)] private float escapeReliefBreathSnapCooldownSeconds = 4.25f;
        [SerializeField, Range(0f, 1f)] private float escapeReliefBreathSnapCalmPenalty = 0.42f;
        [SerializeField] private bool escapeReliefBreathSnapEmitsNoise = true;
        [SerializeField, Min(0f)] private float escapeReliefBreathSnapNoiseLoudness = 0.54f;
        [SerializeField, Min(0f)] private float escapeReliefBreathSnapNoiseRadius = 4.3f;
        [SerializeField] private Color escapeReliefBreathSnapColor = new(1f, 0.42f, 0.2f, 0.38f);
        [SerializeField, Min(0.1f)] private float escapeReliefBreathSnapRadius = 1.45f;
        [SerializeField, Min(0.1f)] private float escapeReliefBreathSnapDuration = 0.78f;
        [SerializeField] private int escapeReliefBreathSnapSortingOrder = 40;
        [SerializeField] private bool escapeReliefBreathSnapCameraImpulse = true;
        [SerializeField, Min(0f)] private float escapeReliefBreathSnapImpulseAmplitude = 0.068f;
        [SerializeField, Min(0.05f)] private float escapeReliefBreathSnapImpulseDuration = 0.14f;
        [SerializeField] private bool enableEscapeReliefBreathSnapAudio = true;
        [SerializeField, Range(0f, 1f)] private float escapeReliefBreathSnapAudioVolume = 0.24f;
        [SerializeField, Min(0.05f)] private float escapeReliefBreathSnapAudioDuration = 0.52f;
        [SerializeField, Range(80f, 360f)] private float escapeReliefBreathSnapAudioFrequency = 172f;
        [SerializeField, Range(0f, 1f)] private float escapeReliefBreathSnapAudioSpatialBlend = 0.28f;
        [SerializeField] private bool enableEscapeReliefObjectiveWhisper = true;
        [SerializeField, Min(0.2f)] private float escapeReliefObjectiveWhisperMaxDistance = 12f;
        [SerializeField, Min(0.1f)] private float escapeReliefObjectiveWhisperDuration = 1.35f;
        [SerializeField, Min(0.01f)] private float escapeReliefObjectiveWhisperWidth = 0.055f;
        [SerializeField] private Color escapeReliefBreadcrumbWhisperColor = new(1f, 0.82f, 0.24f, 0.46f);
        [SerializeField] private Color escapeReliefExitWhisperColor = new(0.28f, 1f, 0.56f, 0.52f);
        [SerializeField] private int escapeReliefObjectiveWhisperSortingOrder = 39;

        [Header("Rhythm Release Relief")]
        [SerializeField] private bool enableRhythmReleaseRelief = true;
        [SerializeField, Min(0.5f)] private float rhythmReleaseReliefCooldownSeconds = 2.5f;
        [SerializeField, Min(0f)] private float rhythmReleaseStaminaRecover = 0.28f;
        [SerializeField, Range(0f, 1f)] private float rhythmReleaseReliefIntensityFloor = 0.42f;
        [SerializeField, Range(0f, 1f)] private float rhythmReleaseReliefPressureBonus = 0.24f;
        [SerializeField, Range(0.2f, 1.5f)] private float rhythmReleaseFogRadiusMultiplier = 0.85f;
        [SerializeField, Range(0.2f, 1.5f)] private float rhythmReleaseQuietBreathMultiplier = 1.08f;
        [SerializeField, Range(0.5f, 2f)] private float rhythmReleaseObjectiveWhisperDistanceMultiplier = 1.22f;
        [SerializeField, Range(0.5f, 2f)] private float rhythmReleaseObjectiveWhisperDurationMultiplier = 1.34f;
        [SerializeField, Range(0.5f, 1.8f)] private float rhythmReleaseObjectiveWhisperWidthMultiplier = 1.14f;
        [SerializeField] private bool enableRhythmReleaseCameraExhale = true;
        [SerializeField, Min(0.1f)] private float rhythmReleaseCameraExhaleSeconds = 2.2f;
        [SerializeField, Min(0f)] private float rhythmReleaseCameraExhaleZoomBonus = 0.22f;
        [SerializeField, Range(0.5f, 1.2f)] private float rhythmReleaseCameraExhaleLookAheadMultiplier = 0.76f;
        [SerializeField, Range(0.5f, 1.3f)] private float rhythmReleaseCameraExhaleSmoothMultiplier = 0.88f;

        [Header("Flashlight Dread")]
        [SerializeField] private bool enableFlashlightDread = true;
        [SerializeField, Range(0f, 1f)] private float flashlightDreadPressureThreshold = 0.36f;
        [SerializeField, Range(0.35f, 1f)] private float minDreadFlashlightRangeMultiplier = 0.78f;
        [SerializeField, Range(0.5f, 1f)] private float minDreadFlashlightAngleMultiplier = 0.86f;
        [SerializeField, Range(0f, 0.3f)] private float flashlightFlickerStrength = 0.11f;
        [SerializeField, Min(0.05f)] private float flashlightFlickerSpeed = 7.2f;
        [SerializeField, Range(0f, 0.2f)] private float flashlightBreathStrength = 0.06f;
        [SerializeField, Min(0.05f)] private float flashlightBreathSpeed = 0.54f;

        [Header("Fog Tuning")]
        [SerializeField, Range(0.45f, 2.2f)] private float minFogRevealRadiusMultiplier = 0.95f;
        [SerializeField, Range(0.45f, 2.2f)] private float maxFogRevealRadiusMultiplier = 1.22f;
        [SerializeField, Range(0.45f, 2.2f)] private float minFogSoftnessMultiplier = 0.88f;
        [SerializeField, Range(0.45f, 2.2f)] private float maxFogSoftnessMultiplier = 1.2f;
        [SerializeField, Range(0.45f, 2.4f)] private float minFogFlashlightRangeMultiplier = 0.92f;
        [SerializeField, Range(0.45f, 2.4f)] private float maxFogFlashlightRangeMultiplier = 1.26f;
        [SerializeField, Range(0.2f, 2.4f)] private float minFogRefogMultiplier = 0.92f;
        [SerializeField, Range(0.2f, 2.4f)] private float maxFogRefogMultiplier = 1.38f;

        [Header("Enemy Perception Tuning")]
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemyVisionMultiplier = 0.95f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemyVisionMultiplier = 1.18f;
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemyHearingMultiplier = 0.94f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemyHearingMultiplier = 1.18f;
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemySuspicionGainMultiplier = 0.9f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemySuspicionGainMultiplier = 1.14f;

        [Header("Chase Readability Tuning")]
        [SerializeField, Range(0.55f, 1.8f)] private float minTransitionDurationMultiplier = 1.22f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxTransitionDurationMultiplier = 0.94f;
        [SerializeField, Range(0.6f, 1.9f)] private float minTransitionPulseSpeedMultiplier = 0.84f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxTransitionPulseSpeedMultiplier = 1.06f;
        [SerializeField, Range(0.6f, 1.9f)] private float minTransitionFlashStrengthMultiplier = 0.9f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxTransitionFlashStrengthMultiplier = 1.32f;
        [SerializeField, Range(0.55f, 1.8f)] private float minDisengageCueDurationMultiplier = 1.12f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxDisengageCueDurationMultiplier = 0.96f;
        [SerializeField, Range(0.55f, 1.8f)] private float minDisengageGraceMultiplier = 1.12f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxDisengageGraceMultiplier = 0.94f;
        [SerializeField, Range(0.6f, 1.9f)] private float minChaseBlinkSpeedMultiplier = 0.88f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxChaseBlinkSpeedMultiplier = 1.08f;

        [Header("Playtest Envelope")]
        [SerializeField] private bool useStageReadabilityEnvelope = true;
        [SerializeField, Min(1)] private int envelopeStageEarly = 3;
        [SerializeField, Min(2)] private int envelopeStageMid = 5;
        [SerializeField, Min(3)] private int envelopeStageLate = 7;
        [SerializeField, Range(0f, 1f)] private float earlyReadabilityCap = 0.72f;
        [SerializeField, Range(0f, 1f)] private float midReadabilityCap = 0.86f;
        [SerializeField, Range(0f, 1f)] private float lateReadabilityCap = 1f;
        [SerializeField, Range(0.4f, 1.4f)] private float earlyChaseAggressionScale = 0.72f;
        [SerializeField, Range(0.4f, 1.4f)] private float midChaseAggressionScale = 0.84f;
        [SerializeField, Range(0.4f, 1.4f)] private float lateChaseAggressionScale = 0.94f;

        private float updateElapsed;
        private float currentNearbyThreat;
        private float currentStagePressure;
        private float currentReadabilityPressure;
        private float lastLoggedPressure = -1f;
        private float baseCameraOrthoSize;
        private bool hasBaseCameraOrthoSize;
        private Color baseCameraBackgroundColor;
        private bool hasBaseCameraBackgroundColor;
        private int lastEnemySampleCount;
        private string lastPresetLabel = "Unknown";
        private float previousReadabilityPressure;
        private bool hasPreviousReadabilityPressure;
        private float nextAllowedPulseTime;
        private float nextPressureWaveEventTime;
        private float nextDreadBeatTime;
        private float nextPhantomCueTime;
        private float nextCloseStalkerCueTime;
        private float nextMajorThreatCueTime;
        private float nextMinorThreatCueTime;
        private int suppressedThreatCueCount;
        private float nextReferenceResolveTime;
        private AudioSource phantomCueAudioSource;
        private AudioClip phantomCueClip;
        private float phantomCueClipDuration;
        private float phantomCueClipFrequency;
        private AudioSource closeStalkerCueAudioSource;
        private AudioClip closeStalkerCueClip;
        private float closeStalkerCueClipDuration;
        private float closeStalkerCueClipFrequency;
        private AudioSource escapeReliefAudioSource;
        private AudioClip escapeReliefClip;
        private float escapeReliefClipDuration;
        private float escapeReliefClipFrequency;
        private AudioSource escapeReliefBreathSnapAudioSource;
        private AudioClip escapeReliefBreathSnapClip;
        private float escapeReliefBreathSnapClipDuration;
        private float escapeReliefBreathSnapClipFrequency;
        private Material escapeReliefTrailMaterial;
        private PlayerDummyController playerController;
        private float currentFlashlightDread;
        private float currentCloseThreatDistance = float.PositiveInfinity;
        private int activeChaseEventCount;
        private float activeChaseStartRealtime = -1f;
        private float nextEscapeReliefRewardRealtime;
        private float nextRhythmReleaseReliefRealtime;
        private float rhythmReleaseCameraExhaleUntilRealtime;
        private float rhythmReleaseCameraExhaleStartedRealtime;
        private float rhythmReleaseCameraExhaleDuration;
        private float rhythmReleaseCameraExhaleIntensity;
        private float escapeReliefCalmStartedRealtime;
        private float escapeReliefCalmUntilRealtime;
        private float escapeReliefCalmDuration;
        private float quietBreathStrainElapsed;
        private float nextEscapeReliefBreathSnapRealtime;
        private float currentThreatTunnelVision;
        private float currentCameraTargetOrthoSize;
        private readonly List<EnemyController> cachedEnemies = new(16);

        public float CurrentNearbyThreat => currentNearbyThreat;
        public float CurrentStagePressure => currentStagePressure;
        public float CurrentReadabilityPressure => currentReadabilityPressure;
        public int LastEnemySampleCount => lastEnemySampleCount;
        public string LastPresetLabel => string.IsNullOrWhiteSpace(lastPresetLabel) ? "Unknown" : lastPresetLabel;
        public float BaseCameraOrthoSize => baseCameraOrthoSize;
        public bool HasBaseCameraOrthoSize => hasBaseCameraOrthoSize;
        public bool RuntimeArtGradeEnabled => enableRuntimeArtGrade;
        public float ThreatPulseCooldownRemaining => Mathf.Max(0f, nextAllowedPulseTime - Time.time);
        public float CurrentFlashlightDread => currentFlashlightDread;
        public float CurrentCloseThreatDistance => currentCloseThreatDistance;
        public float CurrentThreatTunnelVision => currentThreatTunnelVision;
        public float CurrentCameraTargetOrthoSize => currentCameraTargetOrthoSize;
        public float EscapeReliefCooldownRemaining => Mathf.Max(0f, nextEscapeReliefRewardRealtime - Time.realtimeSinceStartup);
        public float RhythmReleaseReliefCooldownRemaining => Mathf.Max(0f, nextRhythmReleaseReliefRealtime - Time.realtimeSinceStartup);
        public float CurrentEscapeReliefCalm => EvaluateEscapeReliefCalm01();
        public float CurrentQuietBreathStrain => Mathf.Clamp01(quietBreathStrainElapsed / Mathf.Max(0.05f, escapeReliefBreathSnapStrainSeconds));
        public float BreathSnapCooldownRemaining => Mathf.Max(0f, nextEscapeReliefBreathSnapRealtime - Time.realtimeSinceStartup);
        public float MajorThreatCueCooldownRemaining => Mathf.Max(0f, nextMajorThreatCueTime - Time.time);
        public float MinorThreatCueCooldownRemaining => Mathf.Max(0f, nextMinorThreatCueTime - Time.time);
        public int SuppressedThreatCueCount => Mathf.Max(0, suppressedThreatCueCount);

        private enum ThreatCueBudgetTier
        {
            Minor,
            Major
        }

        private void OnEnable()
        {
            ResolveReferences(force: true);
            SubscribeMap();
            RuntimeEventBus.EventRaised -= HandleRuntimeEventRaised;
            RuntimeEventBus.EventRaised += HandleRuntimeEventRaised;
        }

        private void OnDisable()
        {
            RuntimeEventBus.EventRaised -= HandleRuntimeEventRaised;
            UnsubscribeMap();
            ResetAppliedTuning();
        }

        private void Start()
        {
            ResolveReferences(force: true);
            CaptureBaseCameraOrtho();

            if (applyOnStart)
            {
                ApplyNow(0.12f);
            }
        }

        private void Update()
        {
            updateElapsed += Time.deltaTime;
            if (updateElapsed < updateInterval)
            {
                return;
            }

            float dt = updateElapsed;
            updateElapsed = 0f;
            ApplyNow(dt);
        }

        public void SetReferencesForEditor(
            Transform playerTarget,
            PlayerVisibilitySource playerVisibility,
            Camera cameraRef,
            CameraFollow2D follow,
            FogOfWarSystem fog,
            StagePressureDirector pressure,
            MapTuningDebugController tuning,
            MapSystem targetMap)
        {
            if (mapSystem != targetMap)
            {
                UnsubscribeMap();
                mapSystem = targetMap;
                SubscribeMap();
            }

            player = playerTarget;
            visibilitySource = playerVisibility;
            targetCamera = cameraRef;
            cameraFollow = follow;
            fogOfWar = fog;
            stagePressureDirector = pressure;
            mapTuning = tuning;

            CaptureBaseCameraOrtho();
        }

        public void ApplyNowForEditor()
        {
            ApplyNow(Mathf.Max(0.08f, updateInterval));
        }

        public void ResetTransientRuntimeStateForRuntime()
        {
            ResolveReferences(force: true);
            ResetAppliedTuning();
        }

        public void ApplySavedReadabilityStateForRuntime(
            float savedNearbyThreat01,
            float savedStagePressure01,
            float savedReadabilityPressure01,
            bool applyImmediately = true)
        {
            ResolveReferences(force: true);

            currentNearbyThreat = Mathf.Clamp01(savedNearbyThreat01);
            currentStagePressure = Mathf.Clamp01(savedStagePressure01);
            currentReadabilityPressure = Mathf.Clamp01(savedReadabilityPressure01);
            EvaluatePresetBias(out string presetLabel);
            lastPresetLabel = presetLabel;
            previousReadabilityPressure = currentReadabilityPressure;
            hasPreviousReadabilityPressure = true;
            updateElapsed = 0f;

            if (!applyImmediately)
            {
                return;
            }

            float dt = Mathf.Max(0.08f, updateInterval);
            RefreshEnemyCache();
            ApplyCameraTuning(currentReadabilityPressure, dt);
            ApplyFogTuning(currentReadabilityPressure);
            ApplyEnemyTuning(currentReadabilityPressure);
            ApplyFlashlightDreadTuning(currentReadabilityPressure);
            TryApplyDreadBeat(currentReadabilityPressure);
            TrySpawnPhantomCue(currentReadabilityPressure);
            TryApplyCloseStalkerCue(currentReadabilityPressure);
            TrackEscapeReliefQuietBreathStrain(dt);

            if (logPressureChanges)
            {
                lastLoggedPressure = currentReadabilityPressure;
            }
        }
        private void ApplyNow(float dt)
        {
            ResolveReferences();
            if (targetCamera == null)
            {
                return;
            }

            RefreshEnemyCache();
            currentNearbyThreat = EvaluateNearbyThreat(out int enemySampleCount);
            currentStagePressure = Mathf.Clamp01(stagePressureDirector != null ? stagePressureDirector.CurrentPressure01 : EvaluateFallbackStagePressure());

            float presetBias = EvaluatePresetBias(out string presetLabel);
            lastPresetLabel = presetLabel;

            float weightedNearby = currentNearbyThreat * Mathf.Clamp01(nearbyThreatWeight);
            float weightedStage = currentStagePressure * Mathf.Clamp01(stagePressureWeight);
            float totalWeight = Mathf.Max(0.001f, Mathf.Clamp01(nearbyThreatWeight) + Mathf.Clamp01(stagePressureWeight));
            float rawPressure = Mathf.Clamp01((weightedNearby + weightedStage) / totalWeight);
            rawPressure = Mathf.Clamp01(rawPressure * presetBias);
            rawPressure = ApplyStageReadabilityEnvelope(rawPressure);
            rawPressure = ApplyEscapeReliefCalmPressureDip(rawPressure);

            float smoothFactor = responseSmoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Mathf.Max(0.0001f, responseSmoothing) * Mathf.Max(0.0001f, dt));
            currentReadabilityPressure = Mathf.Lerp(currentReadabilityPressure, rawPressure, smoothFactor);
            TryApplyThreatPulse(currentReadabilityPressure);

            lastEnemySampleCount = enemySampleCount;
            ApplyCameraTuning(currentReadabilityPressure, dt);
            ApplyFogTuning(currentReadabilityPressure);
            ApplyEnemyTuning(currentReadabilityPressure);
            ApplyFlashlightDreadTuning(currentReadabilityPressure);
            TryApplyDreadBeat(currentReadabilityPressure);
            TrySpawnPhantomCue(currentReadabilityPressure);
            TryApplyCloseStalkerCue(currentReadabilityPressure);
            TrackEscapeReliefQuietBreathStrain(dt);

            if (logPressureChanges && Mathf.Abs(currentReadabilityPressure - lastLoggedPressure) >= 0.08f)
            {
                lastLoggedPressure = currentReadabilityPressure;
                Debug.Log(
                    $"ThreatReadability: pressure={currentReadabilityPressure:0.00}, nearby={currentNearbyThreat:0.00}, stage={currentStagePressure:0.00}, preset={lastPresetLabel}, enemies={lastEnemySampleCount}",
                    this);
            }
        }

        private void ApplyCameraTuning(float pressure, float dt)
        {
            if (!targetCamera.orthographic)
            {
                return;
            }

            CaptureBaseCameraOrtho();
            float baseSize = hasBaseCameraOrthoSize
                ? baseCameraOrthoSize
                : Mathf.Max(minimumCameraOrthoSize, targetCamera.orthographicSize);

            float closeDistanceFactor = float.IsInfinity(currentCloseThreatDistance)
                ? 0f
                : 1f - Mathf.Clamp01(currentCloseThreatDistance / Mathf.Max(0.1f, threatRange));
            float tunnelInput = Mathf.Max(currentNearbyThreat, closeDistanceFactor);
            float tunnelTarget = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    Mathf.Min(closeThreatTunnelStart, closeThreatTunnelFull),
                    Mathf.Max(closeThreatTunnelStart + 0.01f, closeThreatTunnelFull),
                    tunnelInput));
            float targetSize = Mathf.Clamp(
                baseSize
                + maxCameraZoomOut * Mathf.Clamp01(currentStagePressure) * Mathf.Lerp(1f, 1f - closeThreatSuppressZoomOut, tunnelTarget)
                - maxCloseThreatZoomIn * tunnelTarget,
                Mathf.Max(1f, minimumCameraOrthoSize),
                Mathf.Max(Mathf.Max(1f, minimumCameraOrthoSize), maximumCameraOrthoSize));
            float releaseExhale = EvaluateRhythmReleaseCameraExhale01();
            if (releaseExhale > 0f)
            {
                targetSize = Mathf.Clamp(
                    targetSize + rhythmReleaseCameraExhaleZoomBonus * releaseExhale,
                    Mathf.Max(1f, minimumCameraOrthoSize),
                    Mathf.Max(Mathf.Max(1f, minimumCameraOrthoSize), maximumCameraOrthoSize));
            }

            float zoomLerp = 1f - Mathf.Exp(-Mathf.Max(0.0001f, responseSmoothing) * Mathf.Max(0.0001f, dt));
            currentThreatTunnelVision = Mathf.Lerp(currentThreatTunnelVision, tunnelTarget, zoomLerp);
            currentCameraTargetOrthoSize = targetSize;
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, zoomLerp);

            if (cameraFollow != null)
            {
                float pressureLookAhead = Mathf.Clamp01(pressure * Mathf.Lerp(1f, 0.55f, currentThreatTunnelVision));
                float lookAheadMultiplier = Mathf.Lerp(minLookAheadMultiplier, maxLookAheadMultiplier, pressureLookAhead);
                float smoothMultiplier = Mathf.Lerp(minSmoothMultiplier, maxSmoothMultiplier, pressure);
                float lookAheadSmoothingMultiplier = Mathf.Lerp(minLookAheadSmoothingMultiplier, maxLookAheadSmoothingMultiplier, pressureLookAhead);
                if (releaseExhale > 0f)
                {
                    lookAheadMultiplier = Mathf.Lerp(lookAheadMultiplier, rhythmReleaseCameraExhaleLookAheadMultiplier, releaseExhale);
                    smoothMultiplier = Mathf.Lerp(smoothMultiplier, rhythmReleaseCameraExhaleSmoothMultiplier, releaseExhale);
                    lookAheadSmoothingMultiplier = Mathf.Lerp(lookAheadSmoothingMultiplier, 1f, releaseExhale);
                }

                cameraFollow.ApplyRuntimeTuningForEditor(lookAheadMultiplier, smoothMultiplier, lookAheadSmoothingMultiplier);
            }

            ApplyCameraArtGrade(pressure, dt);
        }

        private void ApplyFogTuning(float pressure)
        {
            if (fogOfWar == null)
            {
                return;
            }

            float revealRadiusMultiplier = Mathf.Lerp(minFogRevealRadiusMultiplier, maxFogRevealRadiusMultiplier, pressure);
            float revealSoftnessMultiplier = Mathf.Lerp(minFogSoftnessMultiplier, maxFogSoftnessMultiplier, pressure);
            float flashlightRangeMultiplier = Mathf.Lerp(minFogFlashlightRangeMultiplier, maxFogFlashlightRangeMultiplier, pressure);
            float refogMultiplier = Mathf.Lerp(minFogRefogMultiplier, maxFogRefogMultiplier, pressure);

            fogOfWar.ApplyRuntimeRevealTuningForEditor(
                revealRadiusMultiplier,
                revealSoftnessMultiplier,
                flashlightRangeMultiplier,
                refogMultiplier);

            if (enableRuntimeArtGrade)
            {
                Color fogTint = Color.Lerp(calmFogTint, dangerFogTint, pressure);
                float hiddenMul = Mathf.Lerp(calmFogHiddenAlphaMultiplier, dangerFogHiddenAlphaMultiplier, pressure);
                float visibleMul = Mathf.Lerp(calmFogVisibleAlphaMultiplier, dangerFogVisibleAlphaMultiplier, pressure);
                fogOfWar.ApplyRuntimeStyleTuningForEditor(fogTint, hiddenMul, visibleMul);
            }
            else
            {
                fogOfWar.ResetRuntimeStyleTuningForEditor();
            }
        }

        private void ApplyFlashlightDreadTuning(float pressure)
        {
            if (visibilitySource == null)
            {
                return;
            }

            if (!Application.isPlaying || !enableFlashlightDread || RegressionChecklistRunner.IsRegressionRunActive)
            {
                currentFlashlightDread = 0f;
                visibilitySource.ResetDreadRuntimeModifiersForEditor();
                return;
            }

            float dread = Mathf.InverseLerp(flashlightDreadPressureThreshold, 1f, pressure);
            currentFlashlightDread = Mathf.Clamp01(dread);
            if (currentFlashlightDread <= 0.001f)
            {
                visibilitySource.ResetDreadRuntimeModifiersForEditor();
                return;
            }

            float flickerNoise = Mathf.PerlinNoise(Time.time * flashlightFlickerSpeed, 18.31f);
            float flickerDip = Mathf.SmoothStep(0.72f, 1f, 1f - flickerNoise);
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * flashlightBreathSpeed * Mathf.PI * 2f);

            float baseRange = Mathf.Lerp(1f, minDreadFlashlightRangeMultiplier, currentFlashlightDread);
            float baseAngle = Mathf.Lerp(1f, minDreadFlashlightAngleMultiplier, currentFlashlightDread);
            float rangeInstability = (flickerDip * flashlightFlickerStrength + breath * flashlightBreathStrength) * currentFlashlightDread;
            float angleInstability = flickerDip * flashlightFlickerStrength * 0.45f * currentFlashlightDread;

            float rangeMultiplier = Mathf.Clamp(baseRange * (1f - rangeInstability), 0.35f, 1.25f);
            float angleMultiplier = Mathf.Clamp(baseAngle * (1f - angleInstability), 0.5f, 1.25f);
            visibilitySource.ApplyDreadRuntimeModifiersForEditor(rangeMultiplier, angleMultiplier);
        }

        private void ApplyEnemyTuning(float pressure)
        {
            float visionMultiplier = Mathf.Lerp(minEnemyVisionMultiplier, maxEnemyVisionMultiplier, pressure);
            float hearingMultiplier = Mathf.Lerp(minEnemyHearingMultiplier, maxEnemyHearingMultiplier, pressure);
            float suspicionGainMultiplier = Mathf.Lerp(minEnemySuspicionGainMultiplier, maxEnemySuspicionGainMultiplier, pressure);

            float chasePressure = Mathf.Clamp01(pressure * EvaluateStageChaseAggressionScale());
            float transitionDurationMultiplier = Mathf.Lerp(minTransitionDurationMultiplier, maxTransitionDurationMultiplier, chasePressure);
            float transitionPulseMultiplier = Mathf.Lerp(minTransitionPulseSpeedMultiplier, maxTransitionPulseSpeedMultiplier, chasePressure);
            float transitionFlashMultiplier = Mathf.Lerp(minTransitionFlashStrengthMultiplier, maxTransitionFlashStrengthMultiplier, chasePressure);
            float disengageCueDurationMultiplier = Mathf.Lerp(minDisengageCueDurationMultiplier, maxDisengageCueDurationMultiplier, chasePressure);
            float disengageGraceMultiplier = Mathf.Lerp(minDisengageGraceMultiplier, maxDisengageGraceMultiplier, chasePressure);
            float chaseBlinkSpeedMultiplier = Mathf.Lerp(minChaseBlinkSpeedMultiplier, maxChaseBlinkSpeedMultiplier, chasePressure);

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.ApplyRuntimePerceptionTuningForEditor(visionMultiplier, hearingMultiplier, suspicionGainMultiplier);
                enemy.ApplyRuntimeChaseReadabilityTuningForEditor(
                    transitionDurationMultiplier,
                    transitionPulseMultiplier,
                    transitionFlashMultiplier,
                    disengageCueDurationMultiplier,
                    disengageGraceMultiplier,
                    chaseBlinkSpeedMultiplier);
            }
        }

        private void ApplyCameraArtGrade(float pressure, float dt)
        {
            if (targetCamera == null)
            {
                return;
            }

            if (!hasBaseCameraBackgroundColor)
            {
                baseCameraBackgroundColor = targetCamera.backgroundColor;
                hasBaseCameraBackgroundColor = true;
            }

            Color from = enableRuntimeArtGrade ? calmCameraBackgroundColor : baseCameraBackgroundColor;
            Color to = enableRuntimeArtGrade ? dangerCameraBackgroundColor : baseCameraBackgroundColor;
            Color targetColor = Color.Lerp(from, to, pressure);

            if (enableRuntimeArtGrade && enableDreadBreathTint)
            {
                float dread = Mathf.InverseLerp(dreadBeatPressureThreshold, 1f, pressure);
                if (dread > 0.001f)
                {
                    float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * dreadBreathSpeed * Mathf.PI * 2f);
                    targetColor = Color.Lerp(targetColor, dreadBreathTint, dread * dreadBreathTintStrength * breath);
                }
            }

            float lerp = 1f - Mathf.Exp(-Mathf.Max(0.1f, cameraBackgroundLerpSpeed) * Mathf.Max(0.0001f, dt));
            targetCamera.backgroundColor = Color.Lerp(targetCamera.backgroundColor, targetColor, lerp);
        }

        private void TryApplyDreadBeat(float pressure)
        {
            if (!Application.isPlaying || !enableDreadBeat || cameraFollow == null || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (ShouldSuppressDreadCueDuringRelief())
            {
                nextDreadBeatTime = Mathf.Max(nextDreadBeatTime, Time.time + 0.45f);
                return;
            }

            float dread = Mathf.InverseLerp(dreadBeatPressureThreshold, 1f, pressure);
            if (dread <= 0.001f)
            {
                nextDreadBeatTime = Mathf.Max(nextDreadBeatTime, Time.time + 0.35f);
                return;
            }

            if (Time.time < nextDreadBeatTime)
            {
                return;
            }

            if (!TryReserveThreatCueBudget(ThreatCueBudgetTier.Minor))
            {
                nextDreadBeatTime = Time.time + Mathf.Max(0.05f, deniedThreatCueRetrySeconds);
                return;
            }

            float amplitude = Mathf.Lerp(minDreadBeatImpulse, maxDreadBeatImpulse, dread);
            if (amplitude > 0f)
            {
                cameraFollow.AddImpulse(amplitude, Mathf.Max(0.05f, dreadBeatImpulseDuration));
            }

            float interval = Mathf.Lerp(maxDreadBeatInterval, minDreadBeatInterval, dread);
            float jitter = Random.Range(-dreadBeatIntervalJitter, dreadBeatIntervalJitter);
            nextDreadBeatTime = Time.time + Mathf.Max(0.25f, interval + jitter);
        }

        private void TrySpawnPhantomCue(float pressure)
        {
            if (!Application.isPlaying || !enablePhantomCues || player == null || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (ShouldSuppressDreadCueDuringRelief())
            {
                nextPhantomCueTime = Mathf.Max(nextPhantomCueTime, Time.time + 0.85f);
                return;
            }

            float dread = Mathf.InverseLerp(phantomCuePressureThreshold, 1f, pressure);
            if (dread <= 0.001f)
            {
                nextPhantomCueTime = Mathf.Max(nextPhantomCueTime, Time.time + 0.75f);
                return;
            }

            if (Time.time < nextPhantomCueTime)
            {
                return;
            }

            if (!TryReserveThreatCueBudget(ThreatCueBudgetTier.Major))
            {
                nextPhantomCueTime = Time.time + Mathf.Max(0.05f, deniedThreatCueRetrySeconds);
                return;
            }

            ScheduleNextPhantomCue(dread);
            if (!TryPickPhantomCuePosition(out Vector2 cuePosition))
            {
                return;
            }

            float intensity = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(dread));
            SpawnPhantomEchoVisual(cuePosition, intensity);
            PlayPhantomCueAudio(cuePosition, intensity);

            if (phantomCueEmitsNoise && NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    cuePosition,
                    phantomCueNoiseLoudness * intensity,
                    phantomCueNoiseRadius * intensity,
                    NoiseKind.Echo,
                    gameObject);
            }
        }

        private void ScheduleNextPhantomCue(float dread)
        {
            float interval = Mathf.Lerp(maxPhantomCueInterval, minPhantomCueInterval, Mathf.Clamp01(dread));
            float jitter = Random.Range(-phantomCueIntervalJitter, phantomCueIntervalJitter);
            nextPhantomCueTime = Time.time + Mathf.Max(0.75f, interval + jitter);
        }

        private bool TryPickPhantomCuePosition(out Vector2 cuePosition)
        {
            cuePosition = Vector2.zero;
            if (player == null)
            {
                return false;
            }

            Vector2 origin = player.position;
            Vector2 bestPosition = origin;
            float bestFogAlpha = -1f;
            int attempts = fogOfWar == null ? 1 : 8;
            float minDistance = Mathf.Max(0.5f, phantomCueMinDistance);
            float maxDistance = Mathf.Max(minDistance + 0.1f, phantomCueMaxDistance);

            for (int i = 0; i < attempts; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = Random.value < 0.5f ? Vector2.left : Vector2.right;
                }

                direction.Normalize();
                Vector2 candidate = origin + direction * Random.Range(minDistance, maxDistance);
                float fogAlpha = fogOfWar != null ? fogOfWar.SampleFogAlpha01AtWorldPosition(candidate) : 1f;
                if (fogAlpha > bestFogAlpha)
                {
                    bestFogAlpha = fogAlpha;
                    bestPosition = candidate;
                }

                if (fogOfWar == null || fogAlpha >= phantomCueHiddenFogThreshold)
                {
                    cuePosition = candidate;
                    return true;
                }
            }

            cuePosition = bestPosition;
            return true;
        }

        private void SpawnPhantomEchoVisual(Vector2 position, float intensity)
        {
            GameObject visualObject = new($"PhantomEcho_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/PhantomCues");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = phantomCueColor;
            color.a *= Mathf.Clamp01(intensity);
            visual.Configure(
                Mathf.Max(0.35f, phantomCueRadius * Mathf.Lerp(0.82f, 1.15f, Mathf.Clamp01(intensity))),
                color,
                phantomCueDuration,
                phantomCueRingCount,
                phantomCueRingInterval,
                phantomCueSortingOrder);
        }

        private void PlayPhantomCueAudio(Vector2 position, float intensity)
        {
            if (!enablePhantomCueAudio || phantomCueAudioVolume <= 0f)
            {
                return;
            }

            AudioSource source = EnsurePhantomCueAudioSource();
            AudioClip clip = EnsurePhantomCueClip();
            if (source == null || clip == null)
            {
                return;
            }

            source.transform.position = new Vector3(position.x, position.y, 0f);
            source.spatialBlend = phantomCueAudioSpatialBlend;
            source.PlayOneShot(clip, phantomCueAudioVolume * Mathf.Clamp01(intensity));
        }

        private AudioSource EnsurePhantomCueAudioSource()
        {
            if (phantomCueAudioSource != null)
            {
                return phantomCueAudioSource;
            }

            GameObject sourceObject = new("PhantomCueAudio");
            Transform audioRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/AudioEmitters");
            if (audioRoot != null)
            {
                sourceObject.transform.SetParent(audioRoot, false);
            }

            phantomCueAudioSource = sourceObject.AddComponent<AudioSource>();
            phantomCueAudioSource.playOnAwake = false;
            phantomCueAudioSource.loop = false;
            phantomCueAudioSource.volume = 1f;
            phantomCueAudioSource.spatialBlend = phantomCueAudioSpatialBlend;
            phantomCueAudioSource.minDistance = 2f;
            phantomCueAudioSource.maxDistance = 18f;
            phantomCueAudioSource.rolloffMode = AudioRolloffMode.Linear;
            return phantomCueAudioSource;
        }

        private AudioClip EnsurePhantomCueClip()
        {
            float duration = Mathf.Max(0.05f, phantomCueAudioDuration);
            float frequency = Mathf.Max(1f, phantomCueAudioFrequency);
            if (phantomCueClip != null
                && Mathf.Approximately(phantomCueClipDuration, duration)
                && Mathf.Approximately(phantomCueClipFrequency, frequency))
            {
                return phantomCueClip;
            }

            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = Mathf.Clamp01(t / duration);
                float envelope = Mathf.Pow(Mathf.Sin(normalized * Mathf.PI), 1.35f);
                float wobble = Mathf.Sin(t * Mathf.PI * 2f * frequency * 0.23f) * 0.08f;
                float low = Mathf.Sin(t * Mathf.PI * 2f * (frequency + wobble));
                float upper = Mathf.Sin(t * Mathf.PI * 2f * frequency * 1.72f + 1.1f);
                float hiss = Mathf.PerlinNoise(t * 16.7f, 0.37f) - 0.5f;
                samples[i] = (low * 0.64f + upper * 0.22f + hiss * 0.16f) * envelope * 0.42f;
            }

            phantomCueClip = AudioClip.Create("PhantomDreadCue", sampleCount, 1, sampleRate, false);
            phantomCueClip.SetData(samples, 0);
            phantomCueClipDuration = duration;
            phantomCueClipFrequency = frequency;
            return phantomCueClip;
        }

        private void TryApplyCloseStalkerCue(float pressure)
        {
            if (!Application.isPlaying || !enableCloseStalkerCues || player == null)
            {
                currentCloseThreatDistance = float.PositiveInfinity;
                return;
            }

            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (ShouldSuppressDreadCueDuringRelief())
            {
                currentCloseThreatDistance = float.PositiveInfinity;
                nextCloseStalkerCueTime = Mathf.Max(nextCloseStalkerCueTime, Time.time + 0.7f);
                return;
            }

            float dread = Mathf.InverseLerp(closeStalkerPressureThreshold, 1f, pressure);
            if (dread <= 0.001f || currentNearbyThreat < closeStalkerNearbyThreatThreshold)
            {
                currentCloseThreatDistance = float.PositiveInfinity;
                nextCloseStalkerCueTime = Mathf.Max(nextCloseStalkerCueTime, Time.time + 0.65f);
                return;
            }

            if (!TryFindCloseStalkerThreat(out EnemyController enemy, out float distance, out float stateWeight))
            {
                currentCloseThreatDistance = float.PositiveInfinity;
                nextCloseStalkerCueTime = Mathf.Max(nextCloseStalkerCueTime, Time.time + 0.65f);
                return;
            }

            currentCloseThreatDistance = distance;
            float distanceTension = 1f - Mathf.Clamp01(distance / Mathf.Max(0.5f, closeStalkerTriggerDistance));
            float intensity = Mathf.Clamp01(dread * 0.55f + distanceTension * 0.55f + stateWeight * 0.18f);

            if (Time.time < nextCloseStalkerCueTime)
            {
                return;
            }

            if (Random.value > Mathf.Clamp01(closeStalkerCueChance + intensity * 0.12f))
            {
                ScheduleNextCloseStalkerCue(intensity);
                return;
            }

            if (!TryReserveThreatCueBudget(ThreatCueBudgetTier.Major))
            {
                nextCloseStalkerCueTime = Time.time + Mathf.Max(0.05f, deniedThreatCueRetrySeconds);
                return;
            }

            ScheduleNextCloseStalkerCue(intensity);
            Vector2 cuePosition = PickCloseStalkerCuePosition(enemy, distance, intensity);
            SpawnCloseStalkerCueVisual(cuePosition, intensity);
            PlayCloseStalkerCueAudio(cuePosition, intensity);

            if (closeStalkerCueCameraImpulse && cameraFollow != null && closeStalkerCueImpulseAmplitude > 0f)
            {
                cameraFollow.AddImpulse(
                    closeStalkerCueImpulseAmplitude * Mathf.Lerp(0.72f, 1.35f, intensity),
                    Mathf.Max(0.05f, closeStalkerCueImpulseDuration));
            }
        }

        private void ScheduleNextCloseStalkerCue(float intensity)
        {
            float maxInterval = Mathf.Max(closeStalkerMinInterval, closeStalkerMaxInterval);
            float minInterval = Mathf.Max(0.5f, closeStalkerMinInterval);
            float interval = Mathf.Lerp(maxInterval, minInterval, Mathf.Clamp01(intensity));
            nextCloseStalkerCueTime = Time.time + Mathf.Max(0.5f, interval * Random.Range(0.82f, 1.18f));
        }

        private bool TryFindCloseStalkerThreat(out EnemyController selectedEnemy, out float selectedDistance, out float selectedStateWeight)
        {
            selectedEnemy = null;
            selectedDistance = float.PositiveInfinity;
            selectedStateWeight = 0f;
            if (player == null || cachedEnemies.Count <= 0)
            {
                return false;
            }

            Vector2 playerPosition = player.position;
            float triggerDistance = Mathf.Max(0.5f, closeStalkerTriggerDistance);
            float bestScore = 0f;

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || enemy.CurrentState == EnemyStateId.Stunned)
                {
                    continue;
                }

                float distance = Vector2.Distance(playerPosition, enemy.transform.position);
                if (distance > triggerDistance)
                {
                    continue;
                }

                float stateWeight = EvaluateStateWeight(enemy.CurrentState);
                float distanceFactor = 1f - Mathf.Clamp01(distance / triggerDistance);
                float suspicionFactor = Mathf.Clamp01(enemy.Suspicion) * suspicionWeight;
                float score = distanceFactor * Mathf.Lerp(0.42f, 1f, stateWeight) + suspicionFactor * 0.36f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                selectedEnemy = enemy;
                selectedDistance = distance;
                selectedStateWeight = stateWeight;
            }

            return selectedEnemy != null;
        }

        private Vector2 PickCloseStalkerCuePosition(EnemyController enemy, float distance, float intensity)
        {
            Vector2 playerPosition = player != null ? player.position : Vector2.zero;
            Vector2 enemyPosition = enemy != null ? (Vector2)enemy.transform.position : playerPosition;
            Vector2 towardEnemy = enemyPosition - playerPosition;
            if (towardEnemy.sqrMagnitude <= 0.001f)
            {
                towardEnemy = Random.insideUnitCircle;
            }

            if (towardEnemy.sqrMagnitude <= 0.001f)
            {
                towardEnemy = Vector2.right;
            }

            towardEnemy.Normalize();
            Vector2 side = new(-towardEnemy.y, towardEnemy.x);
            float forwardDistance = Mathf.Min(
                Mathf.Max(0.2f, distance * 0.72f),
                closeStalkerCueTowardEnemyDistance * Mathf.Lerp(0.85f, 1.24f, Mathf.Clamp01(intensity)));
            float sideOffset = Random.Range(-closeStalkerCueSideJitter, closeStalkerCueSideJitter) * Mathf.Lerp(0.55f, 1f, intensity);
            return playerPosition + towardEnemy * forwardDistance + side * sideOffset;
        }

        private void SpawnCloseStalkerCueVisual(Vector2 position, float intensity)
        {
            GameObject visualObject = new($"CloseStalkerCue_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/CloseStalkerCues");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = closeStalkerCueColor;
            color.a *= Mathf.Lerp(0.72f, 1.16f, Mathf.Clamp01(intensity));
            visual.Configure(
                Mathf.Max(0.25f, closeStalkerCueRadius * Mathf.Lerp(0.82f, 1.28f, Mathf.Clamp01(intensity))),
                color,
                Mathf.Max(0.1f, closeStalkerCueDuration),
                Mathf.Clamp(closeStalkerCueRingCount, 1, 4),
                Mathf.Max(0f, closeStalkerCueRingInterval),
                closeStalkerCueSortingOrder);
        }

        private void PlayCloseStalkerCueAudio(Vector2 position, float intensity)
        {
            if (!enableCloseStalkerCueAudio || closeStalkerCueAudioVolume <= 0f)
            {
                return;
            }

            AudioSource source = EnsureCloseStalkerCueAudioSource();
            AudioClip clip = EnsureCloseStalkerCueClip();
            if (source == null || clip == null)
            {
                return;
            }

            source.transform.position = new Vector3(position.x, position.y, 0f);
            source.spatialBlend = closeStalkerCueAudioSpatialBlend;
            source.pitch = Random.Range(0.86f, 1.08f);
            source.PlayOneShot(clip, closeStalkerCueAudioVolume * Mathf.Clamp01(intensity));
        }

        private AudioSource EnsureCloseStalkerCueAudioSource()
        {
            if (closeStalkerCueAudioSource != null)
            {
                return closeStalkerCueAudioSource;
            }

            GameObject sourceObject = new("CloseStalkerCueAudio");
            Transform audioRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/AudioEmitters");
            if (audioRoot != null)
            {
                sourceObject.transform.SetParent(audioRoot, false);
            }

            closeStalkerCueAudioSource = sourceObject.AddComponent<AudioSource>();
            closeStalkerCueAudioSource.playOnAwake = false;
            closeStalkerCueAudioSource.loop = false;
            closeStalkerCueAudioSource.volume = 1f;
            closeStalkerCueAudioSource.spatialBlend = closeStalkerCueAudioSpatialBlend;
            closeStalkerCueAudioSource.minDistance = 1.2f;
            closeStalkerCueAudioSource.maxDistance = 12f;
            closeStalkerCueAudioSource.rolloffMode = AudioRolloffMode.Linear;
            return closeStalkerCueAudioSource;
        }

        private AudioClip EnsureCloseStalkerCueClip()
        {
            float duration = Mathf.Max(0.05f, closeStalkerCueAudioDuration);
            float frequency = Mathf.Max(1f, closeStalkerCueAudioFrequency);
            if (closeStalkerCueClip != null
                && Mathf.Approximately(closeStalkerCueClipDuration, duration)
                && Mathf.Approximately(closeStalkerCueClipFrequency, frequency))
            {
                return closeStalkerCueClip;
            }

            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float thumpA = BuildCloseStalkerThump(t, 0.015f, frequency, 0.115f, 0.9f);
                float thumpB = BuildCloseStalkerThump(t, 0.22f, frequency * 0.78f, 0.15f, 0.62f);
                float scrapeWindow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.18f, t))
                                     * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duration * 0.58f, duration * 0.92f, t)));
                float scrape = (Mathf.PerlinNoise(t * 75f, 0.37f) - 0.5f) * 0.16f * scrapeWindow;
                samples[i] = Mathf.Clamp(thumpA + thumpB + scrape, -1f, 1f);
            }

            closeStalkerCueClip = AudioClip.Create("CloseStalkerCue", sampleCount, 1, sampleRate, false);
            closeStalkerCueClip.SetData(samples, 0);
            closeStalkerCueClipDuration = duration;
            closeStalkerCueClipFrequency = frequency;
            return closeStalkerCueClip;
        }

        private static float BuildCloseStalkerThump(float time, float offset, float frequency, float decay, float gain)
        {
            float localTime = time - offset;
            if (localTime < 0f)
            {
                return 0f;
            }

            float attack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.012f, localTime));
            float envelope = attack * Mathf.Exp(-localTime / Mathf.Max(0.001f, decay));
            float low = Mathf.Sin(localTime * frequency * Mathf.PI * 2f);
            float knock = Mathf.Sin(localTime * frequency * 2.6f * Mathf.PI * 2f) * 0.28f;
            return (low + knock) * envelope * gain;
        }

        private void HandleRuntimeEventRaised(RuntimeEventRecord record)
        {
            if (!Application.isPlaying || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            switch (record.Semantic)
            {
                case RuntimeEventSemantic.ChaseStarted:
                    RegisterEscapeReliefChaseStart(record);
                    break;
                case RuntimeEventSemantic.ChaseDisengaged:
                    TryGrantEscapeReliefReward(record);
                    break;
            }
        }

        private void RegisterEscapeReliefChaseStart(RuntimeEventRecord record)
        {
            float now = record.RealtimeSinceStartup > 0f ? record.RealtimeSinceStartup : Time.realtimeSinceStartup;
            if (activeChaseEventCount <= 0)
            {
                activeChaseStartRealtime = now;
            }

            activeChaseEventCount = Mathf.Clamp(activeChaseEventCount + 1, 1, 64);
        }

        private void TryGrantEscapeReliefReward(RuntimeEventRecord record)
        {
            float now = record.RealtimeSinceStartup > 0f ? record.RealtimeSinceStartup : Time.realtimeSinceStartup;
            if (activeChaseEventCount > 0)
            {
                activeChaseEventCount--;
            }

            if (activeChaseEventCount > 0)
            {
                return;
            }

            float chaseDuration = activeChaseStartRealtime > 0f ? Mathf.Max(0f, now - activeChaseStartRealtime) : 0f;
            activeChaseStartRealtime = -1f;

            if (!enableEscapeReliefReward || chaseDuration < minEscapeReliefChaseSeconds || now < nextEscapeReliefRewardRealtime)
            {
                return;
            }

            PlayerDummyController controller = ResolvePlayerController();
            if (controller == null)
            {
                return;
            }

            float durationIntensity = Mathf.Clamp01(Mathf.InverseLerp(
                minEscapeReliefChaseSeconds,
                minEscapeReliefChaseSeconds + 4f,
                chaseDuration));
            float pressureIntensity = Mathf.Clamp01(currentReadabilityPressure);
            float intensity = Mathf.Clamp01(0.45f + durationIntensity * 0.35f + pressureIntensity * 0.28f);
            float recoverAmount = Mathf.Max(0f, escapeReliefStaminaRecover + escapeReliefPressureStaminaBonus * pressureIntensity);
            float recovered = controller.RecoverStamina(recoverAmount);

            Vector2 rewardPosition = controller.transform.position;
            if (escapeReliefRevealFog && fogOfWar != null)
            {
                fogOfWar.ApplyEchoRevealPulse(
                    rewardPosition,
                    escapeReliefRevealRadius * Mathf.Lerp(0.85f, 1.25f, intensity),
                    escapeReliefRevealSoftnessBoost * Mathf.Lerp(0.85f, 1.2f, intensity));
            }

            SpawnEscapeReliefPulse(rewardPosition, intensity);
            TrySpawnEscapeReliefEnemyTrail(record, rewardPosition, intensity);
            TrySpawnEscapeReliefObjectiveWhisper(rewardPosition, intensity);
            PlayEscapeReliefAudio(rewardPosition, intensity);
            StartEscapeReliefCalmWindow(intensity);
            ApplyEscapeReliefQuietBreath(controller, intensity);
            nextEscapeReliefRewardRealtime = now + Mathf.Max(0.5f, escapeReliefRewardCooldownSeconds);

            int stage = record.HasStage ? record.Stage : (mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 0);
            RuntimeEventBus.Raise(
                RuntimeEventType.Ability,
                BuildEscapeReliefRewardMessage(recovered, chaseDuration),
                this,
                stage,
                semantic: RuntimeEventSemantic.EscapeRelief);
        }

        public bool TryGrantRhythmReleaseRelief(float pressure01, int stage)
        {
            if (!Application.isPlaying
                || RegressionChecklistRunner.IsRegressionRunActive
                || !enableEscapeReliefReward
                || !enableRhythmReleaseRelief)
            {
                return false;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextRhythmReleaseReliefRealtime)
            {
                return false;
            }

            PlayerDummyController controller = ResolvePlayerController();
            if (controller == null)
            {
                return false;
            }

            float safePressure = Mathf.Clamp01(pressure01 >= 0f ? pressure01 : currentReadabilityPressure);
            float intensity = Mathf.Clamp01(Mathf.Max(rhythmReleaseReliefIntensityFloor, 0.36f + safePressure * rhythmReleaseReliefPressureBonus));
            float recovered = controller.RecoverStamina(rhythmReleaseStaminaRecover * Mathf.Lerp(0.75f, 1.25f, intensity));
            Vector2 rewardPosition = controller.transform.position;

            if (escapeReliefRevealFog && fogOfWar != null)
            {
                fogOfWar.ApplyEchoRevealPulse(
                    rewardPosition,
                    escapeReliefRevealRadius * rhythmReleaseFogRadiusMultiplier * Mathf.Lerp(0.9f, 1.18f, intensity),
                    escapeReliefRevealSoftnessBoost * Mathf.Lerp(0.75f, 1.05f, intensity));
            }

            SpawnEscapeReliefPulse(rewardPosition, intensity);
            TrySpawnEscapeReliefObjectiveWhisper(
                rewardPosition,
                intensity,
                rhythmReleaseObjectiveWhisperDistanceMultiplier,
                rhythmReleaseObjectiveWhisperDurationMultiplier,
                rhythmReleaseObjectiveWhisperWidthMultiplier);
            PlayEscapeReliefAudio(rewardPosition, intensity * 0.82f);
            StartEscapeReliefCalmWindow(intensity);
            ApplyRhythmReleaseQuietBreath(controller, intensity);
            StartRhythmReleaseCameraExhale(intensity);

            nextRhythmReleaseReliefRealtime = now + Mathf.Max(0.5f, rhythmReleaseReliefCooldownSeconds);

            RuntimeEventBus.Raise(
                RuntimeEventType.Ability,
                BuildRhythmReleaseReliefMessage(recovered),
                this,
                Mathf.Max(0, stage),
                semantic: RuntimeEventSemantic.EscapeRelief);

            return true;
        }

        private static string BuildEscapeReliefRewardMessage(float recovered, float chaseDuration)
        {
            return $"숨 돌릴 틈 (+{Mathf.Max(0f, recovered):0.0} 스태미나 / 추격 {Mathf.Max(0f, chaseDuration):0.0}초)";
        }

        private static string BuildRhythmReleaseReliefMessage(float recovered)
        {
            return $"안도 회복 (+{Mathf.Max(0f, recovered):0.0} 스태미나)";
        }

        private void StartRhythmReleaseCameraExhale(float intensity)
        {
            if (!enableRhythmReleaseCameraExhale)
            {
                return;
            }

            rhythmReleaseCameraExhaleStartedRealtime = Time.realtimeSinceStartup;
            rhythmReleaseCameraExhaleDuration = Mathf.Max(0.1f, rhythmReleaseCameraExhaleSeconds);
            rhythmReleaseCameraExhaleUntilRealtime = rhythmReleaseCameraExhaleStartedRealtime + rhythmReleaseCameraExhaleDuration;
            rhythmReleaseCameraExhaleIntensity = Mathf.Clamp01(intensity);
        }

        private float EvaluateRhythmReleaseCameraExhale01()
        {
            if (!enableRhythmReleaseCameraExhale || rhythmReleaseCameraExhaleUntilRealtime <= 0f)
            {
                return 0f;
            }

            float remaining = rhythmReleaseCameraExhaleUntilRealtime - Time.realtimeSinceStartup;
            if (remaining <= 0f)
            {
                return 0f;
            }

            float duration = Mathf.Max(0.1f, rhythmReleaseCameraExhaleDuration);
            float progress = Mathf.Clamp01((Time.realtimeSinceStartup - rhythmReleaseCameraExhaleStartedRealtime) / duration);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
            return Mathf.Clamp01(fade * Mathf.Lerp(0.72f, 1f, rhythmReleaseCameraExhaleIntensity));
        }

        private void SpawnEscapeReliefPulse(Vector2 position, float intensity)
        {
            GameObject visualObject = new($"EscapeReliefPulse_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/EscapeRelief");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = escapeReliefPulseColor;
            color.a *= Mathf.Lerp(0.72f, 1.12f, Mathf.Clamp01(intensity));
            visual.Configure(
                Mathf.Max(0.25f, escapeReliefPulseRadius * Mathf.Lerp(0.9f, 1.28f, Mathf.Clamp01(intensity))),
                color,
                Mathf.Max(0.1f, escapeReliefPulseDuration),
                Mathf.Clamp(escapeReliefPulseRingCount, 1, 4),
                Mathf.Max(0f, escapeReliefPulseRingInterval),
                escapeReliefPulseSortingOrder);
        }

        private void TrySpawnEscapeReliefEnemyTrail(RuntimeEventRecord record, Vector2 playerPosition, float intensity)
        {
            if (!enableEscapeReliefEnemyTrail)
            {
                return;
            }

            if (!TryResolveEscapeReliefThreatPosition(record, playerPosition, out Vector2 threatPosition))
            {
                return;
            }

            Vector2 toThreat = threatPosition - playerPosition;
            float distance = toThreat.magnitude;
            if (distance <= 0.2f)
            {
                return;
            }

            Vector2 direction = toThreat / distance;
            float cappedDistance = Mathf.Min(distance, Mathf.Max(0.2f, escapeReliefTrailMaxDistance));
            Vector2 trailEnd = playerPosition + direction * cappedDistance;

            GameObject trailObject = new($"EscapeReliefThreatTrail_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/EscapeRelief");
            if (vfxRoot != null)
            {
                trailObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = trailObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = Mathf.Max(0.01f, escapeReliefTrailWidth);
            line.sharedMaterial = GetEscapeReliefTrailMaterial();
            line.sortingOrder = escapeReliefTrailSortingOrder;

            Vector3[] points = BuildEscapeReliefTrailPoints(playerPosition, trailEnd, Mathf.Clamp01(intensity));
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }

            StartCoroutine(EscapeReliefTrailRoutine(trailObject, line, points, Mathf.Clamp01(intensity)));
        }

        private bool TryResolveEscapeReliefThreatPosition(RuntimeEventRecord record, Vector2 playerPosition, out Vector2 threatPosition)
        {
            threatPosition = default;
            if (record.Source is EnemyController eventEnemy && eventEnemy != null)
            {
                threatPosition = eventEnemy.transform.position;
                return true;
            }

            RefreshEnemyCache();
            EnemyController nearestEnemy = null;
            float nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || enemy.IsStunned)
                {
                    continue;
                }

                float distanceSqr = ((Vector2)enemy.transform.position - playerPosition).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestEnemy = enemy;
                nearestDistanceSqr = distanceSqr;
            }

            if (nearestEnemy == null)
            {
                return false;
            }

            threatPosition = nearestEnemy.transform.position;
            return true;
        }

        private Vector3[] BuildEscapeReliefTrailPoints(Vector2 origin, Vector2 target, float intensity)
        {
            Vector2 direction = target - origin;
            Vector2 side = direction.sqrMagnitude > 0.001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            if (Random.value < 0.5f)
            {
                side = -side;
            }

            float waver = Mathf.Max(0f, escapeReliefTrailWaver) * Mathf.Lerp(0.55f, 1.15f, intensity);
            return new[]
            {
                new Vector3(origin.x, origin.y, 0f),
                (Vector3)(Vector2.Lerp(origin, target, 0.34f) + side * waver),
                (Vector3)(Vector2.Lerp(origin, target, 0.68f) - side * waver * 0.58f),
                new Vector3(target.x, target.y, 0f)
            };
        }

        private IEnumerator EscapeReliefTrailRoutine(GameObject trailObject, LineRenderer line, Vector3[] basePoints, float intensity)
        {
            float duration = Mathf.Max(0.1f, escapeReliefTrailDuration);
            float startedAt = Time.time;
            Vector3 direction = basePoints[^1] - basePoints[0];
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;
            float waver = Mathf.Max(0f, escapeReliefTrailWaver) * Mathf.Lerp(0.45f, 1.05f, intensity);

            while (line != null && Time.time < startedAt + duration)
            {
                float elapsed = Time.time - startedAt;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0.08f, 1f, t);
                float shimmer = 0.75f + Mathf.Sin((elapsed * 5.4f + intensity * 1.7f) * Mathf.PI * 2f) * 0.25f;

                for (int i = 0; i < basePoints.Length; i++)
                {
                    Vector3 point = basePoints[i];
                    if (i > 0 && i < basePoints.Length - 1)
                    {
                        float local = Mathf.Sin((elapsed * 3.8f + i * 0.77f) * Mathf.PI * 2f);
                        point += side * local * waver * fade;
                    }

                    line.SetPosition(i, point);
                }

                Color color = escapeReliefTrailColor;
                color.a *= fade * shimmer * Mathf.Lerp(0.82f, 1.12f, intensity);
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Max(0.01f, escapeReliefTrailWidth) * Mathf.Lerp(1.2f, 0.2f, t);
                yield return null;
            }

            if (trailObject != null)
            {
                Destroy(trailObject);
            }
        }

        private Material GetEscapeReliefTrailMaterial()
        {
            if (escapeReliefTrailMaterial != null)
            {
                return escapeReliefTrailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            escapeReliefTrailMaterial = new Material(shader)
            {
                name = "EscapeReliefTrailMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return escapeReliefTrailMaterial;
        }

        private void TrySpawnEscapeReliefObjectiveWhisper(
            Vector2 playerPosition,
            float intensity,
            float distanceMultiplier = 1f,
            float durationMultiplier = 1f,
            float widthMultiplier = 1f)
        {
            if (!enableEscapeReliefObjectiveWhisper)
            {
                return;
            }

            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop == null)
            {
                return;
            }

            if (!stageLoop.TryGetNextObjectiveTarget(playerPosition, out Vector3 target, out bool targetIsExit))
            {
                return;
            }

            Vector2 toTarget = (Vector2)target - playerPosition;
            float distance = toTarget.magnitude;
            if (distance <= 0.35f)
            {
                return;
            }

            float safeDistanceMultiplier = Mathf.Max(0.1f, distanceMultiplier);
            float safeDurationMultiplier = Mathf.Max(0.1f, durationMultiplier);
            float safeWidthMultiplier = Mathf.Max(0.1f, widthMultiplier);
            float maxDistance = Mathf.Max(0.2f, escapeReliefObjectiveWhisperMaxDistance * safeDistanceMultiplier);
            Vector2 whisperTarget = playerPosition + toTarget.normalized * Mathf.Min(distance, maxDistance);

            GameObject whisperObject = new($"EscapeReliefObjectiveWhisper_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/EscapeRelief");
            if (vfxRoot != null)
            {
                whisperObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = whisperObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = Mathf.Max(0.01f, escapeReliefObjectiveWhisperWidth * safeWidthMultiplier);
            line.sharedMaterial = GetEscapeReliefTrailMaterial();
            line.sortingOrder = escapeReliefObjectiveWhisperSortingOrder;

            Vector3[] points = BuildEscapeReliefObjectiveWhisperPoints(playerPosition, whisperTarget, intensity);
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i]);
            }

            Color color = targetIsExit ? escapeReliefExitWhisperColor : escapeReliefBreadcrumbWhisperColor;
            StartCoroutine(EscapeReliefObjectiveWhisperRoutine(
                whisperObject,
                line,
                points,
                color,
                Mathf.Clamp01(intensity),
                safeDurationMultiplier,
                safeWidthMultiplier));

            Color pulseColor = color;
            pulseColor.a *= 0.62f;
            GameObject pulseObject = new(targetIsExit ? "EscapeReliefExitHint" : "EscapeReliefBreadcrumbHint");
            if (vfxRoot != null)
            {
                pulseObject.transform.SetParent(vfxRoot, false);
            }

            pulseObject.transform.position = new Vector3(whisperTarget.x, whisperTarget.y, 0f);
            EchoPulseVisualDummy visual = pulseObject.AddComponent<EchoPulseVisualDummy>();
            visual.Configure(
                Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(intensity)),
                pulseColor,
                Mathf.Max(0.1f, escapeReliefObjectiveWhisperDuration * safeDurationMultiplier * 0.9f),
                1,
                0f,
                escapeReliefObjectiveWhisperSortingOrder);
        }

        private Vector3[] BuildEscapeReliefObjectiveWhisperPoints(Vector2 origin, Vector2 target, float intensity)
        {
            Vector2 direction = target - origin;
            Vector2 side = direction.sqrMagnitude > 0.001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;
            float offset = Mathf.Lerp(0.18f, 0.36f, Mathf.Clamp01(intensity));
            return new[]
            {
                new Vector3(origin.x, origin.y, 0f),
                (Vector3)(Vector2.Lerp(origin, target, 0.56f) + side * offset),
                new Vector3(target.x, target.y, 0f)
            };
        }

        private IEnumerator EscapeReliefObjectiveWhisperRoutine(
            GameObject whisperObject,
            LineRenderer line,
            Vector3[] basePoints,
            Color baseColor,
            float intensity,
            float durationMultiplier,
            float widthMultiplier)
        {
            float duration = Mathf.Max(0.1f, escapeReliefObjectiveWhisperDuration * Mathf.Max(0.1f, durationMultiplier));
            float safeWidth = Mathf.Max(0.01f, escapeReliefObjectiveWhisperWidth * Mathf.Max(0.1f, widthMultiplier));
            float startedAt = Time.time;
            Vector3 direction = basePoints[^1] - basePoints[0];
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;

            while (line != null && Time.time < startedAt + duration)
            {
                float elapsed = Time.time - startedAt;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0.1f, 1f, t);
                float shimmer = 0.72f + Mathf.Sin((elapsed * 4.8f + intensity) * Mathf.PI * 2f) * 0.28f;

                for (int i = 0; i < basePoints.Length; i++)
                {
                    Vector3 point = basePoints[i];
                    if (i == 1)
                    {
                        point += side * Mathf.Sin(elapsed * Mathf.PI * 2f * 2.2f) * 0.08f * fade;
                    }

                    line.SetPosition(i, point);
                }

                Color color = baseColor;
                color.a *= fade * shimmer * Mathf.Lerp(0.86f, 1.14f, intensity);
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = safeWidth * Mathf.Lerp(1.15f, 0.25f, t);
                yield return null;
            }

            if (whisperObject != null)
            {
                Destroy(whisperObject);
            }
        }

        private void PlayEscapeReliefAudio(Vector2 position, float intensity)
        {
            if (!enableEscapeReliefAudio || escapeReliefAudioVolume <= 0f)
            {
                return;
            }

            AudioSource source = EnsureEscapeReliefAudioSource();
            AudioClip clip = EnsureEscapeReliefClip();
            if (source == null || clip == null)
            {
                return;
            }

            source.transform.position = new Vector3(position.x, position.y, 0f);
            source.spatialBlend = escapeReliefAudioSpatialBlend;
            source.pitch = Random.Range(0.94f, 1.08f);
            source.PlayOneShot(clip, escapeReliefAudioVolume * Mathf.Clamp01(intensity));
        }

        private AudioSource EnsureEscapeReliefAudioSource()
        {
            if (escapeReliefAudioSource != null)
            {
                return escapeReliefAudioSource;
            }

            GameObject sourceObject = new("EscapeReliefAudio");
            Transform audioRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/AudioEmitters");
            if (audioRoot != null)
            {
                sourceObject.transform.SetParent(audioRoot, false);
            }

            escapeReliefAudioSource = sourceObject.AddComponent<AudioSource>();
            escapeReliefAudioSource.playOnAwake = false;
            escapeReliefAudioSource.loop = false;
            escapeReliefAudioSource.volume = 1f;
            escapeReliefAudioSource.spatialBlend = escapeReliefAudioSpatialBlend;
            escapeReliefAudioSource.minDistance = 1.5f;
            escapeReliefAudioSource.maxDistance = 10f;
            escapeReliefAudioSource.rolloffMode = AudioRolloffMode.Linear;
            return escapeReliefAudioSource;
        }

        private AudioClip EnsureEscapeReliefClip()
        {
            float duration = Mathf.Max(0.05f, escapeReliefAudioDuration);
            float frequency = Mathf.Max(1f, escapeReliefAudioFrequency);
            if (escapeReliefClip != null
                && Mathf.Approximately(escapeReliefClipDuration, duration)
                && Mathf.Approximately(escapeReliefClipFrequency, frequency))
            {
                return escapeReliefClip;
            }

            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = Mathf.Clamp01(t / duration);
                float inhaleWindow = Mathf.SmoothStep(0f, 1f, normalized) * (1f - Mathf.SmoothStep(0.62f, 1f, normalized));
                float exhaleWindow = Mathf.SmoothStep(0.12f, 0.52f, normalized) * (1f - Mathf.SmoothStep(0.62f, 1f, normalized));
                float tone = Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.16f * inhaleWindow;
                float highTone = Mathf.Sin(t * frequency * 1.64f * Mathf.PI * 2f + 0.6f) * 0.06f * inhaleWindow;
                float breath = (Mathf.PerlinNoise(t * 21f, 3.17f) - 0.5f) * 0.18f * exhaleWindow;
                float tail = Mathf.Sin(t * frequency * 0.52f * Mathf.PI * 2f) * 0.11f * Mathf.Pow(1f - normalized, 1.8f);
                samples[i] = Mathf.Clamp(tone + highTone + breath + tail, -1f, 1f);
            }

            escapeReliefClip = AudioClip.Create("EscapeReliefCue", sampleCount, 1, sampleRate, false);
            escapeReliefClip.SetData(samples, 0);
            escapeReliefClipDuration = duration;
            escapeReliefClipFrequency = frequency;
            return escapeReliefClip;
        }

        private void StartEscapeReliefCalmWindow(float intensity)
        {
            if (!enableEscapeReliefCalmWindow)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            escapeReliefCalmStartedRealtime = now;
            escapeReliefCalmDuration = Mathf.Max(0.1f, escapeReliefCalmSeconds * Mathf.Lerp(0.82f, 1.24f, Mathf.Clamp01(intensity)));
            escapeReliefCalmUntilRealtime = now + escapeReliefCalmDuration;

            nextDreadBeatTime = Mathf.Max(nextDreadBeatTime, Time.time + 0.55f);
            nextPhantomCueTime = Mathf.Max(nextPhantomCueTime, Time.time + 1f);
            nextCloseStalkerCueTime = Mathf.Max(nextCloseStalkerCueTime, Time.time + 0.85f);
        }

        private void ApplyEscapeReliefQuietBreath(PlayerDummyController controller, float intensity)
        {
            if (!enableEscapeReliefQuietBreath || controller == null)
            {
                return;
            }

            float duration = Mathf.Max(
                0.1f,
                escapeReliefQuietBreathSeconds * Mathf.Lerp(0.85f, 1.2f, Mathf.Clamp01(intensity)));
            controller.ApplyTemporaryNoiseDampeningForRuntime(
                escapeReliefFootstepNoiseMultiplier,
                escapeReliefSprintNoiseMultiplier,
                duration);
        }

        private void ApplyRhythmReleaseQuietBreath(PlayerDummyController controller, float intensity)
        {
            if (!enableEscapeReliefQuietBreath || controller == null)
            {
                return;
            }

            float safeIntensity = Mathf.Clamp01(intensity);
            float duration = Mathf.Max(
                0.1f,
                escapeReliefQuietBreathSeconds
                * rhythmReleaseQuietBreathMultiplier
                * Mathf.Lerp(0.9f, 1.18f, safeIntensity));

            controller.ApplyTemporaryNoiseDampeningForRuntime(
                escapeReliefFootstepNoiseMultiplier,
                escapeReliefSprintNoiseMultiplier,
                duration);
        }

        private void TrackEscapeReliefQuietBreathStrain(float dt)
        {
            if (!Application.isPlaying || RegressionChecklistRunner.IsRegressionRunActive || !enableEscapeReliefBreathSnap)
            {
                quietBreathStrainElapsed = 0f;
                return;
            }

            PlayerDummyController controller = ResolvePlayerController();
            if (controller == null || controller.TemporaryNoiseDampeningRemaining <= 0.05f)
            {
                quietBreathStrainElapsed = 0f;
                return;
            }

            float safeDt = Mathf.Max(0f, dt);
            if (controller.IsTemporaryNoiseDampeningStrained)
            {
                quietBreathStrainElapsed += safeDt;
            }
            else
            {
                quietBreathStrainElapsed = Mathf.Max(0f, quietBreathStrainElapsed - safeDt * 1.8f);
                return;
            }

            float threshold = Mathf.Max(0.05f, escapeReliefBreathSnapStrainSeconds);
            if (quietBreathStrainElapsed < threshold)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextEscapeReliefBreathSnapRealtime)
            {
                quietBreathStrainElapsed = Mathf.Min(quietBreathStrainElapsed, threshold);
                return;
            }

            TriggerEscapeReliefBreathSnap(controller, Mathf.Clamp01(quietBreathStrainElapsed / threshold));
            quietBreathStrainElapsed = 0f;
            nextEscapeReliefBreathSnapRealtime = now + Mathf.Max(0.5f, escapeReliefBreathSnapCooldownSeconds);
        }

        private void TriggerEscapeReliefBreathSnap(PlayerDummyController controller, float intensity)
        {
            if (controller == null)
            {
                return;
            }

            float safeIntensity = Mathf.Clamp01(intensity);
            Vector2 position = controller.transform.position;

            ShortenEscapeReliefCalmWindow(safeIntensity);
            SpawnEscapeReliefBreathSnapPulse(position, safeIntensity);
            PlayEscapeReliefBreathSnapAudio(position, safeIntensity);

            if (escapeReliefBreathSnapCameraImpulse && cameraFollow != null && escapeReliefBreathSnapImpulseAmplitude > 0f)
            {
                cameraFollow.AddImpulse(
                    escapeReliefBreathSnapImpulseAmplitude * Mathf.Lerp(0.75f, 1.2f, safeIntensity),
                    Mathf.Max(0.05f, escapeReliefBreathSnapImpulseDuration));
            }

            if (escapeReliefBreathSnapEmitsNoise && NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    position,
                    escapeReliefBreathSnapNoiseLoudness * Mathf.Lerp(0.8f, 1.18f, safeIntensity),
                    escapeReliefBreathSnapNoiseRadius * Mathf.Lerp(0.84f, 1.22f, safeIntensity),
                    NoiseKind.Footstep,
                    controller.gameObject);
            }

            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 0;
            RuntimeEventBus.Raise(
                RuntimeEventType.Ability,
                BuildQuietBreathBrokenMessage(),
                this,
                stage,
                semantic: RuntimeEventSemantic.QuietBreathBroken);
        }

        private static string BuildQuietBreathBrokenMessage()
        {
            return "조용한 숨 깨짐 - 달리기 소음 발생";
        }

        private void ShortenEscapeReliefCalmWindow(float intensity)
        {
            if (!enableEscapeReliefCalmWindow || escapeReliefCalmUntilRealtime <= 0f || escapeReliefBreathSnapCalmPenalty <= 0f)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= escapeReliefCalmUntilRealtime)
            {
                return;
            }

            float penalty = Mathf.Max(0.1f, escapeReliefCalmDuration) * escapeReliefBreathSnapCalmPenalty * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(intensity));
            escapeReliefCalmUntilRealtime = Mathf.Max(now, escapeReliefCalmUntilRealtime - penalty);
            nextDreadBeatTime = Mathf.Min(nextDreadBeatTime, Time.time + 0.12f);
            nextPhantomCueTime = Mathf.Min(nextPhantomCueTime, Time.time + 0.32f);
            nextCloseStalkerCueTime = Mathf.Min(nextCloseStalkerCueTime, Time.time + 0.22f);
        }

        private void SpawnEscapeReliefBreathSnapPulse(Vector2 position, float intensity)
        {
            GameObject visualObject = new($"EscapeReliefBreathSnap_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/EscapeRelief");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = escapeReliefBreathSnapColor;
            color.a *= Mathf.Lerp(0.78f, 1.16f, Mathf.Clamp01(intensity));
            visual.Configure(
                Mathf.Max(0.2f, escapeReliefBreathSnapRadius * Mathf.Lerp(0.86f, 1.22f, Mathf.Clamp01(intensity))),
                color,
                Mathf.Max(0.1f, escapeReliefBreathSnapDuration),
                1,
                0f,
                escapeReliefBreathSnapSortingOrder);
        }

        private void PlayEscapeReliefBreathSnapAudio(Vector2 position, float intensity)
        {
            if (!enableEscapeReliefBreathSnapAudio || escapeReliefBreathSnapAudioVolume <= 0f)
            {
                return;
            }

            AudioSource source = EnsureEscapeReliefBreathSnapAudioSource();
            AudioClip clip = EnsureEscapeReliefBreathSnapClip();
            if (source == null || clip == null)
            {
                return;
            }

            source.transform.position = new Vector3(position.x, position.y, 0f);
            source.spatialBlend = escapeReliefBreathSnapAudioSpatialBlend;
            source.pitch = Random.Range(0.96f, 1.12f);
            source.PlayOneShot(clip, escapeReliefBreathSnapAudioVolume * Mathf.Clamp01(intensity));
        }

        private AudioSource EnsureEscapeReliefBreathSnapAudioSource()
        {
            if (escapeReliefBreathSnapAudioSource != null)
            {
                return escapeReliefBreathSnapAudioSource;
            }

            GameObject sourceObject = new("EscapeReliefBreathSnapAudio");
            Transform audioRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/AudioEmitters");
            if (audioRoot != null)
            {
                sourceObject.transform.SetParent(audioRoot, false);
            }

            escapeReliefBreathSnapAudioSource = sourceObject.AddComponent<AudioSource>();
            escapeReliefBreathSnapAudioSource.playOnAwake = false;
            escapeReliefBreathSnapAudioSource.loop = false;
            escapeReliefBreathSnapAudioSource.volume = 1f;
            escapeReliefBreathSnapAudioSource.spatialBlend = escapeReliefBreathSnapAudioSpatialBlend;
            escapeReliefBreathSnapAudioSource.minDistance = 1.2f;
            escapeReliefBreathSnapAudioSource.maxDistance = 9f;
            escapeReliefBreathSnapAudioSource.rolloffMode = AudioRolloffMode.Linear;
            return escapeReliefBreathSnapAudioSource;
        }

        private AudioClip EnsureEscapeReliefBreathSnapClip()
        {
            float duration = Mathf.Max(0.05f, escapeReliefBreathSnapAudioDuration);
            float frequency = Mathf.Max(1f, escapeReliefBreathSnapAudioFrequency);
            if (escapeReliefBreathSnapClip != null
                && Mathf.Approximately(escapeReliefBreathSnapClipDuration, duration)
                && Mathf.Approximately(escapeReliefBreathSnapClipFrequency, frequency))
            {
                return escapeReliefBreathSnapClip;
            }

            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = Mathf.Clamp01(t / duration);
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.035f, t));
                float decay = Mathf.Pow(1f - normalized, 2.25f);
                float rasp = (Mathf.PerlinNoise(t * 68f, 9.37f) - 0.5f) * 0.46f;
                float chirp = Mathf.Sin(t * frequency * (1f + normalized * 1.6f) * Mathf.PI * 2f) * 0.14f;
                float thump = Mathf.Sin(t * 46f * Mathf.PI * 2f) * Mathf.Exp(-t / 0.09f) * 0.22f;
                samples[i] = Mathf.Clamp((rasp + chirp) * attack * decay + thump, -1f, 1f);
            }

            escapeReliefBreathSnapClip = AudioClip.Create("EscapeReliefBreathSnapCue", sampleCount, 1, sampleRate, false);
            escapeReliefBreathSnapClip.SetData(samples, 0);
            escapeReliefBreathSnapClipDuration = duration;
            escapeReliefBreathSnapClipFrequency = frequency;
            return escapeReliefBreathSnapClip;
        }

        private float EvaluateEscapeReliefCalm01()
        {
            if (!enableEscapeReliefCalmWindow || escapeReliefCalmUntilRealtime <= 0f)
            {
                return 0f;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= escapeReliefCalmUntilRealtime)
            {
                return 0f;
            }

            float duration = Mathf.Max(0.1f, escapeReliefCalmDuration);
            float remaining = Mathf.Clamp01((escapeReliefCalmUntilRealtime - now) / duration);
            float elapsed = Mathf.Clamp01((now - escapeReliefCalmStartedRealtime) / duration);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, elapsed));
            return Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, remaining) * fadeIn);
        }

        private float ApplyEscapeReliefCalmPressureDip(float pressure)
        {
            float calm = EvaluateEscapeReliefCalm01();
            if (calm <= 0.001f)
            {
                return Mathf.Clamp01(pressure);
            }

            return Mathf.Clamp01(pressure * (1f - escapeReliefPressureDip * calm));
        }

        private bool ShouldSuppressDreadCueDuringRelief()
        {
            return escapeReliefCueSuppression > 0.001f
                   && EvaluateEscapeReliefCalm01() * escapeReliefCueSuppression >= 0.12f;
        }

        private bool TryReserveThreatCueBudget(ThreatCueBudgetTier tier)
        {
            if (!enableThreatCueBudget)
            {
                return true;
            }

            float now = Time.time;
            bool isMajor = tier == ThreatCueBudgetTier.Major;
            if (now < nextMinorThreatCueTime || (isMajor && now < nextMajorThreatCueTime))
            {
                suppressedThreatCueCount++;
                return false;
            }

            nextMinorThreatCueTime = now + Mathf.Max(0.05f, minorThreatCueCooldownSeconds);
            if (isMajor)
            {
                nextMajorThreatCueTime = now + Mathf.Max(0.25f, majorThreatCueCooldownSeconds);
            }

            return true;
        }

        private void TryApplyThreatPulse(float pressure)
        {
            float delta = hasPreviousReadabilityPressure ? pressure - previousReadabilityPressure : 0f;
            previousReadabilityPressure = pressure;
            hasPreviousReadabilityPressure = true;

            bool spikeDetected = pressure >= pulsePressureThreshold && delta >= pulsePressureDeltaThreshold;
            if (!spikeDetected || Time.time < nextAllowedPulseTime)
            {
                return;
            }

            if (!TryReserveThreatCueBudget(ThreatCueBudgetTier.Major))
            {
                nextAllowedPulseTime = Time.time + Mathf.Max(0.05f, deniedThreatCueRetrySeconds);
                return;
            }

            if (enableThreatPulseImpulse && cameraFollow != null)
            {
                float amplitude = Mathf.Max(0f, pulseImpulseAmplitude);
                if (amplitude > 0f)
                {
                    cameraFollow.AddImpulse(amplitude, Mathf.Max(0.05f, pulseImpulseDuration));
                }
            }

            TriggerPressureWaveFeedback(pressure, delta);
            nextAllowedPulseTime = Time.time + Mathf.Max(0.05f, pulseCooldownSeconds);
        }

        private void TriggerPressureWaveFeedback(float pressure, float delta)
        {
            if (!enablePressureWaveFeedback && !pressureWaveRaisesRuntimeEvent)
            {
                return;
            }

            float pressureIntensity = Mathf.InverseLerp(pulsePressureThreshold, 1f, pressure);
            float deltaIntensity = Mathf.InverseLerp(pulsePressureDeltaThreshold, 0.35f, delta);
            float intensity = Mathf.Clamp01(0.55f + Mathf.Max(pressureIntensity, deltaIntensity) * 0.45f);

            if (enablePressureWaveFeedback && player != null)
            {
                SpawnPressureWaveVisual(player.position, intensity);
            }

            if (!pressureWaveRaisesRuntimeEvent || Time.time < nextPressureWaveEventTime)
            {
                return;
            }

            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 0;
            RuntimeEventBus.Raise(
                RuntimeEventType.System,
                BuildPressureWaveMessage(pressure, delta),
                this,
                stage,
                semantic: RuntimeEventSemantic.PressureWave);
            nextPressureWaveEventTime = Time.time + Mathf.Max(0.1f, pressureWaveEventCooldownSeconds);
        }

        private static string BuildPressureWaveMessage(float pressure, float delta)
        {
            return $"압박 파동 {Mathf.Clamp01(pressure):0.00} (+{Mathf.Max(0f, delta):0.00})";
        }

        private void SpawnPressureWaveVisual(Vector3 position, float intensity)
        {
            GameObject visualObject = new($"PressureWave_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/PressureWave");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = pressureWaveColor;
            color.a *= Mathf.Lerp(0.72f, 1.12f, Mathf.Clamp01(intensity));
            visual.Configure(
                Mathf.Lerp(Mathf.Max(0.3f, pressureWaveMinRadius), Mathf.Max(pressureWaveMinRadius, pressureWaveMaxRadius), Mathf.Clamp01(intensity)),
                color,
                Mathf.Max(0.1f, pressureWaveDuration),
                Mathf.Clamp(pressureWaveRingCount, 1, 4),
                Mathf.Max(0f, pressureWaveRingInterval),
                pressureWaveSortingOrder);
        }

        private float EvaluateNearbyThreat(out int sampledEnemyCount)
        {
            sampledEnemyCount = 0;
            if (player == null)
            {
                return 0f;
            }

            if (cachedEnemies.Count <= 0)
            {
                return 0f;
            }

            float maxScore = 0f;
            float totalScore = 0f;
            float closestDistance = float.PositiveInfinity;
            int counted = 0;

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(player.position, enemy.transform.position);
                float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, threatRange));
                if (distanceFactor <= 0.001f)
                {
                    continue;
                }

                float stateWeight = EvaluateStateWeight(enemy.CurrentState);
                float suspicionFactor = Mathf.Clamp01(enemy.Suspicion) * suspicionWeight;
                float score = Mathf.Clamp01(stateWeight * Mathf.Lerp(0.35f, 1f, distanceFactor) + suspicionFactor);

                maxScore = Mathf.Max(maxScore, score);
                totalScore += score;
                closestDistance = Mathf.Min(closestDistance, distance);
                counted++;
            }

            sampledEnemyCount = counted;
            if (counted <= 0)
            {
                currentCloseThreatDistance = float.PositiveInfinity;
                return 0f;
            }

            currentCloseThreatDistance = closestDistance;
            float averageScore = totalScore / counted;
            return Mathf.Clamp01(maxScore * 0.62f + averageScore * 0.38f);
        }

        private float EvaluateStateWeight(EnemyStateId state)
        {
            return state switch
            {
                EnemyStateId.Chase => chaseWeight,
                EnemyStateId.Investigate => investigateWeight,
                EnemyStateId.Search => searchWeight,
                EnemyStateId.Suspicion => suspicionStateWeight,
                EnemyStateId.Return => returnWeight,
                EnemyStateId.Stunned => stunnedWeight,
                _ => idleWeight
            };
        }

        private float ApplyStageReadabilityEnvelope(float pressure)
        {
            if (!useStageReadabilityEnvelope)
            {
                return Mathf.Clamp01(pressure);
            }

            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 1;
            int early = Mathf.Max(1, envelopeStageEarly);
            int mid = Mathf.Max(early + 1, envelopeStageMid);
            int late = Mathf.Max(mid + 1, envelopeStageLate);
            float cap;
            if (stage <= mid)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(early, mid, stage));
                cap = Mathf.Lerp(earlyReadabilityCap, midReadabilityCap, t);
            }
            else
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(mid, late, stage));
                cap = Mathf.Lerp(midReadabilityCap, lateReadabilityCap, t);
            }

            return Mathf.Clamp01(Mathf.Min(pressure, Mathf.Clamp01(cap)));
        }

        private float EvaluateStageChaseAggressionScale()
        {
            if (!useStageReadabilityEnvelope)
            {
                return 1f;
            }

            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 1;
            int early = Mathf.Max(1, envelopeStageEarly);
            int mid = Mathf.Max(early + 1, envelopeStageMid);
            int late = Mathf.Max(mid + 1, envelopeStageLate);
            if (stage <= mid)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(early, mid, stage));
                return Mathf.Lerp(earlyChaseAggressionScale, midChaseAggressionScale, t);
            }

            float lateT = Mathf.Clamp01(Mathf.InverseLerp(mid, late, stage));
            return Mathf.Lerp(midChaseAggressionScale, lateChaseAggressionScale, lateT);
        }

        private float EvaluateFallbackStagePressure()
        {
            if (mapSystem == null)
            {
                return 0f;
            }

            int stage = Mathf.Max(1, mapSystem.CurrentStage);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 8f, stage));
        }

        private float EvaluatePresetBias(out string presetLabel)
        {
            if (!useMapPresetBias)
            {
                presetLabel = "Disabled";
                return 1f;
            }

            if (mapTuning == null)
            {
                presetLabel = "Unknown";
                return standardPressureBias;
            }

            presetLabel = mapTuning.ActivePresetLabel;
            return mapTuning.ActivePreset switch
            {
                MapTuningPreset.Compact => compactPressureBias,
                MapTuningPreset.Expansive => expansivePressureBias,
                _ => standardPressureBias
            };
        }

        private void ResetAppliedTuning()
        {
            if (cameraFollow != null)
            {
                cameraFollow.ResetRuntimeTuningForEditor();
            }

            if (fogOfWar != null)
            {
                fogOfWar.ResetRuntimeRevealTuningForEditor();
                fogOfWar.ResetRuntimeStyleTuningForEditor();
            }

            if (targetCamera != null && hasBaseCameraBackgroundColor)
            {
                targetCamera.backgroundColor = baseCameraBackgroundColor;
            }

            if (visibilitySource != null)
            {
                visibilitySource.ResetDreadRuntimeModifiersForEditor();
            }

            hasPreviousReadabilityPressure = false;
            previousReadabilityPressure = 0f;
            nextAllowedPulseTime = 0f;
            nextDreadBeatTime = 0f;
            nextPhantomCueTime = 0f;
            nextCloseStalkerCueTime = 0f;
            rhythmReleaseCameraExhaleUntilRealtime = 0f;
            rhythmReleaseCameraExhaleStartedRealtime = 0f;
            rhythmReleaseCameraExhaleDuration = 0f;
            rhythmReleaseCameraExhaleIntensity = 0f;
            nextMajorThreatCueTime = 0f;
            nextMinorThreatCueTime = 0f;
            suppressedThreatCueCount = 0;
            currentFlashlightDread = 0f;
            currentCloseThreatDistance = float.PositiveInfinity;
            currentThreatTunnelVision = 0f;
            currentCameraTargetOrthoSize = hasBaseCameraOrthoSize ? baseCameraOrthoSize : 0f;

            if (phantomCueAudioSource != null)
            {
                phantomCueAudioSource.Stop();
            }

            if (closeStalkerCueAudioSource != null)
            {
                closeStalkerCueAudioSource.Stop();
            }

            if (escapeReliefAudioSource != null)
            {
                escapeReliefAudioSource.Stop();
            }

            if (escapeReliefBreathSnapAudioSource != null)
            {
                escapeReliefBreathSnapAudioSource.Stop();
            }

            activeChaseEventCount = 0;
            activeChaseStartRealtime = -1f;
            nextEscapeReliefRewardRealtime = 0f;
            nextRhythmReleaseReliefRealtime = 0f;
            escapeReliefCalmStartedRealtime = 0f;
            escapeReliefCalmUntilRealtime = 0f;
            escapeReliefCalmDuration = 0f;
            quietBreathStrainElapsed = 0f;
            nextEscapeReliefBreathSnapRealtime = 0f;

            RefreshEnemyCache();
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.ResetRuntimePerceptionTuningForEditor();
                enemy.ResetRuntimeChaseReadabilityTuningForEditor();
            }
        }

        private void RefreshEnemyCache()
        {
            EnemyController.CopyActiveControllers(cachedEnemies);
        }

        private PlayerDummyController ResolvePlayerController()
        {
            if (playerController != null && (player == null || playerController.transform == player))
            {
                return playerController;
            }

            playerController = player != null
                ? player.GetComponent<PlayerDummyController>()
                : FindFirstObjectByType<PlayerDummyController>();
            return playerController;
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force)
            {
                if (Time.unscaledTime < nextReferenceResolveTime)
                {
                    return;
                }

                nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, referenceResolveRetryInterval);
            }

            if (mapSystem == null)
            {
                MapSystem resolvedMapSystem = FindFirstObjectByType<MapSystem>();
                if (resolvedMapSystem != null && resolvedMapSystem != mapSystem)
                {
                    UnsubscribeMap();
                    mapSystem = resolvedMapSystem;
                    SubscribeMap();
                }
            }

            if (player == null)
            {
                GameObject playerObject = null;
                try
                {
                    playerObject = GameObject.FindGameObjectWithTag("Player");
                }
                catch (UnityException)
                {
                    playerObject = null;
                }

                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (visibilitySource == null)
            {
                visibilitySource = player != null
                    ? player.GetComponent<PlayerVisibilitySource>()
                    : FindFirstObjectByType<PlayerVisibilitySource>();
            }

            ResolvePlayerController();

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }

            if (cameraFollow == null && targetCamera != null)
            {
                cameraFollow = targetCamera.GetComponent<CameraFollow2D>();
            }

            if (fogOfWar == null)
            {
                fogOfWar = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (stagePressureDirector == null)
            {
                stagePressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (mapTuning == null)
            {
                mapTuning = FindFirstObjectByType<MapTuningDebugController>();
            }
        }

        private void SubscribeMap()
        {
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.MapGenerated -= HandleMapGenerated;
            mapSystem.MapGenerated += HandleMapGenerated;
        }

        private void UnsubscribeMap()
        {
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.MapGenerated -= HandleMapGenerated;
        }

        private void HandleMapGenerated(int stage, System.Collections.Generic.IReadOnlyList<GeneratedMapCell> cells)
        {
            CaptureBaseCameraOrtho();
            ApplyNow(Mathf.Max(0.08f, updateInterval));
        }

        private static Transform EnsureScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
            {
                root = new GameObject(parts[0]);
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                {
                    GameObject childObject = new(parts[i]);
                    childObject.transform.SetParent(current, false);
                    child = childObject.transform;
                }

                current = child;
            }

            return current;
        }

        private void CaptureBaseCameraOrtho()
        {
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            baseCameraOrthoSize = targetCamera.orthographicSize;
            hasBaseCameraOrthoSize = true;

            if (!hasBaseCameraBackgroundColor)
            {
                baseCameraBackgroundColor = targetCamera.backgroundColor;
                hasBaseCameraBackgroundColor = true;
            }
        }
    }
}





