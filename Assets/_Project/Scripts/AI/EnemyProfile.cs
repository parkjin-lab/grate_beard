using UnityEngine;

namespace LostBreadcrumbs.Runtime.AI
{
    [CreateAssetMenu(fileName = "EnemyProfile", menuName = "LostBreadcrumbs/AI/Enemy Profile")]
    public class EnemyProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "default";

        [Header("Behavior Tendency")]
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Range(0f, 1f)] public float curiosity = 0.6f;
        [Range(0f, 1f)] public float persistence = 0.6f;
        [Range(0f, 1f)] public float predictionBias = 0.5f;
        [Range(0f, 1f)] public float searchBreadth = 0.6f;

        [Header("Senses")]
        [Range(0.2f, 2.5f)] public float audioSensitivity = 1f;
        [Range(0.2f, 2.5f)] public float lightSensitivity = 1f;

        [Header("Counter Senses")]
        [Range(0f, 1f)] public float safeHavenDetectionFactor = 0f;
        [Range(0f, 2f)] public float decoyNoiseResponse = 1f;
        [Range(0f, 2f)] public float itemNoiseResponse = 1f;
        [Range(0f, 1f)] public float smokeVisionPenetration = 0f;

        [Header("Timing")]
        [Min(0.1f)] public float suspicionGainPerNoise = 0.18f;
        [Min(0.01f)] public float suspicionDecayPerSecond = 0.08f;
        [Range(0.05f, 1f)] public float suspicionToInvestigate = 0.35f;
        [Range(0.05f, 1f)] public float suspicionToChase = 0.85f;
        [Min(0.1f)] public float suspicionHoldTime = 1.2f;
        [Min(0.1f)] public float chaseForgetSeconds = 2.4f;
        [Min(0.1f)] public float searchDurationSeconds = 7.5f;
        [Min(0f)] public float resumeDelaySeconds = 1f;

        [Header("Motion")]
        [Min(0.1f)] public float patrolSpeed = 1.45f;
        [Min(0.1f)] public float investigateSpeed = 1.9f;
        [Min(0.1f)] public float chaseSpeed = 2.65f;
        [Min(0.1f)] public float returnSpeed = 2f;

        [Header("Learning")]
        [Min(0.01f)] public float memoryDecayPerSecond = 0.03f;
        [Min(0.1f)] public float memoryCellSize = 1f;
        [Min(2)] public int maxRecentSamples = 10;
    }
}

