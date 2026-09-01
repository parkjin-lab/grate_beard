using System;
using System.Collections;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class StageLoopDirector : MonoBehaviour
    {
        public static StageLoopDirector Instance { get; private set; }

        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private GameplayRhythmDirector gameplayRhythmDirector;
        [SerializeField] private Transform pickupsRoot;
        [SerializeField] private Transform interactablesRoot;

        [Header("Breadcrumb Rules")]
        [SerializeField, Min(1)] private int baseBreadcrumbCount = 3;
        [SerializeField, Min(0)] private int breadcrumbIncreasePerStage = 1;
        [SerializeField, Min(1)] private int maxBreadcrumbCount = 14;
        [SerializeField, Min(0.05f)] private float breadcrumbCorridorWeight = 0.8f;
        [SerializeField, Min(0.05f)] private float breadcrumbRoomWeight = 1.35f;
        [SerializeField, Min(0.05f)] private float breadcrumbForkWeight = 1.25f;
        [SerializeField, Min(0.05f)] private float breadcrumbHideoutWeight = 0.55f;

        [Header("Breadcrumb Chain Echo")]
        [SerializeField] private bool showBreadcrumbChainEcho = true;
        [SerializeField, Min(0.1f)] private float breadcrumbChainEchoDuration = 0.72f;
        [SerializeField, Min(0.02f)] private float breadcrumbChainEchoWidth = 0.08f;
        [SerializeField, Min(0.2f)] private float breadcrumbChainEchoMaxDistance = 18f;
        [SerializeField] private Color breadcrumbChainEchoColor = new(1f, 0.86f, 0.22f, 0.86f);
        [SerializeField] private Color breadcrumbExitChainEchoColor = new(0.35f, 1f, 0.55f, 0.92f);
        [SerializeField] private int breadcrumbChainEchoSortingOrder = 32;
        [SerializeField] private bool emitBreadcrumbChainNoise = true;
        [SerializeField, Min(0f)] private float breadcrumbChainNoiseLoudness = 0.28f;
        [SerializeField, Min(0.1f)] private float breadcrumbChainNoiseRadius = 2.1f;

        [Header("Breadcrumb Momentum")]
        [SerializeField] private bool enableBreadcrumbMomentum = true;
        [SerializeField, Min(0.5f)] private float breadcrumbMomentumWindowSeconds = 5.6f;
        [SerializeField, Range(2, 6)] private int breadcrumbMomentumMaxLevel = 4;
        [SerializeField, Min(0f)] private float breadcrumbMomentumStaminaReward = 0.12f;
        [SerializeField, Min(0f)] private float breadcrumbMomentumStaminaRewardPerLevel = 0.08f;
        [SerializeField, Min(0f)] private float breadcrumbMomentumEchoDurationBonus = 0.24f;
        [SerializeField, Min(0f)] private float breadcrumbMomentumEchoWidthBonus = 0.12f;
        [SerializeField, Min(0f)] private float breadcrumbMomentumNoiseLoudnessBonus = 0.16f;
        [SerializeField, Min(0f)] private float breadcrumbMomentumNoiseRadiusBonus = 0.22f;
        [SerializeField] private Color breadcrumbMomentumPulseColor = new(1f, 0.76f, 0.18f, 0.46f);
        [SerializeField, Min(0.1f)] private float breadcrumbMomentumPulseRadius = 1.25f;
        [SerializeField, Min(0.1f)] private float breadcrumbMomentumPulseDuration = 1.05f;
        [SerializeField] private int breadcrumbMomentumPulseSortingOrder = 35;

        [Header("Corrupted Breadcrumb Echo")]
        [SerializeField] private bool enableCorruptedBreadcrumbEcho = true;
        [SerializeField, Min(1)] private int corruptedBreadcrumbStartStage = 5;
        [SerializeField, Range(0f, 1f)] private float corruptedBreadcrumbPressureThreshold = 0.18f;
        [SerializeField, Range(0f, 1f)] private float corruptedBreadcrumbBaseChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float corruptedBreadcrumbPressureChanceBonus = 0.24f;
        [SerializeField, Min(0.2f)] private float corruptedBreadcrumbCooldownSeconds = 7.5f;
        [SerializeField, Min(0.2f)] private float corruptedBreadcrumbMinDistance = 3.4f;
        [SerializeField, Min(0.2f)] private float corruptedBreadcrumbMaxDistance = 8.2f;
        [SerializeField, Range(0f, 1f)] private float corruptedBreadcrumbPickupBias = 0.34f;
        [SerializeField] private Color corruptedBreadcrumbEchoColor = new(0.26f, 0.56f, 1f, 0.52f);
        [SerializeField, Min(0.1f)] private float corruptedBreadcrumbEchoDuration = 1.18f;
        [SerializeField, Min(0.01f)] private float corruptedBreadcrumbEchoWidth = 0.055f;
        [SerializeField, Min(0f)] private float corruptedBreadcrumbWaverAmplitude = 0.32f;
        [SerializeField, Min(0.1f)] private float corruptedBreadcrumbFlickerSpeed = 12f;
        [SerializeField] private int corruptedBreadcrumbSortingOrder = 31;
        [SerializeField] private bool emitCorruptedBreadcrumbNoise = true;
        [SerializeField, Min(0f)] private float corruptedBreadcrumbNoiseLoudness = 0.36f;
        [SerializeField, Min(0.1f)] private float corruptedBreadcrumbNoiseRadius = 3.2f;

        [Header("Exit Unlock Pressure")]
        [SerializeField] private bool triggerExitUnlockPressure = true;
        [SerializeField, Min(0f)] private float exitUnlockPressureDelay = 0.8f;
        [SerializeField, Min(0f)] private float exitUnlockNoiseLoudness = 1.1f;
        [SerializeField, Min(0.1f)] private float exitUnlockNoiseRadius = 7f;
        [SerializeField, Min(0.2f)] private float exitUnlockBeaconRadius = 3.8f;
        [SerializeField, Min(0.1f)] private float exitUnlockBeaconDuration = 1.1f;
        [SerializeField, Range(1, 4)] private int exitUnlockBeaconRingCount = 2;
        [SerializeField] private Color exitUnlockBeaconColor = new(0.35f, 1f, 0.55f, 0.9f);
        [SerializeField] private int exitUnlockBeaconSortingOrder = 34;

        [Header("Exit Choice Cache")]
        [SerializeField] private bool spawnExitChoiceCache = true;
        [SerializeField, Min(1)] private int exitChoiceCacheStartStage = 1;
        [SerializeField, Range(0f, 1f)] private float exitChoiceCacheSpawnChance = 0.82f;
        [SerializeField, Min(0.1f)] private float exitChoiceCacheRecoverAmount = 1.05f;
        [SerializeField, Min(0.1f)] private float exitChoiceCacheMinDistanceFromExit = 5.4f;
        [SerializeField, Min(0f)] private float exitChoiceCacheMinDistanceFromPlayer = 3.2f;
        [SerializeField] private Color exitChoiceCacheColor = new(1f, 0.74f, 0.2f, 0.95f);
        [SerializeField, Min(0.1f)] private float exitChoiceCacheScale = 0.82f;
        [SerializeField, Min(0f)] private float exitChoiceCacheNoiseLoudness = 0.95f;
        [SerializeField, Min(0.1f)] private float exitChoiceCacheNoiseRadius = 6.2f;
        [SerializeField, Min(0.1f)] private float exitChoiceCacheBeaconRadius = 1.9f;
        [SerializeField, Min(0.1f)] private float exitChoiceCacheBeaconDuration = 1.45f;
        [SerializeField] private int exitChoiceCacheSortingOrder = 36;

        [Header("Exit Choice Carryover")]
        [SerializeField] private bool enableExitChoiceCarryover = true;
        [SerializeField, Min(0.05f)] private float exitChoiceCarryoverEchoDelay = 0.35f;
        [SerializeField, Range(2, 6)] private int exitChoiceCarryoverMomentumLevel = 3;

        [Header("Safe Haven")]
        [SerializeField] private bool spawnSafeHavens = true;
        [SerializeField, Min(0)] private int baseSafeHavenCount = 1;
        [SerializeField, Min(0)] private int safeHavenIncreasePerStage = 1;
        [SerializeField, Min(1)] private int safeHavenCountIncreaseStageInterval = 3;
        [SerializeField, Min(0)] private int maxSafeHavenCount = 4;
        [SerializeField, Min(0.05f)] private float safeHavenHideoutWeight = 1.4f;
        [SerializeField, Min(0.05f)] private float safeHavenRoomWeight = 0.35f;
        [SerializeField, Min(0.05f)] private float safeHavenForkWeight = 0.55f;
        [SerializeField, Min(0.1f)] private float safeHavenRadius = 0.72f;
        [SerializeField] private Color safeHavenColor = new(0.25f, 1f, 0.85f, 0.9f);

        [Header("Stamina Pickup Rules")]
        [SerializeField] private bool spawnStaminaPickups = true;
        [SerializeField, Min(0)] private int baseStaminaPickupCount = 1;
        [SerializeField, Min(0)] private int staminaPickupIncreasePerStage = 1;
        [SerializeField, Min(1)] private int staminaCountIncreaseStageInterval = 2;
        [SerializeField, Min(0)] private int maxStaminaPickupCount = 6;
        [SerializeField, Range(0f, 1f)] private float staminaPickupSpawnChance = 0.72f;
        [SerializeField, Min(0.1f)] private float staminaPickupRecoverAmount = 1.4f;
        [SerializeField] private Color staminaPickupColor = new(0.35f, 0.95f, 1f, 0.95f);
        [SerializeField, Min(0.05f)] private float staminaCorridorWeight = 1.2f;
        [SerializeField, Min(0.05f)] private float staminaRoomWeight = 1f;
        [SerializeField, Min(0.05f)] private float staminaForkWeight = 0.85f;
        [SerializeField, Min(0.05f)] private float staminaHideoutWeight = 0.55f;

        [Header("Risk Cache")]
        [SerializeField] private bool spawnRiskCaches = true;
        [SerializeField, Min(1)] private int riskCacheStartStage = 2;
        [SerializeField, Range(0f, 1f)] private float riskCacheSpawnChance = 0.72f;
        [SerializeField, Range(1, 3)] private int riskCacheMaxPerStage = 1;
        [SerializeField, Min(0.1f)] private float riskCacheStaminaRecoverAmount = 0.78f;
        [SerializeField, Min(0f)] private float riskCachePulseCooldownRefundSeconds = 1.65f;
        [SerializeField, Min(0f)] private float riskCacheNoiseLoudness = 1.18f;
        [SerializeField, Min(0.1f)] private float riskCacheNoiseRadius = 6.8f;
        [SerializeField, Min(0f)] private float riskCacheAftershockNoiseDelay = 0.55f;
        [SerializeField, Range(0f, 1f)] private float riskCacheAftershockNoiseScale = 0.52f;
        [SerializeField] private Color riskCacheColor = new(1f, 0.38f, 0.22f, 0.96f);
        [SerializeField, Min(0.1f)] private float riskCacheScale = 0.72f;
        [SerializeField] private int riskCacheSortingOrder = 36;

        [Header("Risk Cache Rhythm Wager")]
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheCalmRewardMultiplier = 0.92f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheBuildRewardMultiplier = 1.32f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheSpikeRewardMultiplier = 1.55f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheReleaseRewardMultiplier = 0.78f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheCalmNoiseMultiplier = 0.82f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheBuildNoiseMultiplier = 1.08f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheSpikeNoiseMultiplier = 1.48f;
        [SerializeField, Range(0.25f, 2.5f)] private float riskCacheReleaseNoiseMultiplier = 0.68f;

        [Header("Breadcrumb Rhythm Momentum")]
        [SerializeField, Range(0.25f, 2.5f)] private float breadcrumbCalmRewardMultiplier = 0.9f;
        [SerializeField, Range(0.25f, 2.5f)] private float breadcrumbBuildRewardMultiplier = 1.18f;
        [SerializeField, Range(0.25f, 2.5f)] private float breadcrumbSpikeRewardMultiplier = 1.34f;
        [SerializeField, Range(0.25f, 2.5f)] private float breadcrumbReleaseRewardMultiplier = 0.82f;
        [SerializeField, Range(0.5f, 2f)] private float breadcrumbBuildEchoDurationMultiplier = 1.22f;
        [SerializeField, Range(0.5f, 2f)] private float breadcrumbBuildEchoWidthMultiplier = 1.18f;
        [SerializeField, Range(0.5f, 2f)] private float breadcrumbBuildPulseRadiusMultiplier = 1.14f;
        [SerializeField, Range(0.5f, 2f)] private float breadcrumbBuildNoiseRadiusMultiplier = 1.12f;
        [SerializeField, Min(0f)] private float breadcrumbSpikeReleaseAdvanceSeconds = 0.72f;
        [SerializeField, Min(0f)] private float breadcrumbSpikeReleaseAdvancePerLevel = 0.18f;

        [Header("Late Stage Pressure")]
        [SerializeField, Min(1)] private int latePressureStartStage = 5;
        [SerializeField, Min(2)] private int latePressurePeakStage = 11;
        [SerializeField, Range(0f, 1f)] private float lateSafeHavenCountFloor = 0.35f;
        [SerializeField, Range(0f, 1f)] private float lateSafeHavenRadiusFloor = 0.75f;
        [SerializeField, Range(0f, 1f)] private float lateStaminaSpawnChanceFloor = 0.3f;
        [SerializeField, Range(0.25f, 1f)] private float lateStaminaRecoverFloor = 0.72f;
        [SerializeField, Min(0)] private int lateBreadcrumbBonusMax = 2;

        private readonly List<BreadcrumbPickup> activePickups = new();
        private readonly List<StaminaPickup> activeStaminaPickups = new();
        private readonly List<SafeHavenZone> activeSafeHavens = new();
        private readonly List<RiskCachePickup> activeRiskCaches = new();

        private ExitPortalDummy exitPortal;
        private bool lastExitUnlockedState;
        private Sprite debugSprite;
        private Material chainEchoMaterial;
        private Coroutine exitUnlockPressureRoutine;
        private float nextCorruptedBreadcrumbEchoTime;
        private PlayerDummyController momentumPlayer;
        private int breadcrumbMomentumLevel;
        private float lastBreadcrumbCollectRealtime = -999f;
        private StaminaPickup exitChoiceCachePickup;
        private Vector3 exitChoiceCachePosition;
        private bool exitChoiceCacheTakenThisStage;
        private bool pendingExitChoiceCarryover;
        private Coroutine exitChoiceCarryoverRoutine;
        private Coroutine riskCacheAftershockRoutine;
        private float lateHouseMood01;
        private bool lateHouseMoodApplied;

        public int CurrentStage { get; private set; } = 1;
        public int RequiredBreadcrumbs { get; private set; }
        public int CollectedBreadcrumbs { get; private set; }
        public bool ExitUnlocked => exitPortal != null && exitPortal.IsUnlocked;
        public int ActiveSafeHavenCount => activeSafeHavens.Count;
        public int ActiveStaminaPickupCount => activeStaminaPickups.Count;
        public bool HasBreadcrumbMomentum => BreadcrumbMomentumLevel > 1 && BreadcrumbMomentumRemaining > 0.05f;
        public int BreadcrumbMomentumLevel => BreadcrumbMomentumRemaining > 0.05f ? Mathf.Max(0, breadcrumbMomentumLevel) : 0;
        public int BreadcrumbMomentumMaxLevel => Mathf.Max(2, breadcrumbMomentumMaxLevel);
        public float BreadcrumbMomentumRemaining => enableBreadcrumbMomentum && breadcrumbMomentumLevel > 0
            ? Mathf.Max(0f, lastBreadcrumbCollectRealtime + Mathf.Max(0.5f, breadcrumbMomentumWindowSeconds) - Time.realtimeSinceStartup)
            : 0f;
        public int ActiveBreadcrumbCount => CountActiveBreadcrumbs();
        public bool ExitChoiceCacheActive => exitChoiceCachePickup != null;
        public Vector3 ExitChoiceCacheWorldPosition => exitChoiceCachePickup != null ? exitChoiceCachePickup.transform.position : exitChoiceCachePosition;
        public int ActiveRiskCacheCount => CountActiveRiskCaches();

        public bool TryGetNextObjectiveTarget(Vector3 origin, out Vector3 target, out bool targetIsExit)
        {
            target = default;
            targetIsExit = false;

            if (ExitUnlocked && exitPortal != null)
            {
                target = exitPortal.transform.position;
                targetIsExit = true;
                return true;
            }

            if (TryFindNearestActiveBreadcrumbPickup(origin, out target))
            {
                return true;
            }

            return false;
        }

        public bool TryGetNearestBreadcrumbTarget(Vector3 origin, out Vector3 target, out float distance)
        {
            target = default;
            distance = 0f;
            if (!TryFindNearestActiveBreadcrumbPickupComponent(origin, out BreadcrumbPickup pickup, out float distanceSqr))
            {
                return false;
            }

            target = pickup.transform.position;
            distance = Mathf.Sqrt(Mathf.Max(0f, distanceSqr));
            return true;
        }

        public bool TryCollectNearestBreadcrumbForRuntime(Vector3 origin, out Vector3 collectedPosition)
        {
            collectedPosition = default;
            if (!TryFindNearestActiveBreadcrumbPickupComponent(origin, out BreadcrumbPickup pickup, out _))
            {
                return false;
            }

            collectedPosition = pickup.transform.position;
            HandlePickupCollected(pickup, allowFeedback: !RegressionChecklistRunner.IsRegressionRunActive);
            if (pickup != null)
            {
                DestroySafe(pickup.gameObject);
            }

            return true;
        }

        public bool TryGetNearestRiskCacheTarget(Vector3 origin, out Vector3 target, out float distance)
        {
            target = default;
            distance = 0f;
            RiskCachePickup nearest = null;
            float nearestDistanceSqr = float.PositiveInfinity;

            for (int i = activeRiskCaches.Count - 1; i >= 0; i--)
            {
                RiskCachePickup cache = activeRiskCaches[i];
                if (cache == null)
                {
                    activeRiskCaches.RemoveAt(i);
                    continue;
                }

                float distanceSqr = (cache.transform.position - origin).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearest = cache;
                nearestDistanceSqr = distanceSqr;
            }

            if (nearest == null)
            {
                return false;
            }

            target = nearest.transform.position;
            distance = Mathf.Sqrt(nearestDistanceSqr);
            return true;
        }

        public bool TryEnsureRiskCacheForRuntime(Vector3 origin, out Vector3 target, out float distance)
        {
            if (TryGetNearestRiskCacheTarget(origin, out target, out distance))
            {
                return true;
            }

            target = default;
            distance = 0f;
            if (!spawnRiskCaches || pickupsRoot == null || mapSystem == null || CurrentStage < Mathf.Max(1, riskCacheStartStage))
            {
                return false;
            }

            if (!TryPickRiskCacheCellForRuntime(origin, out GeneratedMapCell cell))
            {
                return false;
            }

            SpawnRiskCache(cell, activeRiskCaches.Count);
            target = ToWorld(cell.position, mapSystem);
            distance = Vector3.Distance(origin, target);
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeMapEvents();
        }

        private void OnDisable()
        {
            UnsubscribeMapEvents();
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            BreadcrumbPickup.TickForestLick();
            TickLateHouseMood();
        }

        private void Start()
        {
            ResolveReferences();

            if (mapSystem != null && mapSystem.LastGeneratedCells.Count > 0)
            {
                BuildStageObjects(mapSystem.CurrentStage, mapSystem.LastGeneratedCells);
            }
        }

        public void SetMapSystemForEditor(MapSystem targetMapSystem)
        {
            if (mapSystem == targetMapSystem)
            {
                return;
            }

            UnsubscribeMapEvents();
            mapSystem = targetMapSystem;
            SubscribeMapEvents();
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (gameplayRhythmDirector == null)
            {
                gameplayRhythmDirector = FindFirstObjectByType<GameplayRhythmDirector>();
            }

            if (pickupsRoot == null)
            {
                pickupsRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/Pickups");
            }

            if (interactablesRoot == null)
            {
                interactablesRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/Interactables");
            }

            if (momentumPlayer == null)
            {
                momentumPlayer = FindFirstObjectByType<PlayerDummyController>();
            }
        }

        private void SubscribeMapEvents()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
                mapSystem.MapGenerated += HandleMapGenerated;
            }
        }

        private void UnsubscribeMapEvents()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
            }
        }

        private void HandleMapGenerated(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            BuildStageObjects(stage, cells);
        }

        private void BuildStageObjects(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            CurrentStage = Mathf.Max(1, stage);
            float latePressure01 = EvaluateLateStagePressure01(CurrentStage);

            ClearExistingObjects();

            if (cells == null || cells.Count == 0)
            {
                RequiredBreadcrumbs = 0;
                CollectedBreadcrumbs = 0;
                return;
            }

            List<GeneratedMapCell> candidates = new();
            List<GeneratedMapCell> safeHavenCandidates = new();
            List<GeneratedMapCell> riskCacheCandidates = new();
            bool hasExit = false;
            Vector3 exitPosition = Vector3.zero;

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];

                if (cell.kind == MapCellKind.Exit)
                {
                    hasExit = true;
                    exitPosition = ToWorld(cell.position, mapSystem);
                    continue;
                }

                if (cell.kind is MapCellKind.Hideout or MapCellKind.Room or MapCellKind.Fork)
                {
                    safeHavenCandidates.Add(cell);
                }

                if (cell.kind == MapCellKind.Risk)
                {
                    riskCacheCandidates.Add(cell);
                }

                if (cell.kind is MapCellKind.Corridor or MapCellKind.Room or MapCellKind.Fork or MapCellKind.Hideout)
                {
                    candidates.Add(cell);
                }
            }

            if (!hasExit && TryFindFallbackExitCell(cells, out GeneratedMapCell fallbackExitCell))
            {
                hasExit = true;
                exitPosition = ToWorld(fallbackExitCell.position, mapSystem);
            }

            int lateBonusBreadcrumbs = Mathf.RoundToInt(Mathf.Max(0, lateBreadcrumbBonusMax) * latePressure01);
            int targetCount = baseBreadcrumbCount + (CurrentStage - 1) * breadcrumbIncreasePerStage + lateBonusBreadcrumbs;
            targetCount = Mathf.Clamp(targetCount, 1, maxBreadcrumbCount);
            RequiredBreadcrumbs = Mathf.Min(targetCount, candidates.Count);
            CollectedBreadcrumbs = 0;

            List<GeneratedMapCell> breadcrumbCells = SelectWeightedCells(
                candidates,
                RequiredBreadcrumbs,
                CurrentStage * 173 + cells.Count * 19,
                GetBreadcrumbWeight);

            HashSet<Vector2Int> breadcrumbPositions = new();
            for (int i = 0; i < breadcrumbCells.Count; i++)
            {
                SpawnPickup(breadcrumbCells[i], i);
                breadcrumbPositions.Add(breadcrumbCells[i].position);
            }

            SpawnCorruptedTrailDecoys(candidates, breadcrumbPositions);

            if (spawnStaminaPickups && breadcrumbCells.Count < candidates.Count)
            {
                int interval = Mathf.Max(1, staminaCountIncreaseStageInterval);
                int stageTier = (CurrentStage - 1) / interval;
                int targetStaminaCount = baseStaminaPickupCount + stageTier * staminaPickupIncreasePerStage;
                targetStaminaCount = Mathf.Clamp(targetStaminaCount, 0, maxStaminaPickupCount);

                List<GeneratedMapCell> staminaCandidates = new(candidates.Count - breadcrumbCells.Count);
                for (int i = 0; i < candidates.Count; i++)
                {
                    GeneratedMapCell candidate = candidates[i];
                    if (!breadcrumbPositions.Contains(candidate.position))
                    {
                        staminaCandidates.Add(candidate);
                    }
                }

                float staminaSpawnFloor = Mathf.Clamp01(lateStaminaSpawnChanceFloor);
                float effectiveStaminaSpawnChance = Mathf.Lerp(
                    Mathf.Clamp01(staminaPickupSpawnChance),
                    Mathf.Min(Mathf.Clamp01(staminaPickupSpawnChance), staminaSpawnFloor),
                    latePressure01);

                float recoverFloorMultiplier = Mathf.Clamp(lateStaminaRecoverFloor, 0.25f, 1f);
                float effectiveStaminaRecoverAmount = Mathf.Max(
                    0.15f,
                    Mathf.Lerp(staminaPickupRecoverAmount, staminaPickupRecoverAmount * recoverFloorMultiplier, latePressure01));

                SpawnStaminaPickups(
                    staminaCandidates,
                    targetStaminaCount,
                    CurrentStage * 227 + cells.Count * 43,
                    effectiveStaminaSpawnChance,
                    effectiveStaminaRecoverAmount);
            }

            SpawnRiskCaches(riskCacheCandidates, CurrentStage * 509 + cells.Count * 71, latePressure01);
            TryEnsureLandmarkCacheForLateTrail();

            if (hasExit)
            {
                SpawnExit(exitPosition);
            }

            if (spawnSafeHavens && safeHavenCandidates.Count > 0)
            {
                bool safeHavenCountLikelyUninitialized = baseSafeHavenCount == 0
                                                        && safeHavenIncreasePerStage == 0
                                                        && maxSafeHavenCount == 0;

                int safeHavenBaseCount = safeHavenCountLikelyUninitialized ? 1 : Mathf.Max(0, baseSafeHavenCount);
                int safeHavenIncrease = safeHavenCountLikelyUninitialized ? 1 : Mathf.Max(0, safeHavenIncreasePerStage);
                int safeHavenMaxCount = safeHavenCountLikelyUninitialized ? 4 : Mathf.Max(0, maxSafeHavenCount);

                int interval = safeHavenCountIncreaseStageInterval > 0 ? safeHavenCountIncreaseStageInterval : 3;
                int stageTier = (CurrentStage - 1) / interval;
                int targetSafeHavenCount = safeHavenBaseCount + stageTier * safeHavenIncrease;
                targetSafeHavenCount = Mathf.Clamp(targetSafeHavenCount, 0, safeHavenMaxCount);

                float safeHavenCountScale = Mathf.Lerp(1f, Mathf.Clamp01(lateSafeHavenCountFloor), latePressure01);
                targetSafeHavenCount = Mathf.Clamp(
                    Mathf.RoundToInt(targetSafeHavenCount * safeHavenCountScale),
                    0,
                    safeHavenMaxCount);

                float safeHavenRadiusScale = Mathf.Lerp(1f, Mathf.Clamp01(lateSafeHavenRadiusFloor), latePressure01);
                float effectiveSafeHavenRadius = Mathf.Max(0.24f, safeHavenRadius * safeHavenRadiusScale);

                List<GeneratedMapCell> selectedSafeHavens = SelectWeightedCells(
                    safeHavenCandidates,
                    targetSafeHavenCount,
                    CurrentStage * 397 + cells.Count * 59,
                    GetSafeHavenWeight);

                for (int i = 0; i < selectedSafeHavens.Count; i++)
                {
                    SpawnSafeHaven(selectedSafeHavens[i], i, effectiveSafeHavenRadius, latePressure01);
                }
            }

            UpdateExitState();
        }

        private void SpawnPickup(GeneratedMapCell cell, int index)
        {
            if (pickupsRoot == null || mapSystem == null)
            {
                return;
            }

            GameObject pickupObject = new($"Breadcrumb_{index:00}");
            pickupObject.transform.SetParent(pickupsRoot, false);
            pickupObject.transform.position = ToWorld(cell.position, mapSystem);
            pickupObject.transform.localScale = Vector3.one * 0.45f;

            SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
            Sprite breadSprite = MapReadableArt.TryGetBreadcrumbSprite();
            if (breadSprite != null)
            {
                renderer.sprite = breadSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            }

            renderer.sortingOrder = 25;

            CircleCollider2D trigger = pickupObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.45f;

            BreadcrumbPickup pickup = pickupObject.AddComponent<BreadcrumbPickup>();
            pickup.Collected += HandlePickupCollected;
            pickup.ErasedByForest += HandleFaintTrailErased;

            activePickups.Add(pickup);
        }

        private void SpawnExit(Vector3 position)
        {
            if (interactablesRoot == null)
            {
                return;
            }

            float cellSize = mapSystem != null ? Mathf.Max(0.75f, mapSystem.CellSize) : 1f;
            float exitScale = Mathf.Max(0.9f, cellSize * 0.62f);

            GameObject exitObject = new("ExitPortal");
            exitObject.transform.SetParent(interactablesRoot, false);
            exitObject.transform.position = position;
            exitObject.transform.localScale = Vector3.one * exitScale;

            SpriteRenderer renderer = exitObject.AddComponent<SpriteRenderer>();
            bool houseThreshold = CurrentStage >= Mathf.Max(1, latePressureStartStage);
            Sprite exitSprite = houseThreshold
                ? MapReadableArt.TryGetHouseThresholdExitSprite()
                : MapReadableArt.TryGetStageExitPortalSprite();
            if (exitSprite != null)
            {
                renderer.sprite = exitSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            }

            renderer.sortingOrder = 120;

            GameObject beaconObject = new("ExitPortal_Beacon");
            beaconObject.transform.SetParent(exitObject.transform, false);
            beaconObject.transform.localPosition = Vector3.zero;
            beaconObject.transform.localScale = Vector3.one * 1.55f;

            SpriteRenderer beaconRenderer = beaconObject.AddComponent<SpriteRenderer>();
            beaconRenderer.sprite = renderer.sprite;
            beaconRenderer.color = new Color(1f, 0.52f, 0.18f, 0.36f);
            beaconRenderer.sortingOrder = 119;

            CircleCollider2D trigger = exitObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.55f;

            exitPortal = exitObject.AddComponent<ExitPortalDummy>();
            exitPortal.PlayerEntered += HandleExitEntered;
            exitPortal.SetUnlocked(false);
            exitPortal.SetHouseThresholdHint(houseThreshold);
        }

        private void SpawnStaminaPickups(
            List<GeneratedMapCell> candidates,
            int targetCount,
            int seed,
            float spawnChance,
            float recoverAmount)
        {
            if (targetCount <= 0 || candidates == null || candidates.Count == 0)
            {
                return;
            }

            System.Random random = new(seed);
            int spawned = 0;

            while (spawned < targetCount && candidates.Count > 0)
            {
                int index = PickWeightedStaminaIndex(candidates, random);
                if (index < 0 || index >= candidates.Count)
                {
                    index = random.Next(0, candidates.Count);
                }

                GeneratedMapCell selected = candidates[index];
                candidates.RemoveAt(index);

                bool passedChance = random.NextDouble() <= Mathf.Clamp01(spawnChance);
                int remainingNeeded = targetCount - spawned;

                if (!passedChance && candidates.Count >= remainingNeeded)
                {
                    continue;
                }

                SpawnStaminaPickup(selected, spawned, recoverAmount);
                spawned++;
            }
        }

        private int PickWeightedStaminaIndex(IReadOnlyList<GeneratedMapCell> candidates, System.Random random)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += GetStaminaWeight(candidates[i].kind);
            }

            if (totalWeight <= 0.001f)
            {
                return random.Next(0, candidates.Count);
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            float running = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                running += GetStaminaWeight(candidates[i].kind);
                if (roll <= running)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }

        private float GetStaminaWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Corridor => staminaCorridorWeight,
                MapCellKind.Room => staminaRoomWeight,
                MapCellKind.Fork => staminaForkWeight,
                MapCellKind.Hideout => staminaHideoutWeight,
                _ => 0.2f
            };
        }

        private float GetBreadcrumbWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Corridor => breadcrumbCorridorWeight,
                MapCellKind.Room => breadcrumbRoomWeight,
                MapCellKind.Fork => breadcrumbForkWeight,
                MapCellKind.Hideout => breadcrumbHideoutWeight,
                _ => 0.25f
            };
        }

        private float GetSafeHavenWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Hideout => safeHavenHideoutWeight,
                MapCellKind.Room => safeHavenRoomWeight,
                MapCellKind.Fork => safeHavenForkWeight,
                _ => 0.15f
            };
        }

        private static List<GeneratedMapCell> SelectWeightedCells(
            IReadOnlyList<GeneratedMapCell> source,
            int targetCount,
            int seed,
            Func<MapCellKind, float> weightEvaluator)
        {
            List<GeneratedMapCell> selected = new();
            if (source == null || source.Count == 0 || targetCount <= 0 || weightEvaluator == null)
            {
                return selected;
            }

            List<GeneratedMapCell> pool = new(source);
            System.Random random = new(seed);

            int safeTargetCount = Mathf.Min(targetCount, pool.Count);
            while (selected.Count < safeTargetCount && pool.Count > 0)
            {
                int index = PickWeightedCellIndex(pool, random, weightEvaluator);
                if (index < 0 || index >= pool.Count)
                {
                    index = random.Next(0, pool.Count);
                }

                selected.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return selected;
        }

        private static int PickWeightedCellIndex(
            IReadOnlyList<GeneratedMapCell> candidates,
            System.Random random,
            Func<MapCellKind, float> weightEvaluator)
        {
            if (candidates == null || candidates.Count == 0 || random == null || weightEvaluator == null)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float weight = Mathf.Max(0.01f, weightEvaluator(candidates[i].kind));
                totalWeight += weight;
            }

            if (totalWeight <= 0.001f)
            {
                return random.Next(0, candidates.Count);
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            float running = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                running += Mathf.Max(0.01f, weightEvaluator(candidates[i].kind));
                if (roll <= running)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }
        private void SpawnStaminaPickup(GeneratedMapCell cell, int index, float recoverAmount)
        {
            if (pickupsRoot == null || mapSystem == null)
            {
                return;
            }

            GameObject pickupObject = new($"StaminaPickup_{index:00}");
            pickupObject.transform.SetParent(pickupsRoot, false);
            pickupObject.transform.position = ToWorld(cell.position, mapSystem);
            pickupObject.transform.localScale = Vector3.one * 0.4f;

            SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
            Sprite staminaSprite = MapReadableArt.TryGetStaminaPickupSprite();
            if (staminaSprite != null)
            {
                renderer.sprite = staminaSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = staminaPickupColor;
            }

            renderer.sortingOrder = 24;

            CircleCollider2D trigger = pickupObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.4f;

            StaminaPickup pickup = pickupObject.AddComponent<StaminaPickup>();
            pickup.Configure(Mathf.Max(0.15f, recoverAmount));
            pickup.Collected += HandleStaminaPickupCollected;

            activeStaminaPickups.Add(pickup);
        }

        private void SpawnRiskCaches(IReadOnlyList<GeneratedMapCell> candidates, int seed, float latePressure01)
        {
            if (!spawnRiskCaches
                || candidates == null
                || candidates.Count <= 0
                || pickupsRoot == null
                || mapSystem == null
                || CurrentStage < Mathf.Max(1, riskCacheStartStage)
                || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            float chance = Mathf.Clamp01(riskCacheSpawnChance + latePressure01 * 0.16f);
            System.Random random = new(seed);
            if (random.NextDouble() > chance)
            {
                return;
            }

            List<GeneratedMapCell> pool = new(candidates);
            int targetCount = Mathf.Clamp(riskCacheMaxPerStage, 1, 3);
            targetCount = Mathf.Min(targetCount, pool.Count);
            for (int i = 0; i < targetCount && pool.Count > 0; i++)
            {
                int index = random.Next(0, pool.Count);
                GeneratedMapCell selected = pool[index];
                pool.RemoveAt(index);
                SpawnRiskCache(selected, i);
            }
        }

        private bool TryPickRiskCacheCellForRuntime(Vector3 origin, out GeneratedMapCell selected)
        {
            selected = default;
            IReadOnlyList<GeneratedMapCell> cells = mapSystem != null ? mapSystem.LastGeneratedCells : null;
            if (cells == null || cells.Count <= 0)
            {
                return false;
            }

            bool found = false;
            float bestScore = float.MinValue;
            for (int pass = 0; pass < 2 && !found; pass++)
            {
                bool requireRiskCell = pass == 0;
                for (int i = 0; i < cells.Count; i++)
                {
                    GeneratedMapCell cell = cells[i];
                    if (cell.kind is MapCellKind.Start or MapCellKind.Exit)
                    {
                        continue;
                    }

                    float kindWeight = requireRiskCell
                        ? (cell.kind == MapCellKind.Risk ? 1f : 0f)
                        : GetRiskCacheRuntimeFallbackWeight(cell.kind);
                    if (kindWeight <= 0f)
                    {
                        continue;
                    }

                    Vector3 world = ToWorld(cell.position, mapSystem);
                    float distanceScore = Mathf.Min(64f, (world - origin).sqrMagnitude);
                    float score = kindWeight * 100f + distanceScore + cell.order * 0.01f;
                    if (found && score <= bestScore)
                    {
                        continue;
                    }

                    selected = cell;
                    bestScore = score;
                    found = true;
                }
            }

            return found;
        }

        private static float GetRiskCacheRuntimeFallbackWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Risk => 3f,
                MapCellKind.Fork => 1.6f,
                MapCellKind.Room => 1.2f,
                MapCellKind.Hideout => 0.8f,
                MapCellKind.Corridor => 0.35f,
                _ => 0f
            };
        }

        private void SpawnRiskCache(GeneratedMapCell cell, int index)
        {
            GameObject cacheObject = new($"RiskCache_{index:00}");
            cacheObject.transform.SetParent(pickupsRoot, false);
            cacheObject.transform.position = ToWorld(cell.position, mapSystem);
            cacheObject.transform.localScale = Vector3.one * Mathf.Max(0.1f, riskCacheScale);

            SpriteRenderer renderer = cacheObject.AddComponent<SpriteRenderer>();
            Sprite cacheSprite = MapReadableArt.TryGetLandmarkCacheSprite();
            if (cacheSprite != null)
            {
                renderer.sprite = cacheSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = riskCacheColor;
            }

            renderer.sortingOrder = riskCacheSortingOrder;

            CircleCollider2D trigger = cacheObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.43f;

            RiskCachePickup pickup = cacheObject.AddComponent<RiskCachePickup>();
            pickup.Configure(
                Mathf.Max(0.1f, riskCacheStaminaRecoverAmount),
                Mathf.Max(0f, riskCachePulseCooldownRefundSeconds),
                Mathf.Max(0f, riskCacheNoiseLoudness),
                Mathf.Max(0.1f, riskCacheNoiseRadius));
            pickup.ConfigureRhythmWager(
                gameplayRhythmDirector,
                riskCacheCalmRewardMultiplier,
                riskCacheBuildRewardMultiplier,
                riskCacheSpikeRewardMultiplier,
                riskCacheReleaseRewardMultiplier,
                riskCacheCalmNoiseMultiplier,
                riskCacheBuildNoiseMultiplier,
                riskCacheSpikeNoiseMultiplier,
                riskCacheReleaseNoiseMultiplier);
            pickup.Collected += HandleRiskCacheCollected;
            activeRiskCaches.Add(pickup);
        }

        private void SpawnSafeHaven(GeneratedMapCell cell, int index, float radius, float latePressure01)
        {
            if (interactablesRoot == null || mapSystem == null)
            {
                return;
            }

            GameObject havenObject = new($"SafeHaven_{index:00}");
            havenObject.transform.SetParent(interactablesRoot, false);
            havenObject.transform.position = ToWorld(cell.position, mapSystem);
            havenObject.transform.localScale = Vector3.one * 0.95f;

            SpriteRenderer renderer = havenObject.AddComponent<SpriteRenderer>();
            Sprite havenSprite = MapReadableArt.TryGetSafeHavenSprite();
            if (havenSprite != null)
            {
                renderer.sprite = havenSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = safeHavenColor;
            }

            renderer.sortingOrder = 23;

            CircleCollider2D trigger = havenObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.24f, radius);

            SafeHavenZone safeHaven = havenObject.AddComponent<SafeHavenZone>();
            safeHaven.Configure(trigger.radius, CurrentStage, latePressure01);
            activeSafeHavens.Add(safeHaven);
        }

        private void HandlePickupCollected(BreadcrumbPickup pickup)
        {
            HandlePickupCollected(pickup, allowFeedback: true);
        }

        private void HandlePickupCollected(BreadcrumbPickup pickup, bool allowFeedback)
        {
            Vector3 collectedPosition = pickup != null ? pickup.transform.position : Vector3.zero;
            if (pickup != null)
            {
                pickup.Collected -= HandlePickupCollected;
                pickup.ErasedByForest -= HandleFaintTrailErased;
                activePickups.Remove(pickup);
                if (pickup.IsCorrupted)
                {
                    if (allowFeedback)
                    {
                        RuntimeEventBus.Raise(RuntimeEventType.Objective, "진해지지 않는 가루였다", this, CurrentStage);
                    }
                    return;
                }
            }

            int momentumLevel = UpdateBreadcrumbMomentumLevel();
            CollectedBreadcrumbs++;
            bool shouldUnlockExit = RequiredBreadcrumbs <= 0 || CollectedBreadcrumbs >= RequiredBreadcrumbs;
            if (allowFeedback)
            {
                RuntimeEventBus.Raise(RuntimeEventType.Objective, BuildBreadcrumbCollectedMessage(CollectedBreadcrumbs, RequiredBreadcrumbs), this, CurrentStage);
                SaveManager.Instance?.NotifyBreadcrumbCollected(1);
                ApplyBreadcrumbMomentumReward(collectedPosition, momentumLevel);
                EmitBreadcrumbChainReaction(collectedPosition, shouldUnlockExit, momentumLevel);
            }

            UpdateExitState();
            TryStartExitChoiceCarryover();
        }

        private static string BuildBreadcrumbCollectedMessage(int collected, int required)
        {
            return $"빵부스러기 {Mathf.Max(0, collected)}/{Mathf.Max(0, required)}";
        }

        private int UpdateBreadcrumbMomentumLevel()
        {
            if (!enableBreadcrumbMomentum)
            {
                ResetBreadcrumbMomentum();
                return 1;
            }

            float now = Time.realtimeSinceStartup;
            float window = Mathf.Max(0.5f, breadcrumbMomentumWindowSeconds);
            bool continuesChain = breadcrumbMomentumLevel > 0 && now <= lastBreadcrumbCollectRealtime + window;
            breadcrumbMomentumLevel = continuesChain
                ? Mathf.Min(Mathf.Max(2, breadcrumbMomentumMaxLevel), breadcrumbMomentumLevel + 1)
                : 1;
            lastBreadcrumbCollectRealtime = now;
            return breadcrumbMomentumLevel;
        }

        private void ResetBreadcrumbMomentum()
        {
            breadcrumbMomentumLevel = 0;
            lastBreadcrumbCollectRealtime = -999f;
        }

        private int CountActiveBreadcrumbs()
        {
            int count = 0;
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                if (activePickups[i] == null)
                {
                    activePickups.RemoveAt(i);
                    continue;
                }

                count++;
            }

            return count;
        }

        private int CountActiveRiskCaches()
        {
            int count = 0;
            for (int i = activeRiskCaches.Count - 1; i >= 0; i--)
            {
                if (activeRiskCaches[i] == null)
                {
                    activeRiskCaches.RemoveAt(i);
                    continue;
                }

                count++;
            }

            return count;
        }

        private float EvaluateBreadcrumbMomentumRewardMultiplier()
        {
            GameplayRhythmPhase phase = gameplayRhythmDirector != null
                ? gameplayRhythmDirector.CurrentPhase
                : GameplayRhythmPhase.Calm;
            return phase switch
            {
                GameplayRhythmPhase.Build => breadcrumbBuildRewardMultiplier,
                GameplayRhythmPhase.Spike => breadcrumbSpikeRewardMultiplier,
                GameplayRhythmPhase.Release => breadcrumbReleaseRewardMultiplier,
                _ => breadcrumbCalmRewardMultiplier
            };
        }

        private bool IsBuildRhythmPhase()
        {
            return gameplayRhythmDirector != null
                   && gameplayRhythmDirector.CurrentPhase == GameplayRhythmPhase.Build;
        }

        private float EvaluateBreadcrumbBuildMultiplier(float buildMultiplier)
        {
            return IsBuildRhythmPhase() ? Mathf.Max(0.1f, buildMultiplier) : 1f;
        }

        private bool IsSpikeRhythmPhase()
        {
            return gameplayRhythmDirector != null
                   && gameplayRhythmDirector.CurrentPhase == GameplayRhythmPhase.Spike;
        }

        private void ApplyBreadcrumbMomentumReward(Vector3 origin, int momentumLevel)
        {
            if (!enableBreadcrumbMomentum || momentumLevel <= 1 || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (momentumPlayer == null)
            {
                momentumPlayer = FindFirstObjectByType<PlayerDummyController>();
            }

            float reward = Mathf.Max(0f, breadcrumbMomentumStaminaReward)
                           + Mathf.Max(0, momentumLevel - 2) * Mathf.Max(0f, breadcrumbMomentumStaminaRewardPerLevel);
            float phaseRewardMultiplier = EvaluateBreadcrumbMomentumRewardMultiplier();
            reward *= phaseRewardMultiplier;
            float recovered = momentumPlayer != null ? momentumPlayer.RecoverStamina(reward) : 0f;
            string phaseLabel = gameplayRhythmDirector != null ? gameplayRhythmDirector.CurrentPhaseLabel : "None";
            float releaseAdvance = TryGrantSpikeBreadcrumbReleaseAdvance(momentumLevel);

            RuntimeEventBus.Raise(
                RuntimeEventType.Objective,
                BuildBreadcrumbMomentumRewardMessage(momentumLevel, phaseLabel, recovered, releaseAdvance),
                this,
                CurrentStage);

            SpawnBreadcrumbMomentumPulse(origin, momentumLevel);
        }

        private float TryGrantSpikeBreadcrumbReleaseAdvance(int momentumLevel)
        {
            if (!IsSpikeRhythmPhase())
            {
                return 0f;
            }

            float advance = Mathf.Max(0f, breadcrumbSpikeReleaseAdvanceSeconds)
                            + Mathf.Max(0, momentumLevel - 2) * Mathf.Max(0f, breadcrumbSpikeReleaseAdvancePerLevel);
            if (advance <= 0.01f || gameplayRhythmDirector == null)
            {
                return 0f;
            }

            return gameplayRhythmDirector.TryAdvanceSpikeTowardRelease(advance, out float appliedAdvance, $"breadcrumb chain x{momentumLevel}")
                ? appliedAdvance
                : 0f;
        }

        private static string BuildBreadcrumbMomentumRewardMessage(int momentumLevel, string phaseLabel, float recovered, float releaseAdvance)
        {
            string rhythmLabel = LocalizeRhythmPhaseLabel(phaseLabel);
            string message = recovered > 0.01f
                ? $"흔적 연쇄 x{momentumLevel} {rhythmLabel} (+{recovered:0.0} 스태미나)"
                : $"흔적 연쇄 x{momentumLevel} {rhythmLabel}";

            if (releaseAdvance > 0.01f)
            {
                message += $" / 안도 -{releaseAdvance:0.0}초";
            }

            return message;
        }

        private static string LocalizeRhythmPhaseLabel(string phaseLabel)
        {
            return phaseLabel switch
            {
                "Calm" => "고요",
                "Build" => "고조",
                "Spike" => "습격",
                "Release" => "안도",
                _ => string.IsNullOrWhiteSpace(phaseLabel) ? "리듬" : phaseLabel
            };
        }

        private void EmitBreadcrumbChainReaction(Vector3 origin, bool preferExit, int momentumLevel)
        {
            if (showBreadcrumbChainEcho && TryFindBreadcrumbChainTarget(origin, preferExit, out Vector3 target, out bool targetIsExit))
            {
                SpawnBreadcrumbChainEcho(origin, target, targetIsExit, momentumLevel);
            }

            TrySpawnCorruptedBreadcrumbEcho(origin, preferExit);

            if (!emitBreadcrumbChainNoise || NoiseManager.Instance == null)
            {
                return;
            }

            int momentumSteps = Mathf.Max(0, momentumLevel - 1);
            float buildNoiseRadiusMultiplier = EvaluateBreadcrumbBuildMultiplier(breadcrumbBuildNoiseRadiusMultiplier);
            NoiseManager.Instance.EmitNoise(
                origin,
                Mathf.Max(0f, breadcrumbChainNoiseLoudness) * (1f + breadcrumbMomentumNoiseLoudnessBonus * momentumSteps),
                Mathf.Max(0.1f, breadcrumbChainNoiseRadius) * (1f + breadcrumbMomentumNoiseRadiusBonus * momentumSteps) * buildNoiseRadiusMultiplier,
                NoiseKind.ItemUse,
                gameObject);
        }

        private void TrySpawnCorruptedBreadcrumbEcho(Vector3 origin, bool preferExit)
        {
            if (!enableCorruptedBreadcrumbEcho || preferExit || !Application.isPlaying || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (activePickups.Count <= 0 || CurrentStage < corruptedBreadcrumbStartStage || Time.time < nextCorruptedBreadcrumbEchoTime)
            {
                return;
            }

            float pressure = EvaluateLateStagePressure01(CurrentStage);
            if (pressure < corruptedBreadcrumbPressureThreshold && CurrentStage > corruptedBreadcrumbStartStage)
            {
                return;
            }

            float chance = Mathf.Clamp01(corruptedBreadcrumbBaseChance + pressure * corruptedBreadcrumbPressureChanceBonus);
            if (UnityEngine.Random.value > chance)
            {
                return;
            }

            if (!TryPickCorruptedBreadcrumbTarget(origin, out Vector3 corruptedTarget))
            {
                return;
            }

            nextCorruptedBreadcrumbEchoTime = Time.time + Mathf.Max(0.2f, corruptedBreadcrumbCooldownSeconds * Mathf.Lerp(1.1f, 0.76f, pressure));
            SpawnCorruptedBreadcrumbEcho(origin, corruptedTarget, pressure);

            if (emitCorruptedBreadcrumbNoise && NoiseManager.Instance != null)
            {
                float intensity = Mathf.Lerp(0.72f, 1.18f, pressure);
                NoiseManager.Instance.EmitNoise(
                    corruptedTarget,
                    corruptedBreadcrumbNoiseLoudness * intensity,
                    corruptedBreadcrumbNoiseRadius * intensity,
                    NoiseKind.Decoy,
                    gameObject);
            }
        }

        private bool TryPickCorruptedBreadcrumbTarget(Vector3 origin, out Vector3 corruptedTarget)
        {
            corruptedTarget = origin;
            float minDistance = Mathf.Max(0.2f, corruptedBreadcrumbMinDistance);
            float maxDistance = Mathf.Max(minDistance + 0.1f, corruptedBreadcrumbMaxDistance);

            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            Vector3 randomTarget = origin + (Vector3)(direction * UnityEngine.Random.Range(minDistance, maxDistance));
            if (!TryFindNearestActiveBreadcrumbPickup(origin, out Vector3 realTarget))
            {
                corruptedTarget = randomTarget;
                return true;
            }

            Vector2 toReal = realTarget - origin;
            if (toReal.sqrMagnitude <= 0.001f)
            {
                corruptedTarget = randomTarget;
                return true;
            }

            Vector2 realDirection = toReal.normalized;
            Vector2 side = new(-realDirection.y, realDirection.x);
            if (UnityEngine.Random.value < 0.5f)
            {
                side = -side;
            }

            float sideDistance = UnityEngine.Random.Range(minDistance * 0.45f, maxDistance * 0.62f);
            Vector3 biasedTarget = realTarget + (Vector3)(side * sideDistance);
            corruptedTarget = Vector3.Lerp(randomTarget, biasedTarget, Mathf.Clamp01(corruptedBreadcrumbPickupBias));
            return true;
        }

        private bool TryFindNearestActiveBreadcrumbPickup(Vector3 origin, out Vector3 target)
        {
            target = default;
            if (!TryFindNearestActiveBreadcrumbPickupComponent(origin, out BreadcrumbPickup nearestPickup, out _))
            {
                return false;
            }

            target = nearestPickup.transform.position;
            return true;
        }

        private bool TryFindNearestActiveBreadcrumbPickupComponent(Vector3 origin, out BreadcrumbPickup nearestPickup, out float nearestDistanceSqr)
        {
            nearestPickup = null;
            nearestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < activePickups.Count; i++)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup == null)
                {
                    continue;
                }

                float distanceSqr = (pickup.transform.position - origin).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestPickup = pickup;
                nearestDistanceSqr = distanceSqr;
            }

            if (nearestPickup == null)
            {
                return false;
            }

            return true;
        }

        private bool TryFindBreadcrumbChainTarget(Vector3 origin, bool preferExit, out Vector3 target, out bool targetIsExit)
        {
            target = default;
            targetIsExit = false;

            if (preferExit && exitPortal != null)
            {
                target = exitPortal.transform.position;
                targetIsExit = true;
                return true;
            }

            BreadcrumbPickup nearestPickup = null;
            float nearestDistanceSqr = float.PositiveInfinity;
            float maxDistance = Mathf.Max(0.2f, breadcrumbChainEchoMaxDistance);
            float maxDistanceSqr = maxDistance * maxDistance;
            for (int i = 0; i < activePickups.Count; i++)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup == null)
                {
                    continue;
                }

                float distanceSqr = (pickup.transform.position - origin).sqrMagnitude;
                if (distanceSqr > maxDistanceSqr || distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestPickup = pickup;
                nearestDistanceSqr = distanceSqr;
            }

            if (nearestPickup != null)
            {
                target = nearestPickup.transform.position;
                return true;
            }

            if (ExitUnlocked && exitPortal != null)
            {
                target = exitPortal.transform.position;
                targetIsExit = true;
                return true;
            }

            return false;
        }

        private void SpawnBreadcrumbChainEcho(Vector3 origin, Vector3 target, bool targetIsExit, int momentumLevel)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/BreadcrumbChainEchoes");
            GameObject echoObject = new("BreadcrumbChainEcho");
            if (vfxRoot != null)
            {
                echoObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = echoObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 2;
            line.SetPosition(0, origin);
            line.SetPosition(1, target);
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = EvaluateBreadcrumbChainWidth(momentumLevel);
            line.sharedMaterial = GetChainEchoMaterial();
            line.sortingOrder = breadcrumbChainEchoSortingOrder;

            StartCoroutine(BreadcrumbChainEchoRoutine(echoObject, line, targetIsExit, momentumLevel));
        }

        private void SpawnCorruptedBreadcrumbEcho(Vector3 origin, Vector3 target, float pressure)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/CorruptedBreadcrumbEchoes");
            GameObject echoObject = new("CorruptedBreadcrumbEcho");
            if (vfxRoot != null)
            {
                echoObject.transform.SetParent(vfxRoot, false);
            }

            LineRenderer line = echoObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = Mathf.Max(0.01f, corruptedBreadcrumbEchoWidth);
            line.sharedMaterial = GetChainEchoMaterial();
            line.sortingOrder = corruptedBreadcrumbSortingOrder;

            Vector3[] basePoints = BuildCorruptedBreadcrumbPath(origin, target, pressure);
            for (int i = 0; i < basePoints.Length; i++)
            {
                line.SetPosition(i, basePoints[i]);
            }

            StartCoroutine(CorruptedBreadcrumbEchoRoutine(echoObject, line, basePoints, pressure));
        }

        private IEnumerator BreadcrumbChainEchoRoutine(GameObject echoObject, LineRenderer line, bool targetIsExit, int momentumLevel)
        {
            float buildEchoDurationMultiplier = EvaluateBreadcrumbBuildMultiplier(breadcrumbBuildEchoDurationMultiplier);
            float duration = Mathf.Max(0.1f, breadcrumbChainEchoDuration)
                             * (1f + Mathf.Max(0, momentumLevel - 1) * Mathf.Max(0f, breadcrumbMomentumEchoDurationBonus))
                             * buildEchoDurationMultiplier;
            Color baseColor = targetIsExit ? breadcrumbExitChainEchoColor : breadcrumbChainEchoColor;
            float startedAt = Time.time;
            float baseWidth = EvaluateBreadcrumbChainWidth(momentumLevel)
                              * EvaluateBreadcrumbBuildMultiplier(breadcrumbBuildEchoWidthMultiplier);

            while (line != null && Time.time < startedAt + duration)
            {
                float t = Mathf.Clamp01((Time.time - startedAt) / duration);
                float pulse = 0.5f + Mathf.Sin(t * Mathf.PI * 5f) * 0.5f;
                Color color = baseColor;
                color.a *= Mathf.Lerp(1f, 0f, t)
                           * Mathf.Lerp(0.65f, 1f, pulse)
                           * Mathf.Lerp(1f, 1.18f, Mathf.Clamp01((momentumLevel - 1f) / Mathf.Max(1f, BreadcrumbMomentumMaxLevel - 1f)));
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = baseWidth * Mathf.Lerp(1.2f, 0.35f, t);
                yield return null;
            }

            if (echoObject != null)
            {
                DestroySafe(echoObject);
            }
        }

        private float EvaluateBreadcrumbChainWidth(int momentumLevel)
        {
            return Mathf.Max(0.01f, breadcrumbChainEchoWidth)
                   * (1f + Mathf.Max(0, momentumLevel - 1) * Mathf.Max(0f, breadcrumbMomentumEchoWidthBonus));
        }

        private void SpawnBreadcrumbMomentumPulse(Vector3 position, int momentumLevel)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/BreadcrumbMomentum");
            GameObject pulseObject = new($"BreadcrumbMomentumPulse_x{momentumLevel}");
            if (vfxRoot != null)
            {
                pulseObject.transform.SetParent(vfxRoot, false);
            }

            pulseObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = pulseObject.AddComponent<EchoPulseVisualDummy>();
            int steps = Mathf.Max(0, momentumLevel - 2);
            float alphaScale = Mathf.Lerp(0.86f, 1.18f, Mathf.Clamp01((momentumLevel - 2f) / Mathf.Max(1f, BreadcrumbMomentumMaxLevel - 2f)));
            float radius = Mathf.Max(0.1f, breadcrumbMomentumPulseRadius) * (1f + steps * 0.18f) * EvaluateBreadcrumbBuildMultiplier(breadcrumbBuildPulseRadiusMultiplier);
            float duration = Mathf.Max(0.1f, breadcrumbMomentumPulseDuration) * (1f + steps * 0.12f);
            int ringCount = Mathf.Clamp(2 + steps, 2, 4);
            float interval = Mathf.Max(0.08f, breadcrumbMomentumPulseDuration * 0.18f);
            Sprite momentumPulseSprite = MapReadableArt.TryGetBreadcrumbMomentumPulseSprite();
            Color color;
            if (momentumPulseSprite != null)
            {
                // Painted honey-gold crumb flare - white RGB so breadcrumbMomentumPulseColor does not double-tint.
                color = Color.white;
                color.a = breadcrumbMomentumPulseColor.a * alphaScale;
                visual.Configure(
                    radius,
                    color,
                    duration,
                    ringCount,
                    interval,
                    breadcrumbMomentumPulseSortingOrder,
                    momentumPulseSprite);
            }
            else
            {
                color = breadcrumbMomentumPulseColor;
                color.a *= alphaScale;
                visual.Configure(
                    radius,
                    color,
                    duration,
                    ringCount,
                    interval,
                    breadcrumbMomentumPulseSortingOrder);
            }
        }

        private IEnumerator CorruptedBreadcrumbEchoRoutine(GameObject echoObject, LineRenderer line, Vector3[] basePoints, float pressure)
        {
            float duration = Mathf.Max(0.1f, corruptedBreadcrumbEchoDuration);
            float startedAt = Time.time;
            Vector3 direction = basePoints[^1] - basePoints[0];
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;
            float waver = Mathf.Max(0f, corruptedBreadcrumbWaverAmplitude) * Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(pressure));

            while (line != null && Time.time < startedAt + duration)
            {
                float elapsed = Time.time - startedAt;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - t;
                float flicker = 0.5f + Mathf.Sin((elapsed * corruptedBreadcrumbFlickerSpeed + pressure * 7.1f) * Mathf.PI * 2f) * 0.5f;

                for (int i = 0; i < basePoints.Length; i++)
                {
                    Vector3 point = basePoints[i];
                    if (i > 0 && i < basePoints.Length - 1)
                    {
                        float local = Mathf.Sin((elapsed * corruptedBreadcrumbFlickerSpeed * 0.64f + i * 1.37f) * Mathf.PI * 2f);
                        point += side * local * waver * fade;
                    }

                    line.SetPosition(i, point);
                }

                Color color = corruptedBreadcrumbEchoColor;
                color.a *= fade * Mathf.Lerp(0.48f, 1f, flicker);
                line.startColor = color;
                line.endColor = color;
                float hold01 = 0f;
                if (momentumPlayer != null)
                {
                    PlayerEchoPulseAbility holdPulse = momentumPlayer.GetComponent<PlayerEchoPulseAbility>();
                    if (holdPulse != null)
                    {
                        hold01 = holdPulse.Charge01;
                    }
                }
                float widthScale = Mathf.Lerp(1.35f, 0.18f, t) * Mathf.Lerp(1f, 0.62f, hold01);
                line.widthMultiplier = Mathf.Max(0.01f, corruptedBreadcrumbEchoWidth) * widthScale;
                yield return null;
            }

            if (echoObject != null)
            {
                DestroySafe(echoObject);
            }
        }

        private Vector3[] BuildCorruptedBreadcrumbPath(Vector3 origin, Vector3 target, float pressure)
        {
            Vector3 direction = target - origin;
            Vector3 side = direction.sqrMagnitude > 0.001f
                ? new Vector3(-direction.y, direction.x, 0f).normalized
                : Vector3.up;
            if (UnityEngine.Random.value < 0.5f)
            {
                side = -side;
            }

            float offset = Mathf.Max(0f, corruptedBreadcrumbWaverAmplitude) * Mathf.Lerp(1.2f, 2.2f, Mathf.Clamp01(pressure));
            return new[]
            {
                origin,
                Vector3.Lerp(origin, target, 0.34f) + side * offset,
                Vector3.Lerp(origin, target, 0.68f) - side * offset * 0.72f,
                target
            };
        }

        private Material GetChainEchoMaterial()
        {
            if (chainEchoMaterial != null)
            {
                return chainEchoMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Default-Line");
            }

            if (shader == null)
            {
                return null;
            }

            chainEchoMaterial = new Material(shader)
            {
                name = "BreadcrumbChainEchoMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return chainEchoMaterial;
        }

        private void HandleStaminaPickupCollected(StaminaPickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            if (pickup == exitChoiceCachePickup)
            {
                exitChoiceCachePickup = null;
                exitChoiceCachePosition = pickup.transform.position;
            }

            pickup.Collected -= HandleStaminaPickupCollected;
            pickup.Collected -= HandleExitChoiceCacheCollected;
            activeStaminaPickups.Remove(pickup);
        }

        private void HandleRiskCacheCollected(RiskCachePickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            pickup.Collected -= HandleRiskCacheCollected;
            activeRiskCaches.Remove(pickup);

            string phaseLabel = string.IsNullOrWhiteSpace(pickup.LastRhythmPhaseLabel)
                ? "None"
                : pickup.LastRhythmPhaseLabel;
            RuntimeEventBus.Raise(
                RuntimeEventType.Objective,
                BuildRiskCacheRewardMessage(
                    phaseLabel,
                    pickup.LastRewardMultiplier,
                    pickup.LastRecoveredStamina,
                    pickup.LastPulseCooldownRefund),
                this,
                CurrentStage,
                semantic: RuntimeEventSemantic.RiskReward);

            SpawnRiskCacheRewardPulse(pickup.transform.position);
            TriggerRiskCacheAftershock(pickup.transform.position, pickup.LastNoiseMultiplier);
        }

        private void SpawnRiskCacheRewardPulse(Vector3 position)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/RiskCache");
            GameObject pulseObject = new("RiskCacheRewardPulse");
            if (vfxRoot != null)
            {
                pulseObject.transform.SetParent(vfxRoot, false);
            }

            pulseObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = pulseObject.AddComponent<EchoPulseVisualDummy>();
            Sprite riskPulseSprite = MapReadableArt.TryGetRiskCachePulseSprite();
            Color color;
            if (riskPulseSprite != null)
            {
                // Painted ember flare - white RGB so riskCacheColor does not double-tint; keep same alpha scale.
                color = Color.white;
                color.a = riskCacheColor.a * 0.68f;
                visual.Configure(
                    1.45f,
                    color,
                    1.1f,
                    2,
                    0.2f,
                    riskCacheSortingOrder,
                    riskPulseSprite);
            }
            else
            {
                color = riskCacheColor;
                color.a *= 0.68f;
                visual.Configure(
                    1.45f,
                    color,
                    1.1f,
                    2,
                    0.2f,
                    riskCacheSortingOrder);
            }
        }

        private static string BuildRiskCacheRewardMessage(
            string phaseLabel,
            float rewardMultiplier,
            float recoveredStamina,
            float pulseCooldownRefund)
        {
            string message = $"위험 보상 {LocalizeRhythmPhaseLabel(phaseLabel)} x{rewardMultiplier:0.00} (+{recoveredStamina:0.0} 스태미나)";
            if (pulseCooldownRefund > 0.05f)
            {
                message += $" / 메아리 -{pulseCooldownRefund:0.0}초";
            }

            return message;
        }

        private void TriggerRiskCacheAftershock(Vector3 position, float noiseMultiplier)
        {
            if (riskCacheAftershockNoiseScale <= 0f || riskCacheAftershockNoiseDelay <= 0f)
            {
                return;
            }

            if (riskCacheAftershockRoutine != null)
            {
                StopCoroutine(riskCacheAftershockRoutine);
            }

            riskCacheAftershockRoutine = StartCoroutine(RiskCacheAftershockRoutine(position, noiseMultiplier));
        }

        private IEnumerator RiskCacheAftershockRoutine(Vector3 position, float noiseMultiplier)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, riskCacheAftershockNoiseDelay));

            SpawnRiskCacheRewardPulse(position);
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    position,
                    Mathf.Max(0f, riskCacheNoiseLoudness) * Mathf.Clamp01(riskCacheAftershockNoiseScale) * Mathf.Max(0.25f, noiseMultiplier),
                    Mathf.Max(0.1f, riskCacheNoiseRadius) * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(riskCacheAftershockNoiseScale)) * Mathf.Lerp(0.84f, 1.2f, Mathf.Clamp01(noiseMultiplier - 0.25f)),
                    NoiseKind.ItemUse,
                    gameObject);
            }

            riskCacheAftershockRoutine = null;
        }

        private void UpdateExitState()
        {
            if (exitPortal == null)
            {
                return;
            }

            bool shouldUnlock = RequiredBreadcrumbs <= 0 || CollectedBreadcrumbs >= RequiredBreadcrumbs;
            exitPortal.SetUnlocked(shouldUnlock);

            if (shouldUnlock && !lastExitUnlockedState)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Objective,
                    BuildExitUnlockedMessage(),
                    this,
                    CurrentStage,
                    semantic: RuntimeEventSemantic.ExitUnlocked);
                if (!RegressionChecklistRunner.IsRegressionRunActive)
                {
                    TriggerExitUnlockPressure();
                    TrySpawnExitChoiceCache();
                }
            }

            lastExitUnlockedState = shouldUnlock;
        }

        private void TriggerExitUnlockPressure()
        {
            if (!triggerExitUnlockPressure || !Application.isPlaying || exitPortal == null)
            {
                return;
            }

            if (exitUnlockPressureRoutine != null)
            {
                StopCoroutine(exitUnlockPressureRoutine);
            }

            exitUnlockPressureRoutine = StartCoroutine(ExitUnlockPressureRoutine(exitPortal.transform.position, exitPortal.gameObject));
        }

        private IEnumerator ExitUnlockPressureRoutine(Vector3 exitPosition, GameObject source)
        {
            SpawnExitUnlockBeacon(exitPosition, 0.62f);

            float delay = Mathf.Max(0f, exitUnlockPressureDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnExitUnlockBeacon(exitPosition, 1f);

            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    exitPosition,
                    Mathf.Max(0f, exitUnlockNoiseLoudness),
                    Mathf.Max(0.1f, exitUnlockNoiseRadius),
                    NoiseKind.Echo,
                    source);
            }

            exitUnlockPressureRoutine = null;
        }

        private void TrySpawnExitChoiceCache()
        {
            if (!spawnExitChoiceCache
                || RegressionChecklistRunner.IsRegressionRunActive
                || CurrentStage < Mathf.Max(1, exitChoiceCacheStartStage)
                || exitChoiceCachePickup != null
                || mapSystem == null
                || pickupsRoot == null)
            {
                return;
            }

            System.Random random = new(CurrentStage * 1009 + RequiredBreadcrumbs * 97 + CollectedBreadcrumbs * 17);
            if (random.NextDouble() > Mathf.Clamp01(exitChoiceCacheSpawnChance))
            {
                return;
            }

            if (!TryPickExitChoiceCacheCell(random, out GeneratedMapCell cacheCell))
            {
                return;
            }

            SpawnExitChoiceCache(cacheCell);
        }

        private bool TryPickExitChoiceCacheCell(System.Random random, out GeneratedMapCell cacheCell)
        {
            cacheCell = default;
            IReadOnlyList<GeneratedMapCell> cells = mapSystem != null ? mapSystem.LastGeneratedCells : null;
            if (cells == null || cells.Count <= 0)
            {
                return false;
            }

            Vector3 exitPosition = exitPortal != null ? exitPortal.transform.position : Vector3.zero;
            Vector3 playerPosition = momentumPlayer != null ? momentumPlayer.transform.position : Vector3.zero;
            bool hasPlayer = momentumPlayer != null;
            float minExitDistanceSqr = Mathf.Max(0.1f, exitChoiceCacheMinDistanceFromExit);
            minExitDistanceSqr *= minExitDistanceSqr;
            float minPlayerDistanceSqr = Mathf.Max(0f, exitChoiceCacheMinDistanceFromPlayer);
            minPlayerDistanceSqr *= minPlayerDistanceSqr;

            List<GeneratedMapCell> candidates = new();
            for (int pass = 0; pass < 2 && candidates.Count <= 0; pass++)
            {
                bool relaxed = pass > 0;
                float exitFloor = relaxed ? minExitDistanceSqr * 0.35f : minExitDistanceSqr;
                float playerFloor = relaxed ? minPlayerDistanceSqr * 0.25f : minPlayerDistanceSqr;

                for (int i = 0; i < cells.Count; i++)
                {
                    GeneratedMapCell cell = cells[i];
                    if (cell.kind is MapCellKind.Start or MapCellKind.Exit)
                    {
                        continue;
                    }

                    if (GetExitChoiceCacheWeight(cell.kind) <= 0f)
                    {
                        continue;
                    }

                    Vector3 world = ToWorld(cell.position, mapSystem);
                    if (exitPortal != null && (world - exitPosition).sqrMagnitude < exitFloor)
                    {
                        continue;
                    }

                    if (hasPlayer && (world - playerPosition).sqrMagnitude < playerFloor)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            if (candidates.Count <= 0)
            {
                return false;
            }

            int selectedIndex = PickWeightedExitChoiceCacheIndex(candidates, random);
            cacheCell = candidates[Mathf.Clamp(selectedIndex, 0, candidates.Count - 1)];
            return true;
        }

        private int PickWeightedExitChoiceCacheIndex(IReadOnlyList<GeneratedMapCell> candidates, System.Random random)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += GetExitChoiceCacheWeight(candidates[i].kind);
            }

            if (totalWeight <= 0.001f)
            {
                return random.Next(0, candidates.Count);
            }

            float roll = (float)(random.NextDouble() * totalWeight);
            float running = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                running += GetExitChoiceCacheWeight(candidates[i].kind);
                if (roll <= running)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
        }

        private static float GetExitChoiceCacheWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Risk => 3f,
                MapCellKind.Room => 1.45f,
                MapCellKind.Fork => 1.2f,
                MapCellKind.Hideout => 1f,
                MapCellKind.Corridor => 0.55f,
                _ => 0f
            };
        }

        private void SpawnExitChoiceCache(GeneratedMapCell cell)
        {
            GameObject cacheObject = new("ExitChoiceCache");
            cacheObject.transform.SetParent(pickupsRoot, false);
            cacheObject.transform.position = ToWorld(cell.position, mapSystem);
            cacheObject.transform.localScale = Vector3.one * Mathf.Max(0.1f, exitChoiceCacheScale);

            SpriteRenderer renderer = cacheObject.AddComponent<SpriteRenderer>();
            Sprite cacheSprite = MapReadableArt.TryGetExitChoiceCacheSprite();
            if (cacheSprite != null)
            {
                renderer.sprite = cacheSprite;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = exitChoiceCacheColor;
            }

            renderer.sortingOrder = exitChoiceCacheSortingOrder;

            CircleCollider2D trigger = cacheObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.42f;

            StaminaPickup pickup = cacheObject.AddComponent<StaminaPickup>();
            pickup.Configure(Mathf.Max(0.1f, exitChoiceCacheRecoverAmount));
            pickup.Collected += HandleExitChoiceCacheCollected;
            pickup.Collected += HandleStaminaPickupCollected;
            activeStaminaPickups.Add(pickup);
            exitChoiceCachePickup = pickup;
            exitChoiceCachePosition = cacheObject.transform.position;

            SpawnExitChoiceCacheBeacon(exitChoiceCachePosition, 0.72f);
            RuntimeEventBus.Raise(
                RuntimeEventType.Objective,
                BuildExitChoiceCacheExposedMessage(),
                this,
                CurrentStage);
        }

        private void HandleExitChoiceCacheCollected(StaminaPickup pickup)
        {
            if (pickup == null || pickup != exitChoiceCachePickup)
            {
                return;
            }

            pickup.Collected -= HandleExitChoiceCacheCollected;
            Vector3 position = pickup.transform.position;
            exitChoiceCachePosition = position;
            exitChoiceCachePickup = null;
            exitChoiceCacheTakenThisStage = true;
            pendingExitChoiceCarryover = true;

            SpawnExitChoiceCacheBeacon(position, 1f);
            if (NoiseManager.Instance != null)
            {
                NoiseManager.Instance.EmitNoise(
                    position,
                    Mathf.Max(0f, exitChoiceCacheNoiseLoudness),
                    Mathf.Max(0.1f, exitChoiceCacheNoiseRadius),
                    NoiseKind.ItemUse,
                    gameObject);
            }

            RuntimeEventBus.Raise(
                RuntimeEventType.Objective,
                BuildExitChoiceCacheRewardMessage(exitChoiceCacheRecoverAmount),
                this,
                CurrentStage,
                semantic: RuntimeEventSemantic.EchoChoiceScan);
        }

        private static string BuildExitChoiceCacheRewardMessage(float recoverAmount)
        {
            return $"출구 단서 확보 (+{recoverAmount:0.0} 스태미나 / 다음 길 힌트)";
        }

        private void SpawnExitChoiceCacheBeacon(Vector3 position, float alphaScale)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/ExitChoiceCache");
            GameObject beaconObject = new("ExitChoiceCacheBeacon");
            if (vfxRoot != null)
            {
                beaconObject.transform.SetParent(vfxRoot, false);
            }

            beaconObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = beaconObject.AddComponent<EchoPulseVisualDummy>();
            Sprite choiceBeaconSprite = MapReadableArt.TryGetExitChoiceCacheBeaconSprite();
            Color color;
            if (choiceBeaconSprite != null)
            {
                // Painted amber/pumpkin flare - white RGB so exitChoiceCacheColor does not double-tint; keep base alpha * alphaScale.
                color = Color.white;
                color.a = exitChoiceCacheColor.a * Mathf.Clamp01(alphaScale);
                visual.Configure(
                    Mathf.Max(0.1f, exitChoiceCacheBeaconRadius),
                    color,
                    Mathf.Max(0.1f, exitChoiceCacheBeaconDuration),
                    2,
                    Mathf.Max(0.08f, exitChoiceCacheBeaconDuration * 0.18f),
                    exitChoiceCacheSortingOrder,
                    choiceBeaconSprite);
            }
            else
            {
                color = exitChoiceCacheColor;
                color.a *= Mathf.Clamp01(alphaScale);
                visual.Configure(
                    Mathf.Max(0.1f, exitChoiceCacheBeaconRadius),
                    color,
                    Mathf.Max(0.1f, exitChoiceCacheBeaconDuration),
                    2,
                    Mathf.Max(0.08f, exitChoiceCacheBeaconDuration * 0.18f),
                    exitChoiceCacheSortingOrder);
            }
        }

        private void SpawnExitUnlockBeacon(Vector3 position, float alphaScale)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/ExitUnlockBeacons");
            GameObject beaconObject = new("ExitUnlockBeacon");
            if (vfxRoot != null)
            {
                beaconObject.transform.SetParent(vfxRoot, false);
            }

            beaconObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = beaconObject.AddComponent<EchoPulseVisualDummy>();
            Sprite unlockBeaconSprite = MapReadableArt.TryGetExitUnlockBeaconSprite();
            Color color;
            if (unlockBeaconSprite != null)
            {
                // Painted mint/gold lantern flare - white RGB so exitUnlockBeaconColor does not double-tint; keep base alpha * alphaScale.
                color = Color.white;
                color.a = exitUnlockBeaconColor.a * Mathf.Clamp01(alphaScale);
                visual.Configure(
                    Mathf.Max(0.2f, exitUnlockBeaconRadius),
                    color,
                    Mathf.Max(0.1f, exitUnlockBeaconDuration),
                    Mathf.Clamp(exitUnlockBeaconRingCount, 1, 4),
                    Mathf.Max(0.08f, exitUnlockBeaconDuration * 0.16f),
                    exitUnlockBeaconSortingOrder,
                    unlockBeaconSprite);
            }
            else
            {
                color = exitUnlockBeaconColor;
                color.a *= Mathf.Clamp01(alphaScale);
                visual.Configure(
                    Mathf.Max(0.2f, exitUnlockBeaconRadius),
                    color,
                    Mathf.Max(0.1f, exitUnlockBeaconDuration),
                    Mathf.Clamp(exitUnlockBeaconRingCount, 1, 4),
                    Mathf.Max(0.08f, exitUnlockBeaconDuration * 0.16f),
                    exitUnlockBeaconSortingOrder);
            }
        }

        private void TryStartExitChoiceCarryover()
        {
            if (!enableExitChoiceCarryover
                || !pendingExitChoiceCarryover
                || RegressionChecklistRunner.IsRegressionRunActive)
            {
                pendingExitChoiceCarryover = false;
                return;
            }

            if (exitChoiceCarryoverRoutine != null)
            {
                StopCoroutine(exitChoiceCarryoverRoutine);
            }

            exitChoiceCarryoverRoutine = StartCoroutine(ExitChoiceCarryoverRoutine());
        }

        private IEnumerator ExitChoiceCarryoverRoutine()
        {
            float delay = Mathf.Max(0.05f, exitChoiceCarryoverEchoDelay);
            yield return new WaitForSeconds(delay);

            pendingExitChoiceCarryover = false;

            if (momentumPlayer == null)
            {
                momentumPlayer = FindFirstObjectByType<PlayerDummyController>();
            }

            if (momentumPlayer == null)
            {
                exitChoiceCarryoverRoutine = null;
                yield break;
            }

            Vector3 origin = momentumPlayer.transform.position;
            if (TryGetNextObjectiveTarget(origin, out Vector3 target, out bool targetIsExit))
            {
                int carryoverMomentum = Mathf.Clamp(exitChoiceCarryoverMomentumLevel, 2, Mathf.Max(2, breadcrumbMomentumMaxLevel));
                SpawnBreadcrumbChainEcho(origin, target, targetIsExit, carryoverMomentum);
                SpawnBreadcrumbMomentumPulse(origin, carryoverMomentum);

                RuntimeEventBus.Raise(
                    RuntimeEventType.Objective,
                    BuildExitChoiceCacheCarryoverMessage(),
                    this,
                    CurrentStage,
                    semantic: RuntimeEventSemantic.EchoChoiceScan);
            }

            exitChoiceCarryoverRoutine = null;
        }

        private void HandleExitEntered()
        {
            if (mapSystem == null)
            {
                return;
            }

            RaiseExitDecisionEvent();
            if (StageManager.ActiveInstance != null && StageManager.ActiveInstance.TryHandleStageClear())
            {
                return;
            }

            mapSystem.GenerateNextStage();
        }

        private void RaiseExitDecisionEvent()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (exitChoiceCachePickup != null)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Objective,
                    BuildExitChoiceCacheLeftBehindMessage(),
                    this,
                    CurrentStage,
                    semantic: RuntimeEventSemantic.RiskReward);
                return;
            }

            if (exitChoiceCacheTakenThisStage)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Objective,
                    BuildExitChoiceRouteHintCarriedMessage(),
                    this,
                    CurrentStage,
                    semantic: RuntimeEventSemantic.EchoChoiceScan);
            }
        }

        private static string BuildExitUnlockedMessage()
        {
            return "출구 열림 - 지금 나가";
        }

        private static string BuildExitChoiceCacheExposedMessage()
        {
            return "출구 옆 단서가 드러났다";
        }

        private static string BuildExitChoiceCacheCarryoverMessage()
        {
            return "출구 단서가 다음 길을 비춘다";
        }

        private static string BuildExitChoiceCacheLeftBehindMessage()
        {
            return "탈출 선택 - 남은 단서는 버려졌다";
        }

        private static string BuildExitChoiceRouteHintCarriedMessage()
        {
            return "탈출 선택 - 다음 길 힌트를 가져간다";
        }

        private void ClearExistingObjects()
        {
            if (exitUnlockPressureRoutine != null)
            {
                StopCoroutine(exitUnlockPressureRoutine);
                exitUnlockPressureRoutine = null;
            }

            if (riskCacheAftershockRoutine != null)
            {
                StopCoroutine(riskCacheAftershockRoutine);
                riskCacheAftershockRoutine = null;
            }

            if (exitChoiceCarryoverRoutine != null)
            {
                StopCoroutine(exitChoiceCarryoverRoutine);
                exitChoiceCarryoverRoutine = null;
            }

            ResetBreadcrumbMomentum();
            exitChoiceCachePickup = null;
            exitChoiceCacheTakenThisStage = false;
            exitChoiceCachePosition = Vector3.zero;
            nextCorruptedBreadcrumbEchoTime = 0f;
            lateHouseMood01 = 0f;
            if (lateHouseMoodApplied)
            {
                FogOfWarSystem.ActiveInstance?.ResetRuntimeStyleTuningForEditor();
            }

            lateHouseMoodApplied = false;

            for (int i = 0; i < activePickups.Count; i++)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup != null)
                {
                    pickup.Collected -= HandlePickupCollected;
                    pickup.ErasedByForest -= HandleFaintTrailErased;
                    DestroySafe(pickup.gameObject);
                }
            }

            activePickups.Clear();

            for (int i = 0; i < activeStaminaPickups.Count; i++)
            {
                StaminaPickup pickup = activeStaminaPickups[i];
                if (pickup != null)
                {
                    pickup.Collected -= HandleStaminaPickupCollected;
                    pickup.Collected -= HandleExitChoiceCacheCollected;
                    DestroySafe(pickup.gameObject);
                }
            }

            activeStaminaPickups.Clear();

            for (int i = 0; i < activeRiskCaches.Count; i++)
            {
                RiskCachePickup cache = activeRiskCaches[i];
                if (cache != null)
                {
                    cache.Collected -= HandleRiskCacheCollected;
                    DestroySafe(cache.gameObject);
                }
            }

            activeRiskCaches.Clear();

            for (int i = 0; i < activeSafeHavens.Count; i++)
            {
                SafeHavenZone haven = activeSafeHavens[i];
                if (haven != null)
                {
                    DestroySafe(haven.gameObject);
                }
            }

            activeSafeHavens.Clear();

            if (exitPortal != null)
            {
                exitPortal.PlayerEntered -= HandleExitEntered;
                DestroySafe(exitPortal.gameObject);
                exitPortal = null;
            }
        }

        private static bool TryFindFallbackExitCell(IReadOnlyList<GeneratedMapCell> cells, out GeneratedMapCell fallbackCell)
        {
            fallbackCell = default;
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            Vector2Int startPosition = Vector2Int.zero;
            bool hasStart = false;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == MapCellKind.Start)
                {
                    startPosition = cells[i].position;
                    hasStart = true;
                    break;
                }
            }

            int bestDistance = int.MinValue;
            bool found = false;

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                if (cell.kind == MapCellKind.Start)
                {
                    continue;
                }

                if (cell.kind is not (MapCellKind.Corridor or MapCellKind.Room or MapCellKind.Fork or MapCellKind.Hideout or MapCellKind.Risk or MapCellKind.Exit))
                {
                    continue;
                }

                int distance = hasStart
                    ? Mathf.Abs(cell.position.x - startPosition.x) + Mathf.Abs(cell.position.y - startPosition.y)
                    : cell.order;

                if (!found || distance > bestDistance || (distance == bestDistance && cell.order > fallbackCell.order))
                {
                    fallbackCell = cell;
                    bestDistance = distance;
                    found = true;
                }
            }

            if (!found)
            {
                fallbackCell = cells[cells.Count - 1];
                found = true;
            }

            return found;
        }
        private static void DestroySafe(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }


        private void SpawnCorruptedTrailDecoys(IReadOnlyList<GeneratedMapCell> candidates, HashSet<Vector2Int> usedPositions)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive
                || candidates == null
                || usedPositions == null
                || CurrentStage < Mathf.Max(1, corruptedBreadcrumbStartStage))
            {
                return;
            }

            List<GeneratedMapCell> pool = new();
            for (int i = 0; i < candidates.Count; i++)
            {
                GeneratedMapCell cell = candidates[i];
                if (!usedPositions.Contains(cell.position))
                {
                    pool.Add(cell);
                }
            }

            int decoyCount = Mathf.Min(2, pool.Count);
            System.Random random = new(CurrentStage * 811 + pool.Count * 17);
            for (int i = 0; i < decoyCount && pool.Count > 0; i++)
            {
                int index = random.Next(0, pool.Count);
                GeneratedMapCell selected = pool[index];
                pool.RemoveAt(index);
                usedPositions.Add(selected.position);
                SpawnPickup(selected, 80 + i);
                BreadcrumbPickup spawned = activePickups.Count > 0 ? activePickups[activePickups.Count - 1] : null;
                spawned?.ConfigureCorrupted(true);
            }
        }

        private void TryEnsureLandmarkCacheForLateTrail()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive || CurrentStage < 4 || CountActiveRiskCaches() > 0)
            {
                return;
            }

            if (momentumPlayer == null)
            {
                momentumPlayer = FindFirstObjectByType<PlayerDummyController>();
            }

            Vector3 origin = momentumPlayer != null ? momentumPlayer.transform.position : Vector3.zero;
            TryEnsureRiskCacheForRuntime(origin, out _, out _);
        }

        private void HandleFaintTrailErased(BreadcrumbPickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            pickup.Collected -= HandlePickupCollected;
            pickup.ErasedByForest -= HandleFaintTrailErased;
            activePickups.Remove(pickup);
            if (!pickup.IsCorrupted)
            {
                RequiredBreadcrumbs = Mathf.Max(CollectedBreadcrumbs, RequiredBreadcrumbs - 1);
            }

            UpdateExitState();
        }

        private void TickLateHouseMood()
        {
            bool lateHouse = CurrentStage >= Mathf.Max(1, latePressureStartStage);
            float previousMood = lateHouseMood01;
            lateHouseMood01 = lateHouse
                ? Mathf.MoveTowards(lateHouseMood01, 1f, Time.deltaTime / 36f)
                : 0f;

            FogOfWarSystem fog = FogOfWarSystem.ActiveInstance;
            if (fog == null)
            {
                lateHouseMoodApplied = false;
                return;
            }

            if (!lateHouse)
            {
                if (lateHouseMoodApplied)
                {
                    fog.ResetRuntimeStyleTuningForEditor();
                    lateHouseMoodApplied = false;
                }

                return;
            }

            if (lateHouseMoodApplied && Mathf.Abs(lateHouseMood01 - previousMood) < 0.02f)
            {
                return;
            }

            Color woodTint = Color.Lerp(new Color(0.031f, 0.039f, 0.055f, 1f), new Color(0.055f, 0.028f, 0.016f, 1f), lateHouseMood01);
            fog.ApplyRuntimeStyleTuningForEditor(woodTint, Mathf.Lerp(1f, 1.12f, lateHouseMood01), Mathf.Lerp(1f, 0.86f, lateHouseMood01));
            lateHouseMoodApplied = true;
        }

        private float EvaluateLateStagePressure01(int stage)
        {
            int startStage = Mathf.Max(1, latePressureStartStage);
            int peakStage = Mathf.Max(startStage + 1, latePressurePeakStage);
            float t = Mathf.InverseLerp(startStage, peakStage, Mathf.Max(1, stage));
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private static Vector3 ToWorld(Vector2Int cellPosition, MapSystem map)
        {
            float cellSize = map != null ? map.CellSize : 1f;
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
                name = "StageLoopDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "StageLoopDebugSprite";
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
    }
}





