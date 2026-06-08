using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public enum GameplayRhythmPhase
    {
        Calm,
        Build,
        Spike,
        Release
    }

    [DefaultExecutionOrder(-203)]
    public sealed class GameplayRhythmDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private StagePressureDirector stagePressureDirector;
        [SerializeField] private ThreatReadabilityDirector threatReadabilityDirector;
        [SerializeField] private CameraFollow2D cameraFollow;

        [Header("Rhythm Cycle")]
        [SerializeField] private bool enableRuntimeRhythm = true;
        [SerializeField, Min(0.5f)] private float calmSeconds = 7.5f;
        [SerializeField, Min(0.5f)] private float buildSeconds = 10.5f;
        [SerializeField, Min(0.5f)] private float spikeSeconds = 3.4f;
        [SerializeField, Min(0.5f)] private float releaseSeconds = 5.2f;
        [SerializeField, Range(0f, 0.8f)] private float highPressureTempoBias = 0.32f;

        [Header("Pressure Shape")]
        [SerializeField, Range(0.45f, 1.1f)] private float calmPressureMultiplier = 0.78f;
        [SerializeField, Range(0.7f, 1.35f)] private float buildPressureMultiplier = 1.02f;
        [SerializeField, Range(0.9f, 1.65f)] private float spikePressureMultiplier = 1.2f;
        [SerializeField, Range(0.45f, 1.15f)] private float releasePressureMultiplier = 0.72f;
        [SerializeField, Range(0f, 0.18f)] private float spikePressureAdd = 0.06f;
        [SerializeField, Range(0f, 0.14f)] private float releasePressureSubtract = 0.04f;

        [Header("Feedback")]
        [SerializeField] private bool raiseRhythmEvents = true;
        [SerializeField] private bool raiseSpikeTellEvent = true;
        [SerializeField, Min(0.1f)] private float spikeTellLeadSeconds = 1.15f;
        [SerializeField] private bool raiseReleaseEndTellEvent = true;
        [SerializeField, Min(0.1f)] private float releaseEndTellLeadSeconds = 1.05f;
        [SerializeField] private bool grantReliefOnRelease = true;
        [SerializeField] private bool impulseOnSpike = true;
        [SerializeField, Range(0f, 0.18f)] private float spikeCameraImpulse = 0.08f;
        [SerializeField, Min(0.05f)] private float spikeImpulseDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float spikeClutchMinimumRemainingSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float referenceResolveInterval = 0.8f;

        private GameplayRhythmPhase currentPhase = GameplayRhythmPhase.Calm;
        private float phaseStartedAt;
        private float currentPhaseDuration;
        private float nextReferenceResolveTime;
        private int cycleCount;
        private string lastBeatLabel = "Calm";
        private float lastContextPressure;
        private float lastAppliedPressureMultiplier = 1f;
        private bool spikeTellRaisedThisBuild;
        private bool releaseEndTellRaisedThisRelease;

        public GameplayRhythmPhase CurrentPhase => currentPhase;
        public string CurrentPhaseLabel => currentPhase.ToString();
        public string LastBeatLabel => string.IsNullOrWhiteSpace(lastBeatLabel) ? currentPhase.ToString() : lastBeatLabel;
        public int CycleCount => Mathf.Max(0, cycleCount);
        public float CurrentPhaseDuration => Mathf.Max(0.1f, currentPhaseDuration);
        public float CurrentPhaseElapsed => Mathf.Max(0f, Time.realtimeSinceStartup - phaseStartedAt);
        public float CurrentPhaseProgress => Mathf.Clamp01(CurrentPhaseElapsed / CurrentPhaseDuration);
        public float CurrentTempo01 => EvaluateTempo01(currentPhase, CurrentPhaseProgress);
        public float CurrentRhythmIntensity => EvaluateRhythmIntensity(currentPhase, CurrentPhaseProgress);
        public float CurrentContextPressure => lastContextPressure;
        public float CurrentPressureMultiplier => lastAppliedPressureMultiplier;
        public bool RuntimeRhythmEnabled => enableRuntimeRhythm;

        private void OnEnable()
        {
            ResolveReferences(force: true);
            SubscribeMap();
            EnterPhase(GameplayRhythmPhase.Calm, raiseEvent: false, resetCycle: true);
        }

        private void OnDisable()
        {
            UnsubscribeMap();
        }

        private void Update()
        {
            if (!Application.isPlaying || !enableRuntimeRhythm || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            ResolveReferences();
            lastContextPressure = EvaluateContextPressure();
            lastAppliedPressureMultiplier = GetPressureMultiplierForPhase(currentPhase);

            if (CurrentPhaseElapsed < currentPhaseDuration)
            {
                TryRaiseSpikeTell();
                TryRaiseReleaseEndTell();
                return;
            }

            EnterPhase(GetNextPhase(currentPhase), raiseEvent: true, resetCycle: false);
        }

        public void SetReferencesForEditor(
            MapSystem targetMapSystem,
            StagePressureDirector targetPressure,
            ThreatReadabilityDirector targetReadability,
            CameraFollow2D targetCameraFollow)
        {
            if (mapSystem != targetMapSystem)
            {
                UnsubscribeMap();
                mapSystem = targetMapSystem;
                SubscribeMap();
            }

            stagePressureDirector = targetPressure;
            threatReadabilityDirector = targetReadability;
            cameraFollow = targetCameraFollow;
        }

        public float ApplyPressureRhythmForRuntime(float basePressure)
        {
            if (!enableRuntimeRhythm)
            {
                return Mathf.Clamp01(basePressure);
            }

            float shaped = Mathf.Clamp01(basePressure) * GetPressureMultiplierForPhase(currentPhase);
            if (currentPhase == GameplayRhythmPhase.Spike)
            {
                shaped += spikePressureAdd;
            }
            else if (currentPhase == GameplayRhythmPhase.Release)
            {
                shaped -= releasePressureSubtract;
            }

            return Mathf.Clamp01(shaped);
        }

        public float GetPressureMultiplierForPhase(GameplayRhythmPhase phase)
        {
            return phase switch
            {
                GameplayRhythmPhase.Calm => calmPressureMultiplier,
                GameplayRhythmPhase.Build => buildPressureMultiplier,
                GameplayRhythmPhase.Spike => spikePressureMultiplier,
                GameplayRhythmPhase.Release => releasePressureMultiplier,
                _ => 1f
            };
        }

        public void ForceSetPhaseForRuntime(GameplayRhythmPhase phase, bool raiseEvent = false)
        {
            EnterPhase(phase, raiseEvent, resetCycle: false);
        }

        public bool TryAdvanceSpikeTowardRelease(float seconds, out float appliedSeconds, string reason = null)
        {
            appliedSeconds = 0f;
            if (!Application.isPlaying
                || !enableRuntimeRhythm
                || RegressionChecklistRunner.IsRegressionRunActive
                || currentPhase != GameplayRhythmPhase.Spike)
            {
                return false;
            }

            float safeSeconds = Mathf.Max(0f, seconds);
            float remaining = Mathf.Max(0f, currentPhaseDuration - CurrentPhaseElapsed);
            float minimumRemaining = Mathf.Max(0.05f, spikeClutchMinimumRemainingSeconds);
            float advance = Mathf.Min(safeSeconds, Mathf.Max(0f, remaining - minimumRemaining));
            if (advance <= 0.01f)
            {
                return false;
            }

            phaseStartedAt -= advance;
            appliedSeconds = advance;

            RuntimeEventBus.Raise(
                RuntimeEventType.Stage,
                string.IsNullOrWhiteSpace(reason)
                    ? $"Spike clutch release advance {advance:0.0}s"
                    : $"Spike clutch {reason} (-{advance:0.0}s)",
                this,
                mapSystem != null ? mapSystem.CurrentStage : 0,
                semantic: RuntimeEventSemantic.EscapeRelief);

            return true;
        }

        private void EnterPhase(GameplayRhythmPhase phase, bool raiseEvent, bool resetCycle)
        {
            if (resetCycle)
            {
                cycleCount = 0;
            }
            else if (currentPhase == GameplayRhythmPhase.Release && phase == GameplayRhythmPhase.Calm)
            {
                cycleCount++;
            }

            currentPhase = phase;
            phaseStartedAt = Time.realtimeSinceStartup;
            spikeTellRaisedThisBuild = false;
            releaseEndTellRaisedThisRelease = false;
            lastAppliedPressureMultiplier = GetPressureMultiplierForPhase(phase);
            lastBeatLabel = BuildBeatLabel(phase);

            if (Application.isPlaying)
            {
                stagePressureDirector?.ApplyPressureNow(rebuildEnemies: false, raiseEvent: false);
                threatReadabilityDirector?.ApplyNowForEditor();
            }

            lastContextPressure = EvaluateContextPressure();
            currentPhaseDuration = EvaluatePhaseDuration(phase, lastContextPressure);

            if (phase == GameplayRhythmPhase.Spike && impulseOnSpike && cameraFollow != null)
            {
                cameraFollow.AddImpulse(spikeCameraImpulse, spikeImpulseDuration);
            }
            else if (phase == GameplayRhythmPhase.Release && grantReliefOnRelease)
            {
                threatReadabilityDirector?.TryGrantRhythmReleaseRelief(
                    lastContextPressure,
                    mapSystem != null ? mapSystem.CurrentStage : 0);
            }

            if (!raiseEvent || !raiseRhythmEvents || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            RuntimeEventBus.Raise(
                RuntimeEventType.Stage,
                $"Rhythm {lastBeatLabel} ({currentPhaseDuration:0.0}s)",
                this,
                mapSystem != null ? mapSystem.CurrentStage : 0,
                semantic: RuntimeEventSemantic.RhythmShift);
        }

        private float EvaluatePhaseDuration(GameplayRhythmPhase phase, float pressure)
        {
            float tempo = Mathf.Clamp01(pressure) * Mathf.Clamp01(highPressureTempoBias);
            float multiplier = phase switch
            {
                GameplayRhythmPhase.Calm => Mathf.Lerp(1f, 0.78f, tempo),
                GameplayRhythmPhase.Build => Mathf.Lerp(1f, 0.72f, tempo),
                GameplayRhythmPhase.Spike => Mathf.Lerp(1f, 1.18f, tempo),
                GameplayRhythmPhase.Release => Mathf.Lerp(1f, 0.86f, tempo),
                _ => 1f
            };

            float baseDuration = phase switch
            {
                GameplayRhythmPhase.Calm => calmSeconds,
                GameplayRhythmPhase.Build => buildSeconds,
                GameplayRhythmPhase.Spike => spikeSeconds,
                GameplayRhythmPhase.Release => releaseSeconds,
                _ => calmSeconds
            };

            return Mathf.Max(0.5f, baseDuration * multiplier);
        }

        private float EvaluateContextPressure()
        {
            float pressure = stagePressureDirector != null ? stagePressureDirector.CurrentPressure01 : 0f;
            float readability = threatReadabilityDirector != null ? threatReadabilityDirector.CurrentReadabilityPressure : pressure;
            return Mathf.Clamp01((pressure * 0.58f) + (readability * 0.42f));
        }

        private static GameplayRhythmPhase GetNextPhase(GameplayRhythmPhase phase)
        {
            return phase switch
            {
                GameplayRhythmPhase.Calm => GameplayRhythmPhase.Build,
                GameplayRhythmPhase.Build => GameplayRhythmPhase.Spike,
                GameplayRhythmPhase.Spike => GameplayRhythmPhase.Release,
                GameplayRhythmPhase.Release => GameplayRhythmPhase.Calm,
                _ => GameplayRhythmPhase.Calm
            };
        }

        private static float EvaluateTempo01(GameplayRhythmPhase phase, float progress)
        {
            return phase switch
            {
                GameplayRhythmPhase.Calm => Mathf.Lerp(0.12f, 0.24f, progress),
                GameplayRhythmPhase.Build => Mathf.Lerp(0.32f, 0.72f, progress),
                GameplayRhythmPhase.Spike => 1f,
                GameplayRhythmPhase.Release => Mathf.Lerp(0.46f, 0.18f, progress),
                _ => 0f
            };
        }

        private static float EvaluateRhythmIntensity(GameplayRhythmPhase phase, float progress)
        {
            return phase switch
            {
                GameplayRhythmPhase.Calm => Mathf.Lerp(0.08f, 0.18f, progress),
                GameplayRhythmPhase.Build => Mathf.Lerp(0.28f, 0.74f, progress),
                GameplayRhythmPhase.Spike => Mathf.Lerp(0.9f, 1f, 1f - Mathf.Abs(progress - 0.5f) * 2f),
                GameplayRhythmPhase.Release => Mathf.Lerp(0.36f, 0.12f, progress),
                _ => 0f
            };
        }

        private static string BuildBeatLabel(GameplayRhythmPhase phase)
        {
            return phase switch
            {
                GameplayRhythmPhase.Calm => "Calm",
                GameplayRhythmPhase.Build => "Build",
                GameplayRhythmPhase.Spike => "Spike",
                GameplayRhythmPhase.Release => "Release",
                _ => "None"
            };
        }

        private void TryRaiseSpikeTell()
        {
            if (!Application.isPlaying
                || !raiseRhythmEvents
                || !raiseSpikeTellEvent
                || RegressionChecklistRunner.IsRegressionRunActive
                || currentPhase != GameplayRhythmPhase.Build
                || spikeTellRaisedThisBuild)
            {
                return;
            }

            float remaining = currentPhaseDuration - CurrentPhaseElapsed;
            if (remaining > Mathf.Max(0.1f, spikeTellLeadSeconds))
            {
                return;
            }

            spikeTellRaisedThisBuild = true;
            RuntimeEventBus.Raise(
                RuntimeEventType.Stage,
                $"Spike incoming ({remaining:0.0}s)",
                this,
                mapSystem != null ? mapSystem.CurrentStage : 0,
                semantic: RuntimeEventSemantic.LockOnWarning);
        }

        private void TryRaiseReleaseEndTell()
        {
            if (!Application.isPlaying
                || !raiseRhythmEvents
                || !raiseReleaseEndTellEvent
                || RegressionChecklistRunner.IsRegressionRunActive
                || currentPhase != GameplayRhythmPhase.Release
                || releaseEndTellRaisedThisRelease)
            {
                return;
            }

            float remaining = currentPhaseDuration - CurrentPhaseElapsed;
            if (remaining > Mathf.Max(0.1f, releaseEndTellLeadSeconds))
            {
                return;
            }

            releaseEndTellRaisedThisRelease = true;
            RuntimeEventBus.Raise(
                RuntimeEventType.Stage,
                $"Build returning ({remaining:0.0}s)",
                this,
                mapSystem != null ? mapSystem.CurrentStage : 0,
                semantic: RuntimeEventSemantic.RhythmShift);
        }

        private void HandleMapGenerated(int stage, System.Collections.Generic.IReadOnlyList<GeneratedMapCell> cells)
        {
            EnterPhase(GameplayRhythmPhase.Calm, raiseEvent: false, resetCycle: true);
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && Time.unscaledTime < nextReferenceResolveTime)
            {
                return;
            }

            nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, referenceResolveInterval);

            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
                SubscribeMap();
            }

            if (stagePressureDirector == null)
            {
                stagePressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (threatReadabilityDirector == null)
            {
                threatReadabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<CameraFollow2D>();
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
    }
}
