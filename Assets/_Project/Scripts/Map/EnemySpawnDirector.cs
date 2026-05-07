using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class EnemySpawnDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private StagePressureDirector stagePressureDirector;
        [SerializeField] private Transform enemiesRoot;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private EnemyProfile[] profilePool;

        [Header("Spawn Rules")]
        [SerializeField, Min(1)] private int baseEnemyCount = 2;
        [SerializeField, Min(0)] private int enemyIncreasePerStage = 1;
        [SerializeField, Min(1)] private int maxEnemyCount = 8;
        [SerializeField, Min(0)] private int minDistanceFromStartCells = 4;
        [SerializeField, Min(0.05f)] private float enemyCorridorWeight = 0.85f;
        [SerializeField, Min(0.05f)] private float enemyRoomWeight = 1.15f;
        [SerializeField, Min(0.05f)] private float enemyForkWeight = 1.3f;
        [SerializeField, Min(0.05f)] private float enemyHideoutWeight = 0.55f;
        [SerializeField, Min(0.05f)] private float enemyRiskWeight = 1.7f;

        [Header("Spawn Safety")]
        [SerializeField] private bool avoidNarrowSpawnCells = true;
        [SerializeField, Min(0f)] private float spawnStabilizationSeconds = 0.38f;

        [Header("Contact Damage")]
        [SerializeField, Min(1)] private int damagePerHit = 1;
        [SerializeField, Min(0.05f)] private float hitIntervalSeconds = 0.75f;

        [Header("Runtime Pressure")]
        [SerializeField, Range(0.5f, 2.5f)] private float runtimeEnemyCountMultiplier = 1f;
        [SerializeField, Range(0.6f, 2.5f)] private float runtimeRiskWeightMultiplier = 1f;
        [SerializeField, Range(0f, 0.85f)] private float runtimeSeekerExtraChance = 0f;
        [SerializeField, Range(0f, 0.65f)] private float runtimeStartDistanceReduction = 0f;
        [SerializeField] private bool autoRebuildOnPressureChange = true;
        [SerializeField] private bool clearOrphanEnemiesOnRebuild = true;
        [Header("Undead Survivor Visual")]
        [SerializeField] private bool useUndeadSurvivorVisuals = true;
        [SerializeField] private RuntimeAnimatorController undeadEnemyBaseController;
        [SerializeField] private AnimatorOverrideController[] undeadEnemyOverrideControllers;
        [SerializeField, Min(0.1f)] private float undeadEnemyScale = 1.05f;
        [SerializeField] private bool autoBindUndeadAssetsInEditor = true;

        private readonly List<EnemyController> activeEnemies = new();
        private Sprite debugSprite;
        private int lastSpawnTargetEnemyCount;
        private int lastSeekerSpawnCount;
        private int lastSpawnStage;
        private int lastOpenSpawnCandidateCount;
        private int lastNarrowSpawnCandidateCount;
        private int lastSelectedNarrowSpawnCount;

        public int ActiveEnemyCount => activeEnemies.Count;
        public int LastSpawnTargetEnemyCount => lastSpawnTargetEnemyCount;
        public int LastSeekerSpawnCount => lastSeekerSpawnCount;
        public int LastSpawnStage => lastSpawnStage;
        public int LastOpenSpawnCandidateCount => lastOpenSpawnCandidateCount;
        public int LastNarrowSpawnCandidateCount => lastNarrowSpawnCandidateCount;
        public int LastSelectedNarrowSpawnCount => lastSelectedNarrowSpawnCount;
        public bool LastNarrowSpawnsWereFallbackOnly => lastSelectedNarrowSpawnCount <= 0 || lastOpenSpawnCandidateCount < lastSpawnTargetEnemyCount;
        public float RuntimeEnemyCountMultiplier => runtimeEnemyCountMultiplier;
        public float RuntimeRiskWeightMultiplier => runtimeRiskWeightMultiplier;
        public float RuntimeSeekerExtraChance => runtimeSeekerExtraChance;
        public float RuntimeStartDistanceReduction => runtimeStartDistanceReduction;

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeMap();
        }

        private void OnDisable()
        {
            UnsubscribeMap();
        }

        private void Start()
        {
            ResolveReferences();

            if (mapSystem != null && mapSystem.LastGeneratedCells.Count > 0)
            {
                BuildEnemies(mapSystem.CurrentStage, mapSystem.LastGeneratedCells);
            }
        }

        public void SetMapSystemForEditor(MapSystem targetMapSystem)
        {
            if (mapSystem == targetMapSystem)
            {
                return;
            }

            UnsubscribeMap();
            mapSystem = targetMapSystem;
            SubscribeMap();
        }

        public void SetRuntimeRootsForEditor(Transform enemiesParent, Transform player)
        {
            enemiesRoot = enemiesParent;
            playerTarget = player;
        }

        public void SetProfilePoolForEditor(EnemyProfile[] profiles)
        {
            profilePool = profiles;
        }

        public void ApplyPressureForRuntime(
            float enemyCountMultiplier,
            float riskWeightMultiplier,
            float seekerExtraChance,
            float startDistanceReduction,
            bool rebuild = true)
        {
            runtimeEnemyCountMultiplier = Mathf.Clamp(enemyCountMultiplier, 0.5f, 2.5f);
            runtimeRiskWeightMultiplier = Mathf.Clamp(riskWeightMultiplier, 0.6f, 2.5f);
            runtimeSeekerExtraChance = Mathf.Clamp01(seekerExtraChance);
            runtimeStartDistanceReduction = Mathf.Clamp(startDistanceReduction, 0f, 0.65f);

            if (!rebuild || !autoRebuildOnPressureChange)
            {
                return;
            }

            if (mapSystem != null && mapSystem.LastGeneratedCells.Count > 0)
            {
                BuildEnemies(mapSystem.CurrentStage, mapSystem.LastGeneratedCells);
            }
        }

        public void ResetPressureForRuntime(bool rebuild = true)
        {
            ApplyPressureForRuntime(1f, 1f, 0f, 0f, rebuild);
        }

        public int SpawnSetPieceReinforcements(int stage, int additionalCount, Vector3 focusWorld, float focusRadiusWorld = 6f)
        {
            ResolveReferences();

            if (additionalCount <= 0 || mapSystem == null || enemiesRoot == null)
            {
                return 0;
            }

            IReadOnlyList<GeneratedMapCell> cells = mapSystem.LastGeneratedCells;
            if (cells == null || cells.Count <= 0)
            {
                return 0;
            }

            List<GeneratedMapCell> candidates = BuildSpawnCandidates(cells);
            if (candidates.Count <= 0)
            {
                return 0;
            }

            float cellSize = Mathf.Max(0.01f, mapSystem.CellSize);
            if (focusRadiusWorld > 0f)
            {
                float radiusCells = focusRadiusWorld / cellSize;
                float radiusSqr = radiusCells * radiusCells;
                Vector2 focusCell = new(focusWorld.x / cellSize, focusWorld.y / cellSize);

                List<GeneratedMapCell> focused = new();
                for (int i = 0; i < candidates.Count; i++)
                {
                    Vector2 candidateCell = candidates[i].position;
                    if ((candidateCell - focusCell).sqrMagnitude <= radiusSqr)
                    {
                        focused.Add(candidates[i]);
                    }
                }

                if (focused.Count > 0)
                {
                    candidates = focused;
                }
            }

            int targetCount = Mathf.Clamp(additionalCount, 1, candidates.Count);
            candidates = PreferOpenSpawnCandidates(candidates, targetCount, cells);
            targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
            int seed = Mathf.Max(1, stage) * 911
                       + Mathf.RoundToInt(focusWorld.x * 37f)
                       + Mathf.RoundToInt(focusWorld.y * 53f)
                       + candidates.Count * 13
                       + activeEnemies.Count * 7;

            System.Random random = new(seed);
            int spawned = 0;
            int seekerSpawned = 0;

            while (spawned < targetCount && candidates.Count > 0)
            {
                int index = PickWeightedIndex(candidates, random, GetSpawnWeight);
                if (index < 0 || index >= candidates.Count)
                {
                    index = random.Next(0, candidates.Count);
                }

                GeneratedMapCell candidate = candidates[index];
                candidates.RemoveAt(index);

                Vector3 world = ToWorld(candidate.position);
                if (IsSpawnPositionOccupied(world, cellSize * 0.55f))
                {
                    continue;
                }

                EnemyProfile profile = ChooseProfile(Mathf.Max(1, stage), activeEnemies.Count + spawned);
                if (IsSeekerProfile(profile))
                {
                    seekerSpawned++;
                }

                SpawnEnemy(candidate, lastSpawnTargetEnemyCount + spawned, profile);
                spawned++;
            }

            if (spawned > 0)
            {
                lastSpawnTargetEnemyCount += spawned;
                lastSeekerSpawnCount += seekerSpawned;
                lastSpawnStage = Mathf.Max(1, stage);
            }

            return spawned;
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (stagePressureDirector == null)
            {
                stagePressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (enemiesRoot == null)
            {
                enemiesRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/Enemies");
            }

            if (playerTarget == null)
            {
                playerTarget = TryFindPlayerTransform();
            }
        }

        private void SubscribeMap()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
                mapSystem.MapGenerated += HandleMapGenerated;
            }
        }

        private void UnsubscribeMap()
        {
            if (mapSystem != null)
            {
                mapSystem.MapGenerated -= HandleMapGenerated;
            }
        }

        private void HandleMapGenerated(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            ResolveReferences();

            // StagePressureDirector can drive a single enemy rebuild for this map event.
            // Skip direct rebuild here to avoid duplicate BuildEnemies() calls.
            if (stagePressureDirector != null && stagePressureDirector.RebuildsEnemiesOnMapGenerated)
            {
                return;
            }

            BuildEnemies(stage, cells);
        }

        private void BuildEnemies(int stage, IReadOnlyList<GeneratedMapCell> cells)
        {
            ClearExistingEnemies();

            if (mapSystem == null || cells == null || cells.Count == 0)
            {
                lastSpawnTargetEnemyCount = 0;
                lastSeekerSpawnCount = 0;
                lastSpawnStage = stage;
                ResetSpawnSafetyTelemetry();
                return;
            }

            if (enemiesRoot == null)
            {
                ResetSpawnSafetyTelemetry();
                return;
            }

            if (playerTarget == null)
            {
                playerTarget = TryFindPlayerTransform();
            }

            List<GeneratedMapCell> spawnCandidates = BuildSpawnCandidates(cells);
            if (spawnCandidates.Count == 0)
            {
                lastSpawnTargetEnemyCount = 0;
                lastSeekerSpawnCount = 0;
                lastSpawnStage = stage;
                ResetSpawnSafetyTelemetry();
                return;
            }

            int baseTargetCount = Mathf.Clamp(baseEnemyCount + (stage - 1) * enemyIncreasePerStage, 1, maxEnemyCount);
            int targetEnemyCount = Mathf.RoundToInt(baseTargetCount * runtimeEnemyCountMultiplier);
            targetEnemyCount = Mathf.Clamp(targetEnemyCount, 1, maxEnemyCount);
            targetEnemyCount = Mathf.Min(targetEnemyCount, spawnCandidates.Count);
            CountSpawnCandidateSafety(spawnCandidates, cells, out lastOpenSpawnCandidateCount, out lastNarrowSpawnCandidateCount);
            spawnCandidates = PreferOpenSpawnCandidates(spawnCandidates, targetEnemyCount, cells);
            targetEnemyCount = Mathf.Min(targetEnemyCount, spawnCandidates.Count);

            List<GeneratedMapCell> selectedSpawnCells = SelectWeightedCells(
                spawnCandidates,
                targetEnemyCount,
                stage * 313 + cells.Count * 17,
                GetSpawnWeight);

            int seekerCount = 0;
            for (int i = 0; i < selectedSpawnCells.Count; i++)
            {
                EnemyProfile profile = ChooseProfile(stage, i);
                if (IsSeekerProfile(profile))
                {
                    seekerCount++;
                }

                SpawnEnemy(selectedSpawnCells[i], i, profile);
            }

            lastSpawnTargetEnemyCount = selectedSpawnCells.Count;
            lastSeekerSpawnCount = seekerCount;
            lastSpawnStage = stage;
            lastSelectedNarrowSpawnCount = CountNarrowSpawnCells(selectedSpawnCells, cells);
        }

        private List<GeneratedMapCell> BuildSpawnCandidates(IReadOnlyList<GeneratedMapCell> cells)
        {
            List<GeneratedMapCell> candidates = new();

            Vector2Int startCell = cells[0].position;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == MapCellKind.Start)
                {
                    startCell = cells[i].position;
                    break;
                }
            }

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];

                if (cell.kind is MapCellKind.Start or MapCellKind.Exit)
                {
                    continue;
                }

                int requiredDistance = Mathf.Max(0, Mathf.RoundToInt(minDistanceFromStartCells * (1f - runtimeStartDistanceReduction)));
                int distance = Mathf.Abs(cell.position.x - startCell.x) + Mathf.Abs(cell.position.y - startCell.y);
                if (distance < requiredDistance)
                {
                    continue;
                }

                bool validKind = cell.kind is MapCellKind.Corridor or MapCellKind.Room or MapCellKind.Fork or MapCellKind.Hideout or MapCellKind.Risk;
                if (!validKind)
                {
                    continue;
                }

                candidates.Add(cell);
            }

            return candidates;
        }

        private List<GeneratedMapCell> PreferOpenSpawnCandidates(
            List<GeneratedMapCell> candidates,
            int desiredCount,
            IReadOnlyList<GeneratedMapCell> cells)
        {
            if (!avoidNarrowSpawnCells || candidates == null || candidates.Count <= 1 || desiredCount <= 0)
            {
                return candidates;
            }

            HashSet<Vector2Int> occupiedCells = BuildOccupiedCellSet(cells);
            if (occupiedCells.Count == 0)
            {
                return candidates;
            }

            List<GeneratedMapCell> openCandidates = new();
            List<GeneratedMapCell> fallbackNarrowCandidates = new();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (IsNarrowSpawnCell(candidates[i], occupiedCells))
                {
                    fallbackNarrowCandidates.Add(candidates[i]);
                    continue;
                }

                openCandidates.Add(candidates[i]);
            }

            if (openCandidates.Count <= 0)
            {
                return candidates;
            }

            if (openCandidates.Count >= desiredCount)
            {
                return openCandidates;
            }

            openCandidates.AddRange(fallbackNarrowCandidates);
            return openCandidates;
        }

        private static HashSet<Vector2Int> BuildOccupiedCellSet(IReadOnlyList<GeneratedMapCell> cells)
        {
            HashSet<Vector2Int> occupiedCells = new();
            if (cells == null)
            {
                return occupiedCells;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                occupiedCells.Add(cells[i].position);
            }

            return occupiedCells;
        }

        private static bool IsNarrowSpawnCell(GeneratedMapCell cell, HashSet<Vector2Int> occupiedCells)
        {
            if (occupiedCells == null || occupiedCells.Count == 0)
            {
                return false;
            }

            int cardinalNeighbors = CountCardinalNeighbors(cell.position, occupiedCells);
            if (cardinalNeighbors <= 1)
            {
                return true;
            }

            if (cell.kind != MapCellKind.Corridor || cardinalNeighbors != 2)
            {
                return false;
            }

            bool horizontal = occupiedCells.Contains(cell.position + Vector2Int.left)
                && occupiedCells.Contains(cell.position + Vector2Int.right);
            bool vertical = occupiedCells.Contains(cell.position + Vector2Int.up)
                && occupiedCells.Contains(cell.position + Vector2Int.down);
            return horizontal || vertical;
        }

        private static int CountCardinalNeighbors(Vector2Int position, HashSet<Vector2Int> occupiedCells)
        {
            int count = 0;
            if (occupiedCells.Contains(position + Vector2Int.left))
            {
                count++;
            }

            if (occupiedCells.Contains(position + Vector2Int.right))
            {
                count++;
            }

            if (occupiedCells.Contains(position + Vector2Int.up))
            {
                count++;
            }

            if (occupiedCells.Contains(position + Vector2Int.down))
            {
                count++;
            }

            return count;
        }

        private static void CountSpawnCandidateSafety(
            IReadOnlyList<GeneratedMapCell> candidates,
            IReadOnlyList<GeneratedMapCell> cells,
            out int openCount,
            out int narrowCount)
        {
            openCount = 0;
            narrowCount = 0;
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> occupiedCells = BuildOccupiedCellSet(cells);
            if (occupiedCells.Count == 0)
            {
                openCount = candidates.Count;
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (IsNarrowSpawnCell(candidates[i], occupiedCells))
                {
                    narrowCount++;
                    continue;
                }

                openCount++;
            }
        }

        private static int CountNarrowSpawnCells(IReadOnlyList<GeneratedMapCell> selectedCells, IReadOnlyList<GeneratedMapCell> cells)
        {
            if (selectedCells == null || selectedCells.Count == 0)
            {
                return 0;
            }

            HashSet<Vector2Int> occupiedCells = BuildOccupiedCellSet(cells);
            if (occupiedCells.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < selectedCells.Count; i++)
            {
                if (IsNarrowSpawnCell(selectedCells[i], occupiedCells))
                {
                    count++;
                }
            }

            return count;
        }

        private void ResetSpawnSafetyTelemetry()
        {
            lastOpenSpawnCandidateCount = 0;
            lastNarrowSpawnCandidateCount = 0;
            lastSelectedNarrowSpawnCount = 0;
        }

        private float GetSpawnWeight(MapCellKind kind)
        {
            return kind switch
            {
                MapCellKind.Corridor => enemyCorridorWeight,
                MapCellKind.Room => enemyRoomWeight,
                MapCellKind.Fork => enemyForkWeight,
                MapCellKind.Hideout => enemyHideoutWeight,
                MapCellKind.Risk => enemyRiskWeight * runtimeRiskWeightMultiplier,
                _ => 0.2f
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
                int index = PickWeightedIndex(pool, random, weightEvaluator);
                if (index < 0 || index >= pool.Count)
                {
                    index = random.Next(0, pool.Count);
                }

                selected.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return selected;
        }

        private static int PickWeightedIndex(
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
                totalWeight += Mathf.Max(0.01f, weightEvaluator(candidates[i].kind));
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

        private bool IsSpawnPositionOccupied(Vector3 worldPosition, float minDistance)
        {
            float safeMinDistance = Mathf.Max(0.1f, minDistance);
            float safeMinDistanceSqr = safeMinDistance * safeMinDistance;

            if (playerTarget != null)
            {
                Vector3 playerDelta = playerTarget.position - worldPosition;
                if (playerDelta.sqrMagnitude <= safeMinDistanceSqr * 2.1f)
                {
                    return true;
                }
            }

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                Vector3 delta = enemy.transform.position - worldPosition;
                if (delta.sqrMagnitude <= safeMinDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnEnemy(GeneratedMapCell cell, int index, EnemyProfile profile)
        {
            GameObject enemyObject = new($"Enemy_{index:00}_{(profile != null ? profile.profileId : "default")}");
            enemyObject.transform.SetParent(enemiesRoot, false);
            enemyObject.transform.position = ToWorld(cell.position);

            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 19;

            bool undeadApplied = TryApplyUndeadSurvivorVisual(enemyObject, renderer, profile, index);
            if (undeadApplied)
            {
                enemyObject.transform.localScale = Vector3.one * undeadEnemyScale;
                renderer.color = Color.white;
            }
            else
            {
                enemyObject.transform.localScale = Vector3.one * 0.7f;
                renderer.sprite = GetDebugSprite();
                renderer.color = GetEnemyColor(profile, cell.kind);
            }

            CircleCollider2D collider = enemyObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.38f;
            collider.isTrigger = true;

            EnemyContactDamage contactDamage = enemyObject.AddComponent<EnemyContactDamage>();
            contactDamage.Configure(damagePerHit, hitIntervalSeconds);

            EnemyController controller = enemyObject.AddComponent<EnemyController>();
            if (profile != null)
            {
                controller.SetProfileReference(profile);
            }

            if (playerTarget != null)
            {
                controller.SetPlayerReference(playerTarget);
            }

            controller.ConfigureMapBoundsConstraintForRuntime(true);
            controller.PrimeSpawnStabilizationForRuntime(spawnStabilizationSeconds);

            activeEnemies.Add(controller);
        }

        private bool TryApplyUndeadSurvivorVisual(GameObject enemyObject, SpriteRenderer renderer, EnemyProfile profile, int index)
        {
            if (!useUndeadSurvivorVisuals || enemyObject == null || renderer == null)
            {
                return false;
            }

            EnsureUndeadControllersInEditor();

            RuntimeAnimatorController controller = SelectUndeadController(profile, index);
            if (controller == null)
            {
                return false;
            }

            Animator animator = enemyObject.GetComponent<Animator>();
            if (animator == null)
            {
                animator = enemyObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            renderer.sprite = null;
            return true;
        }

        private RuntimeAnimatorController SelectUndeadController(EnemyProfile profile, int index)
        {
            int variant = EvaluateUndeadVariant(profile, index);
            if (variant <= 0)
            {
                return undeadEnemyBaseController;
            }

            int overrideIndex = variant - 1;
            if (undeadEnemyOverrideControllers != null
                && overrideIndex >= 0
                && overrideIndex < undeadEnemyOverrideControllers.Length)
            {
                return undeadEnemyOverrideControllers[overrideIndex];
            }

            return undeadEnemyBaseController;
        }

        private static int EvaluateUndeadVariant(EnemyProfile profile, int index)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.profileId))
            {
                string id = profile.profileId.ToLowerInvariant();
                if (id.Contains("obsessive"))
                {
                    return 1;
                }

                if (id.Contains("cautious"))
                {
                    return 2;
                }

                if (id.Contains("impulsive"))
                {
                    return 3;
                }

                if (id.Contains("flanker"))
                {
                    return 4;
                }

                if (id.Contains("seeker"))
                {
                    return 0;
                }
            }

            return Mathf.Abs(index % 5);
        }

        private void EnsureUndeadControllersInEditor()
        {
#if UNITY_EDITOR
            if (!autoBindUndeadAssetsInEditor)
            {
                return;
            }

            bool needsBase = undeadEnemyBaseController == null;
            bool needsOverrides = undeadEnemyOverrideControllers == null || undeadEnemyOverrideControllers.Length < 4;
            if (!needsBase && !needsOverrides)
            {
                return;
            }

            if (undeadEnemyOverrideControllers == null || undeadEnemyOverrideControllers.Length < 4)
            {
                undeadEnemyOverrideControllers = new AnimatorOverrideController[4];
            }

            undeadEnemyBaseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Undead Survivor/Animations/Enemy/AcEnemy 0.controller");

            undeadEnemyOverrideControllers[0] = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/Undead Survivor/Animations/Enemy/AcEnemy 1.overrideController");
            undeadEnemyOverrideControllers[1] = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/Undead Survivor/Animations/Enemy/AcEnemy 2.overrideController");
            undeadEnemyOverrideControllers[2] = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/Undead Survivor/Animations/Enemy/AcEnemy 3.overrideController");
            undeadEnemyOverrideControllers[3] = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                "Assets/Undead Survivor/Animations/Enemy/AcEnemy 4.overrideController");

            EditorUtility.SetDirty(this);
#endif
        }

        private EnemyProfile ChooseProfile(int stage, int index)
        {
            if (profilePool == null || profilePool.Length == 0)
            {
                return null;
            }

            List<EnemyProfile> nonNull = new();
            for (int i = 0; i < profilePool.Length; i++)
            {
                if (profilePool[i] != null)
                {
                    nonNull.Add(profilePool[i]);
                }
            }

            if (nonNull.Count == 0)
            {
                return null;
            }

            int seekerIndex = -1;
            for (int i = 0; i < nonNull.Count; i++)
            {
                string id = nonNull[i].profileId != null ? nonNull[i].profileId.ToLowerInvariant() : string.Empty;
                if (id.Contains("seeker"))
                {
                    seekerIndex = i;
                    break;
                }
            }

            if (seekerIndex >= 0)
            {
                bool shouldForceSeeker = (stage >= 3 && (stage + index) % 3 == 0) || (stage >= 5 && index == 0);
                if (shouldForceSeeker)
                {
                    return nonNull[seekerIndex];
                }

                if (runtimeSeekerExtraChance > 0.001f)
                {
                    float seekerRoll = EvaluateDeterministicRoll(stage, index);
                    if (seekerRoll <= runtimeSeekerExtraChance)
                    {
                        return nonNull[seekerIndex];
                    }
                }
            }

            int profileIndex = Mathf.Abs((stage * 3 + index * 5) % nonNull.Count);
            return nonNull[profileIndex];
        }

        private static float EvaluateDeterministicRoll(int stage, int index)
        {
            unchecked
            {
                int hash = stage * 73856093 ^ index * 19349663 ^ 83492791;
                hash ^= (hash << 13);
                hash ^= (hash >> 17);
                hash ^= (hash << 5);

                uint value = (uint)hash;
                return (value % 1000u) / 1000f;
            }
        }

        private static bool IsSeekerProfile(EnemyProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            string id = profile.profileId != null ? profile.profileId.ToLowerInvariant() : string.Empty;
            return id.Contains("seeker");
        }

        private void ClearExistingEnemies()
        {
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy != null)
                {
                    DestroySafe(enemy.gameObject);
                }
            }

            activeEnemies.Clear();

            if (enemiesRoot != null)
            {
                for (int i = enemiesRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = enemiesRoot.GetChild(i);
                    DestroySafe(child.gameObject);
                }
            }

            if (!clearOrphanEnemiesOnRebuild)
            {
                return;
            }

            EnemyController[] sceneEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (int i = 0; i < sceneEnemies.Length; i++)
            {
                EnemyController enemy = sceneEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                Transform enemyTransform = enemy.transform;
                bool underEnemyRoot = enemiesRoot != null && enemyTransform.IsChildOf(enemiesRoot);
                bool runtimeSpawnName = enemy.name.StartsWith("Enemy_", StringComparison.Ordinal);
                if (!underEnemyRoot && !runtimeSpawnName)
                {
                    continue;
                }

                DestroySafe(enemy.gameObject);
            }
        }

        private Vector3 ToWorld(Vector2Int cellPosition)
        {
            float cellSize = mapSystem != null ? mapSystem.CellSize : 1f;
            return new Vector3(cellPosition.x * cellSize, cellPosition.y * cellSize, 0f);
        }

        private Transform TryFindPlayerTransform()
        {
            try
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                return player != null ? player.transform : null;
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private Sprite GetDebugSprite()
        {
            if (debugSprite != null)
            {
                return debugSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "EnemySpawnDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "EnemySpawnDebugSprite";
            debugSprite.hideFlags = HideFlags.HideAndDontSave;
            return debugSprite;
        }

        private static Color GetEnemyColor(EnemyProfile profile, MapCellKind spawnKind)
        {
            if (profile != null)
            {
                string id = profile.profileId != null ? profile.profileId.ToLowerInvariant() : string.Empty;
                if (id.Contains("obsessive"))
                {
                    return new Color(1f, 0.3f, 0.35f, 0.95f);
                }

                if (id.Contains("cautious"))
                {
                    return new Color(1f, 0.75f, 0.25f, 0.95f);
                }

                if (id.Contains("impulsive"))
                {
                    return new Color(1f, 0.2f, 0.65f, 0.95f);
                }

                if (id.Contains("flanker"))
                {
                    return new Color(0.6f, 0.35f, 1f, 0.95f);
                }

                if (id.Contains("seeker"))
                {
                    return new Color(0.2f, 0.95f, 0.95f, 0.95f);
                }
            }

            return spawnKind switch
            {
                MapCellKind.Fork => new Color(1f, 0.5f, 0.25f, 0.95f),
                MapCellKind.Room => new Color(0.75f, 0.45f, 1f, 0.95f),
                MapCellKind.Risk => new Color(1f, 0.2f, 0.2f, 0.95f),
                _ => new Color(0.85f, 0.35f, 0.35f, 0.95f)
            };
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


