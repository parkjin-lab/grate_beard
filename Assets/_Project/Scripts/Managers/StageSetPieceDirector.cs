using System;
using System.Collections;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public enum StageSetPieceTier
    {
        None,
        Stage3ForkLure,
        Stage5SplitPressure,
        Stage7ExitSiege
    }

    public sealed class StageSetPieceDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private EnemySpawnDirector enemySpawnDirector;
        [SerializeField] private StagePressureDirector stagePressureDirector;
        [SerializeField] private GameplayRhythmDirector gameplayRhythmDirector;
        [SerializeField] private MapTuningDebugController mapTuningDebugController;
        [SerializeField] private Transform setPieceRoot;

        [Header("Flow")]
        [SerializeField] private bool enableSetPieces = true;
        [SerializeField] private bool applyOnStartIfMapExists = true;
        [SerializeField] private bool raiseRuntimeEvents = true;
        [SerializeField] private bool logSummary;

        [Header("Rhythm Alignment")]
        [SerializeField] private bool alignSetPiecesToRhythm = true;
        [SerializeField, Min(0f)] private float rhythmAlignmentMaxWaitSeconds = 12.5f;
        [SerializeField, Range(0f, 1f)] private float buildAlignmentMinProgress = 0.34f;
        [SerializeField, Range(0f, 1f)] private float spikeAlignmentMaxProgress = 0.44f;
        [SerializeField, Range(0.5f, 1.6f)] private float buildAlignedIntensityMultiplier = 1.08f;
        [SerializeField, Range(0.5f, 1.8f)] private float spikeAlignedIntensityMultiplier = 1.16f;

        [Header("Tier Stages")]
        [SerializeField, Min(1)] private int stage3Start = 3;
        [SerializeField, Min(2)] private int stage5Start = 5;
        [SerializeField, Min(3)] private int stage7Start = 7;

        [Header("Tier 3 - Fork Lure")]
        [SerializeField, Min(1)] private int stage3BeaconCount = 1;
        [SerializeField, Min(0)] private int stage3ReinforcementCount = 1;
        [SerializeField, Min(0.2f)] private float stage3BeaconLifetime = 18f;
        [SerializeField, Min(0.05f)] private float stage3PulseInterval = 0.82f;
        [SerializeField, Min(0.1f)] private float stage3PulseLoudness = 2.35f;
        [SerializeField, Min(0.1f)] private float stage3PulseRadius = 8.4f;
        [SerializeField] private Color stage3Color = new(1f, 0.72f, 0.2f, 0.9f);

        [Header("Tier 5 - Split Pressure")]
        [SerializeField, Min(1)] private int stage5BeaconCount = 2;
        [SerializeField, Min(0)] private int stage5ReinforcementCount = 2;
        [SerializeField, Min(0.2f)] private float stage5BeaconLifetime = 24f;
        [SerializeField, Min(0.05f)] private float stage5PulseInterval = 0.68f;
        [SerializeField, Min(0.1f)] private float stage5PulseLoudness = 2.8f;
        [SerializeField, Min(0.1f)] private float stage5PulseRadius = 9.6f;
        [SerializeField] private Color stage5Color = new(1f, 0.36f, 0.22f, 0.92f);

        [Header("Tier 7 - Exit Siege")]
        [SerializeField, Min(1)] private int stage7BeaconCount = 2;
        [SerializeField, Min(0)] private int stage7ReinforcementCount = 3;
        [SerializeField, Min(0.2f)] private float stage7BeaconLifetime = 30f;
        [SerializeField, Min(0.05f)] private float stage7PulseInterval = 0.56f;
        [SerializeField, Min(0.1f)] private float stage7PulseLoudness = 3.35f;
        [SerializeField, Min(0.1f)] private float stage7PulseRadius = 11.2f;
        [SerializeField] private Color stage7Color = new(1f, 0.2f, 0.2f, 0.94f);

        [Header("Runtime Tuning")]
        [SerializeField] private bool scaleByStagePressure = true;
        [SerializeField] private bool scaleByMapPreset = true;
        [SerializeField, Range(0.7f, 1.5f)] private float compactPresetIntensity = 0.92f;
        [SerializeField, Range(0.7f, 1.5f)] private float standardPresetIntensity = 1f;
        [SerializeField, Range(0.7f, 1.5f)] private float expansivePresetIntensity = 1.1f;
        [SerializeField, Range(0.7f, 1.5f)] private float minPressureIntensity = 0.88f;
        [SerializeField, Range(0.7f, 1.6f)] private float maxPressureIntensity = 1.22f;
        [SerializeField, Range(0.5f, 2f)] private float minBeaconCountMultiplier = 1f;
        [SerializeField, Range(0.5f, 2f)] private float maxBeaconCountMultiplier = 1.42f;
        [SerializeField, Min(0)] private int maxExtraBeacons = 1;
        [SerializeField, Range(0.5f, 2.4f)] private float minReinforcementCountMultiplier = 0.95f;
        [SerializeField, Range(0.5f, 2.4f)] private float maxReinforcementCountMultiplier = 1.52f;
        [SerializeField, Min(0)] private int maxExtraReinforcements = 2;
        [SerializeField, Range(0.4f, 1.4f)] private float minBeaconLifetimeMultiplier = 1.08f;
        [SerializeField, Range(0.4f, 1.4f)] private float maxBeaconLifetimeMultiplier = 0.82f;
        [SerializeField, Range(0.55f, 1.8f)] private float minPulseIntervalMultiplier = 1.1f;
        [SerializeField, Range(0.55f, 1.8f)] private float maxPulseIntervalMultiplier = 0.78f;
        [SerializeField, Range(0.55f, 2.2f)] private float minPulseLoudnessMultiplier = 0.94f;
        [SerializeField, Range(0.55f, 2.2f)] private float maxPulseLoudnessMultiplier = 1.3f;
        [SerializeField, Range(0.55f, 2.2f)] private float minPulseRadiusMultiplier = 0.94f;
        [SerializeField, Range(0.55f, 2.2f)] private float maxPulseRadiusMultiplier = 1.24f;

        [Header("Playtest Envelope")]
        [SerializeField] private bool useStageEnvelope = true;
        [SerializeField, Min(1)] private int envelopeStageEarly = 3;
        [SerializeField, Min(2)] private int envelopeStageMid = 5;
        [SerializeField, Min(3)] private int envelopeStageLate = 7;
        [SerializeField, Range(0.55f, 1.8f)] private float earlyIntensityFloor = 0.78f;
        [SerializeField, Range(0.55f, 1.8f)] private float earlyIntensityCap = 1.02f;
        [SerializeField, Range(0.55f, 1.8f)] private float midIntensityCap = 1.18f;
        [SerializeField, Range(0.55f, 1.8f)] private float lateIntensityCap = 1.34f;
        [SerializeField, Range(0f, 1f)] private float earlyTensionCap = 0.74f;
        [SerializeField, Range(0f, 1f)] private float midTensionCap = 0.88f;
        [SerializeField, Range(0f, 1f)] private float lateTensionCap = 0.98f;

        [Header("Placement")]
        [SerializeField, Min(0.15f)] private float beaconScaleRatio = 0.3f;
        [SerializeField, Min(0f)] private float reinforcementFocusRadius = 6.2f;
        [SerializeField, Range(0.55f, 2.2f)] private float minFocusRadiusMultiplier = 0.9f;
        [SerializeField, Range(0.55f, 2.2f)] private float maxFocusRadiusMultiplier = 1.28f;

        private Sprite debugSprite;
        private int generationToken;
        private StageSetPieceTier activeTier;
        private int activeBeaconCount;
        private int lastReinforcementCount;
        private int lastAppliedStage;
        private string lastBeatLabel = "None";
        private float lastRuntimePressure01;
        private float lastRuntimeTension01;
        private float lastRuntimeIntensity = 1f;
        private float lastRuntimePresetIntensity = 1f;
        private string lastRuntimePresetLabel = "Unknown";
        private float lastRuntimeBeaconLifetime;
        private float lastRuntimePulseInterval;
        private float lastRuntimePulseLoudness;
        private float lastRuntimePulseRadius;
        private float lastRuntimeFocusRadius;
        private string lastRhythmPhaseLabel = "Unknown";
        private string lastRhythmAlignmentLabel = "Unaligned";

        public StageSetPieceTier ActiveTier => activeTier;
        public int ActiveBeaconCount => activeBeaconCount;
        public int LastReinforcementCount => lastReinforcementCount;
        public int LastAppliedStage => lastAppliedStage;
        public string LastBeatLabel => string.IsNullOrWhiteSpace(lastBeatLabel) ? "None" : lastBeatLabel;
        public float LastRuntimePressure01 => lastRuntimePressure01;
        public float LastRuntimeTension01 => lastRuntimeTension01;
        public float LastRuntimeIntensity => lastRuntimeIntensity;
        public float LastRuntimePresetIntensity => lastRuntimePresetIntensity;
        public string LastRuntimePresetLabel => string.IsNullOrWhiteSpace(lastRuntimePresetLabel) ? "Unknown" : lastRuntimePresetLabel;
        public float LastRuntimeBeaconLifetime => lastRuntimeBeaconLifetime;
        public float LastRuntimePulseInterval => lastRuntimePulseInterval;
        public float LastRuntimePulseLoudness => lastRuntimePulseLoudness;
        public float LastRuntimePulseRadius => lastRuntimePulseRadius;
        public float LastRuntimeFocusRadius => lastRuntimeFocusRadius;
        public string LastRhythmPhaseLabel => string.IsNullOrWhiteSpace(lastRhythmPhaseLabel) ? "Unknown" : lastRhythmPhaseLabel;
        public string LastRhythmAlignmentLabel => string.IsNullOrWhiteSpace(lastRhythmAlignmentLabel) ? "Unaligned" : lastRhythmAlignmentLabel;

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeMap();
        }

        private void OnDisable()
        {
            UnsubscribeMap();
            ClearSetPieceObjects();
        }

        private void Start()
        {
            ResolveReferences();
            if (!applyOnStartIfMapExists || mapSystem == null || mapSystem.LastGeneratedCells.Count <= 0)
            {
                return;
            }

            QueueBuild(mapSystem.CurrentStage, mapSystem.LastGeneratedCells);
        }

        public void SetReferencesForEditor(
            MapSystem targetMapSystem,
            EnemySpawnDirector targetSpawnDirector,
            Transform targetRoot,
            StagePressureDirector targetPressureDirector = null,
            MapTuningDebugController targetMapTuning = null)
        {
            if (mapSystem != targetMapSystem)
            {
                UnsubscribeMap();
                mapSystem = targetMapSystem;
                SubscribeMap();
            }

            enemySpawnDirector = targetSpawnDirector;
            setPieceRoot = targetRoot;
            stagePressureDirector = targetPressureDirector;
            mapTuningDebugController = targetMapTuning;
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (enemySpawnDirector == null)
            {
                enemySpawnDirector = FindFirstObjectByType<EnemySpawnDirector>();
            }

            if (stagePressureDirector == null)
            {
                stagePressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (gameplayRhythmDirector == null)
            {
                gameplayRhythmDirector = FindFirstObjectByType<GameplayRhythmDirector>();
            }

            if (mapTuningDebugController == null)
            {
                mapTuningDebugController = FindFirstObjectByType<MapTuningDebugController>();
            }

            if (setPieceRoot == null)
            {
                setPieceRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/SetPieces");
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

        private void HandleMapGenerated(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            QueueBuild(stage, cells);
        }

        private void QueueBuild(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            generationToken++;
            StartCoroutine(BuildAfterFrame(generationToken, stage, cells));
        }

        private IEnumerator BuildAfterFrame(int token, int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            yield return null;
            if (token != generationToken)
            {
                yield break;
            }

            if (ShouldAlignSetPieceToRhythm(stage))
            {
                float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, rhythmAlignmentMaxWaitSeconds);
                while (token == generationToken && Time.realtimeSinceStartup < deadline && !IsRhythmAlignedForSetPiece())
                {
                    yield return null;
                }
            }

            if (token != generationToken)
            {
                yield break;
            }

            BuildSetPiece(stage, cells);
        }

        private void BuildSetPiece(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            ResolveReferences();
            ClearSetPieceObjects();

            lastAppliedStage = Mathf.Max(1, stage);
            activeTier = EvaluateTier(lastAppliedStage);
            if (!enableSetPieces || activeTier == StageSetPieceTier.None || cells == null || cells.Count <= 0)
            {
                lastBeatLabel = "None";
                ResetRuntimeTuningTelemetry();
                return;
            }

            EvaluateRuntimeTuning(
                lastAppliedStage,
                out float pressure01,
                out float tension01,
                out float runtimeIntensity,
                out float presetIntensity,
                out string presetLabel);

            lastRuntimePressure01 = pressure01;
            lastRuntimeTension01 = tension01;
            lastRuntimeIntensity = runtimeIntensity;
            lastRuntimePresetIntensity = presetIntensity;
            lastRuntimePresetLabel = presetLabel;
            ApplyRhythmAlignmentTuning(ref runtimeIntensity, ref tension01);
            runtimeIntensity = Mathf.Clamp(runtimeIntensity, 0.65f, 1.8f);
            lastRuntimeIntensity = runtimeIntensity;
            lastRuntimeTension01 = tension01;

            int beaconCount = EvaluateBeaconCount(activeTier, tension01, runtimeIntensity);
            int reinforcementCount = EvaluateReinforcementCount(activeTier, tension01, runtimeIntensity);
            float beaconLifetime = EvaluateBeaconLifetime(activeTier, tension01);
            float pulseInterval = EvaluatePulseInterval(activeTier, tension01);
            float pulseLoudness = EvaluatePulseLoudness(activeTier, tension01);
            float pulseRadius = EvaluatePulseRadius(activeTier, tension01);
            float focusRadius = EvaluateFocusRadius(tension01);

            lastRuntimeBeaconLifetime = beaconLifetime;
            lastRuntimePulseInterval = pulseInterval;
            lastRuntimePulseLoudness = pulseLoudness;
            lastRuntimePulseRadius = pulseRadius;
            lastRuntimeFocusRadius = focusRadius;
            lastBeatLabel = EvaluateBeatLabel(activeTier);
            if (!string.IsNullOrWhiteSpace(lastRhythmAlignmentLabel) && lastRhythmAlignmentLabel != "Unaligned")
            {
                lastBeatLabel = $"{lastBeatLabel}_{lastRhythmAlignmentLabel}";
            }

            List<GeneratedMapCell> selected = SelectSetPieceCells(activeTier, cells, beaconCount, lastAppliedStage);
            if (selected.Count <= 0)
            {
                if (logSummary)
                {
                    Debug.Log($"StageSetPiece: no valid cells at stage {lastAppliedStage}", this);
                }

                return;
            }

            Transform root = setPieceRoot != null ? setPieceRoot : transform;
            GameObject stageRootObject = new($"SetPiece_Stage_{lastAppliedStage:00}_{lastBeatLabel}");
            stageRootObject.transform.SetParent(root, false);
            Transform stageRoot = stageRootObject.transform;

            float cellSize = mapSystem != null ? Mathf.Max(0.1f, mapSystem.CellSize) : 1f;
            float beaconScale = Mathf.Max(0.16f, cellSize * Mathf.Max(0.15f, beaconScaleRatio));
            Color beaconColor = EvaluateTierColor(activeTier);

            Vector3 focusWorld = Vector3.zero;
            activeBeaconCount = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                GeneratedMapCell cell = selected[i];
                Vector3 world = ToWorld(cell.position);
                focusWorld += world;

                GameObject beacon = new($"SetPieceBeacon_{i:00}_{cell.kind}");
                beacon.transform.SetParent(stageRoot, false);
                beacon.transform.position = world;
                beacon.transform.localScale = Vector3.one * beaconScale;

                SpriteRenderer renderer = beacon.AddComponent<SpriteRenderer>();
                renderer.sprite = GetDebugSprite();
                renderer.color = beaconColor;
                renderer.sortingOrder = 30 + i;

                CircleCollider2D trigger = beacon.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = Mathf.Max(0.18f, beaconScale * 0.95f);

                DecoyEmitterDummy emitter = beacon.AddComponent<DecoyEmitterDummy>();
                emitter.Configure(
                    beaconLifetime,
                    pulseInterval * Mathf.Lerp(1f, 0.86f, i),
                    pulseLoudness,
                    pulseRadius);

                activeBeaconCount++;
            }

            focusWorld /= Mathf.Max(1, selected.Count);
            lastReinforcementCount = enemySpawnDirector != null
                ? enemySpawnDirector.SpawnSetPieceReinforcements(
                    lastAppliedStage,
                    reinforcementCount,
                    focusWorld,
                    focusRadius)
                : 0;

            if (raiseRuntimeEvents)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Stage,
                    BuildSetPieceShiftMessage(
                        lastBeatLabel,
                        activeBeaconCount,
                        lastReinforcementCount,
                        lastRuntimePresetLabel,
                        lastRuntimePressure01,
                        lastRuntimeTension01),
                    this,
                    lastAppliedStage,
                    semantic: RuntimeEventSemantic.SetPieceShift);
            }

            if (logSummary)
            {
                Debug.Log(
                    $"StageSetPiece: stage={lastAppliedStage}, tier={activeTier}, beacons={activeBeaconCount}, reinforcements={lastReinforcementCount}, beat={lastBeatLabel}, preset={lastRuntimePresetLabel}, pressure={lastRuntimePressure01:0.00}, tension={lastRuntimeTension01:0.00}, pulse={lastRuntimePulseInterval:0.00}/{lastRuntimePulseLoudness:0.00}/{lastRuntimePulseRadius:0.00}",
                    this);
            }
        }

        private void ClearSetPieceObjects()
        {
            activeBeaconCount = 0;
            lastReinforcementCount = 0;
            ResetRuntimeTuningTelemetry();

            if (setPieceRoot == null)
            {
                return;
            }

            for (int i = setPieceRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = setPieceRoot.GetChild(i);
                DestroySafe(child.gameObject);
            }
        }

        private void ResetRuntimeTuningTelemetry()
        {
            lastRuntimePressure01 = 0f;
            lastRuntimeTension01 = 0f;
            lastRuntimeIntensity = 1f;
            lastRuntimePresetIntensity = 1f;
            lastRuntimePresetLabel = "None";
            lastRuntimeBeaconLifetime = 0f;
            lastRuntimePulseInterval = 0f;
            lastRuntimePulseLoudness = 0f;
            lastRuntimePulseRadius = 0f;
            lastRuntimeFocusRadius = 0f;
            lastRhythmPhaseLabel = gameplayRhythmDirector != null ? gameplayRhythmDirector.CurrentPhaseLabel : "Unknown";
            lastRhythmAlignmentLabel = "Unaligned";
        }

        private StageSetPieceTier EvaluateTier(int stage)
        {
            int s3 = Mathf.Max(1, stage3Start);
            int s5 = Mathf.Max(s3 + 1, stage5Start);
            int s7 = Mathf.Max(s5 + 1, stage7Start);
            int safeStage = Mathf.Max(1, stage);

            if (safeStage >= s7)
            {
                return StageSetPieceTier.Stage7ExitSiege;
            }

            if (safeStage >= s5)
            {
                return StageSetPieceTier.Stage5SplitPressure;
            }

            if (safeStage >= s3)
            {
                return StageSetPieceTier.Stage3ForkLure;
            }

            return StageSetPieceTier.None;
        }

        private static string EvaluateBeatLabel(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => "ForkLure",
                StageSetPieceTier.Stage5SplitPressure => "SplitPressure",
                StageSetPieceTier.Stage7ExitSiege => "ExitSiege",
                _ => "None"
            };
        }

        private static string BuildSetPieceShiftMessage(
            string beatLabel,
            int beaconCount,
            int reinforcementCount,
            string presetLabel,
            float pressure01,
            float tension01)
        {
            string label = LocalizeSetPieceBeatLabel(beatLabel);
            string preset = string.IsNullOrWhiteSpace(presetLabel) ? "기본" : presetLabel;
            return $"사건 전조 {label}: 표식 {Mathf.Max(0, beaconCount)}, 증원 {Mathf.Max(0, reinforcementCount)}, 조율 {preset} 압박 {Mathf.Clamp01(pressure01):0.00}/긴장 {Mathf.Clamp01(tension01):0.00}";
        }

        private static string LocalizeSetPieceBeatLabel(string beatLabel)
        {
            if (string.IsNullOrWhiteSpace(beatLabel))
            {
                return "미정";
            }

            string localized = beatLabel
                .Replace("ForkLure", "갈림길 유혹")
                .Replace("SplitPressure", "분산 압박")
                .Replace("ExitSiege", "출구 포위")
                .Replace("BuildCrest", "고조 정점")
                .Replace("SpikeEntry", "급습 진입")
                .Replace("Unaligned", "즉시 발동")
                .Replace("None", "미정")
                .Replace("_", " / ");

            return localized.Trim();
        }

        private bool ShouldAlignSetPieceToRhythm(int stage)
        {
            if (!alignSetPiecesToRhythm || rhythmAlignmentMaxWaitSeconds <= 0f)
            {
                return false;
            }

            ResolveReferences();
            return gameplayRhythmDirector != null && EvaluateTier(stage) != StageSetPieceTier.None;
        }

        private bool IsRhythmAlignedForSetPiece()
        {
            if (gameplayRhythmDirector == null)
            {
                lastRhythmPhaseLabel = "Unknown";
                lastRhythmAlignmentLabel = "Unaligned";
                return true;
            }

            GameplayRhythmPhase phase = gameplayRhythmDirector.CurrentPhase;
            float progress = gameplayRhythmDirector.CurrentPhaseProgress;
            lastRhythmPhaseLabel = gameplayRhythmDirector.CurrentPhaseLabel;

            if (phase == GameplayRhythmPhase.Build && progress >= buildAlignmentMinProgress)
            {
                lastRhythmAlignmentLabel = "BuildCrest";
                return true;
            }

            if (phase == GameplayRhythmPhase.Spike && progress <= spikeAlignmentMaxProgress)
            {
                lastRhythmAlignmentLabel = "SpikeEntry";
                return true;
            }

            lastRhythmAlignmentLabel = "Waiting";
            return false;
        }

        private void ApplyRhythmAlignmentTuning(ref float runtimeIntensity, ref float tension01)
        {
            if (gameplayRhythmDirector == null)
            {
                lastRhythmPhaseLabel = "Unknown";
                lastRhythmAlignmentLabel = "Unaligned";
                return;
            }

            GameplayRhythmPhase phase = gameplayRhythmDirector.CurrentPhase;
            float progress = gameplayRhythmDirector.CurrentPhaseProgress;
            lastRhythmPhaseLabel = gameplayRhythmDirector.CurrentPhaseLabel;

            if (phase == GameplayRhythmPhase.Build && progress >= buildAlignmentMinProgress)
            {
                lastRhythmAlignmentLabel = "BuildCrest";
                runtimeIntensity *= Mathf.Max(0.5f, buildAlignedIntensityMultiplier);
                tension01 = Mathf.Clamp01(tension01 + 0.08f);
                return;
            }

            if (phase == GameplayRhythmPhase.Spike && progress <= spikeAlignmentMaxProgress)
            {
                lastRhythmAlignmentLabel = "SpikeEntry";
                runtimeIntensity *= Mathf.Max(0.5f, spikeAlignedIntensityMultiplier);
                tension01 = Mathf.Clamp01(tension01 + 0.12f);
                return;
            }

            lastRhythmAlignmentLabel = "Fallback";
        }

        private int EvaluateBeaconCount(StageSetPieceTier tier, float tension01, float runtimeIntensity)
        {
            int baseCount = EvaluateBaseBeaconCount(tier);
            if (baseCount <= 0)
            {
                return 0;
            }

            float intensityBias = Mathf.Lerp(0.92f, 1.12f, Mathf.Clamp01(Mathf.InverseLerp(0.85f, 1.2f, runtimeIntensity)));
            float multiplier = Mathf.Lerp(minBeaconCountMultiplier, maxBeaconCountMultiplier, tension01) * intensityBias;
            int count = ScaleCount(baseCount, multiplier, 1, maxExtraBeacons);
            if (tier == StageSetPieceTier.Stage3ForkLure)
            {
                count += 1;
            }

            return count;
        }

        private int EvaluateReinforcementCount(StageSetPieceTier tier, float tension01, float runtimeIntensity)
        {
            int baseCount = EvaluateBaseReinforcementCount(tier);
            if (baseCount <= 0)
            {
                return 0;
            }

            float intensityBias = Mathf.Lerp(0.9f, 1.2f, Mathf.Clamp01(Mathf.InverseLerp(0.85f, 1.2f, runtimeIntensity)));
            float multiplier = Mathf.Lerp(minReinforcementCountMultiplier, maxReinforcementCountMultiplier, tension01) * intensityBias;
            int count = ScaleCount(baseCount, multiplier, 0, maxExtraReinforcements);
            if (tier == StageSetPieceTier.Stage3ForkLure)
            {
                count += 1;
            }

            return count;
        }

        private float EvaluateBeaconLifetime(StageSetPieceTier tier, float tension01)
        {
            float baseLifetime = EvaluateBaseBeaconLifetime(tier);
            float multiplier = Mathf.Lerp(minBeaconLifetimeMultiplier, maxBeaconLifetimeMultiplier, tension01);
            return Mathf.Max(0.2f, baseLifetime * multiplier);
        }

        private float EvaluatePulseInterval(StageSetPieceTier tier, float tension01)
        {
            float baseInterval = EvaluateBasePulseInterval(tier);
            float multiplier = Mathf.Lerp(minPulseIntervalMultiplier, maxPulseIntervalMultiplier, tension01);
            return Mathf.Max(0.05f, baseInterval * multiplier);
        }

        private float EvaluatePulseLoudness(StageSetPieceTier tier, float tension01)
        {
            float baseLoudness = EvaluateBasePulseLoudness(tier);
            float multiplier = Mathf.Lerp(minPulseLoudnessMultiplier, maxPulseLoudnessMultiplier, tension01);
            return Mathf.Max(0.1f, baseLoudness * multiplier);
        }

        private float EvaluatePulseRadius(StageSetPieceTier tier, float tension01)
        {
            float baseRadius = EvaluateBasePulseRadius(tier);
            float multiplier = Mathf.Lerp(minPulseRadiusMultiplier, maxPulseRadiusMultiplier, tension01);
            return Mathf.Max(0.1f, baseRadius * multiplier);
        }

        private float EvaluateFocusRadius(float tension01)
        {
            float multiplier = Mathf.Lerp(minFocusRadiusMultiplier, maxFocusRadiusMultiplier, tension01);
            return Mathf.Max(0f, reinforcementFocusRadius * multiplier);
        }

        private int EvaluateBaseBeaconCount(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(1, stage3BeaconCount),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(1, stage5BeaconCount),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(1, stage7BeaconCount),
                _ => 0
            };
        }

        private int EvaluateBaseReinforcementCount(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(0, stage3ReinforcementCount),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(0, stage5ReinforcementCount),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(0, stage7ReinforcementCount),
                _ => 0
            };
        }

        private float EvaluateBaseBeaconLifetime(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(0.2f, stage3BeaconLifetime),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(0.2f, stage5BeaconLifetime),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(0.2f, stage7BeaconLifetime),
                _ => 10f
            };
        }

        private float EvaluateBasePulseInterval(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(0.05f, stage3PulseInterval),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(0.05f, stage5PulseInterval),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(0.05f, stage7PulseInterval),
                _ => 0.8f
            };
        }

        private float EvaluateBasePulseLoudness(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(0.1f, stage3PulseLoudness),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(0.1f, stage5PulseLoudness),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(0.1f, stage7PulseLoudness),
                _ => 2f
            };
        }

        private float EvaluateBasePulseRadius(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => Mathf.Max(0.1f, stage3PulseRadius),
                StageSetPieceTier.Stage5SplitPressure => Mathf.Max(0.1f, stage5PulseRadius),
                StageSetPieceTier.Stage7ExitSiege => Mathf.Max(0.1f, stage7PulseRadius),
                _ => 7f
            };
        }

        private Color EvaluateTierColor(StageSetPieceTier tier)
        {
            return tier switch
            {
                StageSetPieceTier.Stage3ForkLure => stage3Color,
                StageSetPieceTier.Stage5SplitPressure => stage5Color,
                StageSetPieceTier.Stage7ExitSiege => stage7Color,
                _ => Color.white
            };
        }

        private void EvaluateRuntimeTuning(
            int stage,
            out float pressure01,
            out float tension01,
            out float runtimeIntensity,
            out float presetIntensity,
            out string presetLabel)
        {
            pressure01 = EvaluatePressure01(stage);
            presetIntensity = EvaluatePresetIntensity(out presetLabel);

            float pressureIntensity = scaleByStagePressure
                ? Mathf.Lerp(minPressureIntensity, maxPressureIntensity, pressure01)
                : 1f;
            runtimeIntensity = Mathf.Clamp(presetIntensity * pressureIntensity, 0.65f, 1.8f);
            if (useStageEnvelope)
            {
                EvaluateStageEnvelope(stage, out float envelopeMinIntensity, out float envelopeMaxIntensity, out _);
                runtimeIntensity = Mathf.Clamp(runtimeIntensity, envelopeMinIntensity, envelopeMaxIntensity);
            }

            float presetT = Mathf.Clamp01(Mathf.InverseLerp(0.85f, 1.2f, presetIntensity));
            float intensityT = Mathf.Clamp01(Mathf.InverseLerp(0.85f, 1.25f, runtimeIntensity));
            tension01 = Mathf.Clamp01(pressure01 * 0.68f + presetT * 0.18f + intensityT * 0.14f);
            if (useStageEnvelope)
            {
                EvaluateStageEnvelope(stage, out _, out _, out float tensionCap);
                tension01 = Mathf.Min(tension01, tensionCap);
            }
        }

        private void EvaluateStageEnvelope(int stage, out float minIntensity, out float maxIntensity, out float tensionCap)
        {
            int early = Mathf.Max(1, envelopeStageEarly);
            int mid = Mathf.Max(early + 1, envelopeStageMid);
            int late = Mathf.Max(mid + 1, envelopeStageLate);
            int safeStage = Mathf.Max(1, stage);

            float earlyToMid = Mathf.Clamp01(Mathf.InverseLerp(early, mid, safeStage));
            float midToLate = Mathf.Clamp01(Mathf.InverseLerp(mid, late, safeStage));
            maxIntensity = safeStage <= mid
                ? Mathf.Lerp(earlyIntensityCap, midIntensityCap, earlyToMid)
                : Mathf.Lerp(midIntensityCap, lateIntensityCap, midToLate);

            minIntensity = Mathf.Min(maxIntensity, Mathf.Max(0.55f, earlyIntensityFloor));

            tensionCap = safeStage <= mid
                ? Mathf.Lerp(earlyTensionCap, midTensionCap, earlyToMid)
                : Mathf.Lerp(midTensionCap, lateTensionCap, midToLate);
            tensionCap = Mathf.Clamp01(tensionCap);
        }

        private float EvaluatePressure01(int stage)
        {
            if (stagePressureDirector != null)
            {
                return Mathf.Clamp01(stagePressureDirector.CurrentPressure01);
            }

            int safeStage = Mathf.Max(1, stage);
            float fallback = Mathf.InverseLerp(1f, 8f, safeStage);
            return Mathf.SmoothStep(0f, 1f, fallback);
        }

        private float EvaluatePresetIntensity(out string presetLabel)
        {
            if (!scaleByMapPreset)
            {
                presetLabel = "Disabled";
                return 1f;
            }

            if (mapTuningDebugController != null)
            {
                presetLabel = mapTuningDebugController.ActivePresetLabel;
                return mapTuningDebugController.ActivePreset switch
                {
                    MapTuningPreset.Compact => compactPresetIntensity,
                    MapTuningPreset.Expansive => expansivePresetIntensity,
                    _ => standardPresetIntensity
                };
            }

            if (mapSystem != null && TryResolvePreset(mapSystem.LastHookPresetLabel, out MapTuningPreset fallbackPreset))
            {
                presetLabel = mapSystem.LastHookPresetLabel;
                return fallbackPreset switch
                {
                    MapTuningPreset.Compact => compactPresetIntensity,
                    MapTuningPreset.Expansive => expansivePresetIntensity,
                    _ => standardPresetIntensity
                };
            }

            presetLabel = "Unknown";
            return standardPresetIntensity;
        }

        private static bool TryResolvePreset(string label, out MapTuningPreset preset)
        {
            preset = MapTuningPreset.Standard;
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            if (label.Equals("Compact", StringComparison.OrdinalIgnoreCase))
            {
                preset = MapTuningPreset.Compact;
                return true;
            }

            if (label.Equals("Expansive", StringComparison.OrdinalIgnoreCase))
            {
                preset = MapTuningPreset.Expansive;
                return true;
            }

            if (label.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                preset = MapTuningPreset.Standard;
                return true;
            }

            return false;
        }

        private static int ScaleCount(int baseCount, float multiplier, int minValue, int maxAdditional)
        {
            if (baseCount <= 0)
            {
                return Mathf.Max(0, minValue);
            }

            int clampedMin = Mathf.Clamp(minValue, 0, baseCount);
            int maxAllowed = Mathf.Max(clampedMin, baseCount + Mathf.Max(0, maxAdditional));
            int scaled = Mathf.RoundToInt(baseCount * Mathf.Max(0f, multiplier));
            return Mathf.Clamp(scaled, clampedMin, maxAllowed);
        }

        private List<GeneratedMapCell> SelectSetPieceCells(StageSetPieceTier tier, IReadOnlyList<GeneratedMapCell> cells, int targetCount, int stage)
        {
            List<GeneratedMapCell> result = new();
            if (cells == null || cells.Count <= 0 || targetCount <= 0)
            {
                return result;
            }

            HashSet<Vector2Int> used = new();
            int seed = stage * 173 + cells.Count * 59;

            switch (tier)
            {
                case StageSetPieceTier.Stage3ForkLure:
                    if (TryPickCellByKinds(cells, used, seed + 11, new[] { MapCellKind.Fork, MapCellKind.Room, MapCellKind.Risk, MapCellKind.Corridor }, out GeneratedMapCell t3Cell)
                        && IsValidPlayableCell(t3Cell.kind))
                    {
                        result.Add(t3Cell);
                    }
                    break;

                case StageSetPieceTier.Stage5SplitPressure:
                    TryPickCellByKinds(cells, used, seed + 23, new[] { MapCellKind.Risk, MapCellKind.Fork, MapCellKind.Room }, out GeneratedMapCell t5a);
                    if (IsValidPlayableCell(t5a.kind))
                    {
                        result.Add(t5a);
                    }

                    TryPickCellByKinds(cells, used, seed + 31, new[] { MapCellKind.Fork, MapCellKind.Room, MapCellKind.Corridor, MapCellKind.Risk }, out GeneratedMapCell t5b);
                    if (IsValidPlayableCell(t5b.kind))
                    {
                        result.Add(t5b);
                    }
                    break;

                case StageSetPieceTier.Stage7ExitSiege:
                    TryPickCellByKinds(cells, used, seed + 41, new[] { MapCellKind.Exit, MapCellKind.Risk, MapCellKind.Fork, MapCellKind.Room }, out GeneratedMapCell t7a);
                    if (IsValidPlayableCell(t7a.kind))
                    {
                        result.Add(t7a);
                    }

                    TryPickCellByKinds(cells, used, seed + 53, new[] { MapCellKind.Risk, MapCellKind.Fork, MapCellKind.Room, MapCellKind.Corridor }, out GeneratedMapCell t7b);
                    if (IsValidPlayableCell(t7b.kind))
                    {
                        result.Add(t7b);
                    }
                    break;
            }

            int fallbackSeed = seed + 97;
            for (int i = result.Count; i < targetCount; i++)
            {
                if (!TryPickCellByKinds(cells, used, fallbackSeed + i * 17, new[] { MapCellKind.Risk, MapCellKind.Fork, MapCellKind.Room, MapCellKind.Corridor, MapCellKind.Hideout, MapCellKind.Exit }, out GeneratedMapCell extra))
                {
                    break;
                }

                if (IsValidPlayableCell(extra.kind))
                {
                    result.Add(extra);
                }
            }

            return result;
        }

        private static bool TryPickCellByKinds(
            IReadOnlyList<GeneratedMapCell> cells,
            HashSet<Vector2Int> used,
            int seed,
            IReadOnlyList<MapCellKind> priority,
            out GeneratedMapCell selected)
        {
            selected = default;
            if (cells == null || cells.Count <= 0 || priority == null || priority.Count <= 0)
            {
                return false;
            }

            List<GeneratedMapCell> candidates = new();
            for (int k = 0; k < priority.Count; k++)
            {
                MapCellKind kind = priority[k];
                candidates.Clear();

                for (int i = 0; i < cells.Count; i++)
                {
                    GeneratedMapCell cell = cells[i];
                    if (cell.kind != kind)
                    {
                        continue;
                    }

                    if (!IsValidPlayableCell(cell.kind) || used.Contains(cell.position))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }

                if (candidates.Count <= 0)
                {
                    continue;
                }

                int index = Mathf.Abs(seed + k * 31) % candidates.Count;
                selected = candidates[index];
                used.Add(selected.position);
                return true;
            }

            candidates.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                if (!IsValidPlayableCell(cell.kind) || used.Contains(cell.position))
                {
                    continue;
                }

                candidates.Add(cell);
            }

            if (candidates.Count <= 0)
            {
                return false;
            }

            int fallbackIndex = Mathf.Abs(seed * 17 + 13) % candidates.Count;
            selected = candidates[fallbackIndex];
            used.Add(selected.position);
            return true;
        }

        private static bool IsValidPlayableCell(MapCellKind kind)
        {
            return kind is MapCellKind.Corridor
                or MapCellKind.Room
                or MapCellKind.Fork
                or MapCellKind.Hideout
                or MapCellKind.Risk
                or MapCellKind.Exit;
        }

        private Vector3 ToWorld(Vector2Int cellPosition)
        {
            float cellSize = mapSystem != null ? mapSystem.CellSize : 1f;
            return new Vector3(cellPosition.x * cellSize, cellPosition.y * cellSize, 0f);
        }

        private Sprite GetDebugSprite()
        {
            if (debugSprite != null)
            {
                return debugSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "StageSetPieceDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "StageSetPieceDebugSprite";
            debugSprite.hideFlags = HideFlags.HideAndDontSave;
            return debugSprite;
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

        private static void DestroySafe(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}







