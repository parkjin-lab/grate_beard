using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class HookTensionProbeDummy : MonoBehaviour
    {
        [Header("Runtime Snapshot")]
        [SerializeField, Min(1)] private int stage = 1;
        [SerializeField] private string presetLabel = "Unknown";
        [SerializeField, Range(0f, 1f)] private float stagePressure01;
        [SerializeField, Min(0.05f)] private float chanceMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float loudnessMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float radiusMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float cooldownMultiplier = 1f;

        public int Stage => stage;
        public string PresetLabel => presetLabel;
        public float StagePressure01 => stagePressure01;
        public float ChanceMultiplier => chanceMultiplier;
        public float LoudnessMultiplier => loudnessMultiplier;
        public float RadiusMultiplier => radiusMultiplier;
        public float CooldownMultiplier => cooldownMultiplier;

        public void Configure(
            int currentStage,
            string currentPresetLabel,
            float currentStagePressure01,
            float currentChanceMultiplier,
            float currentLoudnessMultiplier,
            float currentRadiusMultiplier,
            float currentCooldownMultiplier)
        {
            stage = Mathf.Max(1, currentStage);
            presetLabel = string.IsNullOrWhiteSpace(currentPresetLabel) ? "Unknown" : currentPresetLabel;
            stagePressure01 = Mathf.Clamp01(currentStagePressure01);
            chanceMultiplier = Mathf.Max(0.05f, currentChanceMultiplier);
            loudnessMultiplier = Mathf.Max(0.05f, currentLoudnessMultiplier);
            radiusMultiplier = Mathf.Max(0.05f, currentRadiusMultiplier);
            cooldownMultiplier = Mathf.Max(0.05f, currentCooldownMultiplier);
        }
    }
}
