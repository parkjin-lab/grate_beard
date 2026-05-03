using System;
using System.Collections;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Events;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class StageLoopDirector : MonoBehaviour
    {
        public static StageLoopDirector Instance { get; private set; }

        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
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

        private ExitPortalDummy exitPortal;
        private bool lastExitUnlockedState;
        private Sprite debugSprite;
        private Material chainEchoMaterial;
        private Coroutine exitUnlockPressureRoutine;

        public int CurrentStage { get; private set; } = 1;
        public int RequiredBreadcrumbs { get; private set; }
        public int CollectedBreadcrumbs { get; private set; }
        public bool ExitUnlocked => exitPortal != null && exitPortal.IsUnlocked;
        public int ActiveSafeHavenCount => activeSafeHavens.Count;
        public int ActiveStaminaPickupCount => activeStaminaPickups.Count;

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

            if (pickupsRoot == null)
            {
                pickupsRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/Pickups");
            }

            if (interactablesRoot == null)
            {
                interactablesRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/Interactables");
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
                    SpawnSafeHaven(selectedSafeHavens[i], i, effectiveSafeHavenRadius);
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
            renderer.sprite = GetDebugSprite();
            renderer.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            renderer.sortingOrder = 25;

            CircleCollider2D trigger = pickupObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.45f;

            BreadcrumbPickup pickup = pickupObject.AddComponent<BreadcrumbPickup>();
            pickup.Collected += HandlePickupCollected;

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
            renderer.sprite = GetDebugSprite();
            renderer.color = new Color(1f, 0.25f, 0.25f, 0.95f);
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
            renderer.sprite = GetDebugSprite();
            renderer.color = staminaPickupColor;
            renderer.sortingOrder = 24;

            CircleCollider2D trigger = pickupObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.4f;

            StaminaPickup pickup = pickupObject.AddComponent<StaminaPickup>();
            pickup.Configure(Mathf.Max(0.15f, recoverAmount));
            pickup.Collected += HandleStaminaPickupCollected;

            activeStaminaPickups.Add(pickup);
        }

        private void SpawnSafeHaven(GeneratedMapCell cell, int index, float radius)
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
            renderer.sprite = GetDebugSprite();
            renderer.color = safeHavenColor;
            renderer.sortingOrder = 23;

            CircleCollider2D trigger = havenObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.24f, radius);

            SafeHavenZone safeHaven = havenObject.AddComponent<SafeHavenZone>();
            safeHaven.Configure(trigger.radius);
            activeSafeHavens.Add(safeHaven);
        }

        private void HandlePickupCollected(BreadcrumbPickup pickup)
        {
            Vector3 collectedPosition = pickup != null ? pickup.transform.position : Vector3.zero;
            if (pickup != null)
            {
                pickup.Collected -= HandlePickupCollected;
                activePickups.Remove(pickup);
            }

            CollectedBreadcrumbs++;
            RuntimeEventBus.Raise(RuntimeEventType.Objective, $"Breadcrumb {CollectedBreadcrumbs}/{RequiredBreadcrumbs}", this, CurrentStage);
            SaveManager.Instance?.NotifyBreadcrumbCollected(1);
            bool shouldUnlockExit = RequiredBreadcrumbs <= 0 || CollectedBreadcrumbs >= RequiredBreadcrumbs;
            EmitBreadcrumbChainReaction(collectedPosition, shouldUnlockExit);
            UpdateExitState();
        }

        private void EmitBreadcrumbChainReaction(Vector3 origin, bool preferExit)
        {
            if (showBreadcrumbChainEcho && TryFindBreadcrumbChainTarget(origin, preferExit, out Vector3 target, out bool targetIsExit))
            {
                SpawnBreadcrumbChainEcho(origin, target, targetIsExit);
            }

            if (!emitBreadcrumbChainNoise || NoiseManager.Instance == null)
            {
                return;
            }

            NoiseManager.Instance.EmitNoise(
                origin,
                Mathf.Max(0f, breadcrumbChainNoiseLoudness),
                Mathf.Max(0.1f, breadcrumbChainNoiseRadius),
                NoiseKind.ItemUse,
                gameObject);
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

            if (exitPortal != null)
            {
                target = exitPortal.transform.position;
                targetIsExit = true;
                return true;
            }

            return false;
        }

        private void SpawnBreadcrumbChainEcho(Vector3 origin, Vector3 target, bool targetIsExit)
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
            line.widthMultiplier = Mathf.Max(0.01f, breadcrumbChainEchoWidth);
            line.sharedMaterial = GetChainEchoMaterial();
            line.sortingOrder = breadcrumbChainEchoSortingOrder;

            StartCoroutine(BreadcrumbChainEchoRoutine(echoObject, line, targetIsExit));
        }

        private IEnumerator BreadcrumbChainEchoRoutine(GameObject echoObject, LineRenderer line, bool targetIsExit)
        {
            float duration = Mathf.Max(0.1f, breadcrumbChainEchoDuration);
            Color baseColor = targetIsExit ? breadcrumbExitChainEchoColor : breadcrumbChainEchoColor;
            float startedAt = Time.time;

            while (line != null && Time.time < startedAt + duration)
            {
                float t = Mathf.Clamp01((Time.time - startedAt) / duration);
                float pulse = 0.5f + Mathf.Sin(t * Mathf.PI * 5f) * 0.5f;
                Color color = baseColor;
                color.a *= Mathf.Lerp(1f, 0f, t) * Mathf.Lerp(0.65f, 1f, pulse);
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Max(0.01f, breadcrumbChainEchoWidth) * Mathf.Lerp(1.2f, 0.35f, t);
                yield return null;
            }

            if (echoObject != null)
            {
                DestroySafe(echoObject);
            }
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

            pickup.Collected -= HandleStaminaPickupCollected;
            activeStaminaPickups.Remove(pickup);
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
                    "Exit unlocked",
                    this,
                    CurrentStage,
                    semantic: RuntimeEventSemantic.ExitUnlocked);
                TriggerExitUnlockPressure();
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
            Color color = exitUnlockBeaconColor;
            color.a *= Mathf.Clamp01(alphaScale);
            visual.Configure(
                Mathf.Max(0.2f, exitUnlockBeaconRadius),
                color,
                Mathf.Max(0.1f, exitUnlockBeaconDuration),
                Mathf.Clamp(exitUnlockBeaconRingCount, 1, 4),
                Mathf.Max(0.08f, exitUnlockBeaconDuration * 0.16f),
                exitUnlockBeaconSortingOrder);
        }

        private void HandleExitEntered()
        {
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.GenerateNextStage();
        }

        private void ClearExistingObjects()
        {
            if (exitUnlockPressureRoutine != null)
            {
                StopCoroutine(exitUnlockPressureRoutine);
                exitUnlockPressureRoutine = null;
            }

            for (int i = 0; i < activePickups.Count; i++)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup != null)
                {
                    pickup.Collected -= HandlePickupCollected;
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
                    DestroySafe(pickup.gameObject);
                }
            }

            activeStaminaPickups.Clear();

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





