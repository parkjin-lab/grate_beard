using System.Collections;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.AI;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerEchoPulseAbility : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode pulseKey = KeyCode.Q;

        [Header("Pulse")]
        [SerializeField, Min(0.1f)] private float stunRadius = 2.2f;
        [SerializeField, Min(0.1f)] private float stunDurationSeconds = 1.6f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 5f;

        [Header("Risk (Noise)")]
        [SerializeField, Min(0.1f)] private float noiseLoudness = 2.2f;
        [SerializeField, Min(0.1f)] private float noiseRadius = 8f;

        [Header("Visual")]
        [SerializeField] private bool spawnPulseVisual = true;
        [SerializeField] private Color pulseVisualColor = new(0.35f, 0.92f, 1f, 0.92f);
        [SerializeField, Min(0.1f)] private float pulseVisualDuration = 0.72f;
        [SerializeField, Range(1, 4)] private int pulseVisualRingCount = 2;
        [SerializeField, Min(0f)] private float pulseVisualRingInterval = 0.12f;
        [SerializeField, Min(0.5f)] private float pulseVisualRadiusMultiplier = 1.08f;
        [SerializeField] private int pulseVisualSortingOrder = 36;

        [Header("Fog Reveal")]
        [SerializeField] private bool revealFogWithPulse = true;
        [SerializeField, Min(0.1f)] private float fogRevealRadiusMultiplier = 1.05f;
        [SerializeField, Min(0f)] private float fogRevealSoftnessBoost = 0.6f;

        [Header("Scout Ping")]
        [SerializeField] private bool revealNearbyTargetsWithPulse = true;
        [SerializeField, Min(0.1f)] private float scoutRadiusMultiplier = 1.9f;
        [SerializeField, Min(0.1f)] private float scoutRevealDuration = 0.85f;
        [SerializeField, Range(1, 32)] private int maxScoutRevealTargets = 18;
        [SerializeField, Range(0f, 1f)] private float scoutHiddenFogThreshold = 0.42f;
        [SerializeField] private Color scoutBreadcrumbColor = new(1f, 0.86f, 0.22f, 1f);
        [SerializeField] private Color scoutExitColor = new(0.35f, 1f, 0.55f, 1f);
        [SerializeField] private Color scoutHazardColor = new(1f, 0.34f, 0.22f, 1f);
        [SerializeField] private Color scoutEnemyColor = new(0.42f, 0.92f, 1f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logPulseResult = false;

        private float nextReadyTime;
        private int lastStunnedCount;
        private int lastScoutRevealCount;
        private float lastNoiseScale = 1f;
        private float runtimeCooldownMultiplier = 1f;
        private float runtimeStunRadiusMultiplier = 1f;
        private float runtimeNoiseMultiplier = 1f;

        private PlayerConcealmentState concealmentState;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private FogOfWarSystem fogSystem;
        private readonly List<EnemyController> cachedEnemies = new(16);

        public bool IsReady => Time.time >= nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
        public int LastStunnedCount => lastStunnedCount;
        public int LastScoutRevealCount => lastScoutRevealCount;
        public float LastNoiseScale => lastNoiseScale;
        public float EffectiveCooldownSeconds => Mathf.Max(0.1f, cooldownSeconds * runtimeCooldownMultiplier);
        public float EffectiveStunRadius => Mathf.Max(0.1f, stunRadius * runtimeStunRadiusMultiplier);
        public float RuntimeCooldownMultiplier => runtimeCooldownMultiplier;
        public float RuntimeStunRadiusMultiplier => runtimeStunRadiusMultiplier;
        public float RuntimeNoiseMultiplier => runtimeNoiseMultiplier;

        private void Awake()
        {
            concealmentState = GetComponent<PlayerConcealmentState>();
            behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            fogSystem = FindFirstObjectByType<FogOfWarSystem>();
        }

        public void ApplyRuntimeModifiers(float cooldownMultiplier, float stunRadiusMultiplier, float noiseMultiplier)
        {
            runtimeCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, 0.2f, 2.8f);
            runtimeStunRadiusMultiplier = Mathf.Clamp(stunRadiusMultiplier, 0.4f, 2.4f);
            runtimeNoiseMultiplier = Mathf.Clamp(noiseMultiplier, 0.25f, 2.8f);
        }

        public void ResetRuntimeModifiers()
        {
            ApplyRuntimeModifiers(1f, 1f, 1f);
        }

        public void ResetCooldown()
        {
            nextReadyTime = 0f;
            lastStunnedCount = 0;
            lastScoutRevealCount = 0;
            lastNoiseScale = 1f;
        }

        public void SetCooldownRemainingForRuntime(float remainingSeconds)
        {
            nextReadyTime = Time.time + Mathf.Max(0f, remainingSeconds);
        }

        private void Update()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (!RuntimeInputAdapter.GetKeyDown(pulseKey))
            {
                return;
            }

            TryCastPulse();
        }

        public bool TryCastPulse()
        {
            if (!IsReady)
            {
                return false;
            }

            nextReadyTime = Time.time + EffectiveCooldownSeconds;
            lastStunnedCount = 0;
            lastScoutRevealCount = 0;

            Vector2 origin = transform.position;
            float effectiveStunRadius = EffectiveStunRadius;

            SpawnPulseVisual(origin, effectiveStunRadius);
            ApplyEchoFogReveal(origin, effectiveStunRadius);
            lastScoutRevealCount = RevealNearbyScoutTargets(origin, effectiveStunRadius);
            EmitRiskNoise();

            EnemyController.CopyActiveControllers(cachedEnemies);
            for (int i = 0; i < cachedEnemies.Count; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > effectiveStunRadius)
                {
                    continue;
                }

                if (enemy.ApplyStun(stunDurationSeconds, origin, "EchoPulse"))
                {
                    lastStunnedCount++;
                }
            }

            behaviorTelemetry?.RegisterPulseCast();
            RuntimeEventBus.Raise(RuntimeEventType.Ability, $"Echo Pulse used (Stun {lastStunnedCount}, Scout {lastScoutRevealCount})", this);

            if (logPulseResult)
            {
                Debug.Log($"Echo Pulse cast. Stunned={lastStunnedCount}, Scout={lastScoutRevealCount}, NoiseScale={lastNoiseScale:0.00}", this);
            }

            return true;
        }

        private void SpawnPulseVisual(Vector2 origin, float effectiveStunRadius)
        {
            if (!spawnPulseVisual)
            {
                return;
            }

            GameObject visualObject = new($"EchoPulseWave_{Time.frameCount}");
            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(origin.x, origin.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            float visualRadius = Mathf.Max(0.45f, effectiveStunRadius * pulseVisualRadiusMultiplier);
            visual.Configure(
                visualRadius,
                pulseVisualColor,
                pulseVisualDuration,
                pulseVisualRingCount,
                pulseVisualRingInterval,
                pulseVisualSortingOrder);
        }

        private void ApplyEchoFogReveal(Vector2 origin, float effectiveStunRadius)
        {
            if (!revealFogWithPulse)
            {
                return;
            }

            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (fogSystem == null)
            {
                return;
            }

            float revealRadius = Mathf.Max(0.1f, effectiveStunRadius * fogRevealRadiusMultiplier);
            fogSystem.ApplyEchoRevealPulse(origin, revealRadius, fogRevealSoftnessBoost);
        }

        private int RevealNearbyScoutTargets(Vector2 origin, float effectiveStunRadius)
        {
            if (!revealNearbyTargetsWithPulse)
            {
                return 0;
            }

            float radius = Mathf.Max(0.1f, effectiveStunRadius * scoutRadiusMultiplier);
            int maxTargets = Mathf.Clamp(maxScoutRevealTargets, 1, 32);
            int revealedCount = 0;

            revealedCount += RevealScoutTargetsOfType<BreadcrumbPickup>(origin, radius, maxTargets - revealedCount, scoutBreadcrumbColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            revealedCount += RevealScoutTargetsOfType<ExitPortalDummy>(origin, radius, maxTargets - revealedCount, scoutExitColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            revealedCount += RevealScoutTargetsOfType<RoomArchetypeHookDummy>(origin, radius, maxTargets - revealedCount, scoutHazardColor);
            if (revealedCount >= maxTargets)
            {
                return revealedCount;
            }

            EnemyController.CopyActiveControllers(cachedEnemies);
            for (int i = 0; i < cachedEnemies.Count && revealedCount < maxTargets; i++)
            {
                EnemyController enemy = cachedEnemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                if (TryRevealScoutTarget(enemy.gameObject, origin, radius, scoutEnemyColor))
                {
                    revealedCount++;
                }
            }

            return revealedCount;
        }

        private int RevealScoutTargetsOfType<T>(Vector2 origin, float radius, int remainingBudget, Color color)
            where T : Component
        {
            if (remainingBudget <= 0)
            {
                return 0;
            }

            T[] targets = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int revealed = 0;
            for (int i = 0; i < targets.Length && revealed < remainingBudget; i++)
            {
                T target = targets[i];
                if (target == null)
                {
                    continue;
                }

                if (TryRevealScoutTarget(target.gameObject, origin, radius, color))
                {
                    revealed++;
                }
            }

            return revealed;
        }

        private bool TryRevealScoutTarget(GameObject target, Vector2 origin, float radius, Color scoutColor)
        {
            if (target == null || target == gameObject || !target.activeInHierarchy)
            {
                return false;
            }

            Vector2 targetPosition = target.transform.position;
            float radiusSqr = radius * radius;
            if ((targetPosition - origin).sqrMagnitude > radiusSqr)
            {
                return false;
            }

            if (!IsScoutTargetHiddenByFog(targetPosition))
            {
                return false;
            }

            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(false);
            if (renderers == null || renderers.Length <= 0)
            {
                return false;
            }

            StartCoroutine(RevealScoutTargetRoutine(renderers, scoutColor, scoutRevealDuration));
            return true;
        }

        private bool IsScoutTargetHiddenByFog(Vector2 targetPosition)
        {
            if (fogSystem == null)
            {
                fogSystem = FindFirstObjectByType<FogOfWarSystem>();
            }

            if (fogSystem == null || !fogSystem.isActiveAndEnabled)
            {
                return true;
            }

            return fogSystem.SampleFogAlpha01AtWorldPosition(targetPosition) >= scoutHiddenFogThreshold;
        }

        private static IEnumerator RevealScoutTargetRoutine(SpriteRenderer[] renderers, Color scoutColor, float duration)
        {
            float safeDuration = Mathf.Max(0.1f, duration);
            Color[] originalColors = new Color[renderers.Length];
            Vector3[] originalScales = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                originalColors[i] = renderer.color;
                originalScales[i] = renderer.transform.localScale;
            }

            float startedAt = Time.time;
            while (Time.time < startedAt + safeDuration)
            {
                float t = Mathf.Clamp01((Time.time - startedAt) / safeDuration);
                float pulse = 0.5f + Mathf.Sin(t * Mathf.PI * 4f) * 0.5f;
                float blend = Mathf.Lerp(0.25f, 0.9f, pulse) * (1f - t * 0.2f);
                float scale = Mathf.Lerp(1f, 1.18f, pulse) * Mathf.Lerp(1f, 0.96f, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Color color = Color.Lerp(originalColors[i], scoutColor, blend);
                    color.a = Mathf.Max(originalColors[i].a, scoutColor.a * Mathf.Lerp(0.8f, 1f, pulse));
                    renderer.color = color;
                    renderer.transform.localScale = originalScales[i] * scale;
                }

                yield return null;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.color = originalColors[i];
                renderer.transform.localScale = originalScales[i];
            }
        }

        private void EmitRiskNoise()
        {
            if (NoiseManager.Instance == null)
            {
                return;
            }

            float concealNoiseScale = concealmentState != null ? concealmentState.CurrentNoiseMultiplier : 1f;
            float smokeNoiseScale = SmokeScreenFieldDummy.EvaluateNoiseMultiplierAt(transform.position);
            float noiseScale = concealNoiseScale * smokeNoiseScale * runtimeNoiseMultiplier;

            lastNoiseScale = noiseScale;

            float scaledLoudness = noiseLoudness * noiseScale;
            float scaledRadius = noiseRadius * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(noiseScale));

            NoiseManager.Instance.EmitNoise(
                transform.position,
                Mathf.Max(0.1f, scaledLoudness),
                Mathf.Max(0.3f, scaledRadius),
                NoiseKind.Echo,
                gameObject);
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.95f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, EffectiveStunRadius);
        }
    }
}
