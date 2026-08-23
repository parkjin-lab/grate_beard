using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Core.Input;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Player
{
    public sealed class PlayerSmokeAbility : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode deployKey = KeyCode.R;

        [Header("Deploy")]
        [SerializeField, Min(0.1f)] private float deployDistance = 1.2f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 8.5f;
        [SerializeField, Min(1)] private int maxActiveSmokes = 1;

        [Header("Smoke Field")]
        [SerializeField, Min(0.5f)] private float smokeRadius = 2.4f;
        [SerializeField, Min(0.2f)] private float smokeLifetimeSeconds = 5.5f;
        [SerializeField, Range(0f, 1f)] private float smokeVisionBlockStrength = 0.82f;

        [Header("Risk (Noise)")]
        [SerializeField, Min(0f)] private float deployNoiseLoudness = 0.35f;
        [SerializeField, Min(0f)] private float deployNoiseRadius = 3f;

        [Header("Visual")]
        [SerializeField] private Color smokeColor = new(0.62f, 0.66f, 0.73f, 0.58f);

        private readonly List<SmokeScreenFieldDummy> activeSmokes = new();

        private float nextReadyTime;
        private int deployedCount;
        private Sprite debugSprite;
        private PlayerBehaviorTelemetry behaviorTelemetry;
        private float runtimeCooldownMultiplier = 1f;
        private float runtimeRadiusMultiplier = 1f;
        private float runtimeLifetimeMultiplier = 1f;
        private float runtimeNoiseMultiplier = 1f;

        public bool IsReady => Time.time >= nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
        public int DeployedCount => deployedCount;
        public float EffectiveCooldownSeconds => Mathf.Max(0.1f, cooldownSeconds * runtimeCooldownMultiplier);
        public float RuntimeCooldownMultiplier => runtimeCooldownMultiplier;
        public float RuntimeRadiusMultiplier => runtimeRadiusMultiplier;
        public float RuntimeLifetimeMultiplier => runtimeLifetimeMultiplier;
        public float RuntimeNoiseMultiplier => runtimeNoiseMultiplier;
        public int ActiveSmokeCount
        {
            get
            {
                CleanupSmokeList();
                return activeSmokes.Count;
            }
        }

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

            TryDeploySmoke();
        }

        public void ApplyRuntimeModifiers(float cooldownMultiplier, float radiusMultiplier, float lifetimeMultiplier, float noiseMultiplier)
        {
            runtimeCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, 0.2f, 2.8f);
            runtimeRadiusMultiplier = Mathf.Clamp(radiusMultiplier, 0.35f, 2.6f);
            runtimeLifetimeMultiplier = Mathf.Clamp(lifetimeMultiplier, 0.3f, 2.8f);
            runtimeNoiseMultiplier = Mathf.Clamp(noiseMultiplier, 0.2f, 2.5f);
        }

        public void ResetRuntimeModifiers()
        {
            ApplyRuntimeModifiers(1f, 1f, 1f, 1f);
        }

        public bool TryDeploySmoke()
        {
            CleanupSmokeList();

            if (behaviorTelemetry == null)
            {
                behaviorTelemetry = GetComponent<PlayerBehaviorTelemetry>();
            }

            if (!RegressionChecklistRunner.IsRegressionRunActive && !StageManager.IsSmokeUnlocked)
            {
                return false;
            }

            if (!IsReady)
            {
                return false;
            }

            if (activeSmokes.Count >= maxActiveSmokes)
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
            GameObject smokeObject = new($"Smoke_{deployedCount:00}");

            Transform vfxRoot = EnsureScenePath("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot != null)
            {
                smokeObject.transform.SetParent(vfxRoot, false);
            }

            smokeObject.transform.position = spawnPosition;
            smokeObject.transform.localScale = Vector3.one;

            SpriteRenderer renderer = smokeObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDebugSprite();
            renderer.color = smokeColor;
            renderer.sortingOrder = 24;

            float effectiveRadius = smokeRadius * runtimeRadiusMultiplier;
            CircleCollider2D trigger = smokeObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.2f, effectiveRadius);

            SmokeScreenFieldDummy smokeField = smokeObject.AddComponent<SmokeScreenFieldDummy>();
            smokeField.Configure(effectiveRadius, smokeLifetimeSeconds * runtimeLifetimeMultiplier, smokeVisionBlockStrength);

            activeSmokes.Add(smokeField);
            deployedCount++;
            nextReadyTime = Time.time + EffectiveCooldownSeconds;

            EmitDeployNoise();
            behaviorTelemetry?.RegisterSmokeDeploy();
            RuntimeEventBus.Raise(RuntimeEventType.Ability, BuildSmokeDeployedMessage(activeSmokes.Count, maxActiveSmokes), this);
            return true;
        }

        private static string BuildSmokeDeployedMessage(int activeCount, int maxCount)
        {
            return $"연막 전개 ({Mathf.Max(0, activeCount)}/{Mathf.Max(1, maxCount)})";
        }

        public void SetCooldownRemainingForRuntime(float remainingSeconds)
        {
            nextReadyTime = Time.time + Mathf.Max(0f, remainingSeconds);
        }

        public void ResetAbilityState(bool clearActiveSmokes = true)
        {
            nextReadyTime = 0f;

            if (!clearActiveSmokes)
            {
                return;
            }

            CleanupSmokeList();
            for (int i = 0; i < activeSmokes.Count; i++)
            {
                if (activeSmokes[i] != null)
                {
                    Destroy(activeSmokes[i].gameObject);
                }
            }

            activeSmokes.Clear();
        }

        private void EmitDeployNoise()
        {
            if (NoiseManager.Instance == null || deployNoiseLoudness <= 0f || deployNoiseRadius <= 0f)
            {
                return;
            }

            float smokeScale = SmokeScreenFieldDummy.EvaluateNoiseMultiplierAt(transform.position);
            float scaledLoudness = deployNoiseLoudness * smokeScale * runtimeNoiseMultiplier;
            float scaledRadius = deployNoiseRadius * Mathf.Lerp(0.75f, 1f, smokeScale) * runtimeNoiseMultiplier;

            NoiseManager.Instance.EmitNoise(
                transform.position,
                Mathf.Max(0.05f, scaledLoudness),
                Mathf.Max(0.2f, scaledRadius),
                NoiseKind.ItemUse,
                gameObject);
        }

        private void CleanupSmokeList()
        {
            for (int i = activeSmokes.Count - 1; i >= 0; i--)
            {
                if (activeSmokes[i] == null)
                {
                    activeSmokes.RemoveAt(i);
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
                name = "SmokeDebugTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            debugSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            debugSprite.name = "SmokeDebugSprite";
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


