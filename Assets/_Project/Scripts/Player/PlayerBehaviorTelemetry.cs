using LostBreadcrumbs.Runtime.AI.Learning;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public enum EnemyLearningPhase
    {
        Early,
        Mid,
        Late
    }

    public readonly struct LearningSnapshot
    {
        public LearningSnapshot(EnemyLearningPhase phase, float behaviorScore, float learningWeight, float predictionWeight)
        {
            Phase = phase;
            BehaviorScore = Mathf.Clamp01(behaviorScore);
            LearningWeight = Mathf.Clamp01(learningWeight);
            PredictionWeight = Mathf.Clamp01(predictionWeight);
        }

        public EnemyLearningPhase Phase { get; }
        public float BehaviorScore { get; }
        public float LearningWeight { get; }
        public float PredictionWeight { get; }
    }

    public sealed class PlayerBehaviorTelemetry : MonoBehaviour
    {
        public static PlayerBehaviorTelemetry Instance { get; private set; }

        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private EnemyLearningPhaseConfig learningPhaseConfig;

        [Header("Score Tuning")]
        [SerializeField, Min(0f)] private float scoreDecayPerSecond = 0.06f;
        [SerializeField, Min(0f)] private float sprintScorePerSecond = 0.09f;
        [SerializeField, Min(0f)] private float echoScoreGain = 0.05f;
        [SerializeField, Min(0f)] private float pulseScoreGain = 0.11f;
        [SerializeField, Min(0f)] private float decoyScoreGain = 0.08f;
        [SerializeField, Min(0f)] private float smokeScoreGain = 0.07f;
        [SerializeField, Min(0f)] private float flashlightScoreGain = 0.03f;
        [SerializeField, Min(0f)] private float deathScoreGain = 0.18f;
        [SerializeField, Min(0f)] private float stageAdvanceScoreGain = 0.12f;
        [SerializeField, Min(0f)] private float staminaPickupScoreGain = 0.04f;

        [Header("Debug")]
        [SerializeField] private bool logTelemetryEvents;

        private float behaviorScore;
        private float sprintSeconds;
        private int echoCount;
        private int pulseCastCount;
        private int overchargeCastCount;
        private int fullChargeAutoCastCount;
        private float lastPulseCharge01;
        private bool lastPulseWasInsideSmoke;
        private float lastPulseRevealMultiplier = 1f;
        private float lastPulseNoiseMultiplier = 1f;
        private int decoyDeployCount;
        private int smokeDeployCount;
        private int flashlightToggleCount;
        private int deathCount;
        private int stageAdvanceCount;
        private int staminaPickupCount;

        private int lastKnownStage = 1;

        public float BehaviorScore => behaviorScore;
        public float SprintSeconds => sprintSeconds;
        public int EchoCount => echoCount;
        public int PulseCastCount => pulseCastCount;
        public int OverchargeCastCount => overchargeCastCount;
        public int FullChargeAutoCastCount => fullChargeAutoCastCount;
        public float LastPulseCharge01 => Mathf.Clamp01(lastPulseCharge01);
        public bool LastPulseWasInsideSmoke => lastPulseWasInsideSmoke;
        public float LastPulseRevealMultiplier => Mathf.Max(0.1f, lastPulseRevealMultiplier);
        public float LastPulseNoiseMultiplier => Mathf.Max(0.01f, lastPulseNoiseMultiplier);
        public int DecoyDeployCount => decoyDeployCount;
        public int SmokeDeployCount => smokeDeployCount;
        public int FlashlightToggleCount => flashlightToggleCount;
        public int DeathCount => deathCount;
        public int StageAdvanceCount => stageAdvanceCount;
        public int StaminaPickupCount => staminaPickupCount;
        public int CurrentStage => Mathf.Max(1, mapSystem != null ? mapSystem.CurrentStage : lastKnownStage);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
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
            SubscribeMap();
        }

        private void OnDisable()
        {
            UnsubscribeMap();
        }

        private void Update()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (behaviorScore <= 0f || scoreDecayPerSecond <= 0f)
            {
                return;
            }

            behaviorScore = Mathf.Max(0f, behaviorScore - scoreDecayPerSecond * Time.deltaTime);
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

        public void SetLearningConfigForEditor(EnemyLearningPhaseConfig config)
        {
            learningPhaseConfig = config;
        }

        public void ApplySavedState(
            float savedBehaviorScore,
            float savedSprintSeconds,
            int savedEchoCount,
            int savedPulseCount,
            int savedDecoyCount,
            int savedSmokeCount,
            int savedFlashlightCount,
            int savedDeathCount,
            int savedStageAdvanceCount,
            int savedStaminaPickupCount)
        {
            behaviorScore = Mathf.Clamp01(savedBehaviorScore);
            sprintSeconds = Mathf.Max(0f, savedSprintSeconds);
            echoCount = Mathf.Max(0, savedEchoCount);
            pulseCastCount = Mathf.Max(0, savedPulseCount);
            overchargeCastCount = 0;
            fullChargeAutoCastCount = 0;
            lastPulseCharge01 = 0f;
            lastPulseWasInsideSmoke = false;
            lastPulseRevealMultiplier = 1f;
            lastPulseNoiseMultiplier = 1f;
            decoyDeployCount = Mathf.Max(0, savedDecoyCount);
            smokeDeployCount = Mathf.Max(0, savedSmokeCount);
            flashlightToggleCount = Mathf.Max(0, savedFlashlightCount);
            deathCount = Mathf.Max(0, savedDeathCount);
            stageAdvanceCount = Mathf.Max(0, savedStageAdvanceCount);
            staminaPickupCount = Mathf.Max(0, savedStaminaPickupCount);
            if (mapSystem != null)
            {
                lastKnownStage = Mathf.Max(1, mapSystem.CurrentStage);
            }
        }

        public LearningSnapshot GetSnapshot()
        {
            EnemyLearningPhase phase = EvaluatePhase();
            float maxComp = Mathf.Clamp01(learningPhaseConfig != null ? learningPhaseConfig.maxCheatCompensation : 0.9f);
            float adaptiveFactor = Mathf.Clamp01(behaviorScore * maxComp);

            float baseLearning = GetBaseLearningWeight(phase);
            float basePrediction = GetBasePredictionWeight(phase);

            float learningWeight = Mathf.Lerp(baseLearning, 1f, adaptiveFactor * 0.34f);
            float predictionWeight = Mathf.Lerp(basePrediction, 1f, adaptiveFactor * 0.4f);

            return new LearningSnapshot(phase, behaviorScore, learningWeight, predictionWeight);
        }

        public void RegisterSprintTick(float deltaTime, bool isSprinting)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (!isSprinting || deltaTime <= 0f)
            {
                return;
            }

            sprintSeconds += deltaTime;
            AddScore(sprintScorePerSecond * deltaTime, "Sprint");
        }

        public void RegisterEcho()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            echoCount++;
            AddScore(echoScoreGain, "Echo");
        }

        public void RegisterPulseCast()
        {
            RegisterPulseCast(0f, autoFullCharge: false);
        }

        public void RegisterPulseCast(float charge01, bool autoFullCharge)
        {
            RegisterPulseCast(charge01, autoFullCharge, insideSmoke: false, revealMultiplier: 1f, noiseMultiplier: 1f);
        }

        public void RegisterPulseCast(float charge01, bool autoFullCharge, bool insideSmoke, float revealMultiplier, float noiseMultiplier)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            lastPulseCharge01 = Mathf.Clamp01(charge01);
            lastPulseWasInsideSmoke = insideSmoke;
            lastPulseRevealMultiplier = Mathf.Max(0.1f, revealMultiplier);
            lastPulseNoiseMultiplier = Mathf.Max(0.01f, noiseMultiplier);
            pulseCastCount++;
            if (lastPulseCharge01 > 0.001f)
            {
                overchargeCastCount++;
            }

            if (autoFullCharge && lastPulseCharge01 >= 0.999f)
            {
                fullChargeAutoCastCount++;
            }

            AddScore(pulseScoreGain, lastPulseCharge01 > 0.001f ? "OverchargePulse" : "Pulse");
        }

        public void RegisterDecoyDeploy()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            decoyDeployCount++;
            AddScore(decoyScoreGain, "Decoy");
        }

        public void RegisterSmokeDeploy()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            smokeDeployCount++;
            AddScore(smokeScoreGain, "Smoke");
        }

        public void RegisterFlashlightToggle()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            flashlightToggleCount++;
            AddScore(flashlightScoreGain, "Flashlight");
        }

        public void RegisterDeath()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            deathCount++;
            AddScore(deathScoreGain, "Death");
        }

        public void RegisterStaminaPickup()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            staminaPickupCount++;
            AddScore(staminaPickupScoreGain, "StaminaPickup");
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (mapSystem != null)
            {
                lastKnownStage = Mathf.Max(1, mapSystem.CurrentStage);
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

        private void HandleMapGenerated(int stage, System.Collections.Generic.IReadOnlyList<LostBreadcrumbs.Runtime.Map.GeneratedMapCell> cells)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            stage = Mathf.Max(1, stage);
            if (stage > lastKnownStage)
            {
                stageAdvanceCount += stage - lastKnownStage;
                AddScore(stageAdvanceScoreGain * (stage - lastKnownStage), "StageAdvance");
            }

            lastKnownStage = stage;
        }

        private EnemyLearningPhase EvaluatePhase()
        {
            int stage = CurrentStage;

            EnemyLearningPhase phase = stage >= 5
                ? EnemyLearningPhase.Late
                : stage >= 3
                    ? EnemyLearningPhase.Mid
                    : EnemyLearningPhase.Early;

            if (behaviorScore >= 0.86f && phase == EnemyLearningPhase.Mid)
            {
                phase = EnemyLearningPhase.Late;
            }
            else if (behaviorScore >= 0.74f && phase == EnemyLearningPhase.Early)
            {
                phase = EnemyLearningPhase.Mid;
            }

            return phase;
        }

        private float GetBaseLearningWeight(EnemyLearningPhase phase)
        {
            if (learningPhaseConfig == null)
            {
                return phase switch
                {
                    EnemyLearningPhase.Early => 0.25f,
                    EnemyLearningPhase.Mid => 0.55f,
                    EnemyLearningPhase.Late => 0.85f,
                    _ => 0.25f
                };
            }

            return phase switch
            {
                EnemyLearningPhase.Early => learningPhaseConfig.earlyLearningWeight,
                EnemyLearningPhase.Mid => learningPhaseConfig.midLearningWeight,
                EnemyLearningPhase.Late => learningPhaseConfig.lateLearningWeight,
                _ => learningPhaseConfig.earlyLearningWeight
            };
        }

        private float GetBasePredictionWeight(EnemyLearningPhase phase)
        {
            if (learningPhaseConfig == null)
            {
                return phase switch
                {
                    EnemyLearningPhase.Early => 0.2f,
                    EnemyLearningPhase.Mid => 0.5f,
                    EnemyLearningPhase.Late => 0.8f,
                    _ => 0.2f
                };
            }

            return phase switch
            {
                EnemyLearningPhase.Early => learningPhaseConfig.earlyPredictionWeight,
                EnemyLearningPhase.Mid => learningPhaseConfig.midPredictionWeight,
                EnemyLearningPhase.Late => learningPhaseConfig.latePredictionWeight,
                _ => learningPhaseConfig.earlyPredictionWeight
            };
        }

        private void AddScore(float amount, string reason)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (amount <= 0f)
            {
                return;
            }

            behaviorScore = Mathf.Clamp01(behaviorScore + amount);

            if (logTelemetryEvents)
            {
                Debug.Log($"Telemetry +{amount:0.000} ({reason}) => {behaviorScore:0.000}", this);
            }
        }
    }
}
