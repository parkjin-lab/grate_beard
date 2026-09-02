using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerDecoyAbility : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode deployKey = KeyCode.E;

        [Header("Deploy")]
        [SerializeField, Min(0.1f)] private float deployDistance = 2.1f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 5.5f;
        [SerializeField, Min(1)] private int maxActiveDecoys = 1;

        [Header("Decoy Pulse")]
        [SerializeField, Min(0.2f)] private float decoyLifetimeSeconds = 4.5f;
        [SerializeField, Min(0.05f)] private float decoyPulseInterval = 0.6f;
        [SerializeField, Min(0.1f)] private float decoyLoudness = 2.9f;
        [SerializeField, Min(0.1f)] private float decoyRadius = 9.2f;

        [Header("Visual")]
        [SerializeField] private Color decoyColor = new(1f, 0.25f, 0.85f, 0.95f);

        private readonly List<DecoyEmitterDummy> activeDecoys = new();

        private float nextReadyTime;
        private int deployedCount;
        private Sprite debugSprite;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private float runtimeCooldownMultiplier = 1f;
        private float runtimeNoiseMultiplier = 1f;
        private float runtimeLifetimeMultiplier = 1f;

        public bool IsReady => Time.time >= nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
        public float EffectiveCooldownSeconds => Mathf.Max(0.1f, cooldownSeconds * runtimeCooldownMultiplier);
        public float RuntimeCooldownMultiplier => runtimeCooldownMultiplier;
        public float RuntimeNoiseMultiplier => runtimeNoiseMultiplier;
        public float RuntimeLifetimeMultiplier => runtimeLifetimeMultiplier;
        public int ActiveDecoyCount
        {
            get
            {
                CleanupDecoyList();
                return activeDecoys.Count;
            }
        }

        public int DeployedCount => deployedCount;

        private void Update()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            if (!RuntimeInputAdapter.GetKeyDown(deployKey))
            {
                return;
            }

            TryDeployDecoy();
        }

        public void ApplyRuntimeModifiers(float cooldownMultiplier, float noiseMultiplier, float lifetimeMultiplier)
        {
            runtimeCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, 0.2f, 2.8f);
            runtimeNoiseMultiplier = Mathf.Clamp(noiseMultiplier, 0.25f, 2.6f);
            runtimeLifetimeMultiplier = Mathf.Clamp(lifetimeMultiplier, 0.3f, 2.6f);
        }

        public void ResetRuntimeModifiers()
        {
            ApplyRuntimeModifiers(1f, 1f, 1f);
        }

        public bool TryDeployDecoy()
        {
            CleanupDecoyList();

            if (behaviorTelemetry == null)
            {
                behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            }

            if (!IsReady)
            {
                return false;
            }

            if (activeDecoys.Count >= maxActiveDecoys)
            {
                return false;
            }

            PlayerDummyController movement = GetComponent<PlayerDummyController>();
            Vector2 forward = movement != null ? movement.FacingDirection : (Vector2)transform.right;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector2.right;
            }

            Vector3 spawnPosition = transform.position + (Vector3)(forward.normalized * deployDistance);
            GameObject decoyObject = new($"Decoy_{deployedCount:00}");

            Transform audioRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/AudioEmitters");
            if (audioRoot != null)
            {
                decoyObject.transform.SetParent(audioRoot, false);
            }

            decoyObject.transform.position = spawnPosition;
            decoyObject.transform.localScale = Vector3.one * 0.4f;

            SpriteRenderer renderer = decoyObject.AddComponent<SpriteRenderer>();
            Sprite decoyArt = MapReadableArt.TryGetDecoySprite();
            if (decoyArt != null)
            {
                renderer.sprite = decoyArt;
                renderer.color = Color.white;
            }
            else
            {
                renderer.sprite = GetDebugSprite();
                renderer.color = decoyColor;
            }

            renderer.sortingOrder = 26;

            CircleCollider2D trigger = decoyObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.28f;

            DecoyEmitterDummy emitter = decoyObject.AddComponent<DecoyEmitterDummy>();
            float effectiveLifetime = decoyLifetimeSeconds * runtimeLifetimeMultiplier;
            float effectiveLoudness = decoyLoudness * runtimeNoiseMultiplier;
            float effectiveRadius = decoyRadius * runtimeNoiseMultiplier;
            emitter.Configure(effectiveLifetime, decoyPulseInterval, effectiveLoudness, effectiveRadius);

            activeDecoys.Add(emitter);
            deployedCount++;
            nextReadyTime = Time.time + EffectiveCooldownSeconds;
            behaviorTelemetry?.RegisterDecoyDeploy();
            RuntimeEventBus.Raise(RuntimeEventType.Ability, BuildDecoyDeployedMessage(activeDecoys.Count, maxActiveDecoys), this);
            return true;
        }

        private static string BuildDecoyDeployedMessage(int activeCount, int maxCount)
        {
            return $"미끼 배치 ({Mathf.Max(0, activeCount)}/{Mathf.Max(1, maxCount)})";
        }

        public void ResetCooldown()
        {
            ResetAbilityState(clearActiveDecoys: false);
        }

        public void SetCooldownRemainingForRuntime(float remainingSeconds)
        {
            nextReadyTime = Time.time + Mathf.Max(0f, remainingSeconds);
        }

        public void ResetAbilityState(bool clearActiveDecoys = true)
        {
            nextReadyTime = 0f;

            if (!clearActiveDecoys)
            {
                return;
            }

            CleanupDecoyList();
            for (int i = 0; i < activeDecoys.Count; i++)
            {
                if (activeDecoys[i] != null)
                {
                    Destroy(activeDecoys[i].gameObject);
                }
            }

            activeDecoys.Clear();
        }

        private void CleanupDecoyList()
        {
            for (int i = activeDecoys.Count - 1; i >= 0; i--)
            {
                if (activeDecoys[i] == null)
                {
                    activeDecoys.RemoveAt(i);
                }
            }
        }

        private Sprite GetDebugSprite()
        {
            if (debugSprite != null)
            {
                return debugSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "DecoyDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "DecoyDebugSprite";
            debugSprite.hideFlags = HideFlags.HideAndDontSave;
            return debugSprite;
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



