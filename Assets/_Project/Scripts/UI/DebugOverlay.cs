using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Core;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using LostBreadcrumbs.Runtime.Systems;
using LostBreadcrumbs.Runtime.Player;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.UI
{
    public sealed class DebugOverlay : MonoBehaviour
    {
        private enum RegressionResultFilter
        {
            FailOnly,
            All,
            PassOnly
        }

        private enum RegressionEntrySource
        {
            Checklist,
            Soak
        }

        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode cycleEnemyKey = KeyCode.Tab;

        [Header("Regression Panel")]
        [SerializeField] private bool showRegressionChecklistPanel = true;
        [SerializeField] private KeyCode cycleRegressionFilterKey = KeyCode.F12;
        [SerializeField] private KeyCode cycleRegressionEntrySourceKey = KeyCode.BackQuote;
        [SerializeField] private RegressionResultFilter regressionResultFilter = RegressionResultFilter.FailOnly;
        [SerializeField] private RegressionEntrySource regressionEntrySource = RegressionEntrySource.Soak;
        [SerializeField, Min(0f)] private float regressionPanelX = 500f;
        [SerializeField, Min(0f)] private float regressionPanelY = 16f;
        [SerializeField, Min(200f)] private float regressionPanelWidth = 620f;
        [SerializeField, Min(160f)] private float regressionPanelHeight = 420f;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.8f;
        [SerializeField, Min(0.1f)] private float hookCacheRefreshInterval = 0.5f;

        [Header("Rhythm Validation")]
        [SerializeField] private bool showRhythmValidation = true;
        [SerializeField] private KeyCode resetRhythmValidationKey = KeyCode.F9;
        [SerializeField] private KeyCode writeRhythmSnapshotKey = KeyCode.F8;
        [SerializeField, Min(0.1f)] private float rhythmPhaseObservedSeconds = 0.75f;

        private int enemyIndex;
        private Vector2 mainScrollPosition;
        private Vector2 regressionScrollPosition;
        private readonly List<EnemyController> cachedEnemies = new(16);
        private readonly List<RoomArchetypeHookDummy> cachedHooks = new(16);
        private float nextReferenceResolveTime;
        private float nextHookCacheRefreshTime;
        private int lastReferenceResolveFrame = -1;

        private MapSystem mapSystem;
        private CameraFollow2D cameraFollow;
        private FogOfWarSystem fogOfWar;
        private MapTuningDebugController mapTuning;
        private RegressionChecklistRunner regressionChecklist;
        private AudioDummyLoopRuntime dummyLoop;
        private EnemySpawnDirector spawnDirector;
        private StageSetPieceDirector setPieceDirector;
        private StagePressureDirector pressureDirector;
        private GameplayRhythmDirector rhythmDirector;
        private ThreatReadabilityDirector readabilityDirector;
        private PlayerVitalSystem playerVitals;
        private PlayerVisibilitySource visibilitySource;
        private PlayerDummyController playerController;
        private RunLoadoutDirector runLoadout;
        private PlayerBehaviorTelemetry telemetry;
        private PlayerConcealmentState concealmentState;
        private PlayerEchoPulseAbility pulseAbility;
        private PlayerDecoyAbility decoyAbility;
        private PlayerSmokeAbility smokeAbility;
        private GameplayRhythmPhase lastObservedRhythmPhase = GameplayRhythmPhase.Calm;
        private float rhythmPhaseObservationElapsed;
        private bool rhythmCalmObserved;
        private bool rhythmBuildObserved;
        private bool rhythmSpikeObserved;
        private bool rhythmReleaseObserved;
        private string lastRhythmSnapshotPath = "-";

        private void OnEnable()
        {
            DebugManager.OverlayToggled += OnOverlayToggled;
            TryResolveReferences(force: true);
            RefreshHookCache(force: true);
        }

        private void OnDisable()
        {
            DebugManager.OverlayToggled -= OnOverlayToggled;
        }

        private void Update()
        {
            TryResolveReferences();
            ObserveRhythmValidation();

            if (RuntimeInputAdapter.GetKeyDown(cycleEnemyKey))
            {
                enemyIndex++;
            }

            if (RuntimeInputAdapter.GetKeyDown(cycleRegressionFilterKey))
            {
                CycleRegressionResultFilter();
            }

            if (RuntimeInputAdapter.GetKeyDown(cycleRegressionEntrySourceKey))
            {
                CycleRegressionEntrySource();
            }

            if (RuntimeInputAdapter.GetKeyDown(resetRhythmValidationKey))
            {
                ResetRhythmValidation();
            }

            if (showRhythmValidation && RuntimeInputAdapter.GetKeyDown(writeRhythmSnapshotKey))
            {
                WriteRhythmValidationSnapshot();
            }
        }

        private void OnOverlayToggled(bool enabled)
        {
            visible = enabled;
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            TryResolveReferences();
            RefreshHookCache();
            EnemyController.CopyActiveControllers(cachedEnemies);
            EnemyController target = null;
            if (cachedEnemies.Count > 0)
            {
                target = cachedEnemies[Mathf.Abs(enemyIndex) % cachedEnemies.Count];
            }

            Rect mainPanelRect = BuildClampedPanelRect(16f, 16f, 470f, 920f);
            GUILayout.BeginArea(mainPanelRect, GUI.skin.box);
            mainScrollPosition = GUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandHeight(true));
            GUILayout.Label("Lost Breadcrumbs Debug Overlay");
            GUILayout.Label($"Enemies: {cachedEnemies.Count} (TAB to cycle)");

            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop != null)
            {
                GUILayout.Label($"Stage: {stageLoop.CurrentStage}");
                GUILayout.Label($"Breadcrumbs: {stageLoop.CollectedBreadcrumbs}/{stageLoop.RequiredBreadcrumbs}");
                GUILayout.Label($"Exit: {(stageLoop.ExitUnlocked ? "Unlocked" : "Locked")}");
                GUILayout.Label($"Safe Havens: {stageLoop.ActiveSafeHavenCount}");
                GUILayout.Label($"Stamina Pickups: {stageLoop.ActiveStaminaPickupCount}");
            }

            if (mapSystem != null)
            {
                var mapCells = mapSystem.LastGeneratedCells;
                GUILayout.Label($"Map Cells: {mapCells.Count} @ CellSize {mapSystem.CellSize:0.00}");
                GUILayout.Label($"Map Variant: {mapSystem.CurrentStageVariantSalt}");
                GUILayout.Label($"Map Walls: {mapSystem.LastWallSegmentCount}");
                GUILayout.Label($"Map Occluders: {mapSystem.LastOccluderCount}");
                GUILayout.Label($"Map Choke Occluders: {mapSystem.LastChokeOccluderCount}");
                GUILayout.Label($"Map Hooks: {mapSystem.LastArchetypeHookCount}");
                GUILayout.Label($"Map Hook Tuning: {mapSystem.LastHookPresetLabel} P{mapSystem.LastHookStagePressure01:0.00}");
                GUILayout.Label($"Map Hook Mult C/L/R/CD: {mapSystem.LastHookChanceMultiplier:0.00}/{mapSystem.LastHookLoudnessMultiplier:0.00}/{mapSystem.LastHookRadiusMultiplier:0.00}/{mapSystem.LastHookCooldownMultiplier:0.00}");
                if (cachedHooks.Count > 0)
                {
                    int triggeredHookCount = 0;
                    int playerInsideHookCount = 0;
                    int warningHookCount = 0;
                    float totalHookStageReadability = 0f;
                    float totalHookLeadTime = 0f;
                    float totalHookPulseSpeed = 0f;
                    int sampledHookCount = 0;

                    for (int i = 0; i < cachedHooks.Count; i++)
                    {
                        RoomArchetypeHookDummy hook = cachedHooks[i];
                        if (hook == null)
                        {
                            continue;
                        }

                        if (hook.TriggerCount > 0)
                        {
                            triggeredHookCount++;
                        }

                        if (hook.IsPlayerInside)
                        {
                            playerInsideHookCount++;
                        }

                        if (hook.IsPreEmitWarning)
                        {
                            warningHookCount++;
                        }

                        totalHookStageReadability += hook.StageReadability01;
                        totalHookLeadTime += hook.EffectiveTelegraphLeadTime;
                        totalHookPulseSpeed += hook.EffectiveWarningPulseSpeed;
                        sampledHookCount++;
                    }

                    GUILayout.Label($"Hooks Inside/Triggered/Warning: {playerInsideHookCount}/{triggeredHookCount}/{warningHookCount}");
                    if (sampledHookCount > 0)
                    {
                        float inv = 1f / sampledHookCount;
                        GUILayout.Label($"Hooks StageRead/Lead/Pulse(avg): {totalHookStageReadability * inv:0.00}/{totalHookLeadTime * inv:0.00}s/{totalHookPulseSpeed * inv:0.00}");
                    }
                }

                if (mapCells.Count > 0)
                {
                    Vector2Int min = mapCells[0].position;
                    Vector2Int max = mapCells[0].position;

                    int corridorCount = 0;
                    int roomCount = 0;
                    int forkCount = 0;
                    int hideoutCount = 0;
                    int riskCount = 0;

                    for (int i = 0; i < mapCells.Count; i++)
                    {
                        GeneratedMapCell cell = mapCells[i];
                        Vector2Int cellPos = cell.position;
                        min = Vector2Int.Min(min, cellPos);
                        max = Vector2Int.Max(max, cellPos);

                        switch (cell.kind)
                        {
                            case MapCellKind.Corridor:
                                corridorCount++;
                                break;
                            case MapCellKind.Room:
                                roomCount++;
                                break;
                            case MapCellKind.Fork:
                                forkCount++;
                                break;
                            case MapCellKind.Hideout:
                                hideoutCount++;
                                break;
                            case MapCellKind.Risk:
                                riskCount++;
                                break;
                        }
                    }

                    Vector2 spanCells = new(max.x - min.x + 1, max.y - min.y + 1);
                    Vector2 spanWorld = spanCells * mapSystem.CellSize;
                    GUILayout.Label($"Map Bounds(Cell): {min} -> {max} (Span {spanCells.x:0}x{spanCells.y:0})");
                    GUILayout.Label($"Map Bounds(World): {spanWorld.x:0.0} x {spanWorld.y:0.0}");
                    GUILayout.Label($"Map Center(World): {mapSystem.LastGeneratedWorldCenter}");
                    GUILayout.Label($"Map World Size(Runtime): {mapSystem.LastGeneratedWorldSize.x:0.0} x {mapSystem.LastGeneratedWorldSize.y:0.0}");
                    GUILayout.Label($"Map Kind C/R/F/H/Risk: {corridorCount}/{roomCount}/{forkCount}/{hideoutCount}/{riskCount}");
                }
            }


            if (cameraFollow != null)
            {
                Camera followCamera = cameraFollow.GetComponent<Camera>();
                if (followCamera != null && followCamera.orthographic)
                {
                    GUILayout.Label($"Camera OrthographicSize: {followCamera.orthographicSize:0.00}");
                    Color bg = followCamera.backgroundColor;
                    GUILayout.Label($"Camera BG RGB: {bg.r:0.00}/{bg.g:0.00}/{bg.b:0.00}");
                }

                GUILayout.Label($"Camera Bounds Clamp: {(cameraFollow.HasBounds ? "On" : "Off")}");
                if (cameraFollow.HasBounds)
                {
                    GUILayout.Label($"Camera Bounds Size: {cameraFollow.BoundsSize.x:0.0} x {cameraFollow.BoundsSize.y:0.0}");
                    GUILayout.Label($"Camera Bounds Padding: {cameraFollow.BoundsPadding:0.00}");
                }

                GUILayout.Label($"Camera LookAhead: {cameraFollow.CurrentLookAheadMagnitude:0.00}");
                GUILayout.Label($"Camera Runtime Mul LA/Sm/LAS: {cameraFollow.RuntimeLookAheadMultiplier:0.00}/{cameraFollow.RuntimeSmoothMultiplier:0.00}/{cameraFollow.RuntimeLookAheadSmoothingMultiplier:0.00}");
            }

            if (fogOfWar != null)
            {
                GUILayout.Label($"Fog World Size: {fogOfWar.WorldSize.x:0.0} x {fogOfWar.WorldSize.y:0.0}");
                GUILayout.Label($"Fog Reveal Radius: {fogOfWar.EffectiveRevealRadius:0.00}");
                GUILayout.Label($"Fog Reveal Softness: {fogOfWar.EffectiveRevealSoftness:0.00}");
                GUILayout.Label($"Fog Texture: {fogOfWar.TextureResolutionLabel}");
                GUILayout.Label($"Fog Runtime Mul R/S/F/Refog: {fogOfWar.RuntimeRevealRadiusMultiplier:0.00}/{fogOfWar.RuntimeRevealSoftnessMultiplier:0.00}/{fogOfWar.RuntimeFlashlightExtraRangeMultiplier:0.00}/{fogOfWar.RuntimeRefogMultiplier:0.00}");
                GUILayout.Label($"Fog Style RGB/H/V: {fogOfWar.RuntimeFogTint.r:0.00}/{fogOfWar.RuntimeFogTint.g:0.00}/{fogOfWar.RuntimeFogTint.b:0.00} | {fogOfWar.RuntimeHiddenAlphaMultiplier:0.00}/{fogOfWar.RuntimeVisibleAlphaMultiplier:0.00}");
                GUILayout.Label($"Fog Effective A Hidden/Visible: {fogOfWar.EffectiveHiddenAlpha:0.00}/{fogOfWar.EffectiveVisibleAlpha:0.00}");
                GUILayout.Label($"Fog Effective Refog: {fogOfWar.EffectiveRefogPerSecond:0.00}/s");
            }

            if (mapTuning != null)
            {
                GUILayout.Label($"Map Preset: {mapTuning.ActivePresetLabel} ({mapTuning.CyclePresetKey}/{mapTuning.RegenerateStageKey})");
                GUILayout.Label($"Map Config Source: {mapTuning.ActiveConfigName} (RuntimeClone {(mapTuning.UsingRuntimeClone ? "Yes" : "No")})");
            }

            if (regressionChecklist != null)
            {
                string regressionState = regressionChecklist.IsRunning
                    ? "Running"
                    : regressionChecklist.HasRun
                        ? (regressionChecklist.LastRunPassed ? "PASS" : "FAIL")
                        : "NotRun";

                GUILayout.Label($"Regression Checklist: {regressionState} ({regressionChecklist.RunChecklistKey})");
                GUILayout.Label($"Regression Result: {regressionChecklist.LastRunSummary}");
                GUILayout.Label($"Regression Matrix: {(regressionChecklist.LastMatrixRan ? (regressionChecklist.LastMatrixPassed ? "PASS" : "FAIL") : "NotRun")}");
                GUILayout.Label($"Regression Matrix Detail: {regressionChecklist.LastMatrixSummary}");
                GUILayout.Label($"Regression Matrix Samples: {regressionChecklist.LastMatrixPassCount}/{regressionChecklist.LastMatrixSampleCount}");
                GUILayout.Label($"Regression Matrix Baseline: {(regressionChecklist.HasMatrixBaseline ? "Locked" : "None")} ({regressionChecklist.MatrixBaselineSummary})");
                GUILayout.Label($"Regression Matrix Baseline Policy: {regressionChecklist.MatrixBaselinePolicySummary}");
                GUILayout.Label($"Regression Matrix Final Lock: {regressionChecklist.MatrixFinalLockSummary}");
                GUILayout.Label($"Release Soak: {(regressionChecklist.IsSoakRunning ? "Running" : regressionChecklist.HasSoakRun ? (regressionChecklist.LastSoakPassed ? "PASS" : "FAIL") : "NotRun")} ({regressionChecklist.RunReleaseSoakKey})");
                GUILayout.Label($"Release Soak Detail: {regressionChecklist.LastSoakSummary}");
                GUILayout.Label($"Release Soak Failures: {regressionChecklist.LastSoakFailureDigest}");
                GUILayout.Label($"Release Soak Iteration Failures: {regressionChecklist.LastSoakIterationFailureSummary}");
                GUILayout.Label($"Release Soak Actions: {regressionChecklist.LastSoakFailureActionSummary}");
                GUILayout.Label($"Release Checklist Gate: {regressionChecklist.ReleaseChecklistSummary}");
                GUILayout.Label($"Chase Readability Regression: {(regressionChecklist.LastChaseReadabilityRan ? (regressionChecklist.LastChaseReadabilityPassed ? "PASS" : "FAIL") : "NotRun")}");
                GUILayout.Label($"Chase Readability Detail: {regressionChecklist.LastChaseReadabilitySummary}");
                GUILayout.Label($"Chase Readability Samples: {regressionChecklist.LastChaseReadabilityPassCount}/{regressionChecklist.LastChaseReadabilitySampleCount}");
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                GUILayout.Label($"Checkpoint: {(saveManager.HasCheckpoint ? "Yes" : "No")} @Stage {saveManager.CheckpointStage}");
                GUILayout.Label($"Meta Runs/Best: {saveManager.TotalRuns}/{saveManager.HighestStageReached}");
                GUILayout.Label($"Meta Deaths/Breadcrumbs: {saveManager.TotalDeaths}/{saveManager.TotalBreadcrumbs}");
                GUILayout.Label($"Saved Loadout: {saveManager.SelectedLoadoutId} (Unlocked {saveManager.UnlockedLoadoutCount})");
                GUILayout.Label("Save Hotkeys: F5 Save / F9 Load / F10 NewRun");
            }

            EventManager eventManager = EventManager.Instance;
            if (eventManager != null && eventManager.TryGetLatestEvent(out RuntimeEventRecord latestEvent))
            {
                GUILayout.Label($"Last Event: [{latestEvent.TypeLabel}] {latestEvent.Message}");
                GUILayout.Label($"Recent Event Count: {eventManager.RecentEvents.Count}");
            }
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                GUILayout.Label($"Event Audio Muted: {(audioManager.Muted ? "Yes" : "No")}");
                GUILayout.Label($"Event Audio Master Volume: {audioManager.MasterVolume:0.00}");
                GUILayout.Label($"Event Audio Mode: {(audioManager.PreferAssignedClips ? "AssignedClipFirst" : "ToneOnly")}");
                GUILayout.Label($"Event Audio Assigned Clips: {audioManager.AssignedClipCount}");
                GUILayout.Label($"Event Audio Assigned Stingers: {audioManager.AssignedStingerClipCount}");
                GUILayout.Label($"Event Audio Burst: {audioManager.RecentBurstCount} ({audioManager.BurstLevelNormalized:0.00})");
                GUILayout.Label($"Event Audio Last: {audioManager.LastPlayedEventType} via {audioManager.LastPlaySource}");
                if (audioManager.HasRuntimeStingerTelemetry)
                {
                    GUILayout.Label($"Event Audio Stinger Last: {audioManager.LastRuntimeStingerLabel} via {audioManager.LastRuntimeStingerSource} age {audioManager.LastRuntimeStingerAge:0.0}s");
                    GUILayout.Label($"Event Audio Stinger Mix: vol {audioManager.LastRuntimeStingerVolume:0.00} pitch {audioManager.LastRuntimeStingerPitch:0.00} suppressed {audioManager.SuppressedRuntimeStingerCount}");
                }
                else
                {
                    GUILayout.Label($"Event Audio Stinger Last: none suppressed {audioManager.SuppressedRuntimeStingerCount}");
                }
                GUILayout.Label($"Event Audio Stinger StageIntensity: {audioManager.LastStingerStageIntensity:0.00}");
                GUILayout.Label($"Event Audio Duck: {audioManager.CombatDuckCurrent:0.00} -> {audioManager.CombatDuckTarget:0.00} (eff {audioManager.EffectiveDuck:0.00})");
                GUILayout.Label($"Event Audio Ducking Enabled: {(audioManager.RuntimeDuckingEnabled ? "Yes" : "No")}");
                GUILayout.Label("Audio Hotkey: F4 Mute Toggle");
            }
            if (dummyLoop != null)
            {
                GUILayout.Label($"Dummy Loop BGM: {(dummyLoop.IsBgmPlaying ? "Playing" : "Stopped")} ({(dummyLoop.BgmUsingGeneratedClip ? "Generated" : "Assigned")})");
                GUILayout.Label($"Dummy Loop Ambience: {(dummyLoop.IsAmbiencePlaying ? "Playing" : "Stopped")} ({(dummyLoop.AmbienceUsingGeneratedClip ? "Generated" : "Assigned")})");
                GUILayout.Label($"Dummy Loop Forced Off: {(dummyLoop.ForceDisableDummyLoops ? "Yes" : "No")}");
                GUILayout.Label($"Dummy Loop Rhythm Tempo: {dummyLoop.CurrentRhythmTempo:0.00}");
            }
            if (spawnDirector != null)
            {
                GUILayout.Label($"Active Enemies: {spawnDirector.ActiveEnemyCount}");
                GUILayout.Label($"Enemy Target/Seeker: {spawnDirector.LastSpawnTargetEnemyCount}/{spawnDirector.LastSeekerSpawnCount} @Stage {spawnDirector.LastSpawnStage}");
                GUILayout.Label($"Enemy Pressure Mul C/R/S/N: {spawnDirector.RuntimeEnemyCountMultiplier:0.00}/{spawnDirector.RuntimeRiskWeightMultiplier:0.00}/{spawnDirector.RuntimeSeekerExtraChance:0.00}/{spawnDirector.RuntimeStartDistanceReduction:0.00}");
            }
            if (setPieceDirector != null)
            {
                GUILayout.Label($"SetPiece Tier/Beat: {setPieceDirector.ActiveTier}/{setPieceDirector.LastBeatLabel}");
                GUILayout.Label($"SetPiece Beacons/Reinforce: {setPieceDirector.ActiveBeaconCount}/{setPieceDirector.LastReinforcementCount} @Stage {setPieceDirector.LastAppliedStage}");
                GUILayout.Label($"SetPiece Tune P/T/I: {setPieceDirector.LastRuntimePressure01:0.00}/{setPieceDirector.LastRuntimeTension01:0.00}/{setPieceDirector.LastRuntimeIntensity:0.00} ({setPieceDirector.LastRuntimePresetLabel})");
                GUILayout.Label($"SetPiece Pulse Int/L/R/Life: {setPieceDirector.LastRuntimePulseInterval:0.00}/{setPieceDirector.LastRuntimePulseLoudness:0.00}/{setPieceDirector.LastRuntimePulseRadius:0.00}/{setPieceDirector.LastRuntimeBeaconLifetime:0.00}s");
            }


            if (pressureDirector != null)
            {
                GUILayout.Label($"Stage Pressure Total: {pressureDirector.CurrentPressure01:0.00}");
                GUILayout.Label($"Stage Pressure(Stage/Behavior/Late): {pressureDirector.CurrentStagePressure01:0.00}/{pressureDirector.CurrentBehaviorPressure01:0.00}/{pressureDirector.CurrentLateStageBonus01:0.00}");
                GUILayout.Label($"Cooldown Economy P/D/S: {pressureDirector.AppliedPulseCooldownMultiplier:0.00}/{pressureDirector.AppliedDecoyCooldownMultiplier:0.00}/{pressureDirector.AppliedSmokeCooldownMultiplier:0.00}");
            }

            if (rhythmDirector != null)
            {
                GUILayout.Label($"Rhythm: {rhythmDirector.CurrentPhaseLabel} {rhythmDirector.CurrentPhaseProgress:0.00} ({rhythmDirector.CurrentPhaseElapsed:0.0}/{rhythmDirector.CurrentPhaseDuration:0.0}s) cycle {rhythmDirector.CycleCount}");
                GUILayout.Label($"Rhythm Tempo/Intensity/Pressure: {rhythmDirector.CurrentTempo01:0.00}/{rhythmDirector.CurrentRhythmIntensity:0.00}/{rhythmDirector.CurrentPressureMultiplier:0.00}");
                DrawRhythmValidation();
            }

            if (readabilityDirector != null)
            {
                GUILayout.Label($"Readability Pressure N/S/F: {readabilityDirector.CurrentNearbyThreat:0.00}/{readabilityDirector.CurrentStagePressure:0.00}/{readabilityDirector.CurrentReadabilityPressure:0.00}");
                GUILayout.Label($"Readability Preset/Enemies: {readabilityDirector.LastPresetLabel}/{readabilityDirector.LastEnemySampleCount}");
                GUILayout.Label($"Readability Art/PulseCD: {(readabilityDirector.RuntimeArtGradeEnabled ? "On" : "Off")}/{readabilityDirector.ThreatPulseCooldownRemaining:0.00}s");
                GUILayout.Label($"Readability Tunnel/Close: {readabilityDirector.CurrentThreatTunnelVision:0.00}/{readabilityDirector.CurrentCloseThreatDistance:0.0}m -> cam {readabilityDirector.CurrentCameraTargetOrthoSize:0.00}");
                GUILayout.Label($"Readability BreathSnap: strain={readabilityDirector.CurrentQuietBreathStrain:0.00} cd={readabilityDirector.BreathSnapCooldownRemaining:0.00}s");
                if (readabilityDirector.HasBaseCameraOrthoSize)
                {
                    GUILayout.Label($"Readability Camera BaseSize: {readabilityDirector.BaseCameraOrthoSize:0.00}");
                }
            }

            if (playerVitals != null)
            {
                GUILayout.Label($"HP: {playerVitals.CurrentHealth}/{playerVitals.MaxHealth}");
                GUILayout.Label($"Deaths: {playerVitals.DeathCount}");
                GUILayout.Label($"Invulnerable: {(playerVitals.IsInvulnerable ? "Yes" : "No")}");
                GUILayout.Label($"SafeHaven Heal CD: {playerVitals.SafeHavenHealCooldownRemaining:0.00}s");
            }

            if (visibilitySource != null)
            {
                GUILayout.Label($"Flashlight: {(visibilitySource.FlashlightEnabled ? "On" : "Off")}");
            }

            if (playerController != null)
            {
                GUILayout.Label($"Sprint: {(playerController.IsSprinting ? "On" : "Off")}");
                GUILayout.Label($"Exhausted: {(playerController.IsExhausted ? "Yes" : "No")}");
                GUILayout.Label($"Stamina: {playerController.CurrentStamina:0.00}/{playerController.MaxStamina:0.00}");
                GUILayout.Label($"Move Speed: {playerController.CurrentMoveSpeed:0.00}");
                GUILayout.Label($"Quiet Breath: {playerController.TemporaryNoiseDampeningRemaining:0.00}s {(playerController.IsTemporaryNoiseDampeningStrained ? $"strain x{playerController.TemporaryNoiseSprintDecayMultiplier:0.00}" : "calm")}");
                GUILayout.Label($"Noise Foot/Sprint: {playerController.EffectiveFootstepNoiseMultiplier:0.00}/{playerController.EffectiveSprintNoiseMultiplier:0.00}");
                GUILayout.Label($"Echo Scan: total={playerController.LastEchoObjectiveScanCount}, choice={playerController.LastEchoObjectiveChoiceScanCount}, primary={(playerController.LastEchoObjectivePrimaryWasExit ? "Exit" : "Breadcrumb")}, status={playerController.EchoObjectiveScanStatusRemaining:0.00}s");
                GUILayout.Label($"Position Safety: {playerController.UnsafePositionRecoveryCount} recoveries, watch {playerController.UnsafePositionRecoveryWindowRemaining:0.00}s");
            }

            if (runLoadout != null)
            {
                GUILayout.Label($"Loadout: {runLoadout.SelectedLoadout}");
                GUILayout.Label($"Loadout Locked: {(runLoadout.SelectionLocked ? "Yes" : "No")}");
                GUILayout.Label($"Loadout Summary: {runLoadout.CurrentLoadoutSummary}");
                GUILayout.Label($"Loadout Catalog: {(runLoadout.HasCatalog ? $"{runLoadout.CatalogUnlockedDefaultCount}/{runLoadout.CatalogLoadoutCount} default-unlocked" : "Fallback")}");
                GUILayout.Label($"Loadout Unlocked(Default): {(runLoadout.SelectedLoadoutUnlockedByDefault ? "Yes" : "No")}");
                GUILayout.Label($"Loadout Pressure CD P/D/S: {runLoadout.PressurePulseCooldownMultiplier:0.00}/{runLoadout.PressureDecoyCooldownMultiplier:0.00}/{runLoadout.PressureSmokeCooldownMultiplier:0.00}");
            }

            if (telemetry != null)
            {
                LearningSnapshot snapshot = telemetry.GetSnapshot();
                GUILayout.Label($"Learn Phase: {snapshot.Phase}");
                GUILayout.Label($"Learn Score: {snapshot.BehaviorScore:0.00}");
                GUILayout.Label($"Learn W/P: {snapshot.LearningWeight:0.00}/{snapshot.PredictionWeight:0.00}");
                GUILayout.Label($"Telemetry SprintSec: {telemetry.SprintSeconds:0.0}");
                GUILayout.Label($"Telemetry Pulse/Decoy/Smoke: {telemetry.PulseCastCount}/{telemetry.DecoyDeployCount}/{telemetry.SmokeDeployCount}");
                GUILayout.Label($"Telemetry Deaths/Stage+: {telemetry.DeathCount}/{telemetry.StageAdvanceCount}");
            }

            if (concealmentState != null)
            {
                GUILayout.Label($"SafeHaven Inside: {(concealmentState.IsInsideSafeHaven ? "Yes" : "No")}");
                GUILayout.Label($"Concealed: {(concealmentState.IsConcealedFromEnemies ? "Yes" : "No")}");
                GUILayout.Label($"Noise Multiplier: {concealmentState.CurrentNoiseMultiplier:0.00}");

                float smokeNoiseScale = SmokeScreenFieldDummy.EvaluateNoiseMultiplierAt(concealmentState.transform.position);
                float smokeNoiseDampen = SmokeScreenFieldDummy.EvaluateNoiseDampenAt(concealmentState.transform.position);
                GUILayout.Label($"Smoke Noise Scale: {smokeNoiseScale:0.00}");
                GUILayout.Label($"Smoke Noise Dampen: {smokeNoiseDampen:0.00}");
            }

            if (pulseAbility != null)
            {
                GUILayout.Label($"Pulse Ready: {(pulseAbility.IsReady ? "Yes" : "No")}");
                GUILayout.Label($"Pulse Cooldown: {pulseAbility.CooldownRemaining:0.00}s");
                GUILayout.Label($"Pulse Resonance: {(pulseAbility.IsEchoResonating ? "On" : "Off")} {pulseAbility.EchoResonanceRemaining:0.00}s");
                GUILayout.Label($"Pulse Last Stun Count: {pulseAbility.LastStunnedCount}");
                GUILayout.Label($"Pulse Last Return: {pulseAbility.LastEchoReturnThreatCount} / {pulseAbility.LastEchoReturnDistance:0.00}m warn={pulseAbility.EchoReturnWarningRemaining:0.00}s");
                GUILayout.Label($"Pulse Last Noise Scale: {pulseAbility.LastNoiseScale:0.00}");
            }

            if (decoyAbility != null)
            {
                GUILayout.Label($"Decoy Ready: {(decoyAbility.IsReady ? "Yes" : "No")}");
                GUILayout.Label($"Decoy Cooldown: {decoyAbility.CooldownRemaining:0.00}s");
                GUILayout.Label($"Decoy Active: {decoyAbility.ActiveDecoyCount}");
            }

            if (smokeAbility != null)
            {
                GUILayout.Label($"Smoke Ready: {(smokeAbility.IsReady ? "Yes" : "No")}");
                GUILayout.Label($"Smoke Cooldown: {smokeAbility.CooldownRemaining:0.00}s");
                GUILayout.Label($"Smoke Active: {smokeAbility.ActiveSmokeCount}");
            }

            if (target == null)
            {
                GUILayout.Label("No enemy controllers in scene.");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                DrawRegressionChecklistPanel(mainPanelRect);
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label($"Target: {target.name}");
            GUILayout.Label($"State: {target.CurrentState}");
            GUILayout.Label($"Cause: {target.LastDetectionReason}");
            GUILayout.Label($"Suspicion: {target.Suspicion:0.00}");
            GUILayout.Label($"Chase Transition: {(target.IsChaseTransitionPending ? "Pending" : "Idle")} ({target.ChaseTransitionProgress:0.00})");
            GUILayout.Label($"Chase Effective T/P/F: {target.EffectiveChaseTransitionSeconds:0.00}s/{target.EffectiveTransitionPulseSpeed:0.00}/{target.EffectiveTransitionFlashStrength:0.00}");
            GUILayout.Label($"Chase Disengage Cue/Grace/Reacquire: {target.DisengageCueRemaining:0.00}s/{target.EffectiveDisengageDistanceGraceSeconds:0.00}s/{target.ChaseReacquireBlockedRemaining:0.00}s");
            GUILayout.Label($"Chase Blink Speed: {target.EffectiveChaseBlinkSpeed:0.00}");
            GUILayout.Label($"Player Concealed: {(target.IsTargetConcealed ? "Yes" : "No")}");
            GUILayout.Label($"Concealment Pierce: {target.ConcealmentPierce:0.00}");
            GUILayout.Label($"Decoy Response: {target.DecoyResponse:0.00}");
            GUILayout.Label($"Item Noise Response: {target.ItemNoiseResponse:0.00}");
            GUILayout.Label($"Noise Transmission: {target.LastNoiseTransmission:0.00} (Walls {target.LastNoiseWallHits})");
            GUILayout.Label($"Noise Kind: {target.LastNoiseKind}");
            GUILayout.Label($"Enemy Learn Phase: {target.LearningPhase}");
            GUILayout.Label($"Enemy Learn W/P: {target.LearningWeight:0.00}/{target.PredictionWeight:0.00}");
            GUILayout.Label($"Enemy Learn Score: {target.LearningBehaviorScore:0.00}");
            GUILayout.Label($"Smoke Occlusion: {target.SmokeOcclusion:0.00}");
            GUILayout.Label($"Smoke Raw Occlusion: {target.SmokeRawOcclusion:0.00}");
            GUILayout.Label($"Smoke Penetration: {target.SmokePenetration:0.00}");
            GUILayout.Label($"Stunned: {(target.IsStunned ? "Yes" : "No")}");
            GUILayout.Label($"Stun Remaining: {target.StunRemaining:0.00}s");
            GUILayout.Label($"Stun Count: {target.StunCount}");
            GUILayout.Label($"Target Point: {(target.HasCurrentTarget ? target.CurrentTargetPoint.ToString("F2") : "none")}");
            GUILayout.Label($"Move Recovery: {target.MovementRecoveryCount} total / {target.MovementOverlapRecoveryCount} overlap ({target.LastMovementRecoveryReason})");
            GUILayout.Label($"Move Stuck: {target.MovementStuckElapsed:0.00}s R={(target.HasMovementRecoveryWaypoint ? "Y" : "N")} S={(target.HasMovementSteeringWaypoint ? "Y" : "N")}");
            GUILayout.Label($"Predicted Escape: {target.LastPredictedEscape}");
            GUILayout.Label($"Hotspots: {target.DebugMemorySummary}");

            if (NoiseManager.Instance != null)
            {
                int noiseCount = NoiseManager.Instance.RecentRecords.Count;
                GUILayout.Label($"Recent Noise Records: {noiseCount}");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawRegressionChecklistPanel(mainPanelRect);
        }

        private void DrawRegressionChecklistPanel(Rect mainPanelRect)
        {
            if (!showRegressionChecklistPanel)
            {
                return;
            }

            Rect panelRect = BuildClampedPanelRect(
                regressionPanelX,
                regressionPanelY,
                regressionPanelWidth,
                regressionPanelHeight);
            if (panelRect.Overlaps(mainPanelRect))
            {
                return;
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Regression Checklist Panel");

            TryResolveReferences();
            RegressionChecklistRunner runner = regressionChecklist;
            if (runner == null)
            {
                GUILayout.Label("RegressionChecklistRunner not found.");
                GUILayout.EndArea();
                return;
            }

            string state = runner.IsRunning
                ? "Running"
                : runner.HasRun
                    ? (runner.LastRunPassed ? "PASS" : "FAIL")
                    : "NotRun";

            GUILayout.Label($"State: {state} | Runs: {runner.RunCount}");
            GUILayout.Label($"Summary: {runner.LastRunSummary}");
            GUILayout.Label($"Pass/Fail: {runner.LastRunPassedCount}/{runner.LastRunFailedCount}");
            GUILayout.Label($"Matrix: {(runner.LastMatrixRan ? (runner.LastMatrixPassed ? "PASS" : "FAIL") : "NotRun")} | {runner.LastMatrixPassCount}/{runner.LastMatrixSampleCount}");
            GUILayout.Label($"Soak: {(runner.IsSoakRunning ? "Running" : runner.HasSoakRun ? (runner.LastSoakPassed ? "PASS" : "FAIL") : "NotRun")} | {runner.LastSoakPassedCount}/{(runner.LastSoakPassedCount + runner.LastSoakFailedCount)}");
            GUILayout.Label($"Soak Summary: {runner.LastSoakSummary}");
            GUILayout.Label($"Soak Failures: {runner.LastSoakFailureDigest}");
            GUILayout.Label($"Soak Iterations: {runner.LastSoakIterationFailureSummary}");
            GUILayout.Label($"Soak Actions: {runner.LastSoakFailureActionSummary}");
            GUILayout.Label($"Soak Report File: {runner.LastSoakDetailedReportFilePath}");
            GUILayout.Label($"Release Gate: {runner.ReleaseChecklistSummary}");
            GUILayout.Label($"Entry Source: {GetRegressionEntrySourceLabel()} ({cycleRegressionEntrySourceKey})");
            GUILayout.Label($"ChaseRead: {(runner.LastChaseReadabilityRan ? (runner.LastChaseReadabilityPassed ? "PASS" : "FAIL") : "NotRun")} | {runner.LastChaseReadabilityPassCount}/{runner.LastChaseReadabilitySampleCount}");
            GUILayout.Label($"Event Suppression: {(RuntimeEventBus.IsPublishingSuppressed ? "On" : "Off")} (Depth {RuntimeEventBus.SuppressionDepth})");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Run ({runner.RunChecklistKey})"))
            {
                runner.RunChecklistNow();
            }

            if (GUILayout.Button($"Soak ({runner.RunReleaseSoakKey})"))
            {
                runner.RunReleaseCandidateSoakPassNow();
            }

            if (GUILayout.Button($"Filter: {GetRegressionFilterLabel()} ({cycleRegressionFilterKey})"))
            {
                CycleRegressionResultFilter();
            }

            if (GUILayout.Button($"Entries: {GetRegressionEntrySourceLabel()} ({cycleRegressionEntrySourceKey})"))
            {
                CycleRegressionEntrySource();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Freeze Defaults"))
            {
                runner.ApplyReleaseChecklistFreezeDefaultsForEditor();
            }

            if (GUILayout.Button("Gate Log"))
            {
                runner.LogReleaseChecklistGateForEditor();
            }

            if (GUILayout.Button("Soak Fail Log"))
            {
                runner.LogReleaseSoakFailuresForEditor();
            }

            if (GUILayout.Button("Soak Action Log"))
            {
                runner.LogReleaseSoakActionPlanForEditor();
            }

            if (GUILayout.Button("Soak Report Log"))
            {
                runner.LogReleaseSoakDetailedReportForEditor();
            }

            if (GUILayout.Button("Soak Report File"))
            {
                runner.WriteReleaseSoakDetailedReportFileForEditor();
            }
            GUILayout.EndHorizontal();

            var entries = regressionEntrySource == RegressionEntrySource.Soak
                ? runner.LastSoakResults
                : runner.LastRunResults;
            int shownCount = 0;

            regressionScrollPosition = GUILayout.BeginScrollView(regressionScrollPosition, GUILayout.ExpandHeight(true));
            if (entries == null || entries.Count <= 0)
            {
                GUILayout.Label(regressionEntrySource == RegressionEntrySource.Soak
                    ? "No soak results yet."
                    : "No checklist results yet.");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    RegressionChecklistRunner.ChecklistReportEntry entry = entries[i];
                    if (!ShouldShowRegressionEntry(entry.Passed))
                    {
                        continue;
                    }

                    shownCount++;
                    GUILayout.Label($"[{(entry.Passed ? "PASS" : "FAIL")}] {entry.Key}");
                    GUILayout.Label($"  - {entry.Detail}");
                }

                if (shownCount <= 0)
                {
                    GUILayout.Label($"No entries for filter: {GetRegressionFilterLabel()}");
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static Rect BuildClampedPanelRect(float x, float y, float width, float height)
        {
            float padding = 8f;
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            float maxWidth = Mathf.Max(1f, screenWidth - padding * 2f);
            float maxHeight = Mathf.Max(1f, screenHeight - padding * 2f);
            float clampedWidth = Mathf.Min(Mathf.Max(1f, width), maxWidth);
            float clampedHeight = Mathf.Min(Mathf.Max(1f, height), maxHeight);
            float clampedX = Mathf.Clamp(x, padding, Mathf.Max(padding, screenWidth - clampedWidth - padding));
            float clampedY = Mathf.Clamp(y, padding, Mathf.Max(padding, screenHeight - clampedHeight - padding));
            return new Rect(clampedX, clampedY, clampedWidth, clampedHeight);
        }

        private void ObserveRhythmValidation()
        {
            if (!Application.isPlaying || !showRhythmValidation || rhythmDirector == null)
            {
                return;
            }

            GameplayRhythmPhase phase = rhythmDirector.CurrentPhase;
            if (phase != lastObservedRhythmPhase)
            {
                lastObservedRhythmPhase = phase;
                rhythmPhaseObservationElapsed = 0f;
            }

            rhythmPhaseObservationElapsed += Time.unscaledDeltaTime;
            if (rhythmPhaseObservationElapsed < rhythmPhaseObservedSeconds)
            {
                return;
            }

            MarkRhythmPhaseObserved(phase);
        }

        private void DrawRhythmValidation()
        {
            if (!showRhythmValidation)
            {
                return;
            }

            GUILayout.Label($"Rhythm Validation: {GetRhythmValidationStatusLabel()} ({resetRhythmValidationKey} reset)");
            GUILayout.Label(
                $"Rhythm Phases Seen C/B/S/R: {FormatObserved(rhythmCalmObserved)}/{FormatObserved(rhythmBuildObserved)}/{FormatObserved(rhythmSpikeObserved)}/{FormatObserved(rhythmReleaseObserved)}");
            GUILayout.Label($"Rhythm Missing: {GetMissingRhythmPhaseLabel()}");
            GUILayout.Label($"Rhythm Current Gate: {lastObservedRhythmPhase} {rhythmPhaseObservationElapsed:0.0}/{rhythmPhaseObservedSeconds:0.0}s");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Write Rhythm Snapshot ({writeRhythmSnapshotKey})"))
            {
                WriteRhythmValidationSnapshot();
            }

            GUILayout.Label($"Snapshot: {GetRhythmSnapshotDisplayLabel()}");
            GUILayout.EndHorizontal();
        }

        private void WriteRhythmValidationSnapshot()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string directory = Path.Combine(projectRoot, "Logs", "RhythmValidation");
                Directory.CreateDirectory(directory);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(directory, $"rhythm_snapshot_{stamp}.txt");
                File.WriteAllText(path, BuildRhythmValidationSnapshotText(), Encoding.UTF8);
                lastRhythmSnapshotPath = path;
                Debug.Log($"Rhythm validation snapshot written: {path}", this);
            }
            catch (Exception ex)
            {
                lastRhythmSnapshotPath = $"Failed: {ex.Message}";
                Debug.LogWarning($"Failed to write rhythm validation snapshot: {ex.Message}", this);
            }
        }

        private string BuildRhythmValidationSnapshotText()
        {
            StringBuilder builder = new(1024);
            builder.AppendLine("Lost Breadcrumbs Rhythm Validation Snapshot");
            builder.AppendLine($"CapturedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Frame: {Time.frameCount}");
            builder.AppendLine();

            StageLoopDirector stageLoop = StageLoopDirector.Instance;
            if (stageLoop != null)
            {
                builder.AppendLine($"Stage: {stageLoop.CurrentStage}");
                builder.AppendLine($"Breadcrumbs: {stageLoop.CollectedBreadcrumbs}/{stageLoop.RequiredBreadcrumbs}");
                builder.AppendLine($"ExitUnlocked: {stageLoop.ExitUnlocked}");
                builder.AppendLine($"SafeHavens: {stageLoop.ActiveSafeHavenCount}");
            }

            if (rhythmDirector != null)
            {
                builder.AppendLine($"RhythmPhase: {rhythmDirector.CurrentPhaseLabel}");
                builder.AppendLine($"RhythmProgress: {rhythmDirector.CurrentPhaseProgress:0.000}");
                builder.AppendLine($"RhythmElapsedDuration: {rhythmDirector.CurrentPhaseElapsed:0.00}/{rhythmDirector.CurrentPhaseDuration:0.00}");
                builder.AppendLine($"RhythmTempoIntensityPressure: {rhythmDirector.CurrentTempo01:0.000}/{rhythmDirector.CurrentRhythmIntensity:0.000}/{rhythmDirector.CurrentPressureMultiplier:0.000}");
                builder.AppendLine($"RhythmCycle: {rhythmDirector.CycleCount}");
            }

            builder.AppendLine($"ObservedPhases_CalmBuildSpikeRelease: {FormatObserved(rhythmCalmObserved)}/{FormatObserved(rhythmBuildObserved)}/{FormatObserved(rhythmSpikeObserved)}/{FormatObserved(rhythmReleaseObserved)}");
            builder.AppendLine($"ObservedPhaseStatus: {GetRhythmValidationStatusLabel()}");
            builder.AppendLine($"MissingPhases: {GetMissingRhythmPhaseLabel()}");

            if (pressureDirector != null)
            {
                builder.AppendLine($"StagePressureTotal: {pressureDirector.CurrentPressure01:0.000}");
                builder.AppendLine($"StagePressureParts: {pressureDirector.CurrentStagePressure01:0.000}/{pressureDirector.CurrentBehaviorPressure01:0.000}/{pressureDirector.CurrentLateStageBonus01:0.000}");
            }

            if (readabilityDirector != null)
            {
                builder.AppendLine($"ReadabilityPressure_NearStageFinal: {readabilityDirector.CurrentNearbyThreat:0.000}/{readabilityDirector.CurrentStagePressure:0.000}/{readabilityDirector.CurrentReadabilityPressure:0.000}");
                builder.AppendLine($"ReadabilityTunnelClose: {readabilityDirector.CurrentThreatTunnelVision:0.000}/{readabilityDirector.CurrentCloseThreatDistance:0.00}");
                builder.AppendLine($"QuietBreathStrain: {readabilityDirector.CurrentQuietBreathStrain:0.000}");
            }

            AudioManager audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                builder.AppendLine($"AudioLastEvent: {audioManager.LastPlayedEventType} via {audioManager.LastPlaySource}");
                builder.AppendLine($"AudioLastStinger: {(audioManager.HasRuntimeStingerTelemetry ? audioManager.LastRuntimeStingerLabel : "none")} via {audioManager.LastRuntimeStingerSource}");
                builder.AppendLine($"AudioStingerMix: volume={audioManager.LastRuntimeStingerVolume:0.000}, pitch={audioManager.LastRuntimeStingerPitch:0.000}, suppressed={audioManager.SuppressedRuntimeStingerCount}");
                builder.AppendLine($"AudioDuck: {audioManager.CombatDuckCurrent:0.000}->{audioManager.CombatDuckTarget:0.000}, effective={audioManager.EffectiveDuck:0.000}");
            }

            if (playerController != null)
            {
                builder.AppendLine($"PlayerStamina: {playerController.CurrentStamina:0.00}/{playerController.MaxStamina:0.00}");
                builder.AppendLine($"PlayerMoveSpeed: {playerController.CurrentMoveSpeed:0.00}");
                builder.AppendLine($"QuietBreathRemaining: {playerController.TemporaryNoiseDampeningRemaining:0.00}");
            }

            if (playerVitals != null)
            {
                builder.AppendLine($"PlayerHealth: {playerVitals.CurrentHealth}/{playerVitals.MaxHealth}");
                builder.AppendLine($"PlayerDeaths: {playerVitals.DeathCount}");
            }

            return builder.ToString();
        }

        private string GetRhythmSnapshotDisplayLabel()
        {
            if (string.IsNullOrWhiteSpace(lastRhythmSnapshotPath) || lastRhythmSnapshotPath == "-")
            {
                return "-";
            }

            return lastRhythmSnapshotPath.StartsWith("Failed:", StringComparison.Ordinal)
                ? lastRhythmSnapshotPath
                : Path.GetFileName(lastRhythmSnapshotPath);
        }

        private string GetRhythmValidationStatusLabel()
        {
            return rhythmCalmObserved && rhythmBuildObserved && rhythmSpikeObserved && rhythmReleaseObserved
                ? "PASS"
                : "Watching";
        }

        private string GetMissingRhythmPhaseLabel()
        {
            StringBuilder builder = new(48);
            AppendMissingRhythmPhase(builder, rhythmCalmObserved, "Calm");
            AppendMissingRhythmPhase(builder, rhythmBuildObserved, "Build");
            AppendMissingRhythmPhase(builder, rhythmSpikeObserved, "Spike");
            AppendMissingRhythmPhase(builder, rhythmReleaseObserved, "Release");
            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private static void AppendMissingRhythmPhase(StringBuilder builder, bool observed, string label)
        {
            if (observed)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }

        private void ResetRhythmValidation()
        {
            rhythmCalmObserved = false;
            rhythmBuildObserved = false;
            rhythmSpikeObserved = false;
            rhythmReleaseObserved = false;
            rhythmPhaseObservationElapsed = 0f;
            lastObservedRhythmPhase = rhythmDirector != null ? rhythmDirector.CurrentPhase : GameplayRhythmPhase.Calm;
        }

        private void MarkRhythmPhaseObserved(GameplayRhythmPhase phase)
        {
            switch (phase)
            {
                case GameplayRhythmPhase.Calm:
                    rhythmCalmObserved = true;
                    break;
                case GameplayRhythmPhase.Build:
                    rhythmBuildObserved = true;
                    break;
                case GameplayRhythmPhase.Spike:
                    rhythmSpikeObserved = true;
                    break;
                case GameplayRhythmPhase.Release:
                    rhythmReleaseObserved = true;
                    break;
            }
        }

        private static string FormatObserved(bool observed)
        {
            return observed ? "Y" : "-";
        }

        private void TryResolveReferences(bool force = false)
        {
            if (!force && lastReferenceResolveFrame == Time.frameCount)
            {
                return;
            }

            if (!force)
            {
                if (Time.unscaledTime < nextReferenceResolveTime)
                {
                    return;
                }

                nextReferenceResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            lastReferenceResolveFrame = Time.frameCount;

            if (mapSystem == null)
            {
                mapSystem = FindFirstObjectByType<MapSystem>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            }

            if (fogOfWar == null)
            {
                fogOfWar = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (mapTuning == null)
            {
                mapTuning = FindFirstObjectByType<MapTuningDebugController>();
            }

            if (regressionChecklist == null)
            {
                regressionChecklist = FindFirstObjectByType<RegressionChecklistRunner>();
            }

            if (dummyLoop == null)
            {
                dummyLoop = FindFirstObjectByType<AudioDummyLoopRuntime>();
            }

            if (spawnDirector == null)
            {
                spawnDirector = FindFirstObjectByType<EnemySpawnDirector>();
            }

            if (setPieceDirector == null)
            {
                setPieceDirector = FindFirstObjectByType<StageSetPieceDirector>();
            }

            if (pressureDirector == null)
            {
                pressureDirector = FindFirstObjectByType<StagePressureDirector>();
            }

            if (rhythmDirector == null)
            {
                rhythmDirector = FindFirstObjectByType<GameplayRhythmDirector>();
            }

            if (readabilityDirector == null)
            {
                readabilityDirector = FindFirstObjectByType<ThreatReadabilityDirector>();
            }

            if (playerVitals == null)
            {
                playerVitals = FindFirstObjectByType<PlayerVitalSystem>();
            }

            if (visibilitySource == null)
            {
                visibilitySource = FindFirstObjectByType<PlayerVisibilitySource>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerDummyController>();
            }

            if (runLoadout == null)
            {
                runLoadout = FindFirstObjectByType<RunLoadoutDirector>();
            }

            if (telemetry == null)
            {
                telemetry = FindFirstObjectByType<PlayerBehaviorTelemetry>();
            }

            if (concealmentState == null)
            {
                concealmentState = FindFirstObjectByType<PlayerConcealmentState>();
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
        }

        private void RefreshHookCache(bool force = false)
        {
            if (!force)
            {
                if (Time.unscaledTime < nextHookCacheRefreshTime)
                {
                    return;
                }

                nextHookCacheRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, hookCacheRefreshInterval);
            }

            RoomArchetypeHookDummy[] hooks = FindObjectsByType<RoomArchetypeHookDummy>(FindObjectsSortMode.None);
            cachedHooks.Clear();
            for (int i = 0; i < hooks.Length; i++)
            {
                RoomArchetypeHookDummy hook = hooks[i];
                if (hook == null)
                {
                    continue;
                }

                cachedHooks.Add(hook);
            }
        }

        private void CycleRegressionResultFilter()
        {
            regressionResultFilter = regressionResultFilter switch
            {
                RegressionResultFilter.FailOnly => RegressionResultFilter.All,
                RegressionResultFilter.All => RegressionResultFilter.PassOnly,
                _ => RegressionResultFilter.FailOnly
            };
        }

        private void CycleRegressionEntrySource()
        {
            regressionEntrySource = regressionEntrySource == RegressionEntrySource.Checklist
                ? RegressionEntrySource.Soak
                : RegressionEntrySource.Checklist;
        }

        private bool ShouldShowRegressionEntry(bool passed)
        {
            return regressionResultFilter switch
            {
                RegressionResultFilter.FailOnly => !passed,
                RegressionResultFilter.PassOnly => passed,
                _ => true
            };
        }

        private string GetRegressionFilterLabel()
        {
            return regressionResultFilter switch
            {
                RegressionResultFilter.FailOnly => "FailOnly",
                RegressionResultFilter.PassOnly => "PassOnly",
                _ => "All"
            };
        }

        private string GetRegressionEntrySourceLabel()
        {
            return regressionEntrySource == RegressionEntrySource.Soak ? "Soak" : "Checklist";
        }
    }
}











