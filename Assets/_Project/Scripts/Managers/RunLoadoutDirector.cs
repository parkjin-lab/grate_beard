using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public enum RunLoadoutId
    {
        Balanced,
        Pathfinder,
        EchoSpecialist,
        ShadowRunner
    }

    public sealed class RunLoadoutDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerDummyController playerController;
        [SerializeField] private PlayerVisibilitySource visibilitySource;
        [SerializeField] private PlayerEchoPulseAbility pulseAbility;
        [SerializeField] private PlayerDecoyAbility decoyAbility;
        [SerializeField] private PlayerSmokeAbility smokeAbility;
        [SerializeField] private RunLoadoutCatalog loadoutCatalog;

        [Header("Selection")]
        [SerializeField] private bool allowRuntimeHotkeys = true;
        [SerializeField] private bool lockSelectionAfterPick = true;
        [SerializeField] private bool unlockAndResetOnRunEvent = true;
        [SerializeField] private bool enforceCatalogUnlockState = true;
        [SerializeField] private bool useRuntimeUnlockOverrides = true;
        [SerializeField] private bool includeDefaultUnlocksWithRuntime = true;
        [SerializeField] private RunLoadoutId selectedLoadout = RunLoadoutId.Balanced;
        [SerializeField] private bool selectionLocked;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode balancedKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode pathfinderKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode echoSpecialistKey = KeyCode.Alpha3;
        [SerializeField] private KeyCode shadowRunnerKey = KeyCode.Alpha4;
        [SerializeField] private KeyCode unlockSelectionKey = KeyCode.F8;

        [Header("Pressure Economy")]
        [SerializeField, Range(0.5f, 2.5f)] private float pressurePulseCooldownMultiplier = 1f;
        [SerializeField, Range(0.5f, 2.5f)] private float pressureDecoyCooldownMultiplier = 1f;
        [SerializeField, Range(0.5f, 2.5f)] private float pressureSmokeCooldownMultiplier = 1f;

        private readonly HashSet<RunLoadoutId> runtimeUnlockedLoadouts = new();

        public RunLoadoutId SelectedLoadout => selectedLoadout;
        public string SelectedLoadoutId => selectedLoadout.ToString();
        public bool SelectionLocked => selectionLocked;
        public bool HasCatalog => loadoutCatalog != null;
        public int CatalogLoadoutCount => loadoutCatalog != null ? loadoutCatalog.LoadoutCount : 0;
        public int CatalogUnlockedDefaultCount => loadoutCatalog != null ? loadoutCatalog.DefaultUnlockedCount : 4;
        public bool SelectedLoadoutUnlockedByDefault => TryGetActiveTuning(selectedLoadout, out RunLoadoutTuning tuning) && tuning.unlockedByDefault;
        public bool SelectedLoadoutUnlocked => TryGetActiveTuning(selectedLoadout, out RunLoadoutTuning selectedTuning) && IsLoadoutUnlocked(selectedTuning);
        public string CurrentLoadoutSummary => BuildSummary();
        public float PressurePulseCooldownMultiplier => pressurePulseCooldownMultiplier;
        public float PressureDecoyCooldownMultiplier => pressureDecoyCooldownMultiplier;
        public float PressureSmokeCooldownMultiplier => pressureSmokeCooldownMultiplier;

        private void Awake()
        {
            ResolveReferences();
            ApplySelectedLoadout(raiseEvent: false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
            RuntimeEventBus.EventRaised += HandleRuntimeEvent;
        }

        private void OnDisable()
        {
            RuntimeEventBus.EventRaised -= HandleRuntimeEvent;
        }

        private void Update()
        {
            if (!allowRuntimeHotkeys)
            {
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(unlockSelectionKey))
            {
                selectionLocked = false;
            }

            if (selectionLocked)
            {
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(balancedKey))
            {
                TrySelectLoadout(RunLoadoutId.Balanced, userInitiated: true);
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(pathfinderKey))
            {
                TrySelectLoadout(RunLoadoutId.Pathfinder, userInitiated: true);
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(echoSpecialistKey))
            {
                TrySelectLoadout(RunLoadoutId.EchoSpecialist, userInitiated: true);
                return;
            }

            if (RuntimeInputAdapter.GetKeyDown(shadowRunnerKey))
            {
                TrySelectLoadout(RunLoadoutId.ShadowRunner, userInitiated: true);
            }
        }

        public void SetReferencesForEditor(
            PlayerDummyController targetController,
            PlayerVisibilitySource targetVisibility,
            PlayerEchoPulseAbility targetPulse,
            PlayerDecoyAbility targetDecoy,
            PlayerSmokeAbility targetSmoke)
        {
            playerController = targetController;
            visibilitySource = targetVisibility;
            pulseAbility = targetPulse;
            decoyAbility = targetDecoy;
            smokeAbility = targetSmoke;

            ApplySelectedLoadout(raiseEvent: false);
        }

        public void SetCatalogForEditor(RunLoadoutCatalog catalog)
        {
            loadoutCatalog = catalog;
            ApplySelectedLoadout(raiseEvent: false);
        }

        public void ApplyPressureEconomyForRuntime(
            float pulseCooldownMultiplier,
            float decoyCooldownMultiplier,
            float smokeCooldownMultiplier,
            bool reapply = true)
        {
            pressurePulseCooldownMultiplier = Mathf.Clamp(pulseCooldownMultiplier, 0.5f, 2.5f);
            pressureDecoyCooldownMultiplier = Mathf.Clamp(decoyCooldownMultiplier, 0.5f, 2.5f);
            pressureSmokeCooldownMultiplier = Mathf.Clamp(smokeCooldownMultiplier, 0.5f, 2.5f);

            if (reapply)
            {
                ApplySelectedLoadout(raiseEvent: false);
            }
        }

        public void ResetPressureEconomyForRuntime(bool reapply = true)
        {
            ApplyPressureEconomyForRuntime(1f, 1f, 1f, reapply);
        }

        public void SelectLoadoutForEditor(RunLoadoutId loadout, bool lockAfterApply)
        {
            TrySelectLoadout(loadout, userInitiated: false, raiseEvent: false);
            selectionLocked = lockAfterApply;
        }

        public bool TrySelectLoadoutById(string loadoutId, bool lockAfterApply, bool raiseEvent = false)
        {
            if (string.IsNullOrWhiteSpace(loadoutId))
            {
                return false;
            }

            if (!Enum.TryParse(loadoutId.Trim(), true, out RunLoadoutId id))
            {
                return false;
            }

            TrySelectLoadout(id, userInitiated: false, raiseEvent: raiseEvent);
            selectionLocked = lockAfterApply;
            return true;
        }

        public string[] GetUnlockedLoadoutIdsSnapshot()
        {
            List<string> ids = new();
            RunLoadoutId[] order =
            {
                RunLoadoutId.Balanced,
                RunLoadoutId.Pathfinder,
                RunLoadoutId.EchoSpecialist,
                RunLoadoutId.ShadowRunner
            };

            for (int i = 0; i < order.Length; i++)
            {
                if (TryGetActiveTuning(order[i], out RunLoadoutTuning tuning) && IsLoadoutUnlocked(tuning))
                {
                    ids.Add(order[i].ToString());
                }
            }

            return ids.ToArray();
        }

        public void SetUnlockedLoadoutsForRuntime(string[] loadoutIds, bool clearExisting = true)
        {
            if (clearExisting)
            {
                runtimeUnlockedLoadouts.Clear();
            }

            if (loadoutIds != null)
            {
                for (int i = 0; i < loadoutIds.Length; i++)
                {
                    if (Enum.TryParse(loadoutIds[i], true, out RunLoadoutId id))
                    {
                        runtimeUnlockedLoadouts.Add(id);
                    }
                }
            }

            if (enforceCatalogUnlockState && TryGetActiveTuning(selectedLoadout, out RunLoadoutTuning selectedTuning) && !IsLoadoutUnlocked(selectedTuning))
            {
                selectedLoadout = SelectDefaultLoadout();
                ApplySelectedLoadout(raiseEvent: false);
            }
        }

        public void UnlockLoadoutForRuntime(RunLoadoutId id, bool raiseEvent = true)
        {
            runtimeUnlockedLoadouts.Add(id);

            if (raiseEvent)
            {
                RuntimeEventBus.Raise(RuntimeEventType.System, $"Loadout unlocked: {id}", this);
            }
        }

        private void HandleRuntimeEvent(RuntimeEventRecord record)
        {
            if (!unlockAndResetOnRunEvent || record.Type != RuntimeEventType.Run)
            {
                return;
            }

            selectionLocked = false;
            selectedLoadout = SelectDefaultLoadout();
            ApplySelectedLoadout(raiseEvent: true);
        }

        private void TrySelectLoadout(RunLoadoutId loadout, bool userInitiated, bool raiseEvent = true)
        {
            if (!TryGetActiveTuning(loadout, out RunLoadoutTuning tuning))
            {
                return;
            }

            if (enforceCatalogUnlockState && !IsLoadoutUnlocked(tuning))
            {
                if (userInitiated)
                {
                    RuntimeEventBus.Raise(RuntimeEventType.System, $"Loadout locked: {tuning.EffectiveName}", this);
                }

                return;
            }

            selectedLoadout = loadout;
            if (userInitiated && lockSelectionAfterPick)
            {
                selectionLocked = true;
            }

            ApplySelectedLoadout(raiseEvent);
        }

        private void ApplySelectedLoadout(bool raiseEvent)
        {
            ResolveReferences();

            if (!TryGetActiveTuning(selectedLoadout, out RunLoadoutTuning tuning))
            {
                return;
            }

            if (enforceCatalogUnlockState && !IsLoadoutUnlocked(tuning))
            {
                selectedLoadout = SelectDefaultLoadout();
                if (!TryGetActiveTuning(selectedLoadout, out tuning))
                {
                    return;
                }
            }

            ApplyTuning(tuning);

            if (raiseEvent)
            {
                RuntimeEventBus.Raise(RuntimeEventType.System, $"Loadout set: {tuning.EffectiveName} {(selectionLocked ? "(Locked)" : "")}", this);
            }
        }

        private void ApplyTuning(RunLoadoutTuning tuning)
        {
            playerController?.ApplyRuntimeModifiers(
                tuning.moveSpeedMultiplier,
                tuning.staminaCapacityMultiplier,
                tuning.staminaRecoveryMultiplier,
                tuning.footstepNoiseMultiplier,
                tuning.sprintNoiseMultiplier);

            visibilitySource?.ApplyRuntimeModifiers(tuning.flashlightRangeMultiplier, tuning.flashlightAngleMultiplier);
            pulseAbility?.ApplyRuntimeModifiers(
                tuning.pulseCooldownMultiplier * pressurePulseCooldownMultiplier,
                tuning.pulseRadiusMultiplier,
                tuning.pulseNoiseMultiplier);
            decoyAbility?.ApplyRuntimeModifiers(
                tuning.decoyCooldownMultiplier * pressureDecoyCooldownMultiplier,
                tuning.decoyNoiseMultiplier,
                tuning.decoyLifetimeMultiplier);
            smokeAbility?.ApplyRuntimeModifiers(
                tuning.smokeCooldownMultiplier * pressureSmokeCooldownMultiplier,
                tuning.smokeRadiusMultiplier,
                tuning.smokeLifetimeMultiplier,
                tuning.smokeNoiseMultiplier);
        }

        private void ResolveReferences()
        {
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
        }

        private bool TryGetActiveTuning(RunLoadoutId id, out RunLoadoutTuning tuning)
        {
            if (loadoutCatalog != null && loadoutCatalog.TryGetTuning(id, out tuning))
            {
                return true;
            }

            tuning = GetFallbackTuning(id);
            return true;
        }

        private bool IsLoadoutUnlocked(RunLoadoutTuning tuning)
        {
            bool defaultUnlocked = tuning.unlockedByDefault;
            if (!useRuntimeUnlockOverrides || runtimeUnlockedLoadouts.Count <= 0)
            {
                return defaultUnlocked;
            }

            bool runtimeUnlocked = runtimeUnlockedLoadouts.Contains(tuning.id);
            return runtimeUnlocked || (includeDefaultUnlocksWithRuntime && defaultUnlocked);
        }

        private RunLoadoutId SelectDefaultLoadout()
        {
            if (!enforceCatalogUnlockState)
            {
                return RunLoadoutId.Balanced;
            }

            RunLoadoutId[] order =
            {
                RunLoadoutId.Balanced,
                RunLoadoutId.Pathfinder,
                RunLoadoutId.EchoSpecialist,
                RunLoadoutId.ShadowRunner
            };

            for (int i = 0; i < order.Length; i++)
            {
                if (TryGetActiveTuning(order[i], out RunLoadoutTuning tuning) && IsLoadoutUnlocked(tuning))
                {
                    return order[i];
                }
            }

            return RunLoadoutId.Balanced;
        }

        private string BuildSummary()
        {
            if (!TryGetActiveTuning(selectedLoadout, out RunLoadoutTuning tuning))
            {
                return "Unknown loadout";
            }

            string lockLabel = IsLoadoutUnlocked(tuning) ? string.Empty : " [Locked]";
            return selectionLocked
                ? $"{tuning.EffectiveName} (Locked) - {tuning.EffectiveSummary}{lockLabel}"
                : $"{tuning.EffectiveName} - {tuning.EffectiveSummary}{lockLabel}";
        }

        private static RunLoadoutTuning GetFallbackTuning(RunLoadoutId id)
        {
            return id switch
            {
                RunLoadoutId.Pathfinder => new RunLoadoutTuning
                {
                    id = RunLoadoutId.Pathfinder,
                    displayName = "Pathfinder",
                    summary = "+Move/+Vision, slight noise",
                    unlockedByDefault = true,
                    moveSpeedMultiplier = 1.12f,
                    staminaCapacityMultiplier = 1.15f,
                    staminaRecoveryMultiplier = 1.05f,
                    footstepNoiseMultiplier = 1.08f,
                    sprintNoiseMultiplier = 1.06f,
                    flashlightRangeMultiplier = 1.25f,
                    flashlightAngleMultiplier = 1.1f,
                    pulseCooldownMultiplier = 1.02f,
                    pulseRadiusMultiplier = 0.95f,
                    pulseNoiseMultiplier = 1.05f,
                    decoyCooldownMultiplier = 1f,
                    decoyNoiseMultiplier = 1f,
                    decoyLifetimeMultiplier = 1f,
                    smokeCooldownMultiplier = 1f,
                    smokeRadiusMultiplier = 0.95f,
                    smokeLifetimeMultiplier = 0.95f,
                    smokeNoiseMultiplier = 1f
                },
                RunLoadoutId.EchoSpecialist => new RunLoadoutTuning
                {
                    id = RunLoadoutId.EchoSpecialist,
                    displayName = "Echo Specialist",
                    summary = "Long pulse / loud risk",
                    unlockedByDefault = true,
                    moveSpeedMultiplier = 0.97f,
                    staminaCapacityMultiplier = 1f,
                    staminaRecoveryMultiplier = 1f,
                    footstepNoiseMultiplier = 1f,
                    sprintNoiseMultiplier = 1f,
                    flashlightRangeMultiplier = 1f,
                    flashlightAngleMultiplier = 1f,
                    pulseCooldownMultiplier = 0.92f,
                    pulseRadiusMultiplier = 1.24f,
                    pulseNoiseMultiplier = 1.24f,
                    decoyCooldownMultiplier = 0.95f,
                    decoyNoiseMultiplier = 1.05f,
                    decoyLifetimeMultiplier = 1f,
                    smokeCooldownMultiplier = 1.05f,
                    smokeRadiusMultiplier = 1f,
                    smokeLifetimeMultiplier = 1f,
                    smokeNoiseMultiplier = 1.05f
                },
                RunLoadoutId.ShadowRunner => new RunLoadoutTuning
                {
                    id = RunLoadoutId.ShadowRunner,
                    displayName = "Shadow Runner",
                    summary = "Quiet mobility / weaker vision",
                    unlockedByDefault = true,
                    moveSpeedMultiplier = 1.05f,
                    staminaCapacityMultiplier = 1.1f,
                    staminaRecoveryMultiplier = 1.2f,
                    footstepNoiseMultiplier = 0.68f,
                    sprintNoiseMultiplier = 0.72f,
                    flashlightRangeMultiplier = 0.9f,
                    flashlightAngleMultiplier = 0.95f,
                    pulseCooldownMultiplier = 1.1f,
                    pulseRadiusMultiplier = 1f,
                    pulseNoiseMultiplier = 0.9f,
                    decoyCooldownMultiplier = 0.85f,
                    decoyNoiseMultiplier = 0.88f,
                    decoyLifetimeMultiplier = 1.18f,
                    smokeCooldownMultiplier = 0.82f,
                    smokeRadiusMultiplier = 1.22f,
                    smokeLifetimeMultiplier = 1.25f,
                    smokeNoiseMultiplier = 0.84f
                },
                _ => new RunLoadoutTuning
                {
                    id = RunLoadoutId.Balanced,
                    displayName = "Balanced",
                    summary = "Balanced baseline",
                    unlockedByDefault = true,
                    moveSpeedMultiplier = 1f,
                    staminaCapacityMultiplier = 1f,
                    staminaRecoveryMultiplier = 1f,
                    footstepNoiseMultiplier = 1f,
                    sprintNoiseMultiplier = 1f,
                    flashlightRangeMultiplier = 1f,
                    flashlightAngleMultiplier = 1f,
                    pulseCooldownMultiplier = 1f,
                    pulseRadiusMultiplier = 1f,
                    pulseNoiseMultiplier = 1f,
                    decoyCooldownMultiplier = 1f,
                    decoyNoiseMultiplier = 1f,
                    decoyLifetimeMultiplier = 1f,
                    smokeCooldownMultiplier = 1f,
                    smokeRadiusMultiplier = 1f,
                    smokeLifetimeMultiplier = 1f,
                    smokeNoiseMultiplier = 1f
                }
            };
        }
    }
}

