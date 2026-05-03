using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public enum MapTuningPreset
    {
        Compact,
        Standard,
        Expansive
    }

    public sealed class MapTuningDebugController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private SequentialMapConfig baseConfig;

        [Header("Runtime")]
        [SerializeField] private MapTuningPreset initialPreset = MapTuningPreset.Standard;
        [SerializeField] private bool applyPresetOnStart = true;
        [SerializeField] private bool regenerateOnPresetApply = true;
        [SerializeField] private bool logToConsole = true;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode cyclePresetKey = KeyCode.F6;
        [SerializeField] private KeyCode regenerateStageKey = KeyCode.F7;

        private SequentialMapConfig runtimeConfig;
        private MapTuningPreset activePreset;

        public MapTuningPreset ActivePreset => activePreset;
        public string ActivePresetLabel => activePreset.ToString();
        public bool UsingRuntimeClone => runtimeConfig != null;
        public string ActiveConfigName => runtimeConfig != null ? runtimeConfig.name : baseConfig != null ? baseConfig.name : "None";
        public KeyCode CyclePresetKey => cyclePresetKey;
        public KeyCode RegenerateStageKey => regenerateStageKey;

        private void Start()
        {
            ResolveReferences();
            activePreset = initialPreset;

            if (applyPresetOnStart)
            {
                ApplyPreset(activePreset, regenerateOnPresetApply);
            }
        }

        private void Update()
        {
            if (RuntimeInputAdapter.GetKeyDown(cyclePresetKey))
            {
                CyclePreset();
            }

            if (RuntimeInputAdapter.GetKeyDown(regenerateStageKey))
            {
                RegenerateCurrentStage();
            }
        }

        public void SetMapSystemForEditor(MapSystem targetMapSystem)
        {
            mapSystem = targetMapSystem;
        }

        public void SetBaseConfigForEditor(SequentialMapConfig targetConfig)
        {
            baseConfig = targetConfig;
        }

        public void ApplyPresetForEditor(MapTuningPreset preset, bool regenerate = true)
        {
            activePreset = preset;
            ApplyPreset(activePreset, regenerate);
        }

        private void CyclePreset()
        {
            activePreset = NextPreset(activePreset);
            ApplyPreset(activePreset, true);
        }

        private void RegenerateCurrentStage()
        {
            ResolveReferences();
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.GenerateCurrentStage();
            RaiseRuntimeEvent($"Map regenerated at stage {mapSystem.CurrentStage} ({activePreset}).");
        }

        private void ApplyPreset(MapTuningPreset preset, bool regenerate)
        {
            ResolveReferences();
            if (mapSystem == null || baseConfig == null)
            {
                return;
            }

            EnsureRuntimeConfigFromBase();
            ApplyPresetValues(runtimeConfig, preset);
            mapSystem.SetConfigForEditor(runtimeConfig);

            if (regenerate)
            {
                mapSystem.GenerateCurrentStage();
            }

            string message = $"Map preset applied: {preset} (cell {runtimeConfig.cellSize:0.00}, radius {runtimeConfig.maxGenerationRadius}, exitDist {runtimeConfig.minStartToExitDistance}+{runtimeConfig.exitDistanceIncreasePerStage}/stage).";
            RaiseRuntimeEvent(message);

            if (logToConsole)
            {
                Debug.Log(message, this);
            }
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (baseConfig == null && mapSystem != null)
            {
                baseConfig = mapSystem.Config;
            }
        }

        private void EnsureRuntimeConfigFromBase()
        {
            if (runtimeConfig == null)
            {
                runtimeConfig = Instantiate(baseConfig);
                runtimeConfig.name = $"{baseConfig.name}_RuntimeTuning";
                runtimeConfig.hideFlags = HideFlags.DontSave;
                return;
            }

            CopyConfig(baseConfig, runtimeConfig);
        }

        private static void CopyConfig(SequentialMapConfig source, SequentialMapConfig target)
        {
            target.useFixedSeed = source.useFixedSeed;
            target.fixedSeed = source.fixedSeed;
            target.cellSize = source.cellSize;

            target.baseMainPathLength = source.baseMainPathLength;
            target.mainPathIncreasePerStage = source.mainPathIncreasePerStage;
            target.turnChance = source.turnChance;
            target.roomChance = source.roomChance;
            target.forkChance = source.forkChance;
            target.maxForkCount = source.maxForkCount;

            target.minBranchLength = source.minBranchLength;
            target.maxBranchLength = source.maxBranchLength;
            target.branchHideoutChance = source.branchHideoutChance;

            target.minStartToExitDistance = source.minStartToExitDistance;
            target.exitDistanceIncreasePerStage = source.exitDistanceIncreasePerStage;
            target.maxExitExtensionCells = source.maxExitExtensionCells;

            target.riskChanceEarly = source.riskChanceEarly;
            target.riskChanceLate = source.riskChanceLate;
            target.lateStageStart = source.lateStageStart;

            target.enableSpatialExpansion = source.enableSpatialExpansion;
            target.roomExpansionChance = source.roomExpansionChance;
            target.hideoutExpansionChance = source.hideoutExpansionChance;
            target.forkExpansionChance = source.forkExpansionChance;
            target.corridorExpansionChance = source.corridorExpansionChance;
            target.expansionMinRadius = source.expansionMinRadius;
            target.expansionMaxRadius = source.expansionMaxRadius;
            target.maxExpansionCellsPerAnchor = source.maxExpansionCellsPerAnchor;
            target.maxTotalExpansionCells = source.maxTotalExpansionCells;
            target.stageExpansionBonusInterval = source.stageExpansionBonusInterval;

            target.maxGenerationRadius = source.maxGenerationRadius;
        }

        private static void ApplyPresetValues(SequentialMapConfig config, MapTuningPreset preset)
        {
            switch (preset)
            {
                case MapTuningPreset.Compact:
                    config.cellSize = 1.8f;
                    config.baseMainPathLength = 12;
                    config.mainPathIncreasePerStage = 2;
                    config.roomChance = 0.18f;
                    config.forkChance = 0.24f;
                    config.maxForkCount = 4;
                    config.minBranchLength = 2;
                    config.maxBranchLength = 4;
                    config.minStartToExitDistance = 8;
                    config.exitDistanceIncreasePerStage = 0;
                    config.maxExitExtensionCells = 6;
                    config.enableSpatialExpansion = true;
                    config.roomExpansionChance = 0.62f;
                    config.hideoutExpansionChance = 0.68f;
                    config.forkExpansionChance = 0.26f;
                    config.corridorExpansionChance = 0.08f;
                    config.expansionMinRadius = 1;
                    config.expansionMaxRadius = 1;
                    config.maxExpansionCellsPerAnchor = 4;
                    config.maxTotalExpansionCells = 48;
                    config.stageExpansionBonusInterval = 3;
                    config.maxGenerationRadius = 30;
                    break;

                case MapTuningPreset.Standard:
                    config.cellSize = 2.8f;
                    config.baseMainPathLength = 18;
                    config.mainPathIncreasePerStage = 4;
                    config.roomChance = 0.24f;
                    config.forkChance = 0.32f;
                    config.maxForkCount = 6;
                    config.minBranchLength = 3;
                    config.maxBranchLength = 6;
                    config.minStartToExitDistance = 14;
                    config.exitDistanceIncreasePerStage = 1;
                    config.maxExitExtensionCells = 12;
                    config.enableSpatialExpansion = true;
                    config.roomExpansionChance = 0.78f;
                    config.hideoutExpansionChance = 0.82f;
                    config.forkExpansionChance = 0.48f;
                    config.corridorExpansionChance = 0.18f;
                    config.expansionMinRadius = 1;
                    config.expansionMaxRadius = 2;
                    config.maxExpansionCellsPerAnchor = 8;
                    config.maxTotalExpansionCells = 120;
                    config.stageExpansionBonusInterval = 3;
                    config.maxGenerationRadius = 52;
                    break;

                case MapTuningPreset.Expansive:
                    config.cellSize = 3.4f;
                    config.baseMainPathLength = 22;
                    config.mainPathIncreasePerStage = 5;
                    config.roomChance = 0.3f;
                    config.forkChance = 0.36f;
                    config.maxForkCount = 8;
                    config.minBranchLength = 4;
                    config.maxBranchLength = 8;
                    config.minStartToExitDistance = 18;
                    config.exitDistanceIncreasePerStage = 1;
                    config.maxExitExtensionCells = 18;
                    config.enableSpatialExpansion = true;
                    config.roomExpansionChance = 0.86f;
                    config.hideoutExpansionChance = 0.9f;
                    config.forkExpansionChance = 0.56f;
                    config.corridorExpansionChance = 0.22f;
                    config.expansionMinRadius = 2;
                    config.expansionMaxRadius = 3;
                    config.maxExpansionCellsPerAnchor = 12;
                    config.maxTotalExpansionCells = 220;
                    config.stageExpansionBonusInterval = 2;
                    config.maxGenerationRadius = 72;
                    break;
            }
        }

        private void RaiseRuntimeEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            int stage = mapSystem != null ? mapSystem.CurrentStage : 0;
            RuntimeEventBus.Raise(RuntimeEventType.Stage, message, this, stage);
        }

        private static MapTuningPreset NextPreset(MapTuningPreset preset)
        {
            int presetCount = System.Enum.GetValues(typeof(MapTuningPreset)).Length;
            int next = ((int)preset + 1) % presetCount;
            return (MapTuningPreset)next;
        }
    }
}


