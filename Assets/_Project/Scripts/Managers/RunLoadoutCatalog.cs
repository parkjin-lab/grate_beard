using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [Serializable]
    public struct RunLoadoutTuning
    {
        public RunLoadoutId id;
        public string displayName;
        [TextArea(1, 3)] public string summary;
        public bool unlockedByDefault;

        [Header("Player")]
        [Min(0.1f)] public float moveSpeedMultiplier;
        [Min(0.1f)] public float staminaCapacityMultiplier;
        [Min(0.1f)] public float staminaRecoveryMultiplier;
        [Min(0.1f)] public float footstepNoiseMultiplier;
        [Min(0.1f)] public float sprintNoiseMultiplier;

        [Header("Vision")]
        [Min(0.1f)] public float flashlightRangeMultiplier;
        [Min(0.1f)] public float flashlightAngleMultiplier;

        [Header("Pulse")]
        [Min(0.1f)] public float pulseCooldownMultiplier;
        [Min(0.1f)] public float pulseRadiusMultiplier;
        [Min(0.1f)] public float pulseNoiseMultiplier;

        [Header("Decoy")]
        [Min(0.1f)] public float decoyCooldownMultiplier;
        [Min(0.1f)] public float decoyNoiseMultiplier;
        [Min(0.1f)] public float decoyLifetimeMultiplier;

        [Header("Smoke")]
        [Min(0.1f)] public float smokeCooldownMultiplier;
        [Min(0.1f)] public float smokeRadiusMultiplier;
        [Min(0.1f)] public float smokeLifetimeMultiplier;
        [Min(0.1f)] public float smokeNoiseMultiplier;

        public string EffectiveName => string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
        public string EffectiveSummary => string.IsNullOrWhiteSpace(summary) ? "No summary" : summary;
    }

    [CreateAssetMenu(menuName = "LostBreadcrumbs/Balance/Run Loadout Catalog", fileName = "SO_RunLoadoutCatalog")]
    public sealed class RunLoadoutCatalog : ScriptableObject
    {
        [SerializeField] private List<RunLoadoutTuning> loadouts = new();

        public IReadOnlyList<RunLoadoutTuning> Loadouts => loadouts;
        public int LoadoutCount => loadouts != null ? loadouts.Count : 0;

        public int DefaultUnlockedCount
        {
            get
            {
                if (loadouts == null)
                {
                    return 0;
                }

                int count = 0;
                for (int i = 0; i < loadouts.Count; i++)
                {
                    if (loadouts[i].unlockedByDefault)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryGetTuning(RunLoadoutId id, out RunLoadoutTuning tuning)
        {
            if (loadouts != null)
            {
                for (int i = 0; i < loadouts.Count; i++)
                {
                    if (loadouts[i].id == id)
                    {
                        tuning = Sanitize(loadouts[i]);
                        return true;
                    }
                }
            }

            tuning = default;
            return false;
        }

        public void SetLoadoutsForEditor(IReadOnlyList<RunLoadoutTuning> tunings)
        {
            if (loadouts == null)
            {
                loadouts = new List<RunLoadoutTuning>();
            }

            loadouts.Clear();
            if (tunings == null)
            {
                return;
            }

            for (int i = 0; i < tunings.Count; i++)
            {
                loadouts.Add(Sanitize(tunings[i]));
            }
        }

        public void SetDefaultLoadoutsForEditor()
        {
            SetLoadoutsForEditor(BuildDefaultTunings());
        }

        public static List<RunLoadoutTuning> BuildDefaultTunings()
        {
            return new List<RunLoadoutTuning>
            {
                Create(
                    RunLoadoutId.Balanced,
                    "Balanced",
                    "Balanced baseline",
                    true,
                    1f, 1f, 1f, 1f, 1f,
                    1f, 1f,
                    1f, 1f, 1f,
                    1f, 1f, 1f,
                    1f, 1f, 1f, 1f),
                Create(
                    RunLoadoutId.Pathfinder,
                    "Pathfinder",
                    "+Move/+Vision, slight noise",
                    true,
                    1.12f, 1.15f, 1.05f, 1.08f, 1.06f,
                    1.25f, 1.1f,
                    1.02f, 0.95f, 1.05f,
                    1f, 1f, 1f,
                    1f, 0.95f, 0.95f, 1f),
                Create(
                    RunLoadoutId.EchoSpecialist,
                    "Echo Specialist",
                    "Long pulse / loud risk",
                    true,
                    0.97f, 1f, 1f, 1f, 1f,
                    1f, 1f,
                    0.92f, 1.24f, 1.24f,
                    0.95f, 1.05f, 1f,
                    1.05f, 1f, 1f, 1.05f),
                Create(
                    RunLoadoutId.ShadowRunner,
                    "Shadow Runner",
                    "Quiet mobility / weaker vision",
                    true,
                    1.05f, 1.1f, 1.2f, 0.68f, 0.72f,
                    0.9f, 0.95f,
                    1.1f, 1f, 0.9f,
                    0.85f, 0.88f, 1.18f,
                    0.82f, 1.22f, 1.25f, 0.84f)
            };
        }

        private void OnValidate()
        {
            if (loadouts == null)
            {
                return;
            }

            for (int i = 0; i < loadouts.Count; i++)
            {
                loadouts[i] = Sanitize(loadouts[i]);
            }
        }

        private static RunLoadoutTuning Create(
            RunLoadoutId id,
            string displayName,
            string summary,
            bool unlockedByDefault,
            float moveSpeed,
            float staminaCapacity,
            float staminaRecovery,
            float footstepNoise,
            float sprintNoise,
            float flashlightRange,
            float flashlightAngle,
            float pulseCooldown,
            float pulseRadius,
            float pulseNoise,
            float decoyCooldown,
            float decoyNoise,
            float decoyLifetime,
            float smokeCooldown,
            float smokeRadius,
            float smokeLifetime,
            float smokeNoise)
        {
            return Sanitize(new RunLoadoutTuning
            {
                id = id,
                displayName = displayName,
                summary = summary,
                unlockedByDefault = unlockedByDefault,
                moveSpeedMultiplier = moveSpeed,
                staminaCapacityMultiplier = staminaCapacity,
                staminaRecoveryMultiplier = staminaRecovery,
                footstepNoiseMultiplier = footstepNoise,
                sprintNoiseMultiplier = sprintNoise,
                flashlightRangeMultiplier = flashlightRange,
                flashlightAngleMultiplier = flashlightAngle,
                pulseCooldownMultiplier = pulseCooldown,
                pulseRadiusMultiplier = pulseRadius,
                pulseNoiseMultiplier = pulseNoise,
                decoyCooldownMultiplier = decoyCooldown,
                decoyNoiseMultiplier = decoyNoise,
                decoyLifetimeMultiplier = decoyLifetime,
                smokeCooldownMultiplier = smokeCooldown,
                smokeRadiusMultiplier = smokeRadius,
                smokeLifetimeMultiplier = smokeLifetime,
                smokeNoiseMultiplier = smokeNoise
            });
        }

        private static RunLoadoutTuning Sanitize(RunLoadoutTuning tuning)
        {
            tuning.moveSpeedMultiplier = Mathf.Max(0.1f, tuning.moveSpeedMultiplier);
            tuning.staminaCapacityMultiplier = Mathf.Max(0.1f, tuning.staminaCapacityMultiplier);
            tuning.staminaRecoveryMultiplier = Mathf.Max(0.1f, tuning.staminaRecoveryMultiplier);
            tuning.footstepNoiseMultiplier = Mathf.Max(0.1f, tuning.footstepNoiseMultiplier);
            tuning.sprintNoiseMultiplier = Mathf.Max(0.1f, tuning.sprintNoiseMultiplier);

            tuning.flashlightRangeMultiplier = Mathf.Max(0.1f, tuning.flashlightRangeMultiplier);
            tuning.flashlightAngleMultiplier = Mathf.Max(0.1f, tuning.flashlightAngleMultiplier);

            tuning.pulseCooldownMultiplier = Mathf.Max(0.1f, tuning.pulseCooldownMultiplier);
            tuning.pulseRadiusMultiplier = Mathf.Max(0.1f, tuning.pulseRadiusMultiplier);
            tuning.pulseNoiseMultiplier = Mathf.Max(0.1f, tuning.pulseNoiseMultiplier);

            tuning.decoyCooldownMultiplier = Mathf.Max(0.1f, tuning.decoyCooldownMultiplier);
            tuning.decoyNoiseMultiplier = Mathf.Max(0.1f, tuning.decoyNoiseMultiplier);
            tuning.decoyLifetimeMultiplier = Mathf.Max(0.1f, tuning.decoyLifetimeMultiplier);

            tuning.smokeCooldownMultiplier = Mathf.Max(0.1f, tuning.smokeCooldownMultiplier);
            tuning.smokeRadiusMultiplier = Mathf.Max(0.1f, tuning.smokeRadiusMultiplier);
            tuning.smokeLifetimeMultiplier = Mathf.Max(0.1f, tuning.smokeLifetimeMultiplier);
            tuning.smokeNoiseMultiplier = Mathf.Max(0.1f, tuning.smokeNoiseMultiplier);
            return tuning;
        }
    }
}
