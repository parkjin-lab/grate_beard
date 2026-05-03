using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Player;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.UI;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public sealed class RegressionChecklistRunner : MonoBehaviour
    {
        private const string AutoSoakTraceRelativePath = "Logs/ReleaseSoak/auto_soak_flow_trace.log";
        private const string AutoSoakStatusRelativePath = "Logs/ReleaseSoak/auto_soak_flow_last_status.txt";
        private const string AutoSoakPreflightSummaryRelativePath = "Logs/ReleaseSoak/auto_soak_preflight_last_summary.txt";
        private const int ReleaseSoakReportTraceTailLineCount = 24;

        private readonly struct ChecklistResult
        {
            public ChecklistResult(string key, bool passed, string detail)
            {
                Key = key;
                Passed = passed;
                Detail = detail ?? string.Empty;
            }

            public string Key { get; }
            public bool Passed { get; }
            public string Detail { get; }
        }

        public readonly struct ChecklistReportEntry
        {
            public ChecklistReportEntry(string key, bool passed, string detail)
            {
                Key = key ?? string.Empty;
                Passed = passed;
                Detail = detail ?? string.Empty;
            }

            public string Key { get; }
            public bool Passed { get; }
            public string Detail { get; }
        }

        private readonly struct RuntimeSnapshot
        {
            public RuntimeSnapshot(
                int stage,
                Vector3 playerPosition,
                float staminaNormalized,
                int currentHealth,
                int deathCount,
                bool flashlightEnabled,
                float behaviorScore,
                float sprintSeconds,
                int echoCount,
                int pulseCount,
                int decoyCount,
                int smokeCount,
                int flashlightCount,
                int telemetryDeathCount,
                int stageAdvanceCount,
                int staminaPickupCount,
                float pulseCooldownRemaining,
                float decoyCooldownRemaining,
                float smokeCooldownRemaining,
                bool hasMapPreset,
                MapTuningPreset mapPreset)
            {
                Stage = Mathf.Max(1, stage);
                PlayerPosition = playerPosition;
                StaminaNormalized = Mathf.Clamp01(staminaNormalized);
                CurrentHealth = Mathf.Max(1, currentHealth);
                DeathCount = Mathf.Max(0, deathCount);
                FlashlightEnabled = flashlightEnabled;
                BehaviorScore = Mathf.Clamp01(behaviorScore);
                SprintSeconds = Mathf.Max(0f, sprintSeconds);
                EchoCount = Mathf.Max(0, echoCount);
                PulseCount = Mathf.Max(0, pulseCount);
                DecoyCount = Mathf.Max(0, decoyCount);
                SmokeCount = Mathf.Max(0, smokeCount);
                FlashlightCount = Mathf.Max(0, flashlightCount);
                TelemetryDeathCount = Mathf.Max(0, telemetryDeathCount);
                StageAdvanceCount = Mathf.Max(0, stageAdvanceCount);
                StaminaPickupCount = Mathf.Max(0, staminaPickupCount);
                PulseCooldownRemaining = Mathf.Max(0f, pulseCooldownRemaining);
                DecoyCooldownRemaining = Mathf.Max(0f, decoyCooldownRemaining);
                SmokeCooldownRemaining = Mathf.Max(0f, smokeCooldownRemaining);
                HasMapPreset = hasMapPreset;
                MapPreset = mapPreset;
            }

            public int Stage { get; }
            public Vector3 PlayerPosition { get; }
            public float StaminaNormalized { get; }
            public int CurrentHealth { get; }
            public int DeathCount { get; }
            public bool FlashlightEnabled { get; }
            public float BehaviorScore { get; }
            public float SprintSeconds { get; }
            public int EchoCount { get; }
            public int PulseCount { get; }
            public int DecoyCount { get; }
            public int SmokeCount { get; }
            public int FlashlightCount { get; }
            public int TelemetryDeathCount { get; }
            public int StageAdvanceCount { get; }
            public int StaminaPickupCount { get; }
            public float PulseCooldownRemaining { get; }
            public float DecoyCooldownRemaining { get; }
            public float SmokeCooldownRemaining { get; }
            public bool HasMapPreset { get; }
            public MapTuningPreset MapPreset { get; }
        }

        private readonly struct ChaseReadabilitySample
        {
            public ChaseReadabilitySample(
                int enemyCount,
                float transitionSeconds,
                float transitionPulseSpeed,
                float transitionFlashStrength,
                float disengageCueSeconds,
                float disengageGraceSeconds,
                float chaseBlinkSpeed)
            {
                EnemyCount = Mathf.Max(0, enemyCount);
                TransitionSeconds = Mathf.Max(0f, transitionSeconds);
                TransitionPulseSpeed = Mathf.Max(0f, transitionPulseSpeed);
                TransitionFlashStrength = Mathf.Clamp01(transitionFlashStrength);
                DisengageCueSeconds = Mathf.Max(0f, disengageCueSeconds);
                DisengageGraceSeconds = Mathf.Max(0f, disengageGraceSeconds);
                ChaseBlinkSpeed = Mathf.Max(0f, chaseBlinkSpeed);
            }

            public int EnemyCount { get; }
            public float TransitionSeconds { get; }
            public float TransitionPulseSpeed { get; }
            public float TransitionFlashStrength { get; }
            public float DisengageCueSeconds { get; }
            public float DisengageGraceSeconds { get; }
            public float ChaseBlinkSpeed { get; }
        }

        [System.Serializable]
        private struct MatrixBaselineSnapshot
        {
            public bool IsValid;
            public int SampleCount;
            public float MinPressure;
            public float MaxPressure;
            public float MinReadability;
            public float MaxReadability;
            public string LastCapturedSummary;
        }

        [Header("References")]
        [SerializeField] private MapSystem mapSystem;
        [SerializeField] private MapTuningDebugController mapTuning;
        [SerializeField] private StageLoopDirector stageLoopDirector;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private NoiseManager noiseManager;
        [SerializeField] private StagePressureDirector pressureDirector;
        [SerializeField] private ThreatReadabilityDirector readabilityDirector;
        [SerializeField] private EnemySpawnDirector enemySpawnDirector;
        [SerializeField] private RunLoadoutDirector runLoadoutDirector;
        [SerializeField] private GameplayHudRuntime gameplayHud;
        [SerializeField] private EventFeedbackRuntime eventFeedback;
        [SerializeField] private PlayerVitalSystem playerVitals;
        [SerializeField] private PlayerVisibilitySource visibilitySource;
        [SerializeField] private PlayerEchoPulseAbility pulseAbility;
        [SerializeField] private PlayerDecoyAbility decoyAbility;
        [SerializeField] private PlayerSmokeAbility smokeAbility;
        [SerializeField] private PlayerDummyController playerController;
        [SerializeField] private PlayerConcealmentState concealmentState;
        [SerializeField] private PlayerBehaviorTelemetry telemetry;

        [Header("Run")]
        [SerializeField] private KeyCode runChecklistKey = KeyCode.F11;
        [SerializeField] private KeyCode runReleaseSoakKey = KeyCode.F2;
        [SerializeField, Min(0f)] private float settleDelaySeconds = 0.05f;
        [SerializeField] private bool logEachCheck = true;
        [SerializeField] private bool raiseRuntimeEvent = true;
        [SerializeField] private bool suppressRuntimeEventsDuringChecklist = true;
        [SerializeField] private bool suppressSaveWritesDuringChecklist = true;
        [SerializeField] private bool allowFinalSummaryEventWhenSuppressed = true;

        [Header("Preset x Stage Matrix")]
        [SerializeField] private bool runPresetStageMatrix = true;
        [SerializeField, Min(1)] private int matrixStageEarly = 1;
        [SerializeField, Min(1)] private int matrixStageMid = 3;
        [SerializeField, Min(1)] private int matrixStageLate = 5;
        [SerializeField] private bool requireSetPieceFromMidStage = true;
        [SerializeField, Min(0f)] private float matrixCurveTolerance = 0.05f;
        [SerializeField, Min(0)] private int matrixAllowedCurveDips = 1;
        [SerializeField, Min(0f)] private float matrixMaxAllowedDipMagnitude = 0.07f;
        [SerializeField] private bool matrixEnableBaselineLock = true;
        [SerializeField] private bool matrixAutoLockBaselineOnPass = true;
        [SerializeField] private bool matrixAutoRefreshBaselineOnPass;
        [SerializeField] private bool matrixRequireBaselineBeforePass;
        [SerializeField] private bool matrixBaselineFrozen;
        [SerializeField] private bool matrixBaselineAffectsPass = true;
        [SerializeField, Min(0f)] private float matrixBaselineDriftTolerance = 0.08f;
        [SerializeField, Min(0f)] private float matrixFinalLockCurveTolerance = 0.05f;
        [SerializeField, Min(0)] private int matrixFinalLockAllowedCurveDips = 1;
        [SerializeField, Min(0f)] private float matrixFinalLockMaxDipMagnitude = 0.07f;
        [SerializeField, Min(0f)] private float matrixFinalLockBaselineDriftTolerance = 0.08f;

        [Header("Chase Readability Regression")]
        [SerializeField] private bool runChaseReadabilityRegression = true;
        [SerializeField, Min(1)] private int chaseRegressionStageLow = 1;
        [SerializeField, Min(1)] private int chaseRegressionStageHigh = 5;
        [SerializeField, Min(0f)] private float chaseReadabilityTolerance = 0.02f;
        [SerializeField, Min(1)] private int chaseReadabilityEnemySampleCap = 4;

        [Header("Release-Candidate Soak Pass")]
        [SerializeField] private bool enableReleaseCandidateSoakPass = true;
        [SerializeField, Min(1)] private int releaseSoakIterationCount = 3;
        [SerializeField] private bool releaseSoakCycleMapPresets = true;
        [SerializeField] private bool releaseSoakRunMatrixEachIteration = true;
        [SerializeField] private bool releaseSoakRunDeathResetEachIteration = true;
        [SerializeField] private bool releaseSoakSuppressRuntimeEvents = true;
        [SerializeField] private bool releaseSoakSuppressDiskWrites = true;
        [SerializeField, Min(0f)] private float releaseSoakIterationDelaySeconds = 0.08f;
        [SerializeField] private bool releaseSoakLogEachCheck = true;

        [Header("Release Checklist Freeze")]
        [SerializeField] private bool releaseChecklistFreezeApplied;
        [SerializeField] private bool releaseChecklistRequireFreezeApplied = true;
        [SerializeField] private bool releaseChecklistRequireFinalLock = true;
        [SerializeField] private bool releaseChecklistRequireChecklistPass = true;
        [SerializeField] private bool releaseChecklistRequireMatrixPass = true;
        [SerializeField] private bool releaseChecklistRequireChasePass = true;
        [SerializeField] private bool releaseChecklistRequireSoakPass = true;
        [SerializeField, Min(1)] private int releaseChecklistFrozenSoakIterations = 5;
        [SerializeField] private bool releaseChecklistAutoApplyFinalLock = true;

        private readonly List<ChecklistResult> currentResults = new();
        private readonly List<ChecklistReportEntry> lastRunResults = new();
        private readonly List<ChecklistResult> currentSoakResults = new();
        private readonly List<ChecklistReportEntry> lastSoakResults = new();
        private readonly List<EnemyController> chaseReadabilityEnemies = new(16);

        private bool isRunning;
        private bool hasRun;
        private bool lastRunPassed;
        private int lastRunPassedCount;
        private int lastRunFailedCount;
        private int runCount;
        private string lastRunSummary = "Not run";
        private bool isSoakRunning;
        private bool hasSoakRun;
        private bool lastSoakPassed;
        private int lastSoakPassedCount;
        private int lastSoakFailedCount;
        private int soakRunCount;
        private string lastSoakSummary = "Not run";
        private string lastSoakIterationFailureSummary = "none";
        private string lastSoakFailureActionSummary = "none";
        private string lastSoakDetailedReportFilePath = "none";

        private bool lastMatrixRan;
        private bool lastMatrixPassed;
        private int lastMatrixSampleCount;
        private int lastMatrixPassCount;
        private int lastMatrixFailCount;
        private float lastMatrixMinPressure;
        private float lastMatrixMaxPressure;
        private float lastMatrixMinReadability;
        private float lastMatrixMaxReadability;
        private string lastMatrixSummary = "Not run";

        private bool lastChaseReadabilityRan;
        private bool lastChaseReadabilityPassed;
        private int lastChaseReadabilitySampleCount;
        private int lastChaseReadabilityPassCount;
        private int lastChaseReadabilityFailCount;
        private string lastChaseReadabilitySummary = "Not run";

        [SerializeField] private MatrixBaselineSnapshot matrixBaseline;

        public static bool IsRegressionRunActive { get; private set; }

        public bool IsRunning => isRunning;
        public bool HasRun => hasRun;
        public bool LastRunPassed => lastRunPassed;
        public int LastRunPassedCount => lastRunPassedCount;
        public int LastRunFailedCount => lastRunFailedCount;
        public int RunCount => runCount;
        public string LastRunSummary => lastRunSummary;
        public KeyCode RunChecklistKey => runChecklistKey;
        public KeyCode RunReleaseSoakKey => runReleaseSoakKey;
        public IReadOnlyList<ChecklistReportEntry> LastRunResults => lastRunResults;
        public bool IsSoakRunning => isSoakRunning;
        public bool HasSoakRun => hasSoakRun;
        public bool LastSoakPassed => lastSoakPassed;
        public int LastSoakPassedCount => lastSoakPassedCount;
        public int LastSoakFailedCount => lastSoakFailedCount;
        public int SoakRunCount => soakRunCount;
        public string LastSoakSummary => lastSoakSummary;
        public IReadOnlyList<ChecklistReportEntry> LastSoakResults => lastSoakResults;
        public string LastSoakIterationFailureSummary => lastSoakIterationFailureSummary;
        public string LastRunFailureDigest => BuildFailureDigest(lastRunResults, 4);
        public string LastSoakFailureDigest => BuildFailureDigest(lastSoakResults, 6);
        public string LastSoakFailureActionSummary => lastSoakFailureActionSummary;
        public string LastSoakDetailedReportFilePath => string.IsNullOrEmpty(lastSoakDetailedReportFilePath) ? "none" : lastSoakDetailedReportFilePath;
        public bool ReleaseChecklistFreezeApplied => releaseChecklistFreezeApplied;
        public bool ReleaseChecklistReady => EvaluateReleaseChecklistReady(out _, out _, out _, out _, out _, out _);
        public string ReleaseChecklistSummary
        {
            get
            {
                bool ready = EvaluateReleaseChecklistReady(
                    out bool freezePass,
                    out bool finalLockPass,
                    out bool checklistPass,
                    out bool matrixPass,
                    out bool chasePass,
                    out bool soakPass);
                return $"ready={(ready ? "Y" : "N")}, freeze={(releaseChecklistFreezeApplied ? "Y" : "N")}({(freezePass ? "ok" : "req")}), lock={(finalLockPass ? "Y" : "N")}, checklist={(checklistPass ? "Y" : "N")}, matrix={(matrixPass ? "Y" : "N")}, chase={(chasePass ? "Y" : "N")}, soak={(soakPass ? "Y" : "N")}";
            }
        }
        public bool LastMatrixRan => lastMatrixRan;
        public bool LastMatrixPassed => lastMatrixPassed;
        public int LastMatrixSampleCount => lastMatrixSampleCount;
        public int LastMatrixPassCount => lastMatrixPassCount;
        public int LastMatrixFailCount => lastMatrixFailCount;
        public float LastMatrixMinPressure => lastMatrixMinPressure;
        public float LastMatrixMaxPressure => lastMatrixMaxPressure;
        public float LastMatrixMinReadability => lastMatrixMinReadability;
        public float LastMatrixMaxReadability => lastMatrixMaxReadability;
        public string LastMatrixSummary => lastMatrixSummary;
        public bool HasMatrixBaseline => matrixBaseline.IsValid;
        public bool MatrixBaselineFrozen => matrixBaselineFrozen;
        public string MatrixBaselineSummary => matrixBaseline.IsValid
            ? $"samples={matrixBaseline.SampleCount}, P {matrixBaseline.MinPressure:0.00}-{matrixBaseline.MaxPressure:0.00}, R {matrixBaseline.MinReadability:0.00}-{matrixBaseline.MaxReadability:0.00}, frozen={(matrixBaselineFrozen ? "Y" : "N")}"
            : "None";
        public string MatrixBaselinePolicySummary =>
            $"autoLock={(matrixAutoLockBaselineOnPass ? "Y" : "N")}, autoRefresh={(matrixAutoRefreshBaselineOnPass ? "Y" : "N")}, require={(matrixRequireBaselineBeforePass ? "Y" : "N")}";
        public bool MatrixFinalLockReady =>
            matrixEnableBaselineLock
            && matrixBaseline.IsValid
            && matrixRequireBaselineBeforePass
            && matrixBaselineFrozen
            && matrixBaselineAffectsPass
            && !matrixAutoLockBaselineOnPass
            && !matrixAutoRefreshBaselineOnPass;
        public string MatrixFinalLockSummary =>
            $"ready={(MatrixFinalLockReady ? "Y" : "N")}, baseline={(matrixBaseline.IsValid ? "Y" : "N")}, frozen={(matrixBaselineFrozen ? "Y" : "N")}, require={(matrixRequireBaselineBeforePass ? "Y" : "N")}, autoLock={(matrixAutoLockBaselineOnPass ? "Y" : "N")}, autoRefresh={(matrixAutoRefreshBaselineOnPass ? "Y" : "N")}";
        public bool LastChaseReadabilityRan => lastChaseReadabilityRan;
        public bool LastChaseReadabilityPassed => lastChaseReadabilityPassed;
        public int LastChaseReadabilitySampleCount => lastChaseReadabilitySampleCount;
        public int LastChaseReadabilityPassCount => lastChaseReadabilityPassCount;
        public int LastChaseReadabilityFailCount => lastChaseReadabilityFailCount;
        public string LastChaseReadabilitySummary => lastChaseReadabilitySummary;

        private void Update()
        {
            if (RuntimeInputAdapter.GetKeyDown(runChecklistKey))
            {
                RunChecklistNow();
            }

            if (RuntimeInputAdapter.GetKeyDown(runReleaseSoakKey))
            {
                RunReleaseCandidateSoakPassNow();
            }
        }

        [ContextMenu("Run Regression Checklist")]
        public void RunChecklistNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Regression checklist can run only in Play Mode.", this);
                return;
            }

            if (isRunning || isSoakRunning)
            {
                return;
            }

            StartCoroutine(RunChecklistRoutine());
        }

        [ContextMenu("Run Release Candidate Soak Pass")]
        public void RunReleaseCandidateSoakPassNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Release soak pass can run only in Play Mode.", this);
                return;
            }

            if (isSoakRunning || isRunning)
            {
                return;
            }

            StartCoroutine(RunReleaseCandidateSoakRoutine());
        }

        [ContextMenu("Clear Matrix Baseline")]
        public void ClearMatrixBaselineForEditor()
        {
            matrixBaseline = default;
            Debug.Log("Regression matrix baseline cleared.", this);
        }

        [ContextMenu("Lock Matrix Baseline From Last Matrix Run")]
        public void LockMatrixBaselineFromLastRunForEditor()
        {
            if (lastMatrixSampleCount <= 0)
            {
                Debug.LogWarning("Cannot lock matrix baseline: no matrix run samples yet.", this);
                return;
            }

            LockMatrixBaseline(lastMatrixSampleCount, lastMatrixMinPressure, lastMatrixMaxPressure, lastMatrixMinReadability, lastMatrixMaxReadability);
            Debug.Log($"Regression matrix baseline locked from last run ({matrixBaseline.LastCapturedSummary}).", this);
        }

        [ContextMenu("Toggle Matrix Baseline Frozen")]
        public void ToggleMatrixBaselineFrozenForEditor()
        {
            matrixBaselineFrozen = !matrixBaselineFrozen;
            Debug.Log($"Regression matrix baseline frozen={matrixBaselineFrozen}.", this);
        }

        [ContextMenu("Apply Matrix Final Lock Policy")]
        public void ApplyMatrixFinalLockPolicyForEditor()
        {
            if (!matrixBaseline.IsValid && lastMatrixPassed && lastMatrixSampleCount > 0)
            {
                LockMatrixBaseline(lastMatrixSampleCount, lastMatrixMinPressure, lastMatrixMaxPressure, lastMatrixMinReadability, lastMatrixMaxReadability);
            }

            matrixEnableBaselineLock = true;
            matrixAutoLockBaselineOnPass = false;
            matrixAutoRefreshBaselineOnPass = false;
            matrixRequireBaselineBeforePass = true;
            matrixBaselineFrozen = true;
            matrixBaselineAffectsPass = true;

            matrixCurveTolerance = Mathf.Max(0f, matrixFinalLockCurveTolerance);
            matrixAllowedCurveDips = Mathf.Max(0, matrixFinalLockAllowedCurveDips);
            matrixMaxAllowedDipMagnitude = Mathf.Max(0f, matrixFinalLockMaxDipMagnitude);
            matrixBaselineDriftTolerance = Mathf.Max(0f, matrixFinalLockBaselineDriftTolerance);

            string baselineDetail = matrixBaseline.IsValid
                ? matrixBaseline.LastCapturedSummary
                : "baseline missing (run checklist once, then lock baseline)";
            Debug.Log($"Regression matrix final lock policy applied. {baselineDetail}. {MatrixFinalLockSummary}", this);
        }

        [ContextMenu("Apply Release Checklist Freeze Defaults")]
        public void ApplyReleaseChecklistFreezeDefaultsForEditor()
        {
            if (releaseChecklistAutoApplyFinalLock)
            {
                ApplyMatrixFinalLockPolicyForEditor();
            }

            runPresetStageMatrix = true;
            runChaseReadabilityRegression = true;
            enableReleaseCandidateSoakPass = true;
            releaseSoakCycleMapPresets = true;
            releaseSoakRunMatrixEachIteration = true;
            releaseSoakRunDeathResetEachIteration = true;
            releaseSoakSuppressRuntimeEvents = true;
            releaseSoakSuppressDiskWrites = true;
            releaseSoakIterationCount = Mathf.Max(1, releaseChecklistFrozenSoakIterations);

            releaseChecklistFreezeApplied = true;

            Debug.Log($"Release checklist freeze defaults applied. {ReleaseChecklistSummary}", this);
        }

        [ContextMenu("Log Release Checklist Gate")]
        public void LogReleaseChecklistGateForEditor()
        {
            Debug.Log($"Release checklist gate -> {ReleaseChecklistSummary}", this);
        }

        [ContextMenu("Log Release Soak Failures")]
        public void LogReleaseSoakFailuresForEditor()
        {
            if (!hasSoakRun)
            {
                Debug.Log("Release soak has not run yet.", this);
                return;
            }

            Debug.Log($"Release soak failures -> {LastSoakFailureDigest} | iterations={LastSoakIterationFailureSummary} | actions={LastSoakFailureActionSummary}", this);
        }

        [ContextMenu("Log Release Soak Action Plan")]
        public void LogReleaseSoakActionPlanForEditor()
        {
            if (!hasSoakRun)
            {
                Debug.Log("Release soak has not run yet.", this);
                return;
            }

            Debug.Log($"Release soak action plan -> {LastSoakFailureActionSummary}", this);
        }

        [ContextMenu("Log Release Soak Detailed Report")]
        public void LogReleaseSoakDetailedReportForEditor()
        {
            Debug.Log(BuildReleaseSoakDetailedReport(240), this);
        }

        [ContextMenu("Write Release Soak Detailed Report File")]
        public void WriteReleaseSoakDetailedReportFileForEditor()
        {
            if (TryWriteReleaseSoakDetailedReportFile(4096, out string filePath))
            {
                Debug.Log($"Release soak detailed report file written: {filePath}", this);
            }
        }

        public string BuildReleaseSoakDetailedReport(int maxEntries = 240)
        {
            if (!hasSoakRun)
            {
                return "Release soak has not run yet.";
            }

            int entryLimit = Mathf.Clamp(maxEntries, 1, 4096);
            int entryCount = Mathf.Min(entryLimit, lastSoakResults.Count);

            StringBuilder builder = new();
            builder.AppendLine($"ReleaseSoak Detailed Report (run #{soakRunCount})");
            builder.AppendLine($"Summary: {lastSoakSummary}");
            builder.AppendLine($"Failures: {LastSoakFailureDigest}");
            builder.AppendLine($"Iteration Failures: {lastSoakIterationFailureSummary}");
            builder.AppendLine($"Suggested Actions: {lastSoakFailureActionSummary}");
            builder.AppendLine($"Release Gate: {ReleaseChecklistSummary}");
            AppendAutoSoakTraceTail(builder, ReleaseSoakReportTraceTailLineCount);
            builder.AppendLine($"Entries ({entryCount}/{lastSoakResults.Count}):");

            for (int i = 0; i < entryCount; i++)
            {
                ChecklistReportEntry entry = lastSoakResults[i];
                builder.Append(i + 1)
                    .Append(". [")
                    .Append(entry.Passed ? "PASS" : "FAIL")
                    .Append("] ")
                    .Append(entry.Key)
                    .Append(" :: ")
                    .AppendLine(entry.Detail);
            }

            int omittedCount = Mathf.Max(0, lastSoakResults.Count - entryCount);
            if (omittedCount > 0)
            {
                builder.Append("... omitted ").Append(omittedCount).AppendLine(" entries");
            }

            return builder.ToString();
        }

        private static void AppendAutoSoakTraceTail(StringBuilder builder, int maxTailLines)
        {
            if (builder == null)
            {
                return;
            }

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string preflightSummaryPath = Path.Combine(projectRoot, AutoSoakPreflightSummaryRelativePath);
                string statusPath = Path.Combine(projectRoot, AutoSoakStatusRelativePath);
                string tracePath = Path.Combine(projectRoot, AutoSoakTraceRelativePath);

                builder.AppendLine("Auto Preflight Trace:");
                builder.AppendLine($"- Preflight Summary Path: {preflightSummaryPath}");
                if (File.Exists(preflightSummaryPath))
                {
                    builder.AppendLine($"- Preflight Summary LastWrite: {File.GetLastWriteTime(preflightSummaryPath):yyyy-MM-dd HH:mm:ss}");
                    builder.AppendLine("- Preflight Summary:");
                    builder.AppendLine(File.ReadAllText(preflightSummaryPath).Trim());
                }
                else
                {
                    builder.AppendLine("- Preflight Summary: missing");
                }

                builder.AppendLine($"- Status Path: {statusPath}");
                if (File.Exists(statusPath))
                {
                    builder.AppendLine($"- Status LastWrite: {File.GetLastWriteTime(statusPath):yyyy-MM-dd HH:mm:ss}");
                    builder.AppendLine("- Status:");
                    builder.AppendLine(File.ReadAllText(statusPath).Trim());
                }
                else
                {
                    builder.AppendLine("- Status: missing");
                }

                builder.AppendLine($"- Trace Path: {tracePath}");
                if (!File.Exists(tracePath))
                {
                    builder.AppendLine("- Trace Tail: missing");
                    return;
                }

                string[] traceLines = File.ReadAllLines(tracePath);
                int safeTailLines = Mathf.Clamp(maxTailLines, 1, 256);
                int tailCount = Mathf.Min(safeTailLines, traceLines.Length);
                int startIndex = Mathf.Max(0, traceLines.Length - tailCount);

                builder.AppendLine($"- Trace LastWrite: {File.GetLastWriteTime(tracePath):yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine($"- Trace Tail ({tailCount}/{traceLines.Length}):");
                for (int i = startIndex; i < traceLines.Length; i++)
                {
                    builder.AppendLine(traceLines[i]);
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine($"Auto Preflight Trace: unavailable ({ex.Message})");
            }
        }

        public bool TryWriteReleaseSoakDetailedReportFile(int maxEntries, out string filePath)
        {
            filePath = string.Empty;
            if (!hasSoakRun)
            {
                Debug.Log("Release soak has not run yet.", this);
                return false;
            }

            string report = BuildReleaseSoakDetailedReport(maxEntries);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportDirectory = Path.Combine(projectRoot, "Logs", "ReleaseSoak");

            try
            {
                Directory.CreateDirectory(reportDirectory);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"release_soak_report_{timestamp}_run{soakRunCount}.log";
                filePath = Path.Combine(reportDirectory, fileName);
                File.WriteAllText(filePath, report, Encoding.UTF8);
                lastSoakDetailedReportFilePath = filePath;
                return true;
            }
            catch (Exception ex)
            {
                lastSoakDetailedReportFilePath = "write-failed";
                Debug.LogError($"Failed to write release soak report file: {ex.Message}", this);
                return false;
            }
        }

        private IEnumerator RunChecklistRoutine()
        {
            isRunning = true;
            IsRegressionRunActive = true;
            runCount++;
            currentResults.Clear();

            RuntimeEventBus.EventSuppressionScope suppressionScope = default;
            SaveManager.RuntimeSaveSuppressionScope saveSuppressionScope = default;
            bool suppressionPushed = false;
            bool saveSuppressionPushed = false;

            try
            {
                if (suppressRuntimeEventsDuringChecklist)
                {
                    suppressionScope = RuntimeEventBus.CreateSuppressionScope();
                    suppressionPushed = true;
                }

                if (suppressSaveWritesDuringChecklist)
                {
                    saveSuppressionScope = SaveManager.CreateRuntimeSaveSuppressionScope();
                    saveSuppressionPushed = true;
                }

                ResolveReferences();
                RuntimeSnapshot snapshot = CaptureRuntimeSnapshot();

                yield return WaitSettle();

                AddResult("Refs.MapSystem", mapSystem != null, mapSystem != null ? "ok" : "missing");
                AddResult("Refs.PlayerVitals", playerVitals != null, playerVitals != null ? "ok" : "missing");
                AddResult("Refs.Pressure", pressureDirector != null, pressureDirector != null ? "ok" : "missing");
                AddResult("Refs.Readability", readabilityDirector != null, readabilityDirector != null ? "ok" : "missing");

                int hooksAcrossRegressionStages = 0;

                yield return RunMapGenerationCheck(1, false, hooksAcrossRegressionStages, value => hooksAcrossRegressionStages = value);
                yield return RunMapGenerationCheck(3, true, hooksAcrossRegressionStages, value => hooksAcrossRegressionStages = value);
                yield return RunMapGenerationCheck(5, true, hooksAcrossRegressionStages, value => hooksAcrossRegressionStages = value);

                AddResult(
                    "Map.HookCoverage.Stage3_5",
                    hooksAcrossRegressionStages > 0,
                    $"hooks={hooksAcrossRegressionStages}");

                RunFeedbackFeatureWiringCheck(hooksAcrossRegressionStages);
                yield return RunPressureScalingCheck();
                yield return RunPresetStageMatrixCheck();
                yield return RunChaseReadabilityRegressionCheck();
                yield return RunDeathResetCheck();

                FinalizeRun();
                yield return RestoreRuntimeSnapshot(snapshot);
            }
            finally
            {
                if (suppressionPushed)
                {
                    suppressionScope.Dispose();
                }

                if (saveSuppressionPushed)
                {
                    saveSuppressionScope.Dispose();
                }

                IsRegressionRunActive = false;
                isRunning = false;
            }
        }

        private IEnumerator RunReleaseCandidateSoakRoutine()
        {
            isSoakRunning = true;
            soakRunCount++;
            currentSoakResults.Clear();
            lastSoakDetailedReportFilePath = "none";

            RuntimeEventBus.EventSuppressionScope suppressionScope = default;
            SaveManager.RuntimeDiskWriteSuppressionScope diskWriteScope = default;
            bool suppressionPushed = false;
            bool diskWriteSuppressionPushed = false;

            try
            {
                if (releaseSoakSuppressRuntimeEvents)
                {
                    suppressionScope = RuntimeEventBus.CreateSuppressionScope();
                    suppressionPushed = true;
                }

                if (releaseSoakSuppressDiskWrites)
                {
                    diskWriteScope = SaveManager.CreateRuntimeDiskWriteSuppressionScope();
                    diskWriteSuppressionPushed = true;
                }

                ResolveReferences();

                RuntimeSnapshot runtimeSnapshot = CaptureRuntimeSnapshot();
                SaveManager.RuntimeSaveSnapshot saveSnapshot = saveManager != null
                    ? saveManager.CaptureRuntimeSaveSnapshotForRuntime()
                    : default;

                AddSoakResult("ReleaseSoak.Enabled", enableReleaseCandidateSoakPass, enableReleaseCandidateSoakPass ? "enabled" : "disabled");
                if (!enableReleaseCandidateSoakPass)
                {
                    FinalizeSoakRun();
                    yield return RestoreRuntimeSnapshot(runtimeSnapshot);
                    yield break;
                }

                bool ready = mapSystem != null && saveManager != null && playerVitals != null;
                AddSoakResult(
                    "ReleaseSoak.Ready",
                    ready,
                    $"map={(mapSystem != null ? "ok" : "missing")}, save={(saveManager != null ? "ok" : "missing")}, vitals={(playerVitals != null ? "ok" : "missing")}");
                if (!ready)
                {
                    FinalizeSoakRun();
                    yield return RestoreRuntimeSnapshot(runtimeSnapshot);
                    yield break;
                }

                List<int> stages = BuildMatrixStages();
                if (stages.Count <= 0)
                {
                    stages = new List<int> { 1 };
                }

                MapTuningPreset[] presets =
                {
                    MapTuningPreset.Compact,
                    MapTuningPreset.Standard,
                    MapTuningPreset.Expansive
                };

                int iterationCount = Mathf.Max(1, releaseSoakIterationCount);
                for (int iteration = 0; iteration < iterationCount; iteration++)
                {
                    int runIndex = iteration + 1;
                    int stage = stages[iteration % stages.Count];
                    MapTuningPreset preset = presets[iteration % presets.Length];

                    if (releaseSoakCycleMapPresets && mapTuning != null)
                    {
                        mapTuning.ApplyPresetForEditor(preset, regenerate: false);
                    }

                    mapSystem.GenerateMapForStage(stage);
                    yield return WaitSettle();

                    pressureDirector?.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
                    readabilityDirector?.ApplyNowForEditor();
                    yield return WaitSettle();

                    int cellCount = mapSystem.LastGeneratedCells.Count;
                    int wallCount = mapSystem.LastWallSegmentCount;
                    int occluderCount = mapSystem.LastOccluderCount;
                    bool setupPass = cellCount > 0 && wallCount > 0 && occluderCount > 0;
                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.Setup",
                        setupPass,
                        $"preset={preset}, stage={stage}, cells={cellCount}, walls={wallCount}, occluders={occluderCount}");

                    if (visibilitySource != null)
                    {
                        visibilitySource.SetFlashlightEnabled(false);
                    }

                    if (playerController != null)
                    {
                        playerController.ApplySavedStaminaNormalized(1f);
                    }

                    if (playerVitals != null)
                    {
                        playerVitals.ApplySavedVitals(playerVitals.MaxHealth, playerVitals.DeathCount);
                    }

                    int expectedStage = Mathf.Max(1, mapSystem.CurrentStage);
                    float expectedStamina = playerController != null ? playerController.StaminaNormalized : 1f;
                    bool expectedFlashlight = visibilitySource != null && visibilitySource.FlashlightEnabled;
                    int expectedHealth = playerVitals != null ? playerVitals.CurrentHealth : 0;

                    bool savePass = saveManager.SaveCheckpoint($"ReleaseSoak.Iter{runIndex}.Save");
                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.Save",
                        savePass,
                        savePass ? $"checkpointStage={saveManager.CheckpointStage}" : "save failed");

                    if (playerController != null)
                    {
                        playerController.ApplySavedStaminaNormalized(0.18f);
                    }

                    if (visibilitySource != null)
                    {
                        visibilitySource.SetFlashlightEnabled(true);
                    }

                    if (playerVitals != null)
                    {
                        playerVitals.ApplySavedVitals(Mathf.Max(1, playerVitals.MaxHealth - 1), playerVitals.DeathCount);
                    }

                    bool loadPass = saveManager.TryLoadCheckpointToRuntime($"ReleaseSoak.Iter{runIndex}.Load");
                    yield return WaitSettle();

                    int loadedStage = Mathf.Max(1, mapSystem.CurrentStage);
                    float loadedStamina = playerController != null ? playerController.StaminaNormalized : expectedStamina;
                    bool loadedFlashlight = visibilitySource != null && visibilitySource.FlashlightEnabled;
                    int loadedHealth = playerVitals != null ? playerVitals.CurrentHealth : expectedHealth;

                    bool loadStagePass = loadPass && loadedStage == expectedStage;
                    bool loadStatePass = loadPass
                                         && Mathf.Abs(loadedStamina - expectedStamina) <= 0.05f
                                         && loadedFlashlight == expectedFlashlight
                                         && loadedHealth == expectedHealth;

                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.Load",
                        loadPass,
                        loadPass ? $"loadedStage={loadedStage}, checkpoint={saveManager.CheckpointStage}" : "load failed");
                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.LoadStage",
                        loadStagePass,
                        $"expected={expectedStage}, loaded={loadedStage}");
                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.LoadState",
                        loadStatePass,
                        $"stamina={loadedStamina:0.00}/{expectedStamina:0.00}, flash={loadedFlashlight}/{expectedFlashlight}, hp={loadedHealth}/{expectedHealth}");

                    if (releaseSoakRunDeathResetEachIteration)
                    {
                        yield return RunReleaseSoakDeathResetIteration(runIndex);
                    }

                    int totalRunsBefore = saveManager.TotalRuns;
                    saveManager.BeginNewRun(incrementRunCounter: false, resetRuntimeStage: true, reason: $"ReleaseSoak.Iter{runIndex}.NewRun");
                    yield return WaitSettle();

                    bool checkpointClearedPass = !saveManager.HasCheckpoint;
                    bool stageResetPass = mapSystem.CurrentStage <= 1;
                    bool runCounterStablePass = saveManager.TotalRuns == totalRunsBefore;
                    AddSoakResult(
                        $"ReleaseSoak.I{runIndex}.NewRun",
                        checkpointClearedPass && stageResetPass && runCounterStablePass,
                        $"checkpoint={saveManager.HasCheckpoint}, stage={mapSystem.CurrentStage}, runs={saveManager.TotalRuns}/{totalRunsBefore}");

                    if (releaseSoakRunMatrixEachIteration)
                    {
                        yield return RunPresetStageMatrixCheck();
                        AddSoakResult($"ReleaseSoak.I{runIndex}.MatrixGate", lastMatrixPassed, lastMatrixSummary);
                    }

                    if (releaseSoakIterationDelaySeconds > 0f)
                    {
                        yield return new WaitForSecondsRealtime(releaseSoakIterationDelaySeconds);
                    }
                }

                bool saveRestorePass = saveManager.RestoreRuntimeSaveSnapshotForRuntime(saveSnapshot);
                AddSoakResult(
                    "ReleaseSoak.RestoreSaveState",
                    saveRestorePass,
                    saveRestorePass ? "runtime save snapshot restored" : "runtime save snapshot restore failed");

                yield return RestoreRuntimeSnapshot(runtimeSnapshot);

                FinalizeSoakRun();
            }
            finally
            {
                if (suppressionPushed)
                {
                    suppressionScope.Dispose();
                }

                if (diskWriteSuppressionPushed)
                {
                    diskWriteScope.Dispose();
                }

                isSoakRunning = false;
            }
        }

        private IEnumerator RunReleaseSoakDeathResetIteration(int runIndex)
        {
            if (playerVitals == null)
            {
                AddSoakResult($"ReleaseSoak.I{runIndex}.DeathReset", false, "player vitals missing");
                yield break;
            }

            if ((decoyAbility != null && decoyAbility.ActiveDecoyCount > 0) || (smokeAbility != null && smokeAbility.ActiveSmokeCount > 0))
            {
                AddSoakResult($"ReleaseSoak.I{runIndex}.DeathReset", true, "skipped: active decoy/smoke");
                yield break;
            }

            pulseAbility?.SetCooldownRemainingForRuntime(2f);
            decoyAbility?.SetCooldownRemainingForRuntime(2f);
            smokeAbility?.SetCooldownRemainingForRuntime(2f);
            visibilitySource?.SetFlashlightEnabled(true);
            yield return WaitSettle();

            int deathBefore = playerVitals.DeathCount;
            bool damaged = playerVitals.TryTakeDamage(Mathf.Max(1, playerVitals.CurrentHealth), playerVitals.transform.position);
            if (!damaged)
            {
                yield return new WaitForSecondsRealtime(1.15f);
                damaged = playerVitals.TryTakeDamage(Mathf.Max(1, playerVitals.CurrentHealth), playerVitals.transform.position);
            }

            yield return WaitSettle();
            yield return WaitSettle();

            bool deathCountIncreased = playerVitals.DeathCount == deathBefore + 1;
            bool healthReset = playerVitals.CurrentHealth == playerVitals.MaxHealth;
            bool flashlightReset = visibilitySource == null || !visibilitySource.FlashlightEnabled;
            bool pulseReset = pulseAbility == null || pulseAbility.CooldownRemaining <= 0.05f;
            bool decoyReset = decoyAbility == null || (decoyAbility.CooldownRemaining <= 0.05f && decoyAbility.ActiveDecoyCount == 0);
            bool smokeReset = smokeAbility == null || (smokeAbility.CooldownRemaining <= 0.05f && smokeAbility.ActiveSmokeCount == 0);
            bool sprintReset = playerController == null || (!playerController.IsSprinting && playerController.CurrentStamina >= playerController.MaxStamina * 0.95f);
            bool concealmentReset = concealmentState == null || !concealmentState.IsConcealedFromEnemies;

            bool pass = damaged
                        && deathCountIncreased
                        && healthReset
                        && flashlightReset
                        && pulseReset
                        && decoyReset
                        && smokeReset
                        && sprintReset
                        && concealmentReset;

            AddSoakResult(
                $"ReleaseSoak.I{runIndex}.DeathReset",
                pass,
                $"damaged={damaged}, deaths={deathBefore}->{playerVitals.DeathCount}, hp={playerVitals.CurrentHealth}/{playerVitals.MaxHealth}, flash={(visibilitySource != null ? visibilitySource.FlashlightEnabled.ToString() : "n/a")}");
        }

        private RuntimeSnapshot CaptureRuntimeSnapshot()
        {
            int stage = mapSystem != null ? Mathf.Max(1, mapSystem.CurrentStage) : 1;
            Vector3 playerPosition = playerController != null ? playerController.transform.position : Vector3.zero;
            float staminaNormalized = playerController != null ? playerController.StaminaNormalized : 1f;
            int currentHealth = playerVitals != null ? playerVitals.CurrentHealth : 3;
            int deathCount = playerVitals != null ? playerVitals.DeathCount : 0;
            bool flashlightEnabled = visibilitySource != null && visibilitySource.FlashlightEnabled;

            float behaviorScore = telemetry != null ? telemetry.BehaviorScore : 0f;
            float sprintSeconds = telemetry != null ? telemetry.SprintSeconds : 0f;
            int echoCount = telemetry != null ? telemetry.EchoCount : 0;
            int pulseCount = telemetry != null ? telemetry.PulseCastCount : 0;
            int decoyCount = telemetry != null ? telemetry.DecoyDeployCount : 0;
            int smokeCount = telemetry != null ? telemetry.SmokeDeployCount : 0;
            int flashlightCount = telemetry != null ? telemetry.FlashlightToggleCount : 0;
            int telemetryDeathCount = telemetry != null ? telemetry.DeathCount : 0;
            int stageAdvanceCount = telemetry != null ? telemetry.StageAdvanceCount : 0;
            int staminaPickupCount = telemetry != null ? telemetry.StaminaPickupCount : 0;

            float pulseCooldownRemaining = pulseAbility != null ? pulseAbility.CooldownRemaining : 0f;
            float decoyCooldownRemaining = decoyAbility != null ? decoyAbility.CooldownRemaining : 0f;
            float smokeCooldownRemaining = smokeAbility != null ? smokeAbility.CooldownRemaining : 0f;

            bool hasMapPreset = mapTuning != null;
            MapTuningPreset mapPreset = mapTuning != null ? mapTuning.ActivePreset : MapTuningPreset.Standard;

            return new RuntimeSnapshot(
                stage,
                playerPosition,
                staminaNormalized,
                currentHealth,
                deathCount,
                flashlightEnabled,
                behaviorScore,
                sprintSeconds,
                echoCount,
                pulseCount,
                decoyCount,
                smokeCount,
                flashlightCount,
                telemetryDeathCount,
                stageAdvanceCount,
                staminaPickupCount,
                pulseCooldownRemaining,
                decoyCooldownRemaining,
                smokeCooldownRemaining,
                hasMapPreset,
                mapPreset);
        }

        private IEnumerator RestoreRuntimeSnapshot(RuntimeSnapshot snapshot)
        {
            ResolveReferences();

            if (snapshot.HasMapPreset && mapTuning != null)
            {
                mapTuning.ApplyPresetForEditor(snapshot.MapPreset, regenerate: false);
                yield return WaitSettle();
            }

            if (mapSystem != null)
            {
                mapSystem.GenerateMapForStage(snapshot.Stage);
                yield return WaitSettle();
                ResolveReferences();
            }

            if (playerController != null)
            {
                playerController.transform.position = snapshot.PlayerPosition;
                playerController.ApplySavedStaminaNormalized(snapshot.StaminaNormalized);
            }

            if (playerVitals != null)
            {
                playerVitals.ApplySavedVitals(snapshot.CurrentHealth, snapshot.DeathCount);
            }

            if (visibilitySource != null)
            {
                visibilitySource.SetFlashlightEnabled(snapshot.FlashlightEnabled);
            }

            if (telemetry != null)
            {
                telemetry.ApplySavedState(
                    snapshot.BehaviorScore,
                    snapshot.SprintSeconds,
                    snapshot.EchoCount,
                    snapshot.PulseCount,
                    snapshot.DecoyCount,
                    snapshot.SmokeCount,
                    snapshot.FlashlightCount,
                    snapshot.TelemetryDeathCount,
                    snapshot.StageAdvanceCount,
                    snapshot.StaminaPickupCount);
            }

            pulseAbility?.SetCooldownRemainingForRuntime(snapshot.PulseCooldownRemaining);
            decoyAbility?.SetCooldownRemainingForRuntime(snapshot.DecoyCooldownRemaining);
            smokeAbility?.SetCooldownRemainingForRuntime(snapshot.SmokeCooldownRemaining);

            pressureDirector?.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);

            yield return WaitSettle();
        }

        private IEnumerator RunMapGenerationCheck(int stage, bool requireHooks, int hookAccumulator, System.Action<int> setHookAccumulator)
        {
            if (mapSystem == null)
            {
                AddResult($"Map.Stage{stage}", false, "map system missing");
                yield break;
            }

            mapSystem.GenerateMapForStage(stage);
            yield return WaitSettle();

            int cellCount = mapSystem.LastGeneratedCells.Count;
            int wallCount = mapSystem.LastWallSegmentCount;
            int occluderCount = mapSystem.LastOccluderCount;
            int hookCount = mapSystem.LastArchetypeHookCount;

            bool stagePass = cellCount > 0 && wallCount > 0 && occluderCount > 0;
            if (requireHooks)
            {
                setHookAccumulator?.Invoke(hookAccumulator + hookCount);
            }

            string detail = $"cells={cellCount}, walls={wallCount}, occluders={occluderCount}, hooks={hookCount}";
            AddResult($"Map.Stage{stage}", stagePass, detail);

            bool hookTuningWired = mapSystem.LastHookChanceMultiplier > 0.05f
                                   && mapSystem.LastHookLoudnessMultiplier > 0.05f
                                   && mapSystem.LastHookRadiusMultiplier > 0.05f
                                   && mapSystem.LastHookCooldownMultiplier > 0.05f;
            AddResult(
                $"Map.Stage{stage}.HookTuning",
                hookTuningWired,
                $"preset={mapSystem.LastHookPresetLabel}, p={mapSystem.LastHookStagePressure01:0.00}, mul={mapSystem.LastHookChanceMultiplier:0.00}/{mapSystem.LastHookLoudnessMultiplier:0.00}/{mapSystem.LastHookRadiusMultiplier:0.00}/{mapSystem.LastHookCooldownMultiplier:0.00}");

            if (stageLoopDirector != null)
            {
                bool objectiveReady = stageLoopDirector.RequiredBreadcrumbs > 0;
                AddResult($"Map.Stage{stage}.Objective", objectiveReady, $"breadcrumbs={stageLoopDirector.CollectedBreadcrumbs}/{stageLoopDirector.RequiredBreadcrumbs}");
            }
        }

        private IEnumerator RunPressureScalingCheck()
        {
            if (mapSystem == null || pressureDirector == null)
            {
                AddResult("Pressure.Curve", false, "missing map or pressure director");
                yield break;
            }

            mapSystem.GenerateMapForStage(1);
            yield return WaitSettle();
            pressureDirector.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
            float stage1Pressure = pressureDirector.CurrentPressure01;
            float stage1EnemyMultiplier = pressureDirector.AppliedEnemyCountMultiplier;
            float stage1PulseCd = pressureDirector.AppliedPulseCooldownMultiplier;
            readabilityDirector?.ApplyNowForEditor();
            float stage1ReadabilityPressure = readabilityDirector != null ? readabilityDirector.CurrentReadabilityPressure : 0f;
            float stage1HookChanceMultiplier = mapSystem.LastHookChanceMultiplier;

            mapSystem.GenerateMapForStage(5);
            yield return WaitSettle();
            pressureDirector.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
            float stage5Pressure = pressureDirector.CurrentPressure01;
            float stage5EnemyMultiplier = pressureDirector.AppliedEnemyCountMultiplier;
            float stage5PulseCd = pressureDirector.AppliedPulseCooldownMultiplier;
            readabilityDirector?.ApplyNowForEditor();
            float stage5ReadabilityPressure = readabilityDirector != null ? readabilityDirector.CurrentReadabilityPressure : 0f;
            float stage5HookChanceMultiplier = mapSystem.LastHookChanceMultiplier;

            bool pressureCurvePass = stage5Pressure >= stage1Pressure;
            bool enemyScalingPass = stage5EnemyMultiplier >= stage1EnemyMultiplier;
            bool cooldownEconomyPass = stage5PulseCd >= stage1PulseCd;
            bool readabilityCurvePass = readabilityDirector == null || stage5ReadabilityPressure >= stage1ReadabilityPressure;
            bool hookChanceScalingPass = stage5HookChanceMultiplier >= stage1HookChanceMultiplier;

            AddResult("Pressure.Curve", pressureCurvePass, $"p1={stage1Pressure:0.00}, p5={stage5Pressure:0.00}");
            AddResult("Pressure.EnemyMul", enemyScalingPass, $"m1={stage1EnemyMultiplier:0.00}, m5={stage5EnemyMultiplier:0.00}");
            AddResult("Pressure.CooldownMul", cooldownEconomyPass, $"cd1={stage1PulseCd:0.00}, cd5={stage5PulseCd:0.00}");
            AddResult("Pressure.ReadabilityCurve", readabilityCurvePass, $"r1={stage1ReadabilityPressure:0.00}, r5={stage5ReadabilityPressure:0.00}");
            AddResult("Pressure.HookChanceMul", hookChanceScalingPass, $"h1={stage1HookChanceMultiplier:0.00}, h5={stage5HookChanceMultiplier:0.00}");

            if (enemySpawnDirector != null)
            {
                bool wiredEnemyPass = Mathf.Abs(enemySpawnDirector.RuntimeEnemyCountMultiplier - pressureDirector.AppliedEnemyCountMultiplier) <= 0.001f;
                AddResult("Pressure.WireEnemySpawn", wiredEnemyPass, $"spawnMul={enemySpawnDirector.RuntimeEnemyCountMultiplier:0.00}, pressureMul={pressureDirector.AppliedEnemyCountMultiplier:0.00}");
            }

            if (readabilityDirector == null)
            {
                AddResult("Pressure.WireReadability", false, "readability director missing");
            }
            else
            {
                bool readabilityWirePass = readabilityDirector.CurrentReadabilityPressure >= 0f && readabilityDirector.CurrentReadabilityPressure <= 1f;
                AddResult("Pressure.WireReadability", readabilityWirePass, $"pressure={readabilityDirector.CurrentReadabilityPressure:0.00}, preset={readabilityDirector.LastPresetLabel}");
            }

            if (runLoadoutDirector != null)
            {
                bool wiredLoadoutPass = Mathf.Abs(runLoadoutDirector.PressurePulseCooldownMultiplier - pressureDirector.AppliedPulseCooldownMultiplier) <= 0.001f;
                AddResult("Pressure.WireLoadout", wiredLoadoutPass, $"loadoutCd={runLoadoutDirector.PressurePulseCooldownMultiplier:0.00}, pressureCd={pressureDirector.AppliedPulseCooldownMultiplier:0.00}");
            }
        }

        private IEnumerator RunPresetStageMatrixCheck()
        {
            if (!runPresetStageMatrix)
            {
                lastMatrixRan = false;
                lastMatrixPassed = true;
                lastMatrixSampleCount = 0;
                lastMatrixPassCount = 0;
                lastMatrixFailCount = 0;
                lastMatrixMinPressure = 0f;
                lastMatrixMaxPressure = 0f;
                lastMatrixMinReadability = 0f;
                lastMatrixMaxReadability = 0f;
                lastMatrixSummary = "Skipped (disabled)";
                AddResult("Matrix.Enabled", true, "disabled");
                yield break;
            }

            ResolveReferences();

            if (mapSystem == null || pressureDirector == null || readabilityDirector == null)
            {
                lastMatrixRan = true;
                lastMatrixPassed = false;
                lastMatrixSampleCount = 0;
                lastMatrixPassCount = 0;
                lastMatrixFailCount = 1;
                lastMatrixMinPressure = 0f;
                lastMatrixMaxPressure = 0f;
                lastMatrixMinReadability = 0f;
                lastMatrixMaxReadability = 0f;
                lastMatrixSummary = "FAIL: missing map/pressure/readability";
                AddResult("Matrix.Ready", false, "missing map or pressure/readability director");
                yield break;
            }

            List<int> stages = BuildMatrixStages();
            if (stages.Count <= 0)
            {
                lastMatrixRan = true;
                lastMatrixPassed = false;
                lastMatrixSampleCount = 0;
                lastMatrixPassCount = 0;
                lastMatrixFailCount = 1;
                lastMatrixMinPressure = 0f;
                lastMatrixMaxPressure = 0f;
                lastMatrixMinReadability = 0f;
                lastMatrixMaxReadability = 0f;
                lastMatrixSummary = "FAIL: matrix stages invalid";
                AddResult("Matrix.Stages", false, "no valid matrix stages");
                yield break;
            }

            MapTuningPreset[] presets =
            {
                MapTuningPreset.Compact,
                MapTuningPreset.Standard,
                MapTuningPreset.Expansive
            };

            StageSetPieceDirector setPieceDirector = FindFirstObjectByType<StageSetPieceDirector>();
            int midStageThreshold = Mathf.Max(1, matrixStageMid);
            int allowedCurveDips = Mathf.Max(0, matrixAllowedCurveDips);
            float maxCurveDipMagnitude = Mathf.Max(0f, matrixMaxAllowedDipMagnitude);

            int sampleCount = 0;
            int passCount = 0;
            int matrixLogicFailures = 0;
            float minPressure = float.MaxValue;
            float maxPressure = float.MinValue;
            float minReadability = float.MaxValue;
            float maxReadability = float.MinValue;

            for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
            {
                MapTuningPreset preset = presets[presetIndex];
                if (!ApplyMatrixPreset(preset, out string presetDetail))
                {
                    matrixLogicFailures++;
                    AddResult($"Matrix.{preset}.Preset", false, presetDetail);
                    continue;
                }

                AddResult($"Matrix.{preset}.Preset", true, presetDetail);

                List<float> presetPressures = new();
                List<float> presetReadabilities = new();

                for (int stageIndex = 0; stageIndex < stages.Count; stageIndex++)
                {
                    int stage = stages[stageIndex];
                    mapSystem.GenerateMapForStage(stage);
                    yield return WaitSettle();

                    pressureDirector.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
                    readabilityDirector.ApplyNowForEditor();
                    setPieceDirector ??= FindFirstObjectByType<StageSetPieceDirector>();
                    if (setPieceDirector != null && setPieceDirector.LastAppliedStage != stage)
                    {
                        // Set-piece build is queued off map-generated callback and can land one frame later.
                        yield return WaitSettle();
                    }

                    float pressure = Mathf.Clamp01(pressureDirector.CurrentPressure01);
                    float readability = Mathf.Clamp01(readabilityDirector.CurrentReadabilityPressure);
                    int cellCount = mapSystem.LastGeneratedCells.Count;
                    int wallCount = mapSystem.LastWallSegmentCount;
                    int occluderCount = mapSystem.LastOccluderCount;
                    int hookCount = mapSystem.LastArchetypeHookCount;

                    bool mapPass = cellCount > 0 && wallCount > 0 && occluderCount > 0;
                    bool hookPass = mapSystem.LastHookChanceMultiplier > 0.05f
                                    && mapSystem.LastHookLoudnessMultiplier > 0.05f
                                    && mapSystem.LastHookRadiusMultiplier > 0.05f
                                    && mapSystem.LastHookCooldownMultiplier > 0.05f;

                    bool setPiecePass = true;
                    string setPieceDetail = "n/a";
                    if (requireSetPieceFromMidStage && stage >= midStageThreshold)
                    {
                        if (setPieceDirector == null)
                        {
                            setPiecePass = false;
                            setPieceDetail = "director missing";
                        }
                        else
                        {
                            bool hasTier = setPieceDirector.ActiveTier != StageSetPieceTier.None;
                            bool hasBeacon = setPieceDirector.ActiveBeaconCount > 0;
                            bool hasReinforcement = setPieceDirector.LastReinforcementCount > 0;
                            bool hasPulseTelemetry =
                                setPieceDirector.LastRuntimePulseInterval > 0.01f
                                && setPieceDirector.LastRuntimePulseLoudness > 0.01f
                                && setPieceDirector.LastRuntimePulseRadius > 0.01f;
                            setPiecePass = hasTier && hasPulseTelemetry && (hasBeacon || hasReinforcement);
                            setPieceDetail =
                                $"tier={setPieceDirector.ActiveTier}, stage={setPieceDirector.LastAppliedStage}, beacons={setPieceDirector.ActiveBeaconCount}, reinf={setPieceDirector.LastReinforcementCount}, pulse={setPieceDirector.LastRuntimePulseInterval:0.00}/{setPieceDirector.LastRuntimePulseLoudness:0.00}/{setPieceDirector.LastRuntimePulseRadius:0.00}";
                        }
                    }

                    bool samplePass = mapPass && hookPass && setPiecePass;
                    sampleCount++;
                    if (samplePass)
                    {
                        passCount++;
                    }

                    minPressure = Mathf.Min(minPressure, pressure);
                    maxPressure = Mathf.Max(maxPressure, pressure);
                    minReadability = Mathf.Min(minReadability, readability);
                    maxReadability = Mathf.Max(maxReadability, readability);

                    presetPressures.Add(pressure);
                    presetReadabilities.Add(readability);

                    string detail =
                        $"cells={cellCount}, walls={wallCount}, occ={occluderCount}, hooks={hookCount}, p={pressure:0.00}, r={readability:0.00}, setPiece={setPieceDetail}";
                    AddResult($"Matrix.{preset}.S{stage}", samplePass, detail);
                }

                bool pressureCurvePass = EvaluateCurveTrend(
                    presetPressures,
                    matrixCurveTolerance,
                    allowedCurveDips,
                    maxCurveDipMagnitude,
                    out int pressureDipCount,
                    out float pressureMaxDip);
                bool readabilityCurvePass = EvaluateCurveTrend(
                    presetReadabilities,
                    matrixCurveTolerance,
                    allowedCurveDips,
                    maxCurveDipMagnitude,
                    out int readabilityDipCount,
                    out float readabilityMaxDip);
                if (!pressureCurvePass)
                {
                    matrixLogicFailures++;
                }

                if (!readabilityCurvePass)
                {
                    matrixLogicFailures++;
                }

                AddResult(
                    $"Matrix.{preset}.PressureCurve",
                    pressureCurvePass,
                    $"{BuildCurveDetail(stages, presetPressures)} | dips={pressureDipCount}/{allowedCurveDips}, maxDip={pressureMaxDip:0.00}/{maxCurveDipMagnitude:0.00}");
                AddResult(
                    $"Matrix.{preset}.ReadabilityCurve",
                    readabilityCurvePass,
                    $"{BuildCurveDetail(stages, presetReadabilities)} | dips={readabilityDipCount}/{allowedCurveDips}, maxDip={readabilityMaxDip:0.00}/{maxCurveDipMagnitude:0.00}");
            }

            int failCount = Mathf.Max(0, sampleCount - passCount) + Mathf.Max(0, matrixLogicFailures);
            bool baselineGatePass = true;
            if (matrixEnableBaselineLock)
            {
                float driftTolerance = Mathf.Max(0f, matrixBaselineDriftTolerance);
                if (matrixBaseline.IsValid)
                {
                    baselineGatePass = EvaluateMatrixBaselineEnvelope(
                        minPressure,
                        maxPressure,
                        minReadability,
                        maxReadability,
                        driftTolerance,
                        out string baselineDetail);

                    AddResult("Matrix.BaselineEnvelope", baselineGatePass, baselineDetail);
                    if (!baselineGatePass && matrixBaselineAffectsPass)
                    {
                        failCount++;
                    }

                    if (baselineGatePass && matrixAutoRefreshBaselineOnPass && !matrixBaselineFrozen && sampleCount > 0)
                    {
                        LockMatrixBaseline(sampleCount, minPressure, maxPressure, minReadability, maxReadability);
                        AddResult("Matrix.BaselineRefresh", true, matrixBaseline.LastCapturedSummary);
                    }
                }
                else
                {
                    bool canLock = !matrixBaselineFrozen && matrixAutoLockBaselineOnPass && failCount == 0 && sampleCount > 0;
                    if (canLock)
                    {
                        LockMatrixBaseline(sampleCount, minPressure, maxPressure, minReadability, maxReadability);
                        AddResult("Matrix.BaselineLock", true, matrixBaseline.LastCapturedSummary);
                    }
                    else
                    {
                        string pendingDetail;
                        if (matrixBaselineFrozen)
                        {
                            pendingDetail = "baseline pending (frozen)";
                        }
                        else if (!matrixAutoLockBaselineOnPass && failCount == 0)
                        {
                            pendingDetail = "baseline pending (auto-lock disabled)";
                        }
                        else
                        {
                            pendingDetail = "baseline pending (run must pass first)";
                        }

                        AddResult("Matrix.BaselineLock", true, pendingDetail);
                    }
                }

                if (matrixRequireBaselineBeforePass && !matrixBaseline.IsValid)
                {
                    failCount++;
                    AddResult("Matrix.BaselineRequired", false, "baseline missing");
                }
            }

            bool matrixPass = failCount == 0;

            lastMatrixRan = true;
            lastMatrixPassed = matrixPass;
            lastMatrixSampleCount = sampleCount;
            lastMatrixPassCount = passCount;
            lastMatrixFailCount = failCount;
            lastMatrixMinPressure = sampleCount > 0 ? minPressure : 0f;
            lastMatrixMaxPressure = sampleCount > 0 ? maxPressure : 0f;
            lastMatrixMinReadability = sampleCount > 0 ? minReadability : 0f;
            lastMatrixMaxReadability = sampleCount > 0 ? maxReadability : 0f;
            lastMatrixSummary =
                $"PresetStage {(matrixPass ? "PASS" : "FAIL")}: {passCount}/{sampleCount} | P {lastMatrixMinPressure:0.00}-{lastMatrixMaxPressure:0.00} | R {lastMatrixMinReadability:0.00}-{lastMatrixMaxReadability:0.00}";

            AddResult("Matrix.Overall", matrixPass, lastMatrixSummary);
        }

        private List<int> BuildMatrixStages()
        {
            List<int> values = new()
            {
                Mathf.Max(1, matrixStageEarly),
                Mathf.Max(1, matrixStageMid),
                Mathf.Max(1, matrixStageLate)
            };

            values.Sort();

            for (int i = values.Count - 1; i > 0; i--)
            {
                if (values[i] == values[i - 1])
                {
                    values.RemoveAt(i);
                }
            }

            return values;
        }

        private bool ApplyMatrixPreset(MapTuningPreset preset, out string detail)
        {
            if (mapTuning == null)
            {
                bool fallbackPass = preset == MapTuningPreset.Standard;
                detail = fallbackPass
                    ? "map tuning missing: using current config as Standard fallback"
                    : "map tuning missing";
                return fallbackPass;
            }

            mapTuning.ApplyPresetForEditor(preset, regenerate: false);
            detail = $"active={mapTuning.ActivePresetLabel}";
            return true;
        }

        private static bool EvaluateCurveTrend(
            IReadOnlyList<float> values,
            float tolerance,
            int allowedDips,
            float maxDipMagnitude,
            out int observedDipCount,
            out float observedMaxDipMagnitude)
        {
            observedDipCount = 0;
            observedMaxDipMagnitude = 0f;
            if (values == null || values.Count <= 1)
            {
                return true;
            }

            float safeTolerance = Mathf.Abs(tolerance);
            int safeAllowedDips = Mathf.Max(0, allowedDips);
            float safeMaxDipMagnitude = Mathf.Max(0f, maxDipMagnitude);

            float previous = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                float current = values[i];
                if (current + safeTolerance < previous)
                {
                    float dipMagnitude = previous - current;
                    observedDipCount++;
                    observedMaxDipMagnitude = Mathf.Max(observedMaxDipMagnitude, dipMagnitude);

                    bool exceedsCount = observedDipCount > safeAllowedDips;
                    bool exceedsMagnitude = dipMagnitude > safeTolerance + safeMaxDipMagnitude;
                    if (exceedsCount || exceedsMagnitude)
                    {
                        return false;
                    }
                }

                previous = current;
            }

            return true;
        }

        private static string BuildCurveDetail(IReadOnlyList<int> stages, IReadOnlyList<float> values)
        {
            if (stages == null || values == null)
            {
                return "none";
            }

            int count = Mathf.Min(stages.Count, values.Count);
            if (count <= 0)
            {
                return "none";
            }

            string detail = string.Empty;
            for (int i = 0; i < count; i++)
            {
                string token = $"S{stages[i]}={values[i]:0.00}";
                detail = string.IsNullOrEmpty(detail) ? token : $"{detail} | {token}";
            }

            return detail;
        }

        private bool EvaluateMatrixBaselineEnvelope(
            float minPressure,
            float maxPressure,
            float minReadability,
            float maxReadability,
            float driftTolerance,
            out string detail)
        {
            if (!matrixBaseline.IsValid)
            {
                detail = "baseline missing";
                return true;
            }

            bool pressureMinPass = minPressure + driftTolerance >= matrixBaseline.MinPressure;
            bool pressureMaxPass = maxPressure <= matrixBaseline.MaxPressure + driftTolerance;
            bool readabilityMinPass = minReadability + driftTolerance >= matrixBaseline.MinReadability;
            bool readabilityMaxPass = maxReadability <= matrixBaseline.MaxReadability + driftTolerance;
            bool pass = pressureMinPass && pressureMaxPass && readabilityMinPass && readabilityMaxPass;

            detail =
                $"base P {matrixBaseline.MinPressure:0.00}-{matrixBaseline.MaxPressure:0.00}, now {minPressure:0.00}-{maxPressure:0.00}; " +
                $"base R {matrixBaseline.MinReadability:0.00}-{matrixBaseline.MaxReadability:0.00}, now {minReadability:0.00}-{maxReadability:0.00}; " +
                $"tol={driftTolerance:0.00}";

            return pass;
        }

        private void LockMatrixBaseline(
            int sampleCount,
            float minPressure,
            float maxPressure,
            float minReadability,
            float maxReadability)
        {
            matrixBaseline = new MatrixBaselineSnapshot
            {
                IsValid = sampleCount > 0,
                SampleCount = Mathf.Max(0, sampleCount),
                MinPressure = sampleCount > 0 ? minPressure : 0f,
                MaxPressure = sampleCount > 0 ? maxPressure : 0f,
                MinReadability = sampleCount > 0 ? minReadability : 0f,
                MaxReadability = sampleCount > 0 ? maxReadability : 0f,
                LastCapturedSummary =
                    sampleCount > 0
                        ? $"locked samples={sampleCount}, P {minPressure:0.00}-{maxPressure:0.00}, R {minReadability:0.00}-{maxReadability:0.00}"
                        : "empty baseline"
            };
        }

        private IEnumerator RunChaseReadabilityRegressionCheck()
        {
            if (!runChaseReadabilityRegression)
            {
                lastChaseReadabilityRan = false;
                lastChaseReadabilityPassed = true;
                lastChaseReadabilitySampleCount = 0;
                lastChaseReadabilityPassCount = 0;
                lastChaseReadabilityFailCount = 0;
                lastChaseReadabilitySummary = "Skipped (disabled)";
                AddResult("ChaseReadability.Enabled", true, "disabled");
                yield break;
            }

            ResolveReferences();

            if (mapSystem == null || pressureDirector == null || readabilityDirector == null)
            {
                lastChaseReadabilityRan = true;
                lastChaseReadabilityPassed = false;
                lastChaseReadabilitySampleCount = 0;
                lastChaseReadabilityPassCount = 0;
                lastChaseReadabilityFailCount = 1;
                lastChaseReadabilitySummary = "FAIL: missing map/pressure/readability";
                AddResult("ChaseReadability.Ready", false, "missing map or pressure/readability director");
                yield break;
            }

            int lowStage = Mathf.Max(1, Mathf.Min(chaseRegressionStageLow, chaseRegressionStageHigh));
            int highStage = Mathf.Max(1, Mathf.Max(chaseRegressionStageLow, chaseRegressionStageHigh));
            if (highStage <= lowStage)
            {
                highStage = lowStage + 2;
            }

            MapTuningPreset[] presets =
            {
                MapTuningPreset.Compact,
                MapTuningPreset.Standard,
                MapTuningPreset.Expansive
            };

            float tolerance = Mathf.Max(0f, chaseReadabilityTolerance);
            int checkCount = 0;
            int passCount = 0;
            int logicFailures = 0;

            for (int presetIndex = 0; presetIndex < presets.Length; presetIndex++)
            {
                MapTuningPreset preset = presets[presetIndex];
                if (!ApplyMatrixPreset(preset, out string presetDetail))
                {
                    logicFailures++;
                    AddResult($"ChaseReadability.{preset}.Preset", false, presetDetail);
                    continue;
                }

                AddResult($"ChaseReadability.{preset}.Preset", true, presetDetail);

                mapSystem.GenerateMapForStage(lowStage);
                yield return WaitSettle();
                pressureDirector.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
                readabilityDirector.ApplyNowForEditor();
                yield return WaitSettle();

                if (!TryCaptureChaseReadabilitySample(out ChaseReadabilitySample lowSample, out string lowDetail))
                {
                    logicFailures++;
                    AddResult($"ChaseReadability.{preset}.S{lowStage}", false, lowDetail);
                    continue;
                }

                AddResult($"ChaseReadability.{preset}.S{lowStage}", true, lowDetail);

                mapSystem.GenerateMapForStage(highStage);
                yield return WaitSettle();
                pressureDirector.ApplyPressureNow(rebuildEnemies: true, raiseEvent: false);
                readabilityDirector.ApplyNowForEditor();
                yield return WaitSettle();

                if (!TryCaptureChaseReadabilitySample(out ChaseReadabilitySample highSample, out string highDetail))
                {
                    logicFailures++;
                    AddResult($"ChaseReadability.{preset}.S{highStage}", false, highDetail);
                    continue;
                }

                AddResult($"ChaseReadability.{preset}.S{highStage}", true, highDetail);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.TransitionSeconds",
                    highSample.TransitionSeconds <= lowSample.TransitionSeconds + tolerance,
                    $"low={lowSample.TransitionSeconds:0.00}, high={highSample.TransitionSeconds:0.00}, expect high<=low",
                    ref checkCount,
                    ref passCount);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.PulseSpeed",
                    highSample.TransitionPulseSpeed + tolerance >= lowSample.TransitionPulseSpeed,
                    $"low={lowSample.TransitionPulseSpeed:0.00}, high={highSample.TransitionPulseSpeed:0.00}, expect high>=low",
                    ref checkCount,
                    ref passCount);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.FlashStrength",
                    highSample.TransitionFlashStrength + tolerance >= lowSample.TransitionFlashStrength,
                    $"low={lowSample.TransitionFlashStrength:0.00}, high={highSample.TransitionFlashStrength:0.00}, expect high>=low",
                    ref checkCount,
                    ref passCount);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.DisengageCue",
                    highSample.DisengageCueSeconds <= lowSample.DisengageCueSeconds + tolerance,
                    $"low={lowSample.DisengageCueSeconds:0.00}, high={highSample.DisengageCueSeconds:0.00}, expect high<=low",
                    ref checkCount,
                    ref passCount);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.DisengageGrace",
                    highSample.DisengageGraceSeconds <= lowSample.DisengageGraceSeconds + tolerance,
                    $"low={lowSample.DisengageGraceSeconds:0.00}, high={highSample.DisengageGraceSeconds:0.00}, expect high<=low",
                    ref checkCount,
                    ref passCount);

                RegisterChaseReadabilityResult(
                    $"ChaseReadability.{preset}.BlinkSpeed",
                    highSample.ChaseBlinkSpeed + tolerance >= lowSample.ChaseBlinkSpeed,
                    $"low={lowSample.ChaseBlinkSpeed:0.00}, high={highSample.ChaseBlinkSpeed:0.00}, expect high>=low",
                    ref checkCount,
                    ref passCount);
            }

            int failCount = Mathf.Max(0, checkCount - passCount) + Mathf.Max(0, logicFailures);
            bool pass = failCount == 0;

            lastChaseReadabilityRan = true;
            lastChaseReadabilityPassed = pass;
            lastChaseReadabilitySampleCount = checkCount;
            lastChaseReadabilityPassCount = passCount;
            lastChaseReadabilityFailCount = failCount;
            lastChaseReadabilitySummary =
                $"ChaseReadability {(pass ? "PASS" : "FAIL")}: {passCount}/{checkCount} (logic={logicFailures}, stage {lowStage}->{highStage})";

            AddResult("ChaseReadability.Overall", pass, lastChaseReadabilitySummary);
        }

        private void RegisterChaseReadabilityResult(string key, bool passed, string detail, ref int checkCount, ref int passCount)
        {
            checkCount++;
            if (passed)
            {
                passCount++;
            }

            AddResult(key, passed, detail);
        }

        private bool TryCaptureChaseReadabilitySample(out ChaseReadabilitySample sample, out string detail)
        {
            sample = default;
            detail = "no enemy sample";

            EnemyController.CopyActiveControllers(chaseReadabilityEnemies);
            if (chaseReadabilityEnemies.Count <= 0)
            {
                detail = "no active enemies";
                return false;
            }

            int sampleCap = Mathf.Max(1, chaseReadabilityEnemySampleCap);
            int targetSamples = Mathf.Min(sampleCap, chaseReadabilityEnemies.Count);

            float transitionSum = 0f;
            float pulseSum = 0f;
            float flashSum = 0f;
            float disengageCueSum = 0f;
            float disengageGraceSum = 0f;
            float blinkSum = 0f;
            int counted = 0;

            for (int i = 0; i < chaseReadabilityEnemies.Count && counted < targetSamples; i++)
            {
                EnemyController enemy = chaseReadabilityEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                transitionSum += enemy.EffectiveChaseTransitionSeconds;
                pulseSum += enemy.EffectiveTransitionPulseSpeed;
                flashSum += enemy.EffectiveTransitionFlashStrength;
                disengageCueSum += enemy.EffectiveDisengageCueSeconds;
                disengageGraceSum += enemy.EffectiveDisengageDistanceGraceSeconds;
                blinkSum += enemy.EffectiveChaseBlinkSpeed;
                counted++;
            }

            if (counted <= 0)
            {
                detail = "enemy sample missing";
                return false;
            }

            float inv = 1f / counted;
            sample = new ChaseReadabilitySample(
                counted,
                transitionSum * inv,
                pulseSum * inv,
                flashSum * inv,
                disengageCueSum * inv,
                disengageGraceSum * inv,
                blinkSum * inv);

            detail = BuildChaseSampleDetail(sample);
            return true;
        }

        private static string BuildChaseSampleDetail(ChaseReadabilitySample sample)
        {
            return
                $"enemies={sample.EnemyCount}, T={sample.TransitionSeconds:0.00}, P={sample.TransitionPulseSpeed:0.00}, F={sample.TransitionFlashStrength:0.00}, D={sample.DisengageCueSeconds:0.00}, G={sample.DisengageGraceSeconds:0.00}, B={sample.ChaseBlinkSpeed:0.00}";
        }
        private IEnumerator RunDeathResetCheck()
        {
            if (playerVitals == null)
            {
                AddResult("Death.Reset", false, "player vitals missing");
                yield break;
            }

            ResolveReferences();

            if ((decoyAbility != null && decoyAbility.ActiveDecoyCount > 0) || (smokeAbility != null && smokeAbility.ActiveSmokeCount > 0))
            {
                AddResult("Death.Precondition", false, "active decoy/smoke exists - skip death reset check to avoid destructive cleanup");
                yield break;
            }

            pulseAbility?.SetCooldownRemainingForRuntime(2f);
            decoyAbility?.SetCooldownRemainingForRuntime(2f);
            smokeAbility?.SetCooldownRemainingForRuntime(2f);
            visibilitySource?.SetFlashlightEnabled(true);

            yield return WaitSettle();

            int deathBefore = playerVitals.DeathCount;
            bool damaged = playerVitals.TryTakeDamage(Mathf.Max(1, playerVitals.CurrentHealth), playerVitals.transform.position);
            if (!damaged)
            {
                yield return new WaitForSecondsRealtime(1.15f);
                damaged = playerVitals.TryTakeDamage(Mathf.Max(1, playerVitals.CurrentHealth), playerVitals.transform.position);
            }

            yield return WaitSettle();
            yield return WaitSettle();

            bool deathCountIncreased = playerVitals.DeathCount == deathBefore + 1;
            bool healthReset = playerVitals.CurrentHealth == playerVitals.MaxHealth;
            bool flashlightReset = visibilitySource == null || !visibilitySource.FlashlightEnabled;
            bool pulseReset = pulseAbility == null || pulseAbility.CooldownRemaining <= 0.05f;
            bool decoyReset = decoyAbility == null || (decoyAbility.CooldownRemaining <= 0.05f && decoyAbility.ActiveDecoyCount == 0);
            bool smokeReset = smokeAbility == null || (smokeAbility.CooldownRemaining <= 0.05f && smokeAbility.ActiveSmokeCount == 0);
            bool sprintReset = playerController == null || (!playerController.IsSprinting && playerController.CurrentStamina >= playerController.MaxStamina * 0.95f);
            bool concealmentReset = concealmentState == null || !concealmentState.IsConcealedFromEnemies;

            AddResult("Death.Trigger", damaged && deathCountIncreased, $"damaged={damaged}, deaths={deathBefore}->{playerVitals.DeathCount}");
            AddResult("Death.HealthReset", healthReset, $"hp={playerVitals.CurrentHealth}/{playerVitals.MaxHealth}");
            AddResult("Death.FlashlightReset", flashlightReset, visibilitySource != null ? $"flashlight={visibilitySource.FlashlightEnabled}" : "no visibility component");
            AddResult("Death.PulseReset", pulseReset, pulseAbility != null ? $"cooldown={pulseAbility.CooldownRemaining:0.00}" : "no pulse ability");
            AddResult("Death.DecoyReset", decoyReset, decoyAbility != null ? $"cooldown={decoyAbility.CooldownRemaining:0.00}, active={decoyAbility.ActiveDecoyCount}" : "no decoy ability");
            AddResult("Death.SmokeReset", smokeReset, smokeAbility != null ? $"cooldown={smokeAbility.CooldownRemaining:0.00}, active={smokeAbility.ActiveSmokeCount}" : "no smoke ability");
            AddResult("Death.SprintReset", sprintReset, playerController != null ? $"stamina={playerController.CurrentStamina:0.00}/{playerController.MaxStamina:0.00}, sprint={playerController.IsSprinting}" : "no controller");
            AddResult("Death.ConcealmentReset", concealmentReset, concealmentState != null ? $"concealed={concealmentState.IsConcealedFromEnemies}" : "no concealment");
        }

        private void FinalizeSoakRun()
        {
            int passCount = 0;
            for (int i = 0; i < currentSoakResults.Count; i++)
            {
                if (currentSoakResults[i].Passed)
                {
                    passCount++;
                }
            }

            int failCount = Mathf.Max(0, currentSoakResults.Count - passCount);
            bool passed = failCount == 0;

            lastSoakResults.Clear();
            for (int i = 0; i < currentSoakResults.Count; i++)
            {
                ChecklistResult result = currentSoakResults[i];
                lastSoakResults.Add(new ChecklistReportEntry(result.Key, result.Passed, result.Detail));
            }

            hasSoakRun = true;
            lastSoakPassed = passed;
            lastSoakPassedCount = passCount;
            lastSoakFailedCount = failCount;
            lastSoakSummary = $"ReleaseSoak {(passed ? "PASS" : "FAIL")}: {passCount}/{currentSoakResults.Count} (run #{soakRunCount})";
            lastSoakIterationFailureSummary = BuildSoakIterationFailureSummary(lastSoakResults);
            lastSoakFailureActionSummary = BuildSoakFailureActionSummary(lastSoakResults, 4);

            if (raiseRuntimeEvent)
            {
                int stage = mapSystem != null ? mapSystem.CurrentStage : 0;
                RuntimeEventBus.Raise(RuntimeEventType.System, lastSoakSummary, this, stage, allowWhenSuppressed: allowFinalSummaryEventWhenSuppressed);
            }

            Debug.Log($"[ReleaseSoak] {lastSoakSummary}", this);
            if (!passed)
            {
                Debug.LogWarning($"[ReleaseSoak] Failure digest: {LastSoakFailureDigest}", this);
                Debug.LogWarning($"[ReleaseSoak] Iteration failure summary: {lastSoakIterationFailureSummary}", this);
                Debug.LogWarning($"[ReleaseSoak] Suggested actions: {lastSoakFailureActionSummary}", this);
            }
        }

        private void FinalizeRun()
        {
            int passCount = 0;
            for (int i = 0; i < currentResults.Count; i++)
            {
                if (currentResults[i].Passed)
                {
                    passCount++;
                }
            }

            int failCount = Mathf.Max(0, currentResults.Count - passCount);
            bool passed = failCount == 0;

            lastRunResults.Clear();
            for (int i = 0; i < currentResults.Count; i++)
            {
                ChecklistResult result = currentResults[i];
                lastRunResults.Add(new ChecklistReportEntry(result.Key, result.Passed, result.Detail));
            }

            hasRun = true;
            lastRunPassed = passed;
            lastRunPassedCount = passCount;
            lastRunFailedCount = failCount;
            lastRunSummary = $"Checklist {(passed ? "PASS" : "FAIL")}: {passCount}/{currentResults.Count} (run #{runCount})";

            if (raiseRuntimeEvent)
            {
                int stage = mapSystem != null ? mapSystem.CurrentStage : 0;
                RuntimeEventBus.Raise(RuntimeEventType.System, lastRunSummary, this, stage, allowWhenSuppressed: allowFinalSummaryEventWhenSuppressed);
            }

            Debug.Log($"[RegressionChecklist] {lastRunSummary}", this);

            if (!passed)
            {
                Debug.LogWarning($"[RegressionChecklist] Failure digest: {LastRunFailureDigest}", this);
            }
        }

        private void RunFeedbackFeatureWiringCheck(int hooksAcrossRegressionStages)
        {
            bool echoVfxReady = typeof(EchoPulseVisualDummy).IsSubclassOf(typeof(MonoBehaviour));
            bool decoyEmitterReady = typeof(DecoyEmitterDummy).IsSubclassOf(typeof(MonoBehaviour));
            bool roomHookReady = typeof(RoomArchetypeHookDummy).IsSubclassOf(typeof(MonoBehaviour));
            bool noiseReady = noiseManager != null || NoiseManager.Instance != null;

            AddResult(
                "Feedback.EchoVfx.Type",
                echoVfxReady,
                echoVfxReady ? "EchoPulseVisualDummy linked" : "EchoPulseVisualDummy type mismatch");

            AddResult(
                "Feedback.ExitUnlockPressure.Wire",
                stageLoopDirector != null && echoVfxReady && noiseReady,
                $"stageLoop={(stageLoopDirector != null ? "Y" : "N")}, echoVfx={(echoVfxReady ? "Y" : "N")}, noise={(noiseReady ? "Y" : "N")}");

            AddResult(
                "Feedback.DecoySuccess.Wire",
                decoyAbility != null && decoyEmitterReady && echoVfxReady,
                $"decoyAbility={(decoyAbility != null ? "Y" : "N")}, emitter={(decoyEmitterReady ? "Y" : "N")}, echoVfx={(echoVfxReady ? "Y" : "N")}");

            AddResult(
                "Feedback.RiskRoomBonus.Wire",
                hooksAcrossRegressionStages > 0 && roomHookReady && playerController != null && echoVfxReady,
                $"hooks={hooksAcrossRegressionStages}, roomHook={(roomHookReady ? "Y" : "N")}, player={(playerController != null ? "Y" : "N")}, echoVfx={(echoVfxReady ? "Y" : "N")}");

            AddResult(
                "Feedback.HudCues.Wire",
                gameplayHud != null && eventFeedback != null,
                $"hud={(gameplayHud != null ? "Y" : "N")}, priorityCue={(eventFeedback != null ? "Y" : "N")}");
        }

        private void AddResult(string key, bool passed, string detail)
        {
            ChecklistResult result = new(key, passed, detail);
            currentResults.Add(result);

            if (!logEachCheck)
            {
                return;
            }

            string prefix = passed ? "PASS" : "FAIL";
            if (passed)
            {
                Debug.Log($"[RegressionChecklist] {prefix} {key}: {detail}", this);
            }
            else
            {
                Debug.LogWarning($"[RegressionChecklist] {prefix} {key}: {detail}", this);
            }
        }

        private void AddSoakResult(string key, bool passed, string detail)
        {
            ChecklistResult result = new(key, passed, detail);
            currentSoakResults.Add(result);

            if (!releaseSoakLogEachCheck)
            {
                return;
            }

            string prefix = passed ? "PASS" : "FAIL";
            if (passed)
            {
                Debug.Log($"[ReleaseSoak] {prefix} {key}: {detail}", this);
            }
            else
            {
                Debug.LogWarning($"[ReleaseSoak] {prefix} {key}: {detail}", this);
            }
        }

        private static string BuildFailureDigest(IReadOnlyList<ChecklistReportEntry> entries, int maxItems)
        {
            if (entries == null || entries.Count <= 0)
            {
                return "none";
            }

            int maxCount = Mathf.Max(1, maxItems);
            int failedCount = 0;
            int shownCount = 0;
            string digest = string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                ChecklistReportEntry entry = entries[i];
                if (entry.Passed)
                {
                    continue;
                }

                failedCount++;
                if (shownCount >= maxCount)
                {
                    continue;
                }

                if (shownCount > 0)
                {
                    digest += " | ";
                }

                digest += entry.Key;
                shownCount++;
            }

            if (failedCount <= 0)
            {
                return "none";
            }

            if (failedCount > shownCount)
            {
                digest += $" (+{failedCount - shownCount} more)";
            }

            return digest;
        }

        private static string BuildSoakIterationFailureSummary(IReadOnlyList<ChecklistReportEntry> entries)
        {
            if (entries == null || entries.Count <= 0)
            {
                return "none";
            }

            Dictionary<int, int> failCountByIteration = new();
            int globalFailCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                ChecklistReportEntry entry = entries[i];
                if (entry.Passed)
                {
                    continue;
                }

                if (TryGetSoakIterationIndex(entry.Key, out int iterationIndex) && iterationIndex > 0)
                {
                    if (failCountByIteration.TryGetValue(iterationIndex, out int existing))
                    {
                        failCountByIteration[iterationIndex] = existing + 1;
                    }
                    else
                    {
                        failCountByIteration.Add(iterationIndex, 1);
                    }
                }
                else
                {
                    globalFailCount++;
                }
            }

            if (failCountByIteration.Count <= 0 && globalFailCount <= 0)
            {
                return "none";
            }

            List<int> iterationKeys = new(failCountByIteration.Keys);
            iterationKeys.Sort();

            string summary = string.Empty;
            for (int i = 0; i < iterationKeys.Count; i++)
            {
                int key = iterationKeys[i];
                if (summary.Length > 0)
                {
                    summary += " | ";
                }

                summary += $"I{key}:{failCountByIteration[key]}";
            }

            if (globalFailCount > 0)
            {
                if (summary.Length > 0)
                {
                    summary += " | ";
                }

                summary += $"Global:{globalFailCount}";
            }

            return summary;
        }

        private static bool TryGetSoakIterationIndex(string key, out int iterationIndex)
        {
            iterationIndex = 0;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int markerIndex = key.IndexOf(".I", System.StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            int digitStart = markerIndex + 2;
            int digitEnd = digitStart;
            while (digitEnd < key.Length && char.IsDigit(key[digitEnd]))
            {
                digitEnd++;
            }

            if (digitEnd <= digitStart)
            {
                return false;
            }

            if (digitEnd >= key.Length || key[digitEnd] != '.')
            {
                return false;
            }

            string token = key.Substring(digitStart, digitEnd - digitStart);
            return int.TryParse(token, out iterationIndex);
        }

        private static string BuildSoakFailureActionSummary(IReadOnlyList<ChecklistReportEntry> entries, int maxItems)
        {
            if (entries == null || entries.Count <= 0)
            {
                return "none";
            }

            bool hasFailures = false;
            bool needsEnable = false;
            bool needsReadyRefs = false;
            bool needsSetupCheck = false;
            bool needsSaveCheck = false;
            bool needsLoadCheck = false;
            bool needsLoadStageCheck = false;
            bool needsLoadStateCheck = false;
            bool needsDeathResetCheck = false;
            bool needsNewRunCheck = false;
            bool needsMatrixCheck = false;
            bool needsSaveRestoreCheck = false;
            bool hasUnclassifiedFailure = false;

            for (int i = 0; i < entries.Count; i++)
            {
                ChecklistReportEntry entry = entries[i];
                if (entry.Passed)
                {
                    continue;
                }

                hasFailures = true;
                string key = entry.Key ?? string.Empty;
                if (key == "ReleaseSoak.Enabled")
                {
                    needsEnable = true;
                    continue;
                }

                if (key == "ReleaseSoak.Ready")
                {
                    needsReadyRefs = true;
                    continue;
                }

                if (key.Contains(".Setup"))
                {
                    needsSetupCheck = true;
                    continue;
                }

                if (key.Contains(".Save"))
                {
                    needsSaveCheck = true;
                    continue;
                }

                if (key.Contains(".LoadState"))
                {
                    needsLoadStateCheck = true;
                    continue;
                }

                if (key.Contains(".LoadStage"))
                {
                    needsLoadStageCheck = true;
                    continue;
                }

                if (key.Contains(".Load"))
                {
                    needsLoadCheck = true;
                    continue;
                }

                if (key.Contains(".DeathReset"))
                {
                    needsDeathResetCheck = true;
                    continue;
                }

                if (key.Contains(".NewRun"))
                {
                    needsNewRunCheck = true;
                    continue;
                }

                if (key.Contains(".MatrixGate"))
                {
                    needsMatrixCheck = true;
                    continue;
                }

                if (key == "ReleaseSoak.RestoreSaveState")
                {
                    needsSaveRestoreCheck = true;
                    continue;
                }

                hasUnclassifiedFailure = true;
            }

            if (!hasFailures)
            {
                return "none";
            }

            List<string> actions = new();
            int totalActionCount = 0;
            int actionLimit = Mathf.Max(1, maxItems);

            AddFailureAction(actions, ref totalActionCount, actionLimit, needsEnable, "Enable release soak pass on runner (`enableReleaseCandidateSoakPass`).");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsReadyRefs, "Ensure `MapSystem`, `SaveManager`, and `PlayerVitals` are present in active runtime hierarchy.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsSetupCheck, "Map setup failed: verify generated cells/walls/occluders for preset-stage pairs.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsSaveCheck, "Checkpoint save failed: inspect `SaveManager.SaveCheckpoint` flow and suppression state.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsLoadCheck, "Checkpoint load failed: inspect `TryLoadCheckpointToRuntime` and checkpoint validity.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsLoadStageCheck, "Loaded stage mismatch: verify stage restore path right after checkpoint load.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsLoadStateCheck, "Loaded state mismatch: verify stamina/flashlight/health restoration fields.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsDeathResetCheck, "Death reset failed: verify flashlight/cooldown/stamina/concealment reset pipeline.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsNewRunCheck, "New-run reset failed: verify checkpoint clear, stage reset, and run counter invariants.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsMatrixCheck, "Matrix gate failed inside soak: recalibrate matrix envelope or pressure/set-piece drift.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, needsSaveRestoreCheck, "Runtime save snapshot restore failed after soak; inspect save snapshot rollback path.");
            AddFailureAction(actions, ref totalActionCount, actionLimit, hasUnclassifiedFailure, "Unclassified soak failures exist; inspect per-entry keys in regression panel.");

            if (actions.Count <= 0)
            {
                return "none";
            }

            string summary = string.Join(" | ", actions);
            if (totalActionCount > actions.Count)
            {
                summary += $" (+{totalActionCount - actions.Count} more)";
            }

            return summary;
        }

        private static void AddFailureAction(List<string> actions, ref int totalActionCount, int actionLimit, bool condition, string action)
        {
            if (!condition)
            {
                return;
            }

            totalActionCount++;
            if (actions.Count < actionLimit)
            {
                actions.Add(action);
            }
        }

        private bool EvaluateReleaseChecklistReady(
            out bool freezePass,
            out bool finalLockPass,
            out bool checklistPass,
            out bool matrixPass,
            out bool chasePass,
            out bool soakPass)
        {
            freezePass = !releaseChecklistRequireFreezeApplied || releaseChecklistFreezeApplied;
            finalLockPass = !releaseChecklistRequireFinalLock || MatrixFinalLockReady;
            checklistPass = !releaseChecklistRequireChecklistPass || (hasRun && lastRunPassed);
            matrixPass = !releaseChecklistRequireMatrixPass || (lastMatrixRan && lastMatrixPassed);
            chasePass = !releaseChecklistRequireChasePass || (lastChaseReadabilityRan && lastChaseReadabilityPassed);
            soakPass = !releaseChecklistRequireSoakPass || (hasSoakRun && lastSoakPassed);

            return freezePass && finalLockPass && checklistPass && matrixPass && chasePass && soakPass;
        }

        private void ResolveReferences()
        {
            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (mapTuning == null)
            {
                mapTuning = FindFirstObjectByType<MapTuningDebugController>();
            }

            if (stageLoopDirector == null)
            {
                stageLoopDirector = StageLoopDirector.Instance;
                if (stageLoopDirector == null)
                {
                    stageLoopDirector = FindFirstObjectByType<StageLoopDirector>();
                }
            }

            if (saveManager == null)
            {
                saveManager = SaveManager.Instance;
                if (saveManager == null)
                {
                    saveManager = FindFirstObjectByType<SaveManager>();
                }
            }

            if (noiseManager == null)
            {
                noiseManager = NoiseManager.Instance;
                if (noiseManager == null)
                {
                    noiseManager = FindFirstObjectByType<NoiseManager>();
                }
            }

            if (pressureDirector == null)
            {
                pressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (readabilityDirector == null)
            {
                readabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }

            if (enemySpawnDirector == null)
            {
                enemySpawnDirector = FindFirstObjectByType<EnemySpawnDirector>();
            }

            if (runLoadoutDirector == null)
            {
                runLoadoutDirector = FindFirstObjectByType<RunLoadoutDirector>();
            }

            if (gameplayHud == null)
            {
                gameplayHud = FindFirstObjectByType<GameplayHudRuntime>();
            }

            if (eventFeedback == null)
            {
                eventFeedback = FindFirstObjectByType<EventFeedbackRuntime>();
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitalSystem>();
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

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerDummyController>();
            }

            if (concealmentState == null)
            {
                concealmentState = FindFirstObjectByType<PlayerConcealmentState>();
            }

            if (telemetry == null)
            {
                telemetry = FindFirstObjectByType<PlayerBehaviorTelemetry>();
            }
        }

        private IEnumerator WaitSettle()
        {
            if (settleDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(settleDelaySeconds);
                yield break;
            }

            yield return null;
        }
    }
}




















