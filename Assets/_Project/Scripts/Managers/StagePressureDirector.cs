using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public sealed class StagePressureDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private EnemySpawnDirector enemySpawnDirector;
        [SerializeField] private RunLoadoutDirector runLoadoutDirector;
        [SerializeField] private PlayerBehaviorTelemetry behaviorTelemetry;

        [Header("Flow")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool rebuildEnemiesOnApply = true;
        [SerializeField] private bool raiseRuntimeEventOnStageGenerate = false;
        [SerializeField] private bool logToConsole;

        [Header("Pressure Curve")]
        [SerializeField, Min(1)] private int pressureRampStartStage = 1;
        [SerializeField, Min(2)] private int pressureRampEndStage = 8;
        [SerializeField, Range(0f, 1f)] private float stagePressureWeight = 0.72f;
        [SerializeField, Range(0f, 1f)] private float behaviorPressureWeight = 0.28f;
        [SerializeField] private bool applyLowBehaviorCompensation = true;
        [SerializeField, Range(0.05f, 0.6f)] private float lowBehaviorThreshold = 0.24f;
        [SerializeField, Range(0.5f, 1f)] private float lowBehaviorPressureMultiplier = 0.8f;

        [Header("Late Stage Bonus")]
        [SerializeField] private bool enableLateStagePressureBonus = true;
        [SerializeField, Min(1)] private int lateStageBonusStartStage = 6;
        [SerializeField, Min(2)] private int lateStageBonusPeakStage = 12;
        [SerializeField, Range(0f, 0.45f)] private float lateStagePressureBonusMax = 0.2f;

        [Header("Enemy Pressure")]
        [SerializeField, Range(0.5f, 2.5f)] private float minEnemyCountMultiplier = 1f;
        [SerializeField, Range(0.5f, 2.5f)] private float maxEnemyCountMultiplier = 1.75f;
        [SerializeField, Range(0.6f, 2.5f)] private float minRiskWeightMultiplier = 1f;
        [SerializeField, Range(0.6f, 2.5f)] private float maxRiskWeightMultiplier = 1.95f;
        [SerializeField, Range(0f, 0.85f)] private float maxSeekerExtraChance = 0.36f;
        [SerializeField, Range(0f, 0.65f)] private float maxStartDistanceReduction = 0.35f;

        [Header("Ability Cooldown Economy")]
        [SerializeField, Range(0.5f, 2.5f)] private float pulseCooldownPressureMax = 1.38f;
        [SerializeField, Range(0.5f, 2.5f)] private float decoyCooldownPressureMax = 1.46f;
        [SerializeField, Range(0.5f, 2.5f)] private float smokeCooldownPressureMax = 1.58f;

        private float currentPressure01;
        private float currentStagePressure01;
        private float currentBehaviorPressure01;
        private float currentLateStageBonus01;

        private float appliedEnemyCountMultiplier = 1f;
        private float appliedRiskWeightMultiplier = 1f;
        private float appliedSeekerExtraChance;
        private float appliedStartDistanceReduction;
        private float appliedPulseCooldownMultiplier = 1f;
        private float appliedDecoyCooldownMultiplier = 1f;
        private float appliedSmokeCooldownMultiplier = 1f;

        public float CurrentPressure01 => currentPressure01;
        public float CurrentStagePressure01 => currentStagePressure01;
        public float CurrentBehaviorPressure01 => currentBehaviorPressure01;
        public float CurrentLateStageBonus01 => currentLateStageBonus01;
        public float AppliedEnemyCountMultiplier => appliedEnemyCountMultiplier;
        public float AppliedRiskWeightMultiplier => appliedRiskWeightMultiplier;
        public float AppliedSeekerExtraChance => appliedSeekerExtraChance;
        public float AppliedStartDistanceReduction => appliedStartDistanceReduction;
        public float AppliedPulseCooldownMultiplier => appliedPulseCooldownMultiplier;
        public float AppliedDecoyCooldownMultiplier => appliedDecoyCooldownMultiplier;
        public float AppliedSmokeCooldownMultiplier => appliedSmokeCooldownMultiplier;
        public bool RebuildsEnemiesOnMapGenerated => isActiveAndEnabled && rebuildEnemiesOnApply;

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
            if (!applyOnStart)
            {
                return;
            }

            ApplyPressureNow(rebuildEnemiesOnApply, raiseEvent: false);
        }

        public void SetReferencesForEditor(
            MapSystem targetMapSystem,
            EnemySpawnDirector targetEnemySpawn,
            RunLoadoutDirector targetLoadout,
            PlayerBehaviorTelemetry targetTelemetry)
        {
            if (mapSystem != targetMapSystem)
            {
                UnsubscribeMap();
                mapSystem = targetMapSystem;
                SubscribeMap();
            }

            enemySpawnDirector = targetEnemySpawn;
            runLoadoutDirector = targetLoadout;
            behaviorTelemetry = targetTelemetry;
        }

        public void ApplyPressureNow(bool rebuildEnemies = true, bool raiseEvent = false)
        {
            ResolveReferences();

            int stage = Mathf.Max(1, mapSystem != null ? mapSystem.CurrentStage : 1);
            float behaviorScore = Mathf.Clamp01(behaviorTelemetry != null ? behaviorTelemetry.BehaviorScore : 0f);

            currentStagePressure01 = EvaluateStagePressure01(stage);
            currentBehaviorPressure01 = behaviorScore;
            currentPressure01 = Mathf.Clamp01(currentStagePressure01 * stagePressureWeight + currentBehaviorPressure01 * behaviorPressureWeight);

            if (applyLowBehaviorCompensation && behaviorScore < lowBehaviorThreshold)
            {
                float deficit = 1f - Mathf.Clamp01(behaviorScore / Mathf.Max(0.001f, lowBehaviorThreshold));
                float compensation = Mathf.Lerp(1f, lowBehaviorPressureMultiplier, deficit);
                currentPressure01 = Mathf.Clamp01(currentPressure01 * compensation);
            }

            currentLateStageBonus01 = EvaluateLateStageBonus01(stage);
            if (enableLateStagePressureBonus && currentLateStageBonus01 > 0f)
            {
                currentPressure01 = Mathf.Clamp01(currentPressure01 + currentLateStageBonus01 * lateStagePressureBonusMax);
            }

            appliedEnemyCountMultiplier = Mathf.Lerp(minEnemyCountMultiplier, maxEnemyCountMultiplier, currentPressure01);
            appliedRiskWeightMultiplier = Mathf.Lerp(minRiskWeightMultiplier, maxRiskWeightMultiplier, currentPressure01);
            appliedSeekerExtraChance = Mathf.Lerp(0f, maxSeekerExtraChance, currentPressure01);
            appliedStartDistanceReduction = Mathf.Lerp(0f, maxStartDistanceReduction, currentPressure01);

            appliedPulseCooldownMultiplier = Mathf.Lerp(1f, pulseCooldownPressureMax, currentPressure01);
            appliedDecoyCooldownMultiplier = Mathf.Lerp(1f, decoyCooldownPressureMax, currentPressure01);
            appliedSmokeCooldownMultiplier = Mathf.Lerp(1f, smokeCooldownPressureMax, currentPressure01);

            enemySpawnDirector?.ApplyPressureForRuntime(
                appliedEnemyCountMultiplier,
                appliedRiskWeightMultiplier,
                appliedSeekerExtraChance,
                appliedStartDistanceReduction,
                rebuildEnemies);

            runLoadoutDirector?.ApplyPressureEconomyForRuntime(
                appliedPulseCooldownMultiplier,
                appliedDecoyCooldownMultiplier,
                appliedSmokeCooldownMultiplier,
                reapply: true);

            if (raiseEvent && raiseRuntimeEventOnStageGenerate)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Stage,
                    $"Pressure {currentPressure01:0.00} (Late+ {currentLateStageBonus01:0.00}, Enemy x{appliedEnemyCountMultiplier:0.00}, CD x{appliedPulseCooldownMultiplier:0.00}/{appliedDecoyCooldownMultiplier:0.00}/{appliedSmokeCooldownMultiplier:0.00})",
                    this,
                    stage);
            }

            if (logToConsole)
            {
                Debug.Log(
                    $"StagePressure applied: Stage={stage}, Pressure={currentPressure01:0.00}, Late+={currentLateStageBonus01:0.00}, EnemyMul={appliedEnemyCountMultiplier:0.00}, RiskMul={appliedRiskWeightMultiplier:0.00}, Seeker+={appliedSeekerExtraChance:0.00}, CD(P/D/S)={appliedPulseCooldownMultiplier:0.00}/{appliedDecoyCooldownMultiplier:0.00}/{appliedSmokeCooldownMultiplier:0.00}",
                    this);
            }
        }

        public void ApplySavedPressureStateForRuntime(
            float savedStagePressure01,
            float savedBehaviorPressure01,
            float savedTotalPressure01,
            float savedEnemyCountMultiplier,
            float savedRiskWeightMultiplier,
            float savedSeekerExtraChance,
            float savedStartDistanceReduction,
            float savedPulseCooldownMultiplier,
            float savedDecoyCooldownMultiplier,
            float savedSmokeCooldownMultiplier,
            bool rebuildEnemies = true,
            bool raiseEvent = false)
        {
            ResolveReferences();

            currentStagePressure01 = Mathf.Clamp01(savedStagePressure01);
            currentBehaviorPressure01 = Mathf.Clamp01(savedBehaviorPressure01);
            currentPressure01 = Mathf.Clamp01(savedTotalPressure01);

            if (currentPressure01 <= 0.001f && (currentStagePressure01 > 0.001f || currentBehaviorPressure01 > 0.001f))
            {
                float weighted = currentStagePressure01 * stagePressureWeight + currentBehaviorPressure01 * behaviorPressureWeight;
                currentPressure01 = Mathf.Clamp01(weighted);
            }

            int stage = Mathf.Max(1, mapSystem != null ? mapSystem.CurrentStage : 1);
            currentLateStageBonus01 = EvaluateLateStageBonus01(stage);

            float minEnemyMultiplier = Mathf.Min(minEnemyCountMultiplier, maxEnemyCountMultiplier);
            float maxEnemyMultiplier = Mathf.Max(minEnemyCountMultiplier, maxEnemyCountMultiplier);
            float minRiskMultiplier = Mathf.Min(minRiskWeightMultiplier, maxRiskWeightMultiplier);
            float maxRiskMultiplier = Mathf.Max(minRiskWeightMultiplier, maxRiskWeightMultiplier);

            appliedEnemyCountMultiplier = Mathf.Clamp(savedEnemyCountMultiplier, minEnemyMultiplier, maxEnemyMultiplier);
            appliedRiskWeightMultiplier = Mathf.Clamp(savedRiskWeightMultiplier, minRiskMultiplier, maxRiskMultiplier);
            appliedSeekerExtraChance = Mathf.Clamp(savedSeekerExtraChance, 0f, Mathf.Max(0f, maxSeekerExtraChance));
            appliedStartDistanceReduction = Mathf.Clamp(savedStartDistanceReduction, 0f, Mathf.Max(0f, maxStartDistanceReduction));

            appliedPulseCooldownMultiplier = Mathf.Clamp(savedPulseCooldownMultiplier, 0.5f, 2.5f);
            appliedDecoyCooldownMultiplier = Mathf.Clamp(savedDecoyCooldownMultiplier, 0.5f, 2.5f);
            appliedSmokeCooldownMultiplier = Mathf.Clamp(savedSmokeCooldownMultiplier, 0.5f, 2.5f);

            enemySpawnDirector?.ApplyPressureForRuntime(
                appliedEnemyCountMultiplier,
                appliedRiskWeightMultiplier,
                appliedSeekerExtraChance,
                appliedStartDistanceReduction,
                rebuildEnemies);

            runLoadoutDirector?.ApplyPressureEconomyForRuntime(
                appliedPulseCooldownMultiplier,
                appliedDecoyCooldownMultiplier,
                appliedSmokeCooldownMultiplier,
                reapply: true);

            if (raiseEvent && raiseRuntimeEventOnStageGenerate)
            {
                RuntimeEventBus.Raise(
                    RuntimeEventType.Stage,
                    $"Pressure restored {currentPressure01:0.00} (Late+ {currentLateStageBonus01:0.00}, Enemy x{appliedEnemyCountMultiplier:0.00}, CD x{appliedPulseCooldownMultiplier:0.00}/{appliedDecoyCooldownMultiplier:0.00}/{appliedSmokeCooldownMultiplier:0.00})",
                    this,
                    stage);
            }

            if (logToConsole)
            {
                Debug.Log(
                    $"StagePressure restored: Pressure={currentPressure01:0.00}, Late+={currentLateStageBonus01:0.00}, EnemyMul={appliedEnemyCountMultiplier:0.00}, RiskMul={appliedRiskWeightMultiplier:0.00}, Seeker+={appliedSeekerExtraChance:0.00}, CD(P/D/S)={appliedPulseCooldownMultiplier:0.00}/{appliedDecoyCooldownMultiplier:0.00}/{appliedSmokeCooldownMultiplier:0.00}",
                    this);
            }
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

            if (runLoadoutDirector == null)
            {
                runLoadoutDirector = FindFirstObjectByType<RunLoadoutDirector>();
            }

            if (behaviorTelemetry == null)
            {
                behaviorTelemetry = FindFirstObjectByType<PlayerBehaviorTelemetry>();
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
            ApplyPressureNow(rebuildEnemiesOnApply, raiseEvent: true);
        }

        private float EvaluateStagePressure01(int stage)
        {
            int startStage = Mathf.Max(1, pressureRampStartStage);
            int endStage = Mathf.Max(startStage + 1, pressureRampEndStage);
            float t = Mathf.InverseLerp(startStage, endStage, stage);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private float EvaluateLateStageBonus01(int stage)
        {
            if (!enableLateStagePressureBonus)
            {
                return 0f;
            }

            int startStage = Mathf.Max(1, lateStageBonusStartStage);
            int peakStage = Mathf.Max(startStage + 1, lateStageBonusPeakStage);
            float t = Mathf.InverseLerp(startStage, peakStage, Mathf.Max(1, stage));
            return Mathf.SmoothStep(0f, 1f, t);
        }
    }
}

