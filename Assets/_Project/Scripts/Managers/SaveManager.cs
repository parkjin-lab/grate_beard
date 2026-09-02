using System;
using System.Collections;
using System.IO;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-280)]
    public sealed class SaveManager : ManagerBase
    {
        public readonly struct RuntimeSaveSuppressionScope : IDisposable
        {
            private readonly bool active;

            internal RuntimeSaveSuppressionScope(bool active)
            {
                this.active = active;
            }

            public void Dispose()
            {
                if (!active)
                {
                    return;
                }

                PopRuntimeSaveSuppression();
            }
        }

        public readonly struct RuntimeDiskWriteSuppressionScope : IDisposable
        {
            private readonly bool active;

            internal RuntimeDiskWriteSuppressionScope(bool active)
            {
                this.active = active;
            }

            public void Dispose()
            {
                if (!active)
                {
                    return;
                }

                PopRuntimeDiskWriteSuppression();
            }
        }

        public readonly struct RuntimeSaveSnapshot
        {
            internal RuntimeSaveSnapshot(string json)
            {
                Json = string.IsNullOrWhiteSpace(json) ? string.Empty : json;
            }

            internal string Json { get; }
            public bool IsValid => !string.IsNullOrWhiteSpace(Json);
        }

        [Serializable]
        private sealed class SaveFileData
        {
            public int version = 1;
            public MetaProgressData meta = new();
            public RunCheckpointData checkpoint = new();
        }

        [Serializable]
        private sealed class MetaProgressData
        {
            public int totalRuns = 0;
            public int highestStageReached = 1;
            public int totalDeaths = 0;
            public int totalBreadcrumbsCollected = 0;
            public int totalEchoUses = 0;
            public int totalPulseCasts = 0;
            public int totalDecoyDeploys = 0;
            public int totalSmokeDeploys = 0;
            public int totalFlashlightToggles = 0;
            public int totalStaminaPickups = 0;
            public int totalStageAdvances = 0;
            public float totalSprintSeconds = 0f;
            public string lastSavedUtc = string.Empty;
            public string selectedLoadoutId = "Balanced";
            public string[] unlockedLoadoutIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class RunCheckpointData
        {
            public bool hasCheckpoint = false;
            public int stage = 1;
            public float playerX = 0f;
            public float playerY = 0f;
            public int currentHealth = 3;
            public int deathCount = 0;
            public float staminaNormalized = 1f;
            public bool flashlightEnabled = false;
            public float behaviorScore = 0f;
            public float sprintSeconds = 0f;
            public int echoCount = 0;
            public int pulseCount = 0;
            public int decoyCount = 0;
            public int smokeCount = 0;
            public int flashlightCount = 0;
            public int staminaPickupCount = 0;
            public int stageAdvanceCount = 0;
            public bool hasPressureSnapshot = false;
            public float stagePressure01 = 0f;
            public float behaviorPressure01 = 0f;
            public float totalPressure01 = 0f;
            public float enemyCountMultiplier = 1f;
            public float riskWeightMultiplier = 1f;
            public float seekerExtraChance = 0f;
            public float startDistanceReduction = 0f;
            public float pulseCooldownPressureMultiplier = 1f;
            public float decoyCooldownPressureMultiplier = 1f;
            public float smokeCooldownPressureMultiplier = 1f;
            public bool hasReadabilitySnapshot = false;
            public float nearbyThreat01 = 0f;
            public float readabilityStagePressure01 = 0f;
            public float readabilityPressure01 = 0f;
            public string selectedLoadoutId = "Balanced";
            public string[] unlockedLoadoutIds = Array.Empty<string>();
            public string savedUtc = string.Empty;
        }

        public static SaveManager Instance { get; private set; }

        [Header("Save References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private PlayerVitalSystem playerVitals;
        [SerializeField] private PlayerDummyController playerController;
        [SerializeField] private PlayerVisibilitySource visibilitySource;
        [SerializeField] private PlayerEchoPulseAbility pulseAbility;
        [SerializeField] private PlayerDecoyAbility decoyAbility;
        [SerializeField] private PlayerSmokeAbility smokeAbility;
        [SerializeField] private PlayerConcealmentState concealmentState;
        [SerializeField] private PlayerBehaviorTelemetry telemetry;
        [SerializeField] private RunLoadoutDirector runLoadoutDirector;
        [SerializeField] private StagePressureDirector pressureDirector;
        [SerializeField] private ThreatReadabilityDirector readabilityDirector;

        [Header("Startup")]
        [SerializeField] private bool autoLoadCheckpointOnStart = true;
        [SerializeField] private bool startNewRunWhenNoCheckpoint = true;

        [Header("Autosave")]
        [SerializeField] private bool autoSaveOnMapGenerated = true;
        [SerializeField, Min(0f)] private float periodicAutoSaveSeconds = 18f;

        [Header("Debug Hotkeys")]
        [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;
        [SerializeField] private KeyCode startNewRunKey = KeyCode.F10;

        [Header("Debug")]
        [SerializeField] private bool logSaveEvents = false;

        private SaveFileData saveData = new();
        private string saveFilePath;

        private float nextPeriodicSaveTime;
        private bool startupRestoreInProgress = true;
        private bool startupRoutineStarted;
        private bool saveStateTransitionInProgress;

        private int lastEchoCount;
        private int lastPulseCount;
        private int lastDecoyCount;
        private int lastSmokeCount;
        private int lastFlashlightCount;
        private int lastDeathCount;
        private int lastStageAdvanceCount;
        private int lastStaminaPickupCount;
        private float lastSprintSeconds;
        private static int runtimeSaveSuppressionDepth;
        private static int runtimeDiskWriteSuppressionDepth;

        public bool HasCheckpoint => saveData != null && saveData.checkpoint != null && saveData.checkpoint.hasCheckpoint;
        public int CheckpointStage => HasCheckpoint ? Mathf.Max(1, saveData.checkpoint.stage) : 0;
        public int HighestStageReached => saveData != null && saveData.meta != null ? Mathf.Max(1, saveData.meta.highestStageReached) : 1;
        public int TotalRuns => saveData != null && saveData.meta != null ? Mathf.Max(0, saveData.meta.totalRuns) : 0;
        public int TotalDeaths => saveData != null && saveData.meta != null ? Mathf.Max(0, saveData.meta.totalDeaths) : 0;
        public int TotalBreadcrumbs => saveData != null && saveData.meta != null ? Mathf.Max(0, saveData.meta.totalBreadcrumbsCollected) : 0;
        public string LastSavedUtc => saveData != null && saveData.meta != null ? saveData.meta.lastSavedUtc : string.Empty;
        public string SelectedLoadoutId => saveData != null && saveData.meta != null ? saveData.meta.selectedLoadoutId : "Balanced";
        public int UnlockedLoadoutCount => saveData != null && saveData.meta != null && saveData.meta.unlockedLoadoutIds != null ? saveData.meta.unlockedLoadoutIds.Length : 0;
        public string SaveFilePath => saveFilePath;
        public static bool IsRuntimeSaveSuppressed => runtimeSaveSuppressionDepth > 0;
        public static bool IsRuntimeDiskWriteSuppressed => runtimeDiskWriteSuppressionDepth > 0;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            saveFilePath = Path.Combine(Application.persistentDataPath, "lostbreadcrumbs_save_v1.json");

            EnsureData();
            LoadFromDisk();
            ResolveReferences();
            SubscribeMapEvents();
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (startupRoutineStarted)
            {
                return;
            }

            startupRoutineStarted = true;
            StartCoroutine(StartupRoutine());
        }

        private IEnumerator StartupRoutine()
        {
            yield return null;

            ResolveReferences();
            ApplySavedLoadoutToRuntime(saveData.meta.selectedLoadoutId, saveData.meta.unlockedLoadoutIds);

            bool awaitingTitleChoice = StageManager.ActiveInstance != null && StageManager.ActiveInstance.IsAwaitingTitleChoice;
            if (autoLoadCheckpointOnStart && HasCheckpoint)
            {
                TryLoadCheckpointToRuntime("StartupLoad");
            }
            else if (startNewRunWhenNoCheckpoint && !awaitingTitleChoice)
            {
                BeginNewRun(incrementRunCounter: true, resetRuntimeStage: false, reason: "StartupNoCheckpoint");
            }

            startupRestoreInProgress = false;
            nextPeriodicSaveTime = Time.time + Mathf.Max(3f, periodicAutoSaveSeconds);
        }

        private void Update()
        {
            if (IsRuntimeSaveSuppressed)
            {
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(quickSaveKey))
            {
                SaveCheckpoint("QuickSave");
            }

            if (RuntimeInputAdapter.GetKeyDown(quickLoadKey))
            {
                TryLoadCheckpointToRuntime("QuickLoad");
            }

            if (RuntimeInputAdapter.GetKeyDown(startNewRunKey))
            {
                BeginNewRun(incrementRunCounter: true, resetRuntimeStage: true, reason: "QuickNewRun");
            }

            if (!startupRestoreInProgress && periodicAutoSaveSeconds > 0f && Time.time >= nextPeriodicSaveTime)
            {
                SaveCheckpoint("Periodic");
                nextPeriodicSaveTime = Time.time + Mathf.Max(3f, periodicAutoSaveSeconds);
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

        public void NotifyBreadcrumbCollected(int amount = 1)
        {
            if (ShouldSkipRuntimeStateMutation("Breadcrumb"))
            {
                return;
            }

            if (amount <= 0)
            {
                return;
            }

            EnsureData();
            saveData.meta.totalBreadcrumbsCollected += amount;
            saveData.meta.lastSavedUtc = DateTime.UtcNow.ToString("O");
            SaveToDisk("Breadcrumb");
        }

        public void BeginNewRun(bool incrementRunCounter, bool resetRuntimeStage, string reason)
        {
            if (ShouldSkipRuntimeStateMutation(reason))
            {
                return;
            }

            EnsureData();

            if (incrementRunCounter)
            {
                saveData.meta.totalRuns = Mathf.Max(0, saveData.meta.totalRuns) + 1;
            }

            saveData.checkpoint = new RunCheckpointData
            {
                hasCheckpoint = false,
                stage = 1,
                currentHealth = playerVitals != null ? playerVitals.MaxHealth : 3,
                staminaNormalized = 1f,
                selectedLoadoutId = ResolveRuntimeSelectedLoadoutId(),
                unlockedLoadoutIds = ResolveRuntimeUnlockedLoadoutIds(),
                savedUtc = DateTime.UtcNow.ToString("O")
            };

            ResetTelemetryForNewRun();
            ResetTelemetryBaselinesFromRuntime();

            if (resetRuntimeStage)
            {
                ResolveReferences();

                if (mapSystem != null)
                {
                    saveStateTransitionInProgress = true;
                    try
                    {
                        mapSystem.ResetAndGenerate();
                    }
                    finally
                    {
                        saveStateTransitionInProgress = false;
                    }
                }

                if (playerVitals != null)
                {
                    playerVitals.ApplySavedVitals(playerVitals.MaxHealth, 0);
                }

                if (playerController != null)
                {
                    playerController.ForceResetSprintState(refillStamina: true);
                }
            }

            ClearUnsavedTransientRuntimeState(resetFlashlightState: true);
            pressureDirector?.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
            readabilityDirector?.ApplyNowForEditor();

            SyncMetaLoadoutFromRuntime();
            SaveToDisk(reason);
            TryRaiseRunEvent(reason);
        }

        public bool SaveCheckpoint(string reason)
        {
            EnsureData();
            ResolveReferences();

            if (ShouldSkipRuntimeStateMutation(reason))
            {
                return false;
            }

            if (mapSystem == null)
            {
                Log($"Skip save ({reason}): MapSystem missing");
                return false;
            }

            AccumulateTelemetryIntoMeta();

            RunCheckpointData checkpoint = saveData.checkpoint ?? new RunCheckpointData();
            checkpoint.hasCheckpoint = true;
            checkpoint.stage = Mathf.Max(1, mapSystem.CurrentStage);

            if (playerController != null)
            {
                Vector3 position = playerController.transform.position;
                checkpoint.playerX = position.x;
                checkpoint.playerY = position.y;
                checkpoint.staminaNormalized = playerController.StaminaNormalized;
            }

            if (playerVitals != null)
            {
                checkpoint.currentHealth = playerVitals.CurrentHealth;
                checkpoint.deathCount = playerVitals.DeathCount;
            }

            if (visibilitySource != null)
            {
                checkpoint.flashlightEnabled = visibilitySource.FlashlightEnabled;
            }

            if (telemetry != null)
            {
                checkpoint.behaviorScore = telemetry.BehaviorScore;
                checkpoint.sprintSeconds = telemetry.SprintSeconds;
                checkpoint.echoCount = telemetry.EchoCount;
                checkpoint.pulseCount = telemetry.PulseCastCount;
                checkpoint.decoyCount = telemetry.DecoyDeployCount;
                checkpoint.smokeCount = telemetry.SmokeDeployCount;
                checkpoint.flashlightCount = telemetry.FlashlightToggleCount;
                checkpoint.staminaPickupCount = telemetry.StaminaPickupCount;
                checkpoint.stageAdvanceCount = telemetry.StageAdvanceCount;
            }

            if (pressureDirector != null)
            {
                checkpoint.hasPressureSnapshot = true;
                checkpoint.stagePressure01 = pressureDirector.CurrentStagePressure01;
                checkpoint.behaviorPressure01 = pressureDirector.CurrentBehaviorPressure01;
                checkpoint.totalPressure01 = pressureDirector.CurrentPressure01;
                checkpoint.enemyCountMultiplier = pressureDirector.AppliedEnemyCountMultiplier;
                checkpoint.riskWeightMultiplier = pressureDirector.AppliedRiskWeightMultiplier;
                checkpoint.seekerExtraChance = pressureDirector.AppliedSeekerExtraChance;
                checkpoint.startDistanceReduction = pressureDirector.AppliedStartDistanceReduction;
                checkpoint.pulseCooldownPressureMultiplier = pressureDirector.AppliedPulseCooldownMultiplier;
                checkpoint.decoyCooldownPressureMultiplier = pressureDirector.AppliedDecoyCooldownMultiplier;
                checkpoint.smokeCooldownPressureMultiplier = pressureDirector.AppliedSmokeCooldownMultiplier;
            }
            else
            {
                checkpoint.hasPressureSnapshot = false;
                checkpoint.stagePressure01 = 0f;
                checkpoint.behaviorPressure01 = 0f;
                checkpoint.totalPressure01 = 0f;
                checkpoint.enemyCountMultiplier = 1f;
                checkpoint.riskWeightMultiplier = 1f;
                checkpoint.seekerExtraChance = 0f;
                checkpoint.startDistanceReduction = 0f;
                checkpoint.pulseCooldownPressureMultiplier = 1f;
                checkpoint.decoyCooldownPressureMultiplier = 1f;
                checkpoint.smokeCooldownPressureMultiplier = 1f;
            }

            if (readabilityDirector != null)
            {
                checkpoint.hasReadabilitySnapshot = true;
                checkpoint.nearbyThreat01 = readabilityDirector.CurrentNearbyThreat;
                checkpoint.readabilityStagePressure01 = readabilityDirector.CurrentStagePressure;
                checkpoint.readabilityPressure01 = readabilityDirector.CurrentReadabilityPressure;
            }
            else
            {
                checkpoint.hasReadabilitySnapshot = false;
                checkpoint.nearbyThreat01 = 0f;
                checkpoint.readabilityStagePressure01 = 0f;
                checkpoint.readabilityPressure01 = 0f;
            }
            checkpoint.selectedLoadoutId = ResolveRuntimeSelectedLoadoutId();
            checkpoint.unlockedLoadoutIds = ResolveRuntimeUnlockedLoadoutIds();
            saveData.meta.selectedLoadoutId = checkpoint.selectedLoadoutId;
            saveData.meta.unlockedLoadoutIds = checkpoint.unlockedLoadoutIds;

            checkpoint.savedUtc = DateTime.UtcNow.ToString("O");
            saveData.checkpoint = checkpoint;
            saveData.meta.highestStageReached = Mathf.Max(saveData.meta.highestStageReached, checkpoint.stage);
            saveData.meta.lastSavedUtc = checkpoint.savedUtc;

            SyncMetaLoadoutFromRuntime();
            SaveToDisk(reason);
            TryRaiseSaveEvent(reason);
            return true;
        }

        public bool TryLoadCheckpointToRuntime(string reason)
        {
            if (ShouldSkipRuntimeStateMutation(reason))
            {
                return false;
            }

            EnsureData();
            ResolveReferences();

            if (!HasCheckpoint)
            {
                Log($"Skip load ({reason}): no checkpoint");
                return false;
            }

            RunCheckpointData checkpoint = saveData.checkpoint;
            int stage = Mathf.Max(1, checkpoint.stage);

            if (mapSystem != null)
            {
                saveStateTransitionInProgress = true;
                try
                {
                    mapSystem.GenerateMapForStage(stage);
                }
                finally
                {
                    saveStateTransitionInProgress = false;
                }
            }

            ResolveReferences();

            if (playerController != null)
            {
                Vector3 current = playerController.transform.position;
                current.x = checkpoint.playerX;
                current.y = checkpoint.playerY;
                if (mapSystem != null
                    && mapSystem.TryValidateAndRecoverCheckpointPosition(current, playerController.transform, out Vector3 recovered, out _))
                {
                    recovered.z = current.z;
                    current = recovered;
                }

                playerController.transform.position = current;
                playerController.ApplySavedStaminaNormalized(checkpoint.staminaNormalized);
                playerController.TryRecoverUnsafePositionNowForRuntime();
            }

            if (playerVitals != null)
            {
                playerVitals.ApplySavedVitals(checkpoint.currentHealth, checkpoint.deathCount);
            }

            if (visibilitySource != null)
            {
                visibilitySource.SetFlashlightEnabled(checkpoint.flashlightEnabled);
            }

            if (telemetry != null)
            {
                telemetry.ApplySavedState(
                    checkpoint.behaviorScore,
                    checkpoint.sprintSeconds,
                    checkpoint.echoCount,
                    checkpoint.pulseCount,
                    checkpoint.decoyCount,
                    checkpoint.smokeCount,
                    checkpoint.flashlightCount,
                    checkpoint.deathCount,
                    checkpoint.stageAdvanceCount,
                    checkpoint.staminaPickupCount);
            }

            ApplySavedLoadoutToRuntime(checkpoint.selectedLoadoutId, checkpoint.unlockedLoadoutIds);
            ClearUnsavedTransientRuntimeState(resetFlashlightState: false);

            bool restoredPressureSnapshot = false;
            if (checkpoint.hasPressureSnapshot)
            {
                if (pressureDirector != null)
                {
                    pressureDirector.ApplySavedPressureStateForRuntime(
                        checkpoint.stagePressure01,
                        checkpoint.behaviorPressure01,
                        checkpoint.totalPressure01,
                        checkpoint.enemyCountMultiplier,
                        checkpoint.riskWeightMultiplier,
                        checkpoint.seekerExtraChance,
                        checkpoint.startDistanceReduction,
                        checkpoint.pulseCooldownPressureMultiplier,
                        checkpoint.decoyCooldownPressureMultiplier,
                        checkpoint.smokeCooldownPressureMultiplier,
                        rebuildEnemies: true,
                        raiseEvent: false);
                    restoredPressureSnapshot = true;
                }
                else if (runLoadoutDirector != null)
                {
                    runLoadoutDirector.ApplyPressureEconomyForRuntime(
                        checkpoint.pulseCooldownPressureMultiplier,
                        checkpoint.decoyCooldownPressureMultiplier,
                        checkpoint.smokeCooldownPressureMultiplier,
                        reapply: true);
                }
            }

            if (!restoredPressureSnapshot)
            {
                pressureDirector?.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
            }

            if (checkpoint.hasReadabilitySnapshot && readabilityDirector != null)
            {
                readabilityDirector.ApplySavedReadabilityStateForRuntime(
                    checkpoint.nearbyThreat01,
                    checkpoint.readabilityStagePressure01,
                    checkpoint.readabilityPressure01,
                    applyImmediately: true);
            }
            else
            {
                readabilityDirector?.ApplyNowForEditor();
            }

            saveData.meta.selectedLoadoutId = checkpoint.selectedLoadoutId;
            saveData.meta.unlockedLoadoutIds = checkpoint.unlockedLoadoutIds;
            PrimeTelemetryBaselines(checkpoint);
            saveData.meta.highestStageReached = Mathf.Max(saveData.meta.highestStageReached, stage);
            SyncMetaLoadoutFromRuntime();
            SaveToDisk(reason);
            TryRaiseLoadEvent(reason, stage);
            return true;
        }

        private void HandleMapGenerated(int stage, System.Collections.Generic.IReadOnlyList<GeneratedMapCell> cells)
        {
            if (ShouldSkipRuntimeStateMutation("MapGenerated"))
            {
                return;
            }

            EnsureData();
            saveData.meta.highestStageReached = Mathf.Max(saveData.meta.highestStageReached, Mathf.Max(1, stage));

            if (startupRestoreInProgress || saveStateTransitionInProgress)
            {
                return;
            }

            if (autoSaveOnMapGenerated)
            {
                SaveCheckpoint("MapGenerated");
            }
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitalSystem>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerDummyController>();
            }

            if (visibilitySource == null)
            {
                visibilitySource = FindFirstObjectByType<PlayerVisibilitySource>();
            }

            if (pulseAbility == null)
            {
                pulseAbility = FindFirstObjectByType<PlayerEchoPulseAbility>();
            }

            if (decoyAbility == null)
            {
                decoyAbility = FindFirstObjectByType<PlayerDecoyAbility>();
            }

            if (smokeAbility == null)
            {
                smokeAbility = FindFirstObjectByType<PlayerSmokeAbility>();
            }

            if (concealmentState == null)
            {
                concealmentState = FindFirstObjectByType<PlayerConcealmentState>();
            }

            if (telemetry == null)
            {
                telemetry = FindFirstObjectByType<PlayerBehaviorTelemetry>();
            }

            if (runLoadoutDirector == null)
            {
                runLoadoutDirector = FindFirstObjectByType<RunLoadoutDirector>();
            }

            if (pressureDirector == null)
            {
                pressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (readabilityDirector == null)
            {
                readabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }
        }

        private void SubscribeMapEvents()
        {
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.MapGenerated -= HandleMapGenerated;
            mapSystem.MapGenerated += HandleMapGenerated;
        }

        private void UnsubscribeMapEvents()
        {
            if (mapSystem == null)
            {
                return;
            }

            mapSystem.MapGenerated -= HandleMapGenerated;
        }

        private void ClearUnsavedTransientRuntimeState(bool resetFlashlightState)
        {
            ResolveReferences();

            if (playerController != null)
            {
                playerController.ClearTemporaryNoiseDampeningForRuntime();
            }

            if (concealmentState != null)
            {
                concealmentState.ResetConcealment();
            }

            if (resetFlashlightState)
            {
                visibilitySource?.ResetFlashlightState(clearDreadModifiers: true);
            }
            else
            {
                visibilitySource?.ResetDreadRuntimeModifiersForEditor();
            }

            pulseAbility?.ResetAbilityState(clearActiveVisuals: true);
            decoyAbility?.ResetAbilityState(clearActiveDecoys: true);
            smokeAbility?.ResetAbilityState(clearActiveSmokes: true);
            readabilityDirector?.ResetTransientRuntimeStateForRuntime();
        }

        private void ResetTelemetryForNewRun()
        {
            ResolveReferences();
            if (telemetry == null)
            {
                return;
            }

            telemetry.ApplySavedState(0f, 0f, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        private void ResetTelemetryBaselinesFromRuntime()
        {
            lastEchoCount = 0;
            lastPulseCount = 0;
            lastDecoyCount = 0;
            lastSmokeCount = 0;
            lastFlashlightCount = 0;
            lastDeathCount = 0;
            lastStageAdvanceCount = 0;
            lastStaminaPickupCount = 0;
            lastSprintSeconds = 0f;

            if (telemetry == null)
            {
                return;
            }

            lastEchoCount = telemetry.EchoCount;
            lastPulseCount = telemetry.PulseCastCount;
            lastDecoyCount = telemetry.DecoyDeployCount;
            lastSmokeCount = telemetry.SmokeDeployCount;
            lastFlashlightCount = telemetry.FlashlightToggleCount;
            lastDeathCount = telemetry.DeathCount;
            lastStageAdvanceCount = telemetry.StageAdvanceCount;
            lastStaminaPickupCount = telemetry.StaminaPickupCount;
            lastSprintSeconds = telemetry.SprintSeconds;
        }

        private void PrimeTelemetryBaselines(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            lastEchoCount = Mathf.Max(0, checkpoint.echoCount);
            lastPulseCount = Mathf.Max(0, checkpoint.pulseCount);
            lastDecoyCount = Mathf.Max(0, checkpoint.decoyCount);
            lastSmokeCount = Mathf.Max(0, checkpoint.smokeCount);
            lastFlashlightCount = Mathf.Max(0, checkpoint.flashlightCount);
            lastDeathCount = Mathf.Max(0, checkpoint.deathCount);
            lastStageAdvanceCount = Mathf.Max(0, checkpoint.stageAdvanceCount);
            lastStaminaPickupCount = Mathf.Max(0, checkpoint.staminaPickupCount);
            lastSprintSeconds = Mathf.Max(0f, checkpoint.sprintSeconds);
        }

        private void AccumulateTelemetryIntoMeta()
        {
            if (telemetry == null)
            {
                return;
            }

            saveData.meta.totalEchoUses += ConsumePositiveIntDelta(ref lastEchoCount, telemetry.EchoCount);
            saveData.meta.totalPulseCasts += ConsumePositiveIntDelta(ref lastPulseCount, telemetry.PulseCastCount);
            saveData.meta.totalDecoyDeploys += ConsumePositiveIntDelta(ref lastDecoyCount, telemetry.DecoyDeployCount);
            saveData.meta.totalSmokeDeploys += ConsumePositiveIntDelta(ref lastSmokeCount, telemetry.SmokeDeployCount);
            saveData.meta.totalFlashlightToggles += ConsumePositiveIntDelta(ref lastFlashlightCount, telemetry.FlashlightToggleCount);
            saveData.meta.totalDeaths += ConsumePositiveIntDelta(ref lastDeathCount, telemetry.DeathCount);
            saveData.meta.totalStageAdvances += ConsumePositiveIntDelta(ref lastStageAdvanceCount, telemetry.StageAdvanceCount);
            saveData.meta.totalStaminaPickups += ConsumePositiveIntDelta(ref lastStaminaPickupCount, telemetry.StaminaPickupCount);
            saveData.meta.totalSprintSeconds += ConsumePositiveFloatDelta(ref lastSprintSeconds, telemetry.SprintSeconds);
        }

        private void TryRaiseSaveEvent(string reason)
        {
            if (!ShouldBroadcastRuntimeEvent(reason))
            {
                return;
            }

            RuntimeEventBus.Raise(RuntimeEventType.Save, $"체크포인트 저장 ({reason})", this, CheckpointStage);
        }

        private void TryRaiseLoadEvent(string reason, int stage)
        {
            if (!ShouldBroadcastRuntimeEvent(reason))
            {
                return;
            }

            RuntimeEventBus.Raise(RuntimeEventType.Load, $"체크포인트 로드 ({reason})", this, stage);
        }

        private void TryRaiseRunEvent(string reason)
        {
            if (!ShouldBroadcastRuntimeEvent(reason))
            {
                return;
            }

            RuntimeEventBus.Raise(RuntimeEventType.Run, $"새 런 시작 ({reason}) · 누적 {TotalRuns}회", this, 1);
        }

        private static bool ShouldBroadcastRuntimeEvent(string reason)
        {
            return reason == "QuickSave"
                   || reason == "QuickLoad"
                   || reason == "QuickNewRun";
        }

        private void SyncMetaLoadoutFromRuntime()
        {
            EnsureData();
            saveData.meta.selectedLoadoutId = ResolveRuntimeSelectedLoadoutId();
            saveData.meta.unlockedLoadoutIds = ResolveRuntimeUnlockedLoadoutIds();
        }

        private string ResolveRuntimeSelectedLoadoutId()
        {
            ResolveReferences();

            if (runLoadoutDirector != null && !string.IsNullOrWhiteSpace(runLoadoutDirector.SelectedLoadoutId))
            {
                return runLoadoutDirector.SelectedLoadoutId;
            }

            if (!string.IsNullOrWhiteSpace(saveData?.checkpoint?.selectedLoadoutId))
            {
                return saveData.checkpoint.selectedLoadoutId;
            }

            if (!string.IsNullOrWhiteSpace(saveData?.meta?.selectedLoadoutId))
            {
                return saveData.meta.selectedLoadoutId;
            }

            return "Balanced";
        }

        private string[] ResolveRuntimeUnlockedLoadoutIds()
        {
            ResolveReferences();

            if (runLoadoutDirector != null)
            {
                string[] runtime = runLoadoutDirector.GetUnlockedLoadoutIdsSnapshot();
                if (runtime != null && runtime.Length > 0)
                {
                    return runtime;
                }
            }

            if (saveData?.meta?.unlockedLoadoutIds != null && saveData.meta.unlockedLoadoutIds.Length > 0)
            {
                return (string[])saveData.meta.unlockedLoadoutIds.Clone();
            }

            return new[] { "Balanced", "Pathfinder", "EchoSpecialist", "ShadowRunner" };
        }

        private void ApplySavedLoadoutToRuntime(string selectedLoadoutId, string[] unlockedLoadoutIds)
        {
            ResolveReferences();

            if (runLoadoutDirector == null)
            {
                return;
            }

            runLoadoutDirector.SetUnlockedLoadoutsForRuntime(unlockedLoadoutIds, clearExisting: true);
            bool selectedApplied = runLoadoutDirector.TrySelectLoadoutById(selectedLoadoutId, lockAfterApply: false, raiseEvent: false);
            if (!selectedApplied)
            {
                runLoadoutDirector.TrySelectLoadoutById(ResolveRuntimeSelectedLoadoutId(), lockAfterApply: false, raiseEvent: false);
            }
        }
        private void EnsureData()
        {
            saveData ??= new SaveFileData();
            saveData.meta ??= new MetaProgressData();
            saveData.checkpoint ??= new RunCheckpointData();

            if (string.IsNullOrWhiteSpace(saveData.meta.selectedLoadoutId))
            {
                saveData.meta.selectedLoadoutId = "Balanced";
            }

            if (saveData.meta.unlockedLoadoutIds == null || saveData.meta.unlockedLoadoutIds.Length <= 0)
            {
                saveData.meta.unlockedLoadoutIds = new[] { "Balanced", "Pathfinder", "EchoSpecialist", "ShadowRunner" };
            }

            if (string.IsNullOrWhiteSpace(saveData.checkpoint.selectedLoadoutId))
            {
                saveData.checkpoint.selectedLoadoutId = saveData.meta.selectedLoadoutId;
            }

            if (saveData.checkpoint.unlockedLoadoutIds == null || saveData.checkpoint.unlockedLoadoutIds.Length <= 0)
            {
                string[] unlocked = saveData.meta.unlockedLoadoutIds;
                saveData.checkpoint.unlockedLoadoutIds = unlocked != null ? (string[])unlocked.Clone() : Array.Empty<string>();
            }
        }

        private void LoadFromDisk()
        {
            EnsureData();

            try
            {
                if (!File.Exists(saveFilePath))
                {
                    SaveToDisk("InitNewSave");
                    return;
                }

                string json = File.ReadAllText(saveFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    SaveToDisk("EmptySaveFile");
                    return;
                }

                SaveFileData parsed = JsonUtility.FromJson<SaveFileData>(json);
                if (parsed == null)
                {
                    SaveToDisk("InvalidSaveFile");
                    return;
                }

                saveData = parsed;
                EnsureData();
                PrimeTelemetryBaselines(saveData.checkpoint);
                Log("Save file loaded");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Save load failed: {ex.Message}", this);
                saveData = new SaveFileData();
                EnsureData();
            }
        }

        private void SaveToDisk(string reason)
        {
            if (IsRuntimeSaveSuppressed || IsRuntimeDiskWriteSuppressed)
            {
                string suppression = IsRuntimeSaveSuppressed
                    ? "runtime save suppression active"
                    : "runtime disk-write suppression active";
                Log($"Skip disk write ({reason}): {suppression}");
                return;
            }

            EnsureData();

            try
            {
                string directory = Path.GetDirectoryName(saveFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                saveData.meta.lastSavedUtc = DateTime.UtcNow.ToString("O");
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(saveFilePath, json);
                Log($"Save written ({reason})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Save write failed: {ex.Message}", this);
            }
        }

        private void Log(string message)
        {
            if (!logSaveEvents)
            {
                return;
            }

            Debug.Log($"[SaveManager] {message}", this);
        }

        public static RuntimeSaveSuppressionScope CreateRuntimeSaveSuppressionScope()
        {
            PushRuntimeSaveSuppression();
            return new RuntimeSaveSuppressionScope(active: true);
        }

        public static RuntimeDiskWriteSuppressionScope CreateRuntimeDiskWriteSuppressionScope()
        {
            PushRuntimeDiskWriteSuppression();
            return new RuntimeDiskWriteSuppressionScope(active: true);
        }

        public RuntimeSaveSnapshot CaptureRuntimeSaveSnapshotForRuntime()
        {
            EnsureData();
            string json = JsonUtility.ToJson(saveData, false);
            return new RuntimeSaveSnapshot(json);
        }

        public bool RestoreRuntimeSaveSnapshotForRuntime(RuntimeSaveSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                return false;
            }

            try
            {
                SaveFileData parsed = JsonUtility.FromJson<SaveFileData>(snapshot.Json);
                if (parsed == null)
                {
                    return false;
                }

                saveData = parsed;
                EnsureData();
                ResolveReferences();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to restore runtime save snapshot: {ex.Message}", this);
                return false;
            }
        }

        private bool ShouldSkipRuntimeStateMutation(string reason)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                Log($"Skip runtime save mutation ({reason}): regression checklist run active");
                return true;
            }

            if (IsRuntimeSaveSuppressed)
            {
                Log($"Skip runtime save mutation ({reason}): runtime save suppression active");
                return true;
            }

            return false;
        }

        private static void PushRuntimeSaveSuppression()
        {
            runtimeSaveSuppressionDepth = Mathf.Max(0, runtimeSaveSuppressionDepth + 1);
        }

        private static void PopRuntimeSaveSuppression()
        {
            runtimeSaveSuppressionDepth = Mathf.Max(0, runtimeSaveSuppressionDepth - 1);
        }

        private static void PushRuntimeDiskWriteSuppression()
        {
            runtimeDiskWriteSuppressionDepth = Mathf.Max(0, runtimeDiskWriteSuppressionDepth + 1);
        }

        private static void PopRuntimeDiskWriteSuppression()
        {
            runtimeDiskWriteSuppressionDepth = Mathf.Max(0, runtimeDiskWriteSuppressionDepth - 1);
        }

        private static int ConsumePositiveIntDelta(ref int previous, int current)
        {
            int safeCurrent = Mathf.Max(0, current);
            int delta = Mathf.Max(0, safeCurrent - previous);
            previous = Mathf.Max(previous, safeCurrent);
            return delta;
        }

        private static float ConsumePositiveFloatDelta(ref float previous, float current)
        {
            float safeCurrent = Mathf.Max(0f, current);
            float delta = Mathf.Max(0f, safeCurrent - previous);
            previous = Mathf.Max(previous, safeCurrent);
            return delta;
        }
    }
}

































