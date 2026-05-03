using UnityEngine;

namespace LostBreadcrumbs.Runtime.AI.Learning
{
    [CreateAssetMenu(fileName = "EnemyLearningPhaseConfig", menuName = "LostBreadcrumbs/AI/Learning Phase Config")]
    public sealed class EnemyLearningPhaseConfig : ScriptableObject
    {
        [Header("Early")]
        [Range(0f, 1f)] public float earlyLearningWeight = 0.25f;
        [Range(0f, 1f)] public float earlyPredictionWeight = 0.2f;

        [Header("Mid")]
        [Range(0f, 1f)] public float midLearningWeight = 0.55f;
        [Range(0f, 1f)] public float midPredictionWeight = 0.5f;

        [Header("Late")]
        [Range(0f, 1f)] public float lateLearningWeight = 0.85f;
        [Range(0f, 1f)] public float latePredictionWeight = 0.8f;

        [Header("Safety")]
        [Range(0f, 1f)] public float maxCheatCompensation = 0.9f;
    }
}
