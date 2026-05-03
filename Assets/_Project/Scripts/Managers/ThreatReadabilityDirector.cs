using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public sealed class ThreatReadabilityDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private FogOfWarSystem fogOfWar;
        [SerializeField] private StagePressureDirector stagePressureDirector;
        [SerializeField] private MapTuningDebugController mapTuning;
        [SerializeField] private MapSystem mapSystem;

        [Header("Flow")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField, Min(0.02f)] private float updateInterval = 0.16f;
        [SerializeField, Min(0.1f)] private float referenceResolveRetryInterval = 0.75f;
        [SerializeField, Min(0.1f)] private float responseSmoothing = 5.6f;
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
        [SerializeField, Min(0f)] private float maxCameraZoomOut = 1.5f;
        [SerializeField, Min(1f)] private float minimumCameraOrthoSize = 4f;
        [SerializeField, Min(1f)] private float maximumCameraOrthoSize = 22f;
        [SerializeField, Range(0.5f, 2f)] private float minLookAheadMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxLookAheadMultiplier = 1.35f;
        [SerializeField, Range(0.5f, 2f)] private float minSmoothMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxSmoothMultiplier = 1.25f;
        [SerializeField, Range(0.5f, 2f)] private float minLookAheadSmoothingMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float maxLookAheadSmoothingMultiplier = 1.18f;

        [Header("Art Grade")]
        [SerializeField] private bool enableRuntimeArtGrade = true;
        [SerializeField, Min(0.1f)] private float cameraBackgroundLerpSpeed = 5.4f;
        [SerializeField] private Color calmCameraBackgroundColor = new(0.03f, 0.04f, 0.06f, 1f);
        [SerializeField] private Color dangerCameraBackgroundColor = new(0.11f, 0.035f, 0.05f, 1f);
        [SerializeField] private Color calmFogTint = new(0.031f, 0.039f, 0.055f, 1f);
        [SerializeField] private Color dangerFogTint = new(0.075f, 0.025f, 0.03f, 1f);
        [SerializeField, Range(0.65f, 1.35f)] private float calmFogHiddenAlphaMultiplier = 1f;
        [SerializeField, Range(0.65f, 1.35f)] private float dangerFogHiddenAlphaMultiplier = 1.1f;
        [SerializeField, Range(0.4f, 1.3f)] private float calmFogVisibleAlphaMultiplier = 1f;
        [SerializeField, Range(0.4f, 1.3f)] private float dangerFogVisibleAlphaMultiplier = 0.78f;
        [SerializeField] private bool enableThreatPulseImpulse = true;
        [SerializeField, Range(0f, 1f)] private float pulsePressureThreshold = 0.78f;
        [SerializeField, Range(0f, 1f)] private float pulsePressureDeltaThreshold = 0.12f;
        [SerializeField, Min(0.05f)] private float pulseCooldownSeconds = 1.25f;
        [SerializeField, Min(0f)] private float pulseImpulseAmplitude = 0.14f;
        [SerializeField, Min(0.05f)] private float pulseImpulseDuration = 0.18f;

        [Header("Fog Tuning")]
        [SerializeField, Range(0.45f, 2.2f)] private float minFogRevealRadiusMultiplier = 0.95f;
        [SerializeField, Range(0.45f, 2.2f)] private float maxFogRevealRadiusMultiplier = 1.22f;
        [SerializeField, Range(0.45f, 2.2f)] private float minFogSoftnessMultiplier = 0.88f;
        [SerializeField, Range(0.45f, 2.2f)] private float maxFogSoftnessMultiplier = 1.2f;
        [SerializeField, Range(0.45f, 2.4f)] private float minFogFlashlightRangeMultiplier = 0.92f;
        [SerializeField, Range(0.45f, 2.4f)] private float maxFogFlashlightRangeMultiplier = 1.26f;
        [SerializeField, Range(0.2f, 2.4f)] private float minFogRefogMultiplier = 0.75f;
        [SerializeField, Range(0.2f, 2.4f)] private float maxFogRefogMultiplier = 1.08f;

        [Header("Enemy Perception Tuning")]
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemyVisionMultiplier = 0.95f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemyVisionMultiplier = 1.24f;
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemyHearingMultiplier = 0.94f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemyHearingMultiplier = 1.24f;
        [SerializeField, Range(0.35f, 2.5f)] private float minEnemySuspicionGainMultiplier = 0.9f;
        [SerializeField, Range(0.35f, 2.5f)] private float maxEnemySuspicionGainMultiplier = 1.2f;

        [Header("Chase Readability Tuning")]
        [SerializeField, Range(0.55f, 1.8f)] private float minTransitionDurationMultiplier = 1.14f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxTransitionDurationMultiplier = 0.82f;
        [SerializeField, Range(0.6f, 1.9f)] private float minTransitionPulseSpeedMultiplier = 0.92f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxTransitionPulseSpeedMultiplier = 1.28f;
        [SerializeField, Range(0.6f, 1.9f)] private float minTransitionFlashStrengthMultiplier = 0.9f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxTransitionFlashStrengthMultiplier = 1.32f;
        [SerializeField, Range(0.55f, 1.8f)] private float minDisengageCueDurationMultiplier = 1.12f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxDisengageCueDurationMultiplier = 0.86f;
        [SerializeField, Range(0.55f, 1.8f)] private float minDisengageGraceMultiplier = 1.12f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxDisengageGraceMultiplier = 0.84f;
        [SerializeField, Range(0.6f, 1.9f)] private float minChaseBlinkSpeedMultiplier = 0.96f;
        [SerializeField, Range(0.6f, 1.9f)] private float maxChaseBlinkSpeedMultiplier = 1.26f;

        [Header("Playtest Envelope")]
        [SerializeField] private bool useStageReadabilityEnvelope = true;
        [SerializeField, Min(1)] private int envelopeStageEarly = 3;
        [SerializeField, Min(2)] private int envelopeStageMid = 5;
        [SerializeField, Min(3)] private int envelopeStageLate = 7;
        [SerializeField, Range(0f, 1f)] private float earlyReadabilityCap = 0.72f;
        [SerializeField, Range(0f, 1f)] private float midReadabilityCap = 0.86f;
        [SerializeField, Range(0f, 1f)] private float lateReadabilityCap = 1f;
        [SerializeField, Range(0.4f, 1.4f)] private float earlyChaseAggressionScale = 0.78f;
        [SerializeField, Range(0.4f, 1.4f)] private float midChaseAggressionScale = 0.9f;
        [SerializeField, Range(0.4f, 1.4f)] private float lateChaseAggressionScale = 1f;

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
        private float nextReferenceResolveTime;
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

        private void OnEnable()
        {
            ResolveReferences(force: true);
            SubscribeMap();
        }

        private void OnDisable()
        {
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

            float smoothFactor = responseSmoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Mathf.Max(0.0001f, responseSmoothing) * Mathf.Max(0.0001f, dt));
            currentReadabilityPressure = Mathf.Lerp(currentReadabilityPressure, rawPressure, smoothFactor);
            TryApplyThreatPulse(currentReadabilityPressure);

            lastEnemySampleCount = enemySampleCount;
            ApplyCameraTuning(currentReadabilityPressure, dt);
            ApplyFogTuning(currentReadabilityPressure);
            ApplyEnemyTuning(currentReadabilityPressure);

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

            float targetSize = Mathf.Clamp(
                baseSize + maxCameraZoomOut * pressure,
                Mathf.Max(1f, minimumCameraOrthoSize),
                Mathf.Max(Mathf.Max(1f, minimumCameraOrthoSize), maximumCameraOrthoSize));

            float zoomLerp = 1f - Mathf.Exp(-Mathf.Max(0.0001f, responseSmoothing) * Mathf.Max(0.0001f, dt));
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, zoomLerp);

            if (cameraFollow != null)
            {
                float lookAheadMultiplier = Mathf.Lerp(minLookAheadMultiplier, maxLookAheadMultiplier, pressure);
                float smoothMultiplier = Mathf.Lerp(minSmoothMultiplier, maxSmoothMultiplier, pressure);
                float lookAheadSmoothingMultiplier = Mathf.Lerp(minLookAheadSmoothingMultiplier, maxLookAheadSmoothingMultiplier, pressure);
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
            float refogMultiplier = Mathf.Lerp(maxFogRefogMultiplier, minFogRefogMultiplier, pressure);

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

            float lerp = 1f - Mathf.Exp(-Mathf.Max(0.1f, cameraBackgroundLerpSpeed) * Mathf.Max(0.0001f, dt));
            targetCamera.backgroundColor = Color.Lerp(targetCamera.backgroundColor, targetColor, lerp);
        }

        private void TryApplyThreatPulse(float pressure)
        {
            float delta = hasPreviousReadabilityPressure ? pressure - previousReadabilityPressure : 0f;
            previousReadabilityPressure = pressure;
            hasPreviousReadabilityPressure = true;

            if (!enableThreatPulseImpulse || cameraFollow == null)
            {
                return;
            }

            bool spikeDetected = pressure >= pulsePressureThreshold && delta >= pulsePressureDeltaThreshold;
            if (!spikeDetected || Time.time < nextAllowedPulseTime)
            {
                return;
            }

            float amplitude = Mathf.Max(0f, pulseImpulseAmplitude);
            if (amplitude <= 0f)
            {
                return;
            }

            cameraFollow.AddImpulse(amplitude, Mathf.Max(0.05f, pulseImpulseDuration));
            nextAllowedPulseTime = Time.time + Mathf.Max(0.05f, pulseCooldownSeconds);
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
                counted++;
            }

            sampledEnemyCount = counted;
            if (counted <= 0)
            {
                return 0f;
            }

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

            hasPreviousReadabilityPressure = false;
            previousReadabilityPressure = 0f;
            nextAllowedPulseTime = 0f;

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





