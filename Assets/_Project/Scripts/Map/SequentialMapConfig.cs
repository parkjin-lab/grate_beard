using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    [CreateAssetMenu(fileName = "SequentialMapConfig", menuName = "LostBreadcrumbs/Map/Sequential Map Config")]
    public sealed class SequentialMapConfig : ScriptableObject
    {
        [Header("Seed")]
        public bool useFixedSeed = true;
        public int fixedSeed = 1452;

        [Header("Scale")]
        [Min(0.5f)] public float cellSize = 2.25f;

        [Header("Main Path")]
        [Min(4)] public int baseMainPathLength = 14;
        [Min(0)] public int mainPathIncreasePerStage = 3;
        [Range(0f, 0.8f)] public float turnChance = 0.25f;
        [Range(0f, 0.8f)] public float roomChance = 0.28f;
        [Range(0f, 0.8f)] public float forkChance = 0.28f;
        [Min(0)] public int maxForkCount = 6;

        [Header("Branch")]
        [Min(1)] public int minBranchLength = 3;
        [Min(1)] public int maxBranchLength = 6;
        [Range(0f, 1f)] public float branchHideoutChance = 0.35f;

        [Header("Readability Guards")]
        [Min(2)] public int minStartToExitDistance = 12;
        [Min(0)] public int exitDistanceIncreasePerStage = 1;
        [Min(0)] public int maxExitExtensionCells = 12;

        [Header("Risk Curve")]
        [Range(0f, 1f)] public float riskChanceEarly = 0.08f;
        [Range(0f, 1f)] public float riskChanceLate = 0.35f;
        [Min(2)] public int lateStageStart = 5;

        [Header("Spatial Expansion")]
        public bool enableSpatialExpansion = true;
        [Range(0f, 1f)] public float roomExpansionChance = 0.96f;
        [Range(0f, 1f)] public float hideoutExpansionChance = 0.92f;
        [Range(0f, 1f)] public float forkExpansionChance = 0.6f;
        [Range(0f, 1f)] public float corridorExpansionChance = 0.35f;
        [Min(1)] public int expansionMinRadius = 2;
        [Min(1)] public int expansionMaxRadius = 3;
        [Min(1)] public int maxExpansionCellsPerAnchor = 12;
        [Min(1)] public int maxTotalExpansionCells = 180;
        [Min(1)] public int stageExpansionBonusInterval = 3;

        [Header("Bounds")]
        [Min(6)] public int maxGenerationRadius = 42;
    }
}

