using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostBreadcrumbs.Runtime.Systems
{
    public sealed class MapSystem : RuntimeSystemBase
    {
        private const string DefaultMapConfigAssetPath = "Assets/_Project/ScriptableObjects/Map/SO_SequentialMapConfig.asset";

        public event Action<int, IReadOnlyList<GeneratedMapCell>> MapGenerated;

        [Header("Config")]
        [SerializeField] private SequentialMapConfig config;
        [SerializeField, Min(1)] private int currentStage = 1;
        [SerializeField] private bool generateOnStart = true;

        [Header("Build")]
        [SerializeField] private bool clearPreviousOnGenerate = true;
        [SerializeField] private bool aggressiveGeneratedRootCleanup = true;
        [SerializeField] private bool createFloorSpriteRenderer = true;
        [SerializeField] private bool createWallSpriteRenderer = true;
        [SerializeField] private bool createCollisionOnRiskTiles = true;
        [SerializeField] private bool preventInvisibleBlockingCollision = true;
        [SerializeField] private bool logSummary = true;

        [Header("Walls")]
        [SerializeField] private bool buildBoundaryWalls = true;
        [SerializeField] private bool createWallColliders = true;
        [SerializeField, Min(0.02f)] private float wallThickness = 0.22f;
        [SerializeField] private Color wallColor = new(0.12f, 0.12f, 0.16f, 0.95f);
        [SerializeField] private int wallSortingOrder = 6;

        [Header("Room Archetypes (Occluders)")]
        [SerializeField] private bool buildRoomOccluders = true;
        [SerializeField] private bool createOccluderColliders = true;
        [SerializeField] private bool createOccluderSpriteRenderer = true;
        [SerializeField, Range(0f, 1f)] private float roomOccluderChance = 0.88f;
        [SerializeField, Range(0f, 1f)] private float forkOccluderChance = 0.72f;
        [SerializeField, Range(0f, 1f)] private float hideoutOccluderChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float riskOccluderChance = 0.62f;
        [SerializeField, Min(0)] private int roomOccluderMinCount = 1;
        [SerializeField, Min(0)] private int roomOccluderMaxCount = 3;
        [SerializeField, Min(0)] private int forkOccluderMinCount = 1;
        [SerializeField, Min(0)] private int forkOccluderMaxCount = 2;
        [SerializeField, Min(0)] private int hideoutOccluderMinCount = 1;
        [SerializeField, Min(0)] private int hideoutOccluderMaxCount = 2;
        [SerializeField, Min(0)] private int riskOccluderMinCount = 1;
        [SerializeField, Min(0)] private int riskOccluderMaxCount = 2;
        [SerializeField, Range(0.08f, 0.55f)] private float occluderMinSizeRatio = 0.2f;
        [SerializeField, Range(0.1f, 0.75f)] private float occluderMaxSizeRatio = 0.38f;
        [SerializeField, Range(0.02f, 0.4f)] private float occluderEdgePaddingRatio = 0.13f;
        [SerializeField, Range(0.08f, 0.45f)] private float occluderCenterReserveRatio = 0.24f;
        [SerializeField] private Color occluderColor = new(0.2f, 0.18f, 0.15f, 0.92f);
        [SerializeField] private int occluderSortingOrder = 5;
        [SerializeField] private int occluderSeedSalt = 1947;

        [Header("Room Archetypes (Choke Lanes)")]
        [SerializeField] private bool buildChokeLaneOccluders = true;
        [SerializeField, Range(0f, 1f)] private float roomChokeChance = 0.38f;
        [SerializeField, Range(0f, 1f)] private float forkChokeChance = 0.58f;
        [SerializeField, Range(0f, 1f)] private float hideoutChokeChance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float riskChokeChance = 0.68f;
        [SerializeField, Range(0.08f, 0.62f)] private float chokeBarLengthRatio = 0.58f;
        [SerializeField, Range(0.05f, 0.35f)] private float chokeBarThicknessRatio = 0.15f;
        [SerializeField, Range(0.06f, 0.42f)] private float chokeBarOffsetRatio = 0.22f;
        [SerializeField, Range(0.08f, 0.5f)] private float chokePassGapRatio = 0.2f;
        [SerializeField] private int chokeSeedSalt = 6131;

        [Header("Room Archetypes (Interaction Hooks)")]
        [SerializeField] private bool buildRoomInteractionHooks = true;
        [SerializeField] private bool createHookTriggerColliders = true;
        [SerializeField] private bool createHookSpriteRenderer = true;
        [SerializeField, Range(0f, 1f)] private float corridorHookChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float roomHookChance = 0.44f;
        [SerializeField, Range(0f, 1f)] private float forkHookChance = 0.56f;
        [SerializeField, Range(0f, 1f)] private float hideoutHookChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float riskHookChance = 0.72f;
        [SerializeField, Min(0)] private int corridorHookMinCount = 0;
        [SerializeField, Min(0)] private int corridorHookMaxCount = 1;
        [SerializeField, Min(0)] private int roomHookMinCount = 1;
        [SerializeField, Min(0)] private int roomHookMaxCount = 2;
        [SerializeField, Min(0)] private int forkHookMinCount = 1;
        [SerializeField, Min(0)] private int forkHookMaxCount = 2;
        [SerializeField, Min(0)] private int hideoutHookMinCount = 0;
        [SerializeField, Min(0)] private int hideoutHookMaxCount = 1;
        [SerializeField, Min(0)] private int riskHookMinCount = 1;
        [SerializeField, Min(0)] private int riskHookMaxCount = 2;
        [SerializeField, Range(0.06f, 0.45f)] private float hookMinRadiusRatio = 0.14f;
        [SerializeField, Range(0.08f, 0.58f)] private float hookMaxRadiusRatio = 0.22f;
        [SerializeField, Range(0.04f, 0.4f)] private float hookEdgePaddingRatio = 0.11f;
        [SerializeField, Min(0.1f)] private float hookBaseLoudness = 0.95f;
        [SerializeField, Min(0.5f)] private float hookBaseRadius = 6.4f;
        [SerializeField, Min(0.1f)] private float hookBaseCooldown = 7.2f;
        [SerializeField] private Color hookColor = new(0.92f, 0.54f, 0.32f, 0.86f);
        [SerializeField] private int hookSortingOrder = 22;
        [SerializeField] private int hookSeedSalt = 4703;

        [Header("Room Archetypes (Hook Runtime Tuning)")]
        [SerializeField] private bool scaleHooksByStageAndPreset = true;
        [SerializeField, Min(1)] private int hookPressureRampStartStage = 1;
        [SerializeField, Min(2)] private int hookPressureRampEndStage = 8;
        [SerializeField, Range(0f, 0.5f)] private float hookChanceStageBonusMax = 0.22f;
        [SerializeField, Range(0f, 0.75f)] private float hookLoudnessStageBonusMax = 0.34f;
        [SerializeField, Range(0f, 0.65f)] private float hookRadiusStageBonusMax = 0.26f;
        [SerializeField, Range(0f, 0.65f)] private float hookCooldownStageReductionMax = 0.32f;
        [SerializeField, Range(0.6f, 1.4f)] private float compactPresetHookIntensity = 0.9f;
        [SerializeField, Range(0.6f, 1.4f)] private float standardPresetHookIntensity = 1f;
        [SerializeField, Range(0.6f, 1.4f)] private float expansivePresetHookIntensity = 1.12f;
        [SerializeField] private bool createHookTensionProbeDummy = true;
        [SerializeField] private bool hookTensionProbeEditorOnly = true;

        [Header("Integration")]
        [SerializeField] private bool movePlayerToStartOnGenerate = true;
        [SerializeField] private bool resolveSafePlayerSpawn = true;
        [SerializeField, Min(0.05f)] private float playerSpawnClearanceRadius = 0.38f;
        [SerializeField, Min(0.05f)] private float playerSpawnSearchStep = 0.35f;
        [SerializeField, Range(1, 8)] private int playerSpawnSearchRings = 5;
        [SerializeField, Min(0f)] private float playerSpawnCellInset = 0.42f;
        [SerializeField] private LayerMask playerSpawnBlockerMask = ~0;
        [SerializeField] private bool playerSpawnGeneratedBlockersOnly;
        [SerializeField] private bool updateFogBounds = true;
        [SerializeField, Min(0f)] private float fogPadding = 5f;
        [SerializeField] private FogOfWarSystem fogSystem;
        [Header("Camera Fit")]
        [SerializeField] private bool autoFitCameraOnGenerate = true;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(0f)] private float cameraFitPadding = 1.25f;
        [SerializeField, Min(1f)] private float minOrthographicSize = 4.25f;
        [SerializeField, Min(1f)] private float maxOrthographicSize = 5.75f;
        [SerializeField] private bool applyCameraBoundsToFollow = true;
        [SerializeField, Min(0f)] private float cameraFollowBoundsPadding = 0.45f;

        private readonly List<GeneratedMapCell> lastGeneratedCells = new();

        private Transform generatedRoot;
        private Transform generatedWallRoot;
        private Transform generatedOccluderRoot;
        private Transform generatedHookRoot;
        private Sprite debugSprite;
        private int lastWallSegmentCount;
        private int lastOccluderCount;
        private int lastChokeOccluderCount;
        private int lastArchetypeHookCount;
        private int lastRiskTileColliderCount;
        private int lastFallbackVisibleColliderCount;
        private bool lastPlayerSpawnAdjusted;
        private bool lastPlayerSpawnUsedBlockedFallback;
        private Vector3 lastPlayerSpawnPosition;
        private bool loggedRiskFloorVisibilityGuard;
        private bool loggedPlayerSpawnBlockerScopeGuard;
        private float lastHookStagePressure01;
        private float lastHookChanceMultiplier = 1f;
        private float lastHookLoudnessMultiplier = 1f;
        private float lastHookRadiusMultiplier = 1f;
        private float lastHookCooldownMultiplier = 1f;
        private string lastHookPresetLabel = "None";
        private Vector2 lastGeneratedWorldCenter;
        private Vector2 lastGeneratedWorldSize = new(10f, 10f);
        private int currentStageVariantSalt;
        private readonly Collider2D[] playerSpawnOverlapHits = new Collider2D[24];

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public int CurrentStage => currentStage;
        public IReadOnlyList<GeneratedMapCell> LastGeneratedCells => lastGeneratedCells;
        public float CellSize => config != null ? config.cellSize : 1f;
        public SequentialMapConfig Config => config;
        public int LastWallSegmentCount => lastWallSegmentCount;
        public int LastOccluderCount => lastOccluderCount;
        public int LastChokeOccluderCount => lastChokeOccluderCount;
        public int LastArchetypeHookCount => lastArchetypeHookCount;
        public int LastRiskTileColliderCount => lastRiskTileColliderCount;
        public int LastFallbackVisibleColliderCount => lastFallbackVisibleColliderCount;
        public bool LastPlayerSpawnAdjusted => lastPlayerSpawnAdjusted;
        public bool LastPlayerSpawnUsedBlockedFallback => lastPlayerSpawnUsedBlockedFallback;
        public Vector3 LastPlayerSpawnPosition => lastPlayerSpawnPosition;
        public float LastHookStagePressure01 => lastHookStagePressure01;
        public float LastHookChanceMultiplier => lastHookChanceMultiplier;
        public float LastHookLoudnessMultiplier => lastHookLoudnessMultiplier;
        public float LastHookRadiusMultiplier => lastHookRadiusMultiplier;
        public float LastHookCooldownMultiplier => lastHookCooldownMultiplier;
        public string LastHookPresetLabel => string.IsNullOrWhiteSpace(lastHookPresetLabel) ? "None" : lastHookPresetLabel;
        public Vector2 LastGeneratedWorldCenter => lastGeneratedWorldCenter;
        public Vector2 LastGeneratedWorldSize => lastGeneratedWorldSize;
        public int CurrentStageVariantSalt => currentStageVariantSalt;

        private void Start()
        {
            TryAssignEditorDefaultConfigIfMissing();
            ApplyRuntimeVisibilitySafetyGuards();

            if (generateOnStart)
            {
                GenerateMapForStage(currentStage);
            }
        }

        public void SetConfigForEditor(SequentialMapConfig mapConfig)
        {
            config = mapConfig;
        }

        private void TryAssignEditorDefaultConfigIfMissing()
        {
#if UNITY_EDITOR
            if (config != null)
            {
                return;
            }

            SequentialMapConfig resolved = AssetDatabase.LoadAssetAtPath<SequentialMapConfig>(DefaultMapConfigAssetPath);
            if (resolved == null)
            {
                string[] mapConfigGuids = AssetDatabase.FindAssets("t:SequentialMapConfig");
                for (int i = 0; i < mapConfigGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(mapConfigGuids[i]);
                    resolved = AssetDatabase.LoadAssetAtPath<SequentialMapConfig>(path);
                    if (resolved != null)
                    {
                        break;
                    }
                }
            }

            if (resolved == null)
            {
                return;
            }

            config = resolved;
            Debug.Log($"MapSystem auto-assigned SequentialMapConfig in editor: {AssetDatabase.GetAssetPath(resolved)}", this);
#endif
        }

        public void SetFogSystemForEditor(FogOfWarSystem targetFogSystem)
        {
            fogSystem = targetFogSystem;
        }

        public void SetCameraForEditor(Camera cameraRef)
        {
            targetCamera = cameraRef;
        }

        public void ConfigureCameraFitForEditor(float padding, float minSize, float maxSize)
        {
            cameraFitPadding = Mathf.Max(0f, padding);
            minOrthographicSize = Mathf.Max(1f, minSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxSize);
        }

        public void ForceFogReset()
        {
            if (fogSystem == null)
            {
                Transform fogMask = EnsureHierarchy("TilemapRoot/FogMask");
                if (fogMask != null)
                {
                    fogSystem = fogMask.GetComponent<FogOfWarSystem>();
                }
            }

            if (fogSystem != null)
            {
                fogSystem.ResetFogToHidden();
            }
        }

        public bool TryGetSafePlayerStartPosition(Transform playerTransform, out Vector3 position)
        {
            position = playerTransform != null ? playerTransform.position : Vector3.zero;
            if (lastGeneratedCells.Count == 0 || config == null)
            {
                return false;
            }

            return TryResolveSafePlayerSpawnPosition(lastGeneratedCells, playerTransform, out position);
        }

        public bool TryResolveSafePlayerPosition(Vector3 preferredPosition, Transform playerTransform, out Vector3 position)
        {
            position = preferredPosition;
            if (lastGeneratedCells.Count == 0 || config == null)
            {
                return false;
            }

            float z = playerTransform != null ? playerTransform.position.z : preferredPosition.z;
            preferredPosition.z = z;
            return TryResolveSafePlayerPosition(lastGeneratedCells, preferredPosition, playerTransform, out position);
        }

        [ContextMenu("Generate Current Stage")]
        public void GenerateCurrentStage()
        {
            GenerateMapForStage(currentStage);
        }

        [ContextMenu("Generate Next Stage")]
        public void GenerateNextStage()
        {
            currentStage++;
            GenerateMapForStage(currentStage);
        }

        [ContextMenu("Reset To Stage 1 And Generate")]
        public void ResetAndGenerate()
        {
            currentStage = 1;
            GenerateMapForStage(currentStage);
        }

        public void RegenerateCurrentStageWithVariation()
        {
            currentStageVariantSalt = Mathf.Max(0, currentStageVariantSalt + 1);
            GenerateMapForStageInternal(currentStage, currentStageVariantSalt);
        }

        public void GenerateMapForStage(int stageIndex)
        {
            currentStageVariantSalt = 0;
            GenerateMapForStageInternal(stageIndex, currentStageVariantSalt);
        }

        private void GenerateMapForStageInternal(int stageIndex, int variationSalt)
        {
            ApplyRuntimeVisibilitySafetyGuards();

            if (config == null)
            {
                Debug.LogWarning("MapSystem requires SequentialMapConfig.", this);
                return;
            }

            currentStage = Mathf.Max(1, stageIndex);
            currentStageVariantSalt = Mathf.Max(0, variationSalt);
            List<GeneratedMapCell> generated = SequentialMapGenerator.Generate(currentStage, config, currentStageVariantSalt);

            lastGeneratedCells.Clear();
            lastGeneratedCells.AddRange(generated);
            UpdateGeneratedWorldBounds(generated);

            BuildGeneratedHierarchy(generated);

            if (movePlayerToStartOnGenerate)
            {
                MovePlayerToStart(generated);
            }

            if (updateFogBounds)
            {
                UpdateFogBounds(generated);
            }

            if (autoFitCameraOnGenerate)
            {
                FitCameraToGeneratedBounds(generated);
            }

            MapGenerated?.Invoke(currentStage, lastGeneratedCells);
            RuntimeEventBus.Raise(RuntimeEventType.Stage, string.Format("Stage generated {0} ({1} cells)", currentStage, generated.Count), this, currentStage);

            if (logSummary)
            {
                Debug.Log(
                    $"Map generated. Stage={currentStage}, Variant={currentStageVariantSalt}, Cells={generated.Count}, WallSegments={lastWallSegmentCount}, Occluders={lastOccluderCount} (Choke {lastChokeOccluderCount}), Hooks={lastArchetypeHookCount}, RiskColliders={lastRiskTileColliderCount}, FallbackVisibleColliders={lastFallbackVisibleColliderCount}, HookTune={LastHookPresetLabel} P{lastHookStagePressure01:0.00} C/L/R/CD x{lastHookChanceMultiplier:0.00}/{lastHookLoudnessMultiplier:0.00}/{lastHookRadiusMultiplier:0.00}/{lastHookCooldownMultiplier:0.00}",
                    this);
            }
        }

        private void ApplyRuntimeVisibilitySafetyGuards()
        {
            if (!preventInvisibleBlockingCollision)
            {
                return;
            }

            if (createCollisionOnRiskTiles && !createFloorSpriteRenderer)
            {
                createFloorSpriteRenderer = true;
                if (!loggedRiskFloorVisibilityGuard)
                {
                    loggedRiskFloorVisibilityGuard = true;
                    Debug.LogWarning(
                        "MapSystem runtime safety guard enabled floor rendering to avoid invisible blocking collisions on risk tiles.",
                        this);
                }
            }

            if (playerSpawnGeneratedBlockersOnly)
            {
                playerSpawnGeneratedBlockersOnly = false;
                if (!loggedPlayerSpawnBlockerScopeGuard)
                {
                    loggedPlayerSpawnBlockerScopeGuard = true;
                    Debug.LogWarning(
                        "MapSystem runtime safety guard widened player spawn blocker checks to all blocking colliders.",
                        this);
                }
            }
        }

        private void BuildGeneratedHierarchy(List<GeneratedMapCell> cells)
        {
            Transform groundRoot = EnsureHierarchy("TilemapRoot/Ground");
            if (groundRoot == null)
            {
                return;
            }

            Transform wallsRoot = EnsureHierarchy("TilemapRoot/Walls");
            Transform occludersRoot = EnsureHierarchy("TilemapRoot/Occluders");
            Transform archetypesRoot = EnsureHierarchy("TilemapRoot/Archetypes");

            if (clearPreviousOnGenerate && aggressiveGeneratedRootCleanup)
            {
                ClearChildrenWithPrefix(groundRoot, "GeneratedMap_");
                if (wallsRoot != null)
                {
                    ClearChildrenWithPrefix(wallsRoot, "GeneratedWalls_");
                }

                if (occludersRoot != null)
                {
                    ClearChildrenWithPrefix(occludersRoot, "GeneratedOccluders_");
                }

                if (archetypesRoot != null)
                {
                    ClearChildrenWithPrefix(archetypesRoot, "GeneratedHooks_");
                }
            }

            generatedRoot = EnsureChild(groundRoot, $"GeneratedMap_Stage_{currentStage:00}");

            if (clearPreviousOnGenerate)
            {
                ClearSiblings(groundRoot, generatedRoot.name, "GeneratedMap_Stage_");
            }

            ClearChildren(generatedRoot);

            if (wallsRoot != null)
            {
                generatedWallRoot = EnsureChild(wallsRoot, $"GeneratedWalls_Stage_{currentStage:00}");

                if (clearPreviousOnGenerate)
                {
                    ClearSiblings(wallsRoot, generatedWallRoot.name, "GeneratedWalls_Stage_");
                }

                ClearChildren(generatedWallRoot);
            }
            else
            {
                generatedWallRoot = null;
            }

            if (occludersRoot != null)
            {
                generatedOccluderRoot = EnsureChild(occludersRoot, $"GeneratedOccluders_Stage_{currentStage:00}");
                if (clearPreviousOnGenerate)
                {
                    ClearSiblings(occludersRoot, generatedOccluderRoot.name, "GeneratedOccluders_Stage_");
                }

                ClearChildren(generatedOccluderRoot);
            }
            else
            {
                generatedOccluderRoot = null;
            }

            if (archetypesRoot != null)
            {
                generatedHookRoot = EnsureChild(archetypesRoot, $"GeneratedHooks_Stage_{currentStage:00}");
                if (clearPreviousOnGenerate)
                {
                    ClearSiblings(archetypesRoot, generatedHookRoot.name, "GeneratedHooks_Stage_");
                }

                ClearChildren(generatedHookRoot);
            }
            else
            {
                generatedHookRoot = null;
            }

            lastRiskTileColliderCount = 0;
            lastFallbackVisibleColliderCount = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                GameObject tile = new($"Cell_{cell.order:000}_{cell.kind}_{cell.position.x}_{cell.position.y}");
                tile.transform.SetParent(generatedRoot, false);
                tile.transform.localPosition = ToWorld(cell.position);
                tile.transform.localScale = Vector3.one * config.cellSize;

                MapTileDebugView debugView = tile.AddComponent<MapTileDebugView>();
                debugView.Apply(cell);

                if (createFloorSpriteRenderer)
                {
                    SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = GetDebugSprite();
                    renderer.color = debugView.GetTintColor();
                    renderer.sortingOrder = cell.isMainPath ? 1 : 0;
                }

                if (ShouldCreateRiskTileCollision(cell.kind))
                {
                    BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
                    collider.isTrigger = false;
                    lastRiskTileColliderCount++;
                }
            }

            lastWallSegmentCount = 0;
            if (buildBoundaryWalls && generatedWallRoot != null)
            {
                lastWallSegmentCount = BuildBoundaryWalls(cells, generatedWallRoot);
            }

            lastOccluderCount = 0;
            lastChokeOccluderCount = 0;
            if (buildRoomOccluders && generatedOccluderRoot != null)
            {
                lastOccluderCount = BuildRoomOccluders(cells, generatedOccluderRoot, out lastChokeOccluderCount);
            }

            lastArchetypeHookCount = 0;
            lastHookStagePressure01 = 0f;
            lastHookChanceMultiplier = 1f;
            lastHookLoudnessMultiplier = 1f;
            lastHookRadiusMultiplier = 1f;
            lastHookCooldownMultiplier = 1f;
            lastHookPresetLabel = "None";
            if (buildRoomInteractionHooks && generatedHookRoot != null)
            {
                HookRuntimeTuning hookTuning = EvaluateHookRuntimeTuning();
                lastHookStagePressure01 = hookTuning.stagePressure01;
                lastHookChanceMultiplier = hookTuning.chanceMultiplier;
                lastHookLoudnessMultiplier = hookTuning.loudnessMultiplier;
                lastHookRadiusMultiplier = hookTuning.radiusMultiplier;
                lastHookCooldownMultiplier = hookTuning.cooldownMultiplier;
                lastHookPresetLabel = hookTuning.presetLabel;

                if (ShouldCreateHookTensionProbeDummy())
                {
                    CreateHookTensionProbeDummy(generatedHookRoot, hookTuning);
                }

                lastArchetypeHookCount = BuildRoomInteractionHooks(cells, generatedHookRoot, hookTuning);
            }
        }

        private int BuildBoundaryWalls(List<GeneratedMapCell> cells, Transform wallRoot)
        {
            if (cells == null || cells.Count == 0 || wallRoot == null || config == null)
            {
                return 0;
            }

            HashSet<Vector2Int> occupied = new();
            for (int i = 0; i < cells.Count; i++)
            {
                occupied.Add(cells[i].position);
            }

            int createdCount = 0;
            float cellSize = Mathf.Max(0.1f, config.cellSize);
            float thickness = Mathf.Clamp(wallThickness, 0.02f, cellSize * 0.8f);
            float segmentLength = cellSize + thickness * 0.35f;
            float halfCell = cellSize * 0.5f;
            bool renderWallGeometry = ShouldRenderWallGeometry();
            bool fallbackWallVisual = preventInvisibleBlockingCollision && createWallColliders && !createWallSpriteRenderer;

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                Vector3 cellWorld = ToWorld(cell.position);

                for (int d = 0; d < CardinalDirections.Length; d++)
                {
                    Vector2Int direction = CardinalDirections[d];
                    Vector2Int neighbor = cell.position + direction;
                    if (occupied.Contains(neighbor))
                    {
                        continue;
                    }

                    bool horizontal = direction.y != 0;
                    Vector3 position = cellWorld + new Vector3(direction.x, direction.y, 0f) * halfCell;
                    Vector3 scale = horizontal
                        ? new Vector3(segmentLength, thickness, 1f)
                        : new Vector3(thickness, segmentLength, 1f);

                    GameObject wall = new($"Wall_{createdCount:0000}_{cell.position.x}_{cell.position.y}_{direction.x}_{direction.y}");
                    wall.transform.SetParent(wallRoot, false);
                    wall.transform.localPosition = position;
                    wall.transform.localScale = scale;

                    if (renderWallGeometry)
                    {
                        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
                        renderer.sprite = GetDebugSprite();
                        renderer.color = wallColor;
                        renderer.sortingOrder = wallSortingOrder;
                        if (fallbackWallVisual)
                        {
                            lastFallbackVisibleColliderCount++;
                        }
                    }

                    if (createWallColliders)
                    {
                        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
                        collider.isTrigger = false;
                    }

                    createdCount++;
                }
            }

            return createdCount;
        }

        private int BuildRoomOccluders(List<GeneratedMapCell> cells, Transform occluderRoot, out int chokeOccluderCount)
        {
            chokeOccluderCount = 0;

            if (cells == null || cells.Count == 0 || occluderRoot == null || config == null)
            {
                return 0;
            }

            HashSet<Vector2Int> occupied = new();
            for (int i = 0; i < cells.Count; i++)
            {
                occupied.Add(cells[i].position);
            }

            int createdCount = 0;
            float cellSize = Mathf.Max(0.25f, config.cellSize);
            float minSize = Mathf.Clamp(cellSize * Mathf.Min(occluderMinSizeRatio, occluderMaxSizeRatio), 0.12f, cellSize * 0.75f);
            float maxSize = Mathf.Clamp(cellSize * Mathf.Max(occluderMinSizeRatio, occluderMaxSizeRatio), minSize, cellSize * 0.82f);
            float edgePadding = Mathf.Clamp(cellSize * occluderEdgePaddingRatio, 0.05f, cellSize * 0.45f);

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                int createdForCell = 0;

                int chokeCreated = BuildChokeLaneOccludersForCell(cell, occupied, occluderRoot, createdCount + createdForCell);
                if (chokeCreated > 0)
                {
                    createdForCell += chokeCreated;
                    chokeOccluderCount += chokeCreated;
                }

                if (!TryGetOccluderPlan(cell.kind, out float chance, out int minCount, out int maxCount))
                {
                    createdCount += createdForCell;
                    continue;
                }

                System.Random random = BuildCellRandom(cell);
                if (random.NextDouble() > chance)
                {
                    createdCount += createdForCell;
                    continue;
                }

                int safeMinCount = Mathf.Max(0, minCount);
                int safeMaxCount = Mathf.Max(safeMinCount, maxCount);
                int targetCount = safeMaxCount > safeMinCount
                    ? random.Next(safeMinCount, safeMaxCount + 1)
                    : safeMinCount;

                if (createdForCell > 0)
                {
                    targetCount = Mathf.Max(0, targetCount - createdForCell);
                }

                if (targetCount > 0)
                {
                    int randomCreated = BuildOccludersForCell(
                        cell,
                        targetCount,
                        minSize,
                        maxSize,
                        edgePadding,
                        random,
                        occluderRoot,
                        createdCount + createdForCell);

                    createdForCell += randomCreated;
                }

                createdCount += createdForCell;
            }

            return createdCount;
        }

        private int BuildOccludersForCell(
            GeneratedMapCell cell,
            int targetCount,
            float minSize,
            float maxSize,
            float edgePadding,
            System.Random random,
            Transform occluderRoot,
            int globalStartIndex)
        {
            if (targetCount <= 0 || random == null || occluderRoot == null)
            {
                return 0;
            }

            int created = 0;
            int attempts = Mathf.Max(6, targetCount * 7);
            float halfCell = config.cellSize * 0.5f;
            float centerReserveRadius = Mathf.Clamp(config.cellSize * occluderCenterReserveRatio, 0.2f, config.cellSize * 0.48f);
            Vector3 cellWorld = ToWorld(cell.position);
            List<Vector3> placedLocalPositions = new();
            List<float> placedRadii = new();
            bool renderOccluderGeometry = ShouldRenderOccluderGeometry();
            bool fallbackOccluderVisual = preventInvisibleBlockingCollision && createOccluderColliders && !createOccluderSpriteRenderer;

            for (int attempt = 0; attempt < attempts && created < targetCount; attempt++)
            {
                float sizeX = NextRange(random, minSize, maxSize);
                float sizeY = NextRange(random, minSize, maxSize);
                float halfSizeX = sizeX * 0.5f;
                float halfSizeY = sizeY * 0.5f;

                float maxOffsetX = halfCell - edgePadding - halfSizeX;
                float maxOffsetY = halfCell - edgePadding - halfSizeY;
                if (maxOffsetX <= 0.02f || maxOffsetY <= 0.02f)
                {
                    continue;
                }

                Vector3 localOffset = new(
                    NextRange(random, -maxOffsetX, maxOffsetX),
                    NextRange(random, -maxOffsetY, maxOffsetY),
                    0f);

                float radius = Mathf.Max(sizeX, sizeY) * 0.46f;
                if (localOffset.sqrMagnitude <= (centerReserveRadius + radius) * (centerReserveRadius + radius))
                {
                    continue;
                }

                if (OverlapsPlacedOccluders(localOffset, radius, placedLocalPositions, placedRadii))
                {
                    continue;
                }

                int occluderIndex = globalStartIndex + created;
                GameObject cover = new($"Cover_{occluderIndex:0000}_{cell.position.x}_{cell.position.y}");
                cover.transform.SetParent(occluderRoot, false);
                cover.transform.localPosition = cellWorld + localOffset;
                cover.transform.localScale = new Vector3(sizeX, sizeY, 1f);

                if (renderOccluderGeometry)
                {
                    SpriteRenderer renderer = cover.AddComponent<SpriteRenderer>();
                    renderer.sprite = GetDebugSprite();
                    renderer.color = occluderColor;
                    renderer.sortingOrder = occluderSortingOrder;
                    if (fallbackOccluderVisual)
                    {
                        lastFallbackVisibleColliderCount++;
                    }
                }

                if (createOccluderColliders)
                {
                    BoxCollider2D collider = cover.AddComponent<BoxCollider2D>();
                    collider.isTrigger = false;
                }

                placedLocalPositions.Add(localOffset);
                placedRadii.Add(radius);
                created++;
            }

            return created;
        }

        private int BuildChokeLaneOccludersForCell(
            GeneratedMapCell cell,
            HashSet<Vector2Int> occupied,
            Transform occluderRoot,
            int globalStartIndex)
        {
            if (!buildChokeLaneOccluders || occupied == null || occluderRoot == null || config == null)
            {
                return 0;
            }

            if (!TryGetChokePlan(cell.kind, out float chance, out float thicknessMultiplier, out float lengthMultiplier))
            {
                return 0;
            }

            bool hasHorizontal = occupied.Contains(cell.position + Vector2Int.left)
                                 && occupied.Contains(cell.position + Vector2Int.right);
            bool hasVertical = occupied.Contains(cell.position + Vector2Int.up)
                               && occupied.Contains(cell.position + Vector2Int.down);

            if (!hasHorizontal && !hasVertical)
            {
                return 0;
            }

            System.Random random = BuildCellRandom(cell, chokeSeedSalt);
            if (random.NextDouble() > chance)
            {
                return 0;
            }

            bool throughHorizontal;
            if (hasHorizontal && hasVertical)
            {
                throughHorizontal = random.NextDouble() >= 0.5;
            }
            else
            {
                throughHorizontal = hasHorizontal;
            }

            float cellSize = Mathf.Max(0.25f, config.cellSize);
            float barThickness = Mathf.Clamp(
                cellSize * chokeBarThicknessRatio * Mathf.Max(0.5f, thicknessMultiplier),
                0.08f,
                cellSize * 0.52f);

            float barLength = Mathf.Clamp(
                cellSize * chokeBarLengthRatio * Mathf.Max(0.5f, lengthMultiplier),
                barThickness * 1.4f,
                cellSize * 0.92f);

            float halfCell = cellSize * 0.5f;
            float minPassGap = Mathf.Clamp(cellSize * chokePassGapRatio, barThickness * 0.75f, cellSize * 0.42f);
            float maxAbsOffset = halfCell - (barLength * 0.5f) - minPassGap;
            if (maxAbsOffset <= 0.01f)
            {
                return 0;
            }

            float requestedOffset = Mathf.Clamp(cellSize * chokeBarOffsetRatio, minPassGap * 0.35f, maxAbsOffset);
            float sign = random.NextDouble() >= 0.5 ? 1f : -1f;
            float offset = sign * requestedOffset;

            Vector3 localOffset = throughHorizontal
                ? new Vector3(0f, offset, 0f)
                : new Vector3(offset, 0f, 0f);

            Vector3 scale = throughHorizontal
                ? new Vector3(barThickness, barLength, 1f)
                : new Vector3(barLength, barThickness, 1f);

            GameObject choke = new($"Choke_{globalStartIndex:0000}_{cell.kind}_{cell.position.x}_{cell.position.y}");
            choke.transform.SetParent(occluderRoot, false);
            choke.transform.localPosition = ToWorld(cell.position) + localOffset;
            choke.transform.localScale = scale;

            bool renderOccluderGeometry = ShouldRenderOccluderGeometry();
            bool fallbackOccluderVisual = preventInvisibleBlockingCollision && createOccluderColliders && !createOccluderSpriteRenderer;
            if (renderOccluderGeometry)
            {
                SpriteRenderer renderer = choke.AddComponent<SpriteRenderer>();
                renderer.sprite = GetDebugSprite();
                renderer.color = EvaluateChokeOccluderColor(cell.kind);
                renderer.sortingOrder = occluderSortingOrder;
                if (fallbackOccluderVisual)
                {
                    lastFallbackVisibleColliderCount++;
                }
            }

            if (createOccluderColliders)
            {
                BoxCollider2D collider = choke.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
            }

            return 1;
        }

        private bool ShouldRenderWallGeometry()
        {
            return createWallSpriteRenderer || (preventInvisibleBlockingCollision && createWallColliders);
        }

        private bool ShouldRenderOccluderGeometry()
        {
            return createOccluderSpriteRenderer || (preventInvisibleBlockingCollision && createOccluderColliders);
        }

        private bool ShouldCreateRiskTileCollision(MapCellKind kind)
        {
            if (kind != MapCellKind.Risk || !createCollisionOnRiskTiles)
            {
                return false;
            }

            if (!preventInvisibleBlockingCollision)
            {
                return true;
            }

            // Without floor renderers, risk tile blockers become invisible "air walls".
            return createFloorSpriteRenderer;
        }

        private bool TryGetChokePlan(MapCellKind kind, out float chance, out float thicknessMultiplier, out float lengthMultiplier)
        {
            chance = 0f;
            thicknessMultiplier = 1f;
            lengthMultiplier = 1f;

            switch (kind)
            {
                case MapCellKind.Room:
                    chance = roomChokeChance;
                    thicknessMultiplier = 1f;
                    lengthMultiplier = 1f;
                    return true;

                case MapCellKind.Fork:
                    chance = forkChokeChance;
                    thicknessMultiplier = 0.92f;
                    lengthMultiplier = 1.14f;
                    return true;

                case MapCellKind.Hideout:
                    chance = hideoutChokeChance;
                    thicknessMultiplier = 0.8f;
                    lengthMultiplier = 0.88f;
                    return true;

                case MapCellKind.Risk:
                    chance = riskChokeChance;
                    thicknessMultiplier = 1.06f;
                    lengthMultiplier = 1.18f;
                    return true;

                default:
                    return false;
            }
        }

        private Color EvaluateChokeOccluderColor(MapCellKind kind)
        {
            Color baseColor = kind switch
            {
                MapCellKind.Risk => new Color(0.32f, 0.15f, 0.14f, occluderColor.a),
                MapCellKind.Fork => new Color(0.24f, 0.2f, 0.16f, occluderColor.a),
                MapCellKind.Hideout => new Color(0.16f, 0.22f, 0.18f, occluderColor.a),
                _ => occluderColor
            };

            return new Color(
                Mathf.Clamp01(baseColor.r),
                Mathf.Clamp01(baseColor.g),
                Mathf.Clamp01(baseColor.b),
                baseColor.a);
        }
        private bool TryGetOccluderPlan(MapCellKind kind, out float chance, out int minCount, out int maxCount)
        {
            chance = 0f;
            minCount = 0;
            maxCount = 0;

            switch (kind)
            {
                case MapCellKind.Room:
                    chance = roomOccluderChance;
                    minCount = roomOccluderMinCount;
                    maxCount = roomOccluderMaxCount;
                    return true;

                case MapCellKind.Fork:
                    chance = forkOccluderChance;
                    minCount = forkOccluderMinCount;
                    maxCount = forkOccluderMaxCount;
                    return true;

                case MapCellKind.Hideout:
                    chance = hideoutOccluderChance;
                    minCount = hideoutOccluderMinCount;
                    maxCount = hideoutOccluderMaxCount;
                    return true;

                case MapCellKind.Risk:
                    chance = riskOccluderChance;
                    minCount = riskOccluderMinCount;
                    maxCount = riskOccluderMaxCount;
                    return true;

                default:
                    return false;
            }
        }

        private System.Random BuildCellRandom(GeneratedMapCell cell)
        {
            return BuildCellRandom(cell, occluderSeedSalt);
        }

        private System.Random BuildCellRandom(GeneratedMapCell cell, int seedSalt)
        {
            unchecked
            {
                int seed = currentStage * 73856093;
                seed ^= cell.position.x * 19349663;
                seed ^= cell.position.y * 83492791;
                seed ^= ((int)cell.kind + 1) * 29791;
                seed ^= seedSalt * 911;

                if (seed == int.MinValue)
                {
                    seed = 7919;
                }

                return new System.Random(Mathf.Abs(seed));
            }
        }

        private static float NextRange(System.Random random, float min, float max)
        {
            if (random == null)
            {
                return min;
            }

            if (max <= min)
            {
                return min;
            }

            return min + (float)random.NextDouble() * (max - min);
        }

        private static bool OverlapsPlacedOccluders(Vector3 candidatePosition, float candidateRadius, List<Vector3> placedPositions, List<float> placedRadii)
        {
            if (placedPositions == null || placedRadii == null)
            {
                return false;
            }

            int count = Mathf.Min(placedPositions.Count, placedRadii.Count);
            for (int i = 0; i < count; i++)
            {
                float minDistance = (candidateRadius + placedRadii[i]) * 0.95f;
                if (Vector3.SqrMagnitude(candidatePosition - placedPositions[i]) <= minDistance * minDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct HookRuntimeTuning
        {
            public HookRuntimeTuning(
                float stagePressure01,
                float chanceMultiplier,
                float loudnessMultiplier,
                float radiusMultiplier,
                float cooldownMultiplier,
                string presetLabel)
            {
                this.stagePressure01 = Mathf.Clamp01(stagePressure01);
                this.chanceMultiplier = Mathf.Max(0.05f, chanceMultiplier);
                this.loudnessMultiplier = Mathf.Max(0.2f, loudnessMultiplier);
                this.radiusMultiplier = Mathf.Max(0.2f, radiusMultiplier);
                this.cooldownMultiplier = Mathf.Max(0.2f, cooldownMultiplier);
                this.presetLabel = string.IsNullOrWhiteSpace(presetLabel) ? "None" : presetLabel;
            }

            public float stagePressure01 { get; }
            public float chanceMultiplier { get; }
            public float loudnessMultiplier { get; }
            public float radiusMultiplier { get; }
            public float cooldownMultiplier { get; }
            public string presetLabel { get; }
        }

        private int BuildRoomInteractionHooks(List<GeneratedMapCell> cells, Transform hookRoot, HookRuntimeTuning runtimeTuning)
        {
            if (cells == null || cells.Count == 0 || hookRoot == null || config == null)
            {
                return 0;
            }

            int createdCount = 0;
            float cellSize = Mathf.Max(0.25f, config.cellSize);
            float minRadius = Mathf.Clamp(cellSize * Mathf.Min(hookMinRadiusRatio, hookMaxRadiusRatio), 0.08f, cellSize * 0.42f);
            float maxRadius = Mathf.Clamp(cellSize * Mathf.Max(hookMinRadiusRatio, hookMaxRadiusRatio), minRadius, cellSize * 0.48f);
            float edgePadding = Mathf.Clamp(cellSize * hookEdgePaddingRatio, 0.05f, cellSize * 0.45f);

            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                if (!TryGetHookPlan(
                        cell.kind,
                        out float chance,
                        out int minCount,
                        out int maxCount,
                        out float loudnessMultiplier,
                        out float radiusMultiplier,
                        out float cooldownMultiplier,
                        out RoomArchetypeHookVariant variant))
                {
                    continue;
                }

                System.Random random = BuildCellRandom(cell, hookSeedSalt);
                float tunedChance = Mathf.Clamp01(chance * runtimeTuning.chanceMultiplier);
                if (random.NextDouble() > tunedChance)
                {
                    continue;
                }

                int safeMinCount = Mathf.Max(0, minCount);
                int safeMaxCount = Mathf.Max(safeMinCount, maxCount);
                int targetCount = safeMaxCount > safeMinCount
                    ? random.Next(safeMinCount, safeMaxCount + 1)
                    : safeMinCount;

                if (targetCount <= 0)
                {
                    continue;
                }

                int createdForCell = BuildHooksForCell(
                    cell,
                    targetCount,
                    minRadius,
                    maxRadius,
                    edgePadding,
                    loudnessMultiplier * runtimeTuning.loudnessMultiplier,
                    radiusMultiplier * runtimeTuning.radiusMultiplier,
                    cooldownMultiplier * runtimeTuning.cooldownMultiplier,
                    runtimeTuning.stagePressure01,
                    variant,
                    random,
                    hookRoot,
                    createdCount);

                createdCount += createdForCell;
            }

            return createdCount;
        }

        private int BuildHooksForCell(
            GeneratedMapCell cell,
            int targetCount,
            float minRadius,
            float maxRadius,
            float edgePadding,
            float loudnessMultiplier,
            float radiusMultiplier,
            float cooldownMultiplier,
            float stagePressure01,
            RoomArchetypeHookVariant variant,
            System.Random random,
            Transform hookRoot,
            int globalStartIndex)
        {
            if (targetCount <= 0 || random == null || hookRoot == null || config == null)
            {
                return 0;
            }

            int created = 0;
            int attempts = Mathf.Max(5, targetCount * 8);
            float halfCell = config.cellSize * 0.5f;
            Vector3 cellWorld = ToWorld(cell.position);
            List<Vector3> placedLocalPositions = new();
            List<float> placedRadii = new();

            for (int attempt = 0; attempt < attempts && created < targetCount; attempt++)
            {
                float triggerRadius = NextRange(random, minRadius, maxRadius);
                float maxOffsetX = halfCell - edgePadding - triggerRadius;
                float maxOffsetY = halfCell - edgePadding - triggerRadius;
                if (maxOffsetX <= 0.02f || maxOffsetY <= 0.02f)
                {
                    continue;
                }

                Vector3 localOffset = new(
                    NextRange(random, -maxOffsetX, maxOffsetX),
                    NextRange(random, -maxOffsetY, maxOffsetY),
                    0f);

                if (OverlapsPlacedOccluders(localOffset, triggerRadius, placedLocalPositions, placedRadii))
                {
                    continue;
                }

                int hookIndex = globalStartIndex + created;
                GameObject hookObject = new($"Hook_{hookIndex:0000}_{cell.kind}_{cell.position.x}_{cell.position.y}");
                hookObject.transform.SetParent(hookRoot, false);
                hookObject.transform.localPosition = cellWorld + localOffset;
                hookObject.transform.localScale = Vector3.one * (triggerRadius * 2f);

                if (createHookSpriteRenderer)
                {
                    SpriteRenderer renderer = hookObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = GetDebugSprite();
                    renderer.color = EvaluateHookColor(cell.kind, random);
                    renderer.sortingOrder = hookSortingOrder;
                }

                if (createHookTriggerColliders)
                {
                    CircleCollider2D trigger = hookObject.AddComponent<CircleCollider2D>();
                    trigger.isTrigger = true;
                    trigger.radius = 0.5f;
                }

                RoomArchetypeHookDummy hookDummy = hookObject.AddComponent<RoomArchetypeHookDummy>();
                hookDummy.Configure(
                    cell.kind,
                    variant,
                    triggerRadius,
                    Mathf.Max(0.1f, hookBaseLoudness * Mathf.Max(0.1f, loudnessMultiplier)),
                    Mathf.Max(0.5f, hookBaseRadius * Mathf.Max(0.2f, radiusMultiplier)),
                    Mathf.Max(0.15f, hookBaseCooldown * Mathf.Max(0.2f, cooldownMultiplier)),
                    createHookTriggerColliders,
                    Mathf.Abs(random.Next()),
                    currentStage,
                    stagePressure01);

                placedLocalPositions.Add(localOffset);
                placedRadii.Add(triggerRadius);
                created++;
            }

            return created;
        }

        private HookRuntimeTuning EvaluateHookRuntimeTuning()
        {
            float stagePressure01 = EvaluateHookStagePressure01();
            float stageChanceMultiplier = 1f + hookChanceStageBonusMax * stagePressure01;
            float stageLoudnessMultiplier = 1f + hookLoudnessStageBonusMax * stagePressure01;
            float stageRadiusMultiplier = 1f + hookRadiusStageBonusMax * stagePressure01;
            float stageCooldownMultiplier = 1f - hookCooldownStageReductionMax * stagePressure01;

            if (!scaleHooksByStageAndPreset)
            {
                string fallbackLabel = ResolveHookPresetLabel();
                return new HookRuntimeTuning(
                    stagePressure01,
                    1f,
                    1f,
                    1f,
                    1f,
                    fallbackLabel);
            }

            float presetIntensity = EvaluateHookPresetIntensity(out string presetLabel);
            float presetOffset = presetIntensity - 1f;
            float presetChanceMultiplier = Mathf.Max(0.35f, presetIntensity);
            float presetLoudnessMultiplier = Mathf.Max(0.35f, 1f + presetOffset * 0.66f);
            float presetRadiusMultiplier = Mathf.Max(0.35f, 1f + presetOffset * 0.5f);
            float presetCooldownMultiplier = Mathf.Max(0.35f, 1f - presetOffset * 0.52f);

            return new HookRuntimeTuning(
                stagePressure01,
                stageChanceMultiplier * presetChanceMultiplier,
                stageLoudnessMultiplier * presetLoudnessMultiplier,
                stageRadiusMultiplier * presetRadiusMultiplier,
                stageCooldownMultiplier * presetCooldownMultiplier,
                presetLabel);
        }

        private float EvaluateHookStagePressure01()
        {
            int startStage = Mathf.Max(1, hookPressureRampStartStage);
            int endStage = Mathf.Max(startStage + 1, hookPressureRampEndStage);
            float t = Mathf.InverseLerp(startStage, endStage, currentStage);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private float EvaluateHookPresetIntensity(out string presetLabel)
        {
            MapTuningDebugController tuning = FindFirstObjectByType<MapTuningDebugController>();
            if (tuning == null)
            {
                presetLabel = "Unknown";
                return 1f;
            }

            presetLabel = tuning.ActivePresetLabel;
            return tuning.ActivePreset switch
            {
                MapTuningPreset.Compact => compactPresetHookIntensity,
                MapTuningPreset.Expansive => expansivePresetHookIntensity,
                _ => standardPresetHookIntensity
            };
        }

        private string ResolveHookPresetLabel()
        {
            MapTuningDebugController tuning = FindFirstObjectByType<MapTuningDebugController>();
            return tuning != null ? tuning.ActivePresetLabel : "Unknown";
        }

        private bool ShouldCreateHookTensionProbeDummy()
        {
            if (!createHookTensionProbeDummy)
            {
                return false;
            }

            if (!hookTensionProbeEditorOnly)
            {
                return true;
            }

            return Application.isEditor || Debug.isDebugBuild;
        }

        private void CreateHookTensionProbeDummy(Transform hookRoot, HookRuntimeTuning tuning)
        {
            if (hookRoot == null)
            {
                return;
            }

            GameObject probeObject = new($"HookTensionProbe_Stage_{currentStage:00}");
            probeObject.transform.SetParent(hookRoot, false);
            probeObject.transform.localPosition = Vector3.zero;

            HookTensionProbeDummy probe = probeObject.AddComponent<HookTensionProbeDummy>();
            probe.Configure(
                currentStage,
                tuning.presetLabel,
                tuning.stagePressure01,
                tuning.chanceMultiplier,
                tuning.loudnessMultiplier,
                tuning.radiusMultiplier,
                tuning.cooldownMultiplier);
        }
        private bool TryGetHookPlan(
            MapCellKind kind,
            out float chance,
            out int minCount,
            out int maxCount,
            out float loudnessMultiplier,
            out float radiusMultiplier,
            out float cooldownMultiplier,
            out RoomArchetypeHookVariant variant)
        {
            chance = 0f;
            minCount = 0;
            maxCount = 0;
            loudnessMultiplier = 1f;
            radiusMultiplier = 1f;
            cooldownMultiplier = 1f;
            variant = RoomArchetypeHookVariant.LooseMetal;

            switch (kind)
            {
                case MapCellKind.Corridor:
                    chance = corridorHookChance;
                    minCount = corridorHookMinCount;
                    maxCount = corridorHookMaxCount;
                    loudnessMultiplier = 0.95f;
                    radiusMultiplier = 0.9f;
                    cooldownMultiplier = 1.2f;
                    variant = RoomArchetypeHookVariant.RustedVent;
                    return true;

                case MapCellKind.Room:
                    chance = roomHookChance;
                    minCount = roomHookMinCount;
                    maxCount = roomHookMaxCount;
                    variant = RoomArchetypeHookVariant.HangingChain;
                    return true;

                case MapCellKind.Fork:
                    chance = forkHookChance;
                    minCount = forkHookMinCount;
                    maxCount = forkHookMaxCount;
                    loudnessMultiplier = 1.12f;
                    radiusMultiplier = 1.08f;
                    cooldownMultiplier = 0.9f;
                    variant = RoomArchetypeHookVariant.CrackedGlass;
                    return true;

                case MapCellKind.Hideout:
                    chance = hideoutHookChance;
                    minCount = hideoutHookMinCount;
                    maxCount = hideoutHookMaxCount;
                    loudnessMultiplier = 0.72f;
                    radiusMultiplier = 0.82f;
                    cooldownMultiplier = 1.55f;
                    variant = RoomArchetypeHookVariant.ClothRustle;
                    return true;

                case MapCellKind.Risk:
                    chance = riskHookChance;
                    minCount = riskHookMinCount;
                    maxCount = riskHookMaxCount;
                    loudnessMultiplier = 1.38f;
                    radiusMultiplier = 1.3f;
                    cooldownMultiplier = 0.68f;
                    variant = RoomArchetypeHookVariant.AlarmDebris;
                    return true;

                default:
                    return false;
            }
        }

        private Color EvaluateHookColor(MapCellKind kind, System.Random random)
        {
            Color kindColor = kind switch
            {
                MapCellKind.Fork => new Color(0.98f, 0.5f, 0.28f, hookColor.a),
                MapCellKind.Hideout => new Color(0.64f, 0.8f, 0.62f, hookColor.a),
                MapCellKind.Risk => new Color(1f, 0.32f, 0.26f, hookColor.a),
                MapCellKind.Corridor => new Color(0.82f, 0.62f, 0.42f, hookColor.a),
                _ => hookColor
            };

            if (random == null)
            {
                return kindColor;
            }

            float jitter = 0.88f + (float)random.NextDouble() * 0.24f;
            return new Color(
                Mathf.Clamp01(kindColor.r * jitter),
                Mathf.Clamp01(kindColor.g * jitter),
                Mathf.Clamp01(kindColor.b * jitter),
                kindColor.a);
        }

        private void MovePlayerToStart(List<GeneratedMapCell> cells)
        {
            if (cells.Count == 0)
            {
                return;
            }

            GameObject player = TryFindPlayerByTag();
            if (player == null)
            {
                return;
            }

            Vector3 position = ToWorld(cells[0].position);
            position.z = player.transform.position.z;
            if (!TryResolveSafePlayerSpawnPosition(cells, player.transform, out position))
            {
                position.z = player.transform.position.z;
            }

            player.transform.position = position;
            if (player.TryGetComponent(out PlayerDummyController controller))
            {
                controller.RefreshRuntimeReferencesForRespawn();
                controller.TryRecoverUnsafePositionNowForRuntime();
            }
        }

        private bool TryResolveSafePlayerSpawnPosition(
            IReadOnlyList<GeneratedMapCell> cells,
            Transform playerTransform,
            out Vector3 position)
        {
            position = playerTransform != null ? playerTransform.position : Vector3.zero;
            lastPlayerSpawnAdjusted = false;
            lastPlayerSpawnUsedBlockedFallback = false;

            if (cells == null || cells.Count == 0 || config == null)
            {
                return false;
            }

            if (!TryFindStartCell(cells, out GeneratedMapCell spawnCell))
            {
                spawnCell = cells[0];
            }

            float z = playerTransform != null ? playerTransform.position.z : position.z;
            Vector3 preferred = ToWorld(spawnCell.position);
            preferred.z = z;

            return TryResolveSafePlayerPosition(cells, preferred, playerTransform, out position);
        }

        private bool TryResolveSafePlayerPosition(
            IReadOnlyList<GeneratedMapCell> cells,
            Vector3 preferred,
            Transform playerTransform,
            out Vector3 position)
        {
            position = preferred;
            lastPlayerSpawnAdjusted = false;
            lastPlayerSpawnUsedBlockedFallback = false;

            if (cells == null || cells.Count == 0 || config == null)
            {
                return false;
            }

            float z = playerTransform != null ? playerTransform.position.z : preferred.z;
            preferred.z = z;

            if (!resolveSafePlayerSpawn)
            {
                lastPlayerSpawnPosition = preferred;
                position = preferred;
                return true;
            }

            HashSet<Vector2Int> occupied = BuildOccupiedCellSet(cells);
            if (TryFindSafePlayerSpawnCandidate(preferred, cells, occupied, playerTransform, out Vector3 safePosition))
            {
                safePosition.z = z;
                lastPlayerSpawnAdjusted = ((Vector2)safePosition - (Vector2)preferred).sqrMagnitude > 0.0001f;
                lastPlayerSpawnPosition = safePosition;
                position = safePosition;
                return true;
            }

            lastPlayerSpawnUsedBlockedFallback = IsPlayerSpawnBlocked(preferred, playerTransform);
            lastPlayerSpawnPosition = preferred;
            position = preferred;
            return true;
        }

        private bool TryFindStartCell(IReadOnlyList<GeneratedMapCell> cells, out GeneratedMapCell startCell)
        {
            startCell = default;
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            startCell = cells[0];
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == MapCellKind.Start)
                {
                    startCell = cells[i];
                    return true;
                }
            }

            return false;
        }

        private bool TryFindSafePlayerSpawnCandidate(
            Vector3 preferred,
            IReadOnlyList<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            Transform playerTransform,
            out Vector3 safePosition)
        {
            safePosition = preferred;

            if (IsInsideGeneratedWalkableCell(preferred, occupied) && !IsPlayerSpawnBlocked(preferred, playerTransform))
            {
                return true;
            }

            float step = Mathf.Max(0.05f, playerSpawnSearchStep);
            int rings = Mathf.Clamp(playerSpawnSearchRings, 1, 8);
            float bestDistanceSqr = float.PositiveInfinity;
            bool found = false;

            for (int ring = 1; ring <= rings; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring)
                        {
                            continue;
                        }

                        Vector3 candidate = preferred + new Vector3(x * step, y * step, 0f);
                        if (!IsInsideGeneratedWalkableCell(candidate, occupied) || IsPlayerSpawnBlocked(candidate, playerTransform))
                        {
                            continue;
                        }

                        float distanceSqr = ((Vector2)candidate - (Vector2)preferred).sqrMagnitude;
                        if (distanceSqr < bestDistanceSqr)
                        {
                            bestDistanceSqr = distanceSqr;
                            safePosition = candidate;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            if (TryFindSafeGeneratedCellCenter(preferred, cells, playerTransform, out safePosition))
            {
                return true;
            }

            return false;
        }

        private bool TryFindSafeGeneratedCellCenter(
            Vector3 preferred,
            IReadOnlyList<GeneratedMapCell> cells,
            Transform playerTransform,
            out Vector3 safePosition)
        {
            safePosition = preferred;

            if (cells == null || cells.Count == 0 || config == null)
            {
                return false;
            }

            float z = preferred.z;
            float bestDistanceSqr = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 candidate = ToWorld(cells[i].position);
                candidate.z = z;
                if (IsPlayerSpawnBlocked(candidate, playerTransform))
                {
                    continue;
                }

                float distanceSqr = ((Vector2)candidate - (Vector2)preferred).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    safePosition = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool IsInsideGeneratedWalkableCell(Vector3 worldPosition, HashSet<Vector2Int> occupied)
        {
            if (occupied == null || occupied.Count == 0 || config == null)
            {
                return false;
            }

            float cellSize = Mathf.Max(0.1f, config.cellSize);
            Vector2Int cell = new(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.y / cellSize));

            if (!occupied.Contains(cell))
            {
                return false;
            }

            Vector2 center = ToWorld(cell);
            float halfCell = cellSize * 0.5f;
            float inset = Mathf.Clamp(
                Mathf.Max(playerSpawnCellInset, playerSpawnClearanceRadius),
                0f,
                Mathf.Max(0f, halfCell - 0.02f));
            Vector2 delta = (Vector2)worldPosition - center;
            return Mathf.Abs(delta.x) <= halfCell - inset && Mathf.Abs(delta.y) <= halfCell - inset;
        }

        private bool IsPlayerSpawnBlocked(Vector3 worldPosition, Transform playerTransform)
        {
            float radius = Mathf.Max(0.05f, playerSpawnClearanceRadius);
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = playerSpawnBlockerMask,
                useTriggers = false
            };
            int hitCount = Physics2D.OverlapCircle(worldPosition, radius, filter, playerSpawnOverlapHits);
            if (hitCount <= 0)
            {
                return false;
            }

            int safeHitCount = Mathf.Min(hitCount, playerSpawnOverlapHits.Length);
            for (int i = 0; i < safeHitCount; i++)
            {
                Collider2D hit = playerSpawnOverlapHits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                if (playerTransform != null && (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform)))
                {
                    continue;
                }

                if (!IsPlayerSpawnBlockingCollider(hit))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsPlayerSpawnBlockingCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (!playerSpawnGeneratedBlockersOnly)
            {
                return true;
            }

            Transform hitTransform = collider.transform;
            return IsChildOfRoot(hitTransform, generatedWallRoot)
                   || IsChildOfRoot(hitTransform, generatedOccluderRoot)
                   || IsChildOfRoot(hitTransform, generatedRoot);
        }

        private static HashSet<Vector2Int> BuildOccupiedCellSet(IReadOnlyList<GeneratedMapCell> cells)
        {
            HashSet<Vector2Int> occupied = new();
            if (cells == null)
            {
                return occupied;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                occupied.Add(cells[i].position);
            }

            return occupied;
        }

        private static bool IsChildOfRoot(Transform target, Transform root)
        {
            return target != null && root != null && (target == root || target.IsChildOf(root));
        }

        private void UpdateFogBounds(List<GeneratedMapCell> cells)
        {
            if (cells.Count == 0)
            {
                return;
            }

            if (fogSystem == null)
            {
                Transform fogMask = EnsureHierarchy("TilemapRoot/FogMask");
                if (fogMask != null)
                {
                    fogSystem = fogMask.GetComponent<FogOfWarSystem>();
                }
            }

            if (fogSystem == null)
            {
                return;
            }

            if (!TryEvaluateGeneratedWorldBounds(cells, out Vector2 minEdge, out Vector2 maxEdge))
            {
                return;
            }

            Vector2 size = (maxEdge - minEdge) + Vector2.one * Mathf.Max(0f, fogPadding * 2f);
            Vector2 center = (maxEdge + minEdge) * 0.5f;
            fogSystem.SetWorldBounds(center, size);
        }

        private void FitCameraToGeneratedBounds(List<GeneratedMapCell> cells)
        {
            if (cells == null || cells.Count == 0 || config == null)
            {
                return;
            }

            Camera cameraRef = ResolveTargetCamera();
            if (cameraRef == null || !cameraRef.orthographic)
            {
                return;
            }

            if (!TryEvaluateGeneratedWorldBounds(cells, out Vector2 minEdge, out Vector2 maxEdge))
            {
                return;
            }

            float width = (maxEdge.x - minEdge.x) + cameraFitPadding * 2f;
            float height = (maxEdge.y - minEdge.y) + cameraFitPadding * 2f;

            float safeAspect = Mathf.Max(0.2f, cameraRef.aspect);
            float fitByHeight = height * 0.5f;
            float fitByWidth = (width * 0.5f) / safeAspect;
            float targetSize = Mathf.Max(minOrthographicSize, fitByHeight, fitByWidth);

            float safeMaxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            targetSize = Mathf.Clamp(targetSize, minOrthographicSize, safeMaxOrthographicSize);
            cameraRef.orthographicSize = targetSize;

            if (applyCameraBoundsToFollow)
            {
                ApplyCameraFollowBounds(cameraRef, minEdge, maxEdge);
            }
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = FindFirstObjectByType<Camera>();
            return targetCamera;
        }

        private static GameObject TryFindPlayerByTag()
        {
            try
            {
                return GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private Vector3 ToWorld(Vector2Int cellPosition)
        {
            return new Vector3(cellPosition.x * config.cellSize, cellPosition.y * config.cellSize, 0f);
        }

        private void UpdateGeneratedWorldBounds(List<GeneratedMapCell> cells)
        {
            if (!TryEvaluateGeneratedWorldBounds(cells, out Vector2 minEdge, out Vector2 maxEdge))
            {
                lastGeneratedWorldCenter = Vector2.zero;
                lastGeneratedWorldSize = Vector2.one * Mathf.Max(1f, config != null ? config.cellSize : 1f);
                return;
            }

            lastGeneratedWorldCenter = (minEdge + maxEdge) * 0.5f;
            lastGeneratedWorldSize = Vector2.Max(Vector2.one * Mathf.Max(1f, config.cellSize), maxEdge - minEdge);
        }

        private bool TryEvaluateGeneratedWorldBounds(List<GeneratedMapCell> cells, out Vector2 minEdge, out Vector2 maxEdge)
        {
            minEdge = Vector2.zero;
            maxEdge = Vector2.zero;

            if (cells == null || cells.Count == 0 || config == null)
            {
                return false;
            }

            Vector2 minCenter = ToWorld(cells[0].position);
            Vector2 maxCenter = minCenter;

            for (int i = 1; i < cells.Count; i++)
            {
                Vector2 world = ToWorld(cells[i].position);
                minCenter = Vector2.Min(minCenter, world);
                maxCenter = Vector2.Max(maxCenter, world);
            }

            float halfCell = Mathf.Max(0.1f, config.cellSize) * 0.5f;
            Vector2 halfExtents = new(halfCell, halfCell);
            minEdge = minCenter - halfExtents;
            maxEdge = maxCenter + halfExtents;
            return true;
        }

        private void ApplyCameraFollowBounds(Camera cameraRef, Vector2 minEdge, Vector2 maxEdge)
        {
            if (cameraRef == null)
            {
                return;
            }

            CameraFollow2D follow = cameraRef.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                return;
            }

            Vector2 center = (minEdge + maxEdge) * 0.5f;
            Vector2 size = Vector2.Max(Vector2.one * Mathf.Max(1f, config.cellSize), maxEdge - minEdge);
            follow.SetFollowBoundsForEditor(center, size, cameraFollowBoundsPadding);
        }

        private Sprite GetDebugSprite()
        {
            if (debugSprite != null)
            {
                return debugSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "MapCellDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "MapCellDebugSprite";
            debugSprite.hideFlags = HideFlags.HideAndDontSave;
            return debugSprite;
        }

        private static Transform EnsureHierarchy(string path)
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

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroySafe(root.GetChild(i).gameObject);
            }
        }

        private static void ClearSiblings(Transform parent, string keepName, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == keepName)
                {
                    continue;
                }

                if (!child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                DestroySafe(child.gameObject);
            }
        }

        private static void ClearChildrenWithPrefix(Transform parent, string prefix)
        {
            if (parent == null || string.IsNullOrWhiteSpace(prefix))
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                DestroySafe(child.gameObject);
            }
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















