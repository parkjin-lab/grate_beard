using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    [DefaultExecutionOrder(-210)]
    public sealed class AudioCombatDuckingDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private Transform player;

        [Header("Sampling")]
        [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
        [SerializeField, Min(1f)] private float threatRange = 12f;
        [SerializeField, Min(0.1f)] private float missingReferenceResolveInterval = 0.75f;

        [Header("Weighting")]
        [SerializeField, Range(0f, 1f)] private float suspicionWeight = 0.32f;
        [SerializeField, Range(0f, 1f)] private float invulnerableDampen = 0.75f;

        [Header("Debug")]
        [SerializeField] private bool logIntensity;

        private readonly System.Collections.Generic.List<EnemyController> cachedEnemies = new(16);
        private PlayerVitalSystem playerVitals;
        private float nextRefreshTime;
        private float nextResolveTime;

        private void Awake()
        {
            TryResolveRefs(force: true);
        }

        private void Update()
        {
            TryResolveRefs();

            if (audioManager == null)
            {
                return;
            }

            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + updateInterval;
            EnemyController.CopyActiveControllers(cachedEnemies);
            float intensity = EvaluateThreatIntensity();
            audioManager.SetCombatIntensity(intensity);

            if (logIntensity)
            {
                Debug.Log($"[AudioCombatDuckingDirector] combatIntensity={intensity:0.00}, enemies={cachedEnemies.Count}", this);
            }
        }

        public void SetPlayerForEditor(Transform playerTransform)
        {
            player = playerTransform;
            playerVitals = player != null ? player.GetComponent<PlayerVitalSystem>() : null;
        }

        public void SetAudioManagerForEditor(AudioManager manager)
        {
            audioManager = manager;
        }

        private float EvaluateThreatIntensity()
        {
            if (player == null || cachedEnemies.Count == 0)
            {
                return 0f;
            }

            Vector2 origin = player.position;
            float maxScore = 0f;

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, enemy.transform.position);
                float distanceFactor = 1f - Mathf.Clamp01(distance / threatRange);

                float stateWeight = enemy.CurrentState switch
                {
                    EnemyStateId.Chase => 1f,
                    EnemyStateId.Investigate => 0.68f,
                    EnemyStateId.Search => 0.56f,
                    EnemyStateId.Suspicion => 0.42f,
                    EnemyStateId.Return => 0.28f,
                    EnemyStateId.Stunned => 0.14f,
                    _ => 0.18f
                };

                float score = stateWeight * Mathf.Lerp(0.2f, 1f, distanceFactor) + enemy.Suspicion * suspicionWeight;
                if (enemy.CurrentState == EnemyStateId.Chase)
                {
                    score = Mathf.Max(score, 0.95f);
                }

                maxScore = Mathf.Max(maxScore, score);
            }

            if (playerVitals != null && playerVitals.IsInvulnerable)
            {
                maxScore *= invulnerableDampen;
            }

            return Mathf.Clamp01(maxScore);
        }

        private void TryResolveRefs(bool force = false)
        {
            if (!force)
            {
                if (audioManager != null && player != null)
                {
                    return;
                }

                if (Time.unscaledTime < nextResolveTime)
                {
                    return;
                }

                nextResolveTime = Time.unscaledTime + Mathf.Max(0.1f, missingReferenceResolveInterval);
            }

            ResolveRefs();
        }

        private void ResolveRefs()
        {
            if (audioManager == null)
            {
                audioManager = AudioManager.Instance != null
                    ? AudioManager.Instance
                    : FindFirstObjectByType<AudioManager>();
            }

            if (player == null)
            {
                PlayerDummyController activePlayer = PlayerDummyController.ActiveInstance;
                if (activePlayer != null)
                {
                    player = activePlayer.transform;
                }
            }

            if (player != null && (playerVitals == null || playerVitals.transform != player))
            {
                playerVitals = player.GetComponent<PlayerVitalSystem>();
            }
        }
    }
}
