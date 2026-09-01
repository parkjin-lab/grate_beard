using System.Collections.Generic;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Managers;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class DecoyEmitterDummy : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float lifetimeSeconds = 4.5f;
        [SerializeField, Min(0.05f)] private float pulseInterval = 0.6f;
        [SerializeField, Min(0.1f)] private float pulseLoudness = 2.9f;
        [SerializeField, Min(0.1f)] private float pulseRadius = 9.2f;
        [SerializeField] private bool emitOnSpawn = true;

        [Header("Visual Pulse")]
        [SerializeField] private Color idleColor = new(1f, 0.25f, 0.85f, 0.82f);
        [SerializeField] private Color flashColor = new(1f, 0.92f, 0.25f, 0.98f);
        [SerializeField, Min(0.05f)] private float flashDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float idleScaleMin = 0.32f;
        [SerializeField, Min(0.1f)] private float idleScaleMax = 0.44f;
        [SerializeField, Min(0.1f)] private float breatheSpeed = 3.2f;

        [Header("Success Feedback")]
        [SerializeField] private bool showLikelyAttractionFeedback = true;
        [SerializeField, Min(0.1f)] private float successFeedbackCooldown = 0.85f;
        [SerializeField, Min(0.2f)] private float successFeedbackRadiusScale = 0.34f;
        [SerializeField, Min(0.2f)] private float successFeedbackDuration = 0.48f;
        [SerializeField] private Color successFeedbackColor = new(1f, 0.82f, 0.24f, 0.88f);
        [SerializeField] private int successFeedbackSortingOrder = 37;

        private static readonly List<EnemyController> cachedEnemies = new(16);

        private float despawnTime;
        private float nextPulseTime;
        private float pulseFlashUntil;
        private float nextSuccessFeedbackTime;

        private SpriteRenderer spriteRenderer;

        public void Configure(float lifetime, float interval, float loudness, float radius)
        {
            lifetimeSeconds = Mathf.Max(0.2f, lifetime);
            pulseInterval = Mathf.Max(0.05f, interval);
            pulseLoudness = Mathf.Max(0.1f, loudness);
            pulseRadius = Mathf.Max(0.1f, radius);
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = idleColor;
            }
        }

        private void OnEnable()
        {
            despawnTime = Time.time + lifetimeSeconds;
            nextPulseTime = Time.time;

            if (emitOnSpawn)
            {
                EmitPulse();
                nextPulseTime = Time.time + pulseInterval;
            }
        }

        private void Update()
        {
            if (Time.time >= despawnTime)
            {
                Destroy(gameObject);
                return;
            }

            TickVisual();

            if (Time.time >= nextPulseTime)
            {
                EmitPulse();
                nextPulseTime = Time.time + pulseInterval;
            }
        }

        private void EmitPulse()
        {
            if (NoiseManager.Instance == null)
            {
                return;
            }

            NoiseManager.Instance.EmitNoise(transform.position, pulseLoudness, pulseRadius, NoiseKind.Decoy, gameObject);
            pulseFlashUntil = Time.time + flashDuration;
            TryShowLikelyAttractionFeedback();
        }

        private void TryShowLikelyAttractionFeedback()
        {
            if (!showLikelyAttractionFeedback || Time.time < nextSuccessFeedbackTime)
            {
                return;
            }

            int likelyResponderCount = CountLikelyResponders();
            if (likelyResponderCount <= 0)
            {
                return;
            }

            nextSuccessFeedbackTime = Time.time + successFeedbackCooldown;
            SpawnSuccessFeedback(likelyResponderCount);
        }

        private int CountLikelyResponders()
        {
            EnemyController.CopyActiveControllers(cachedEnemies);
            int count = 0;

            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || enemy.IsStunned)
                {
                    continue;
                }

                float response = Mathf.Clamp(enemy.DecoyResponse, 0f, 2f);
                if (response <= 0.001f)
                {
                    continue;
                }

                float hearingRange = pulseRadius
                                     * Mathf.Clamp(enemy.RuntimeHearingRangeMultiplier, 0.1f, 3f)
                                     * Mathf.Clamp(response, 0.15f, 1.6f);
                if (Vector2.Distance(transform.position, enemy.transform.position) <= hearingRange)
                {
                    count++;
                }
            }

            return count;
        }

        private void SpawnSuccessFeedback(int responderCount)
        {
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX/DecoySuccess");
            GameObject visualObject = new("DecoySuccessPulse");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            float alphaScale = Mathf.Clamp01(0.65f + responderCount * 0.1f);
            float radius = Mathf.Clamp(pulseRadius * successFeedbackRadiusScale, 0.8f, 4.8f);
            Sprite decoyPulseSprite = MapReadableArt.TryGetDecoyPulseSprite();
            if (decoyPulseSprite != null)
            {
                // Painted magenta lure flare - white RGB so successFeedbackColor/decoy tint does not muddy art.
                Color color = Color.white;
                color.a = successFeedbackColor.a * alphaScale;
                visual.Configure(radius, color, successFeedbackDuration, 1, 0f, successFeedbackSortingOrder, decoyPulseSprite);
            }
            else
            {
                Color color = successFeedbackColor;
                color.a *= alphaScale;
                visual.Configure(radius, color, successFeedbackDuration, 1, 0f, successFeedbackSortingOrder);
            }
        }

        private void TickVisual()
        {
            float breathe = 0.5f + Mathf.Sin(Time.time * breatheSpeed) * 0.5f;
            float scale = Mathf.Lerp(idleScaleMin, idleScaleMax, breathe);
            transform.localScale = Vector3.one * scale;

            if (spriteRenderer == null)
            {
                return;
            }

            if (Time.time < pulseFlashUntil)
            {
                float t = 1f - Mathf.Clamp01((pulseFlashUntil - Time.time) / Mathf.Max(0.01f, flashDuration));
                spriteRenderer.color = Color.Lerp(flashColor, idleColor, t);
                return;
            }

            spriteRenderer.color = idleColor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, pulseRadius);
        }

        private static Transform EnsureScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
            {
                root = new GameObject(parts[0]);
            }

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                {
                    GameObject childObject = new(parts[i]);
                    childObject.transform.SetParent(current, false);
                    child = childObject.transform;
                }

                current = child;
            }

            return current;
        }
    }
}
