using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class FogOfWarSystem : MonoBehaviour
    {
        public static FogOfWarSystem ActiveInstance { get; private set; }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerVisibilitySource visibilitySource;

        [Header("World")]
        [SerializeField] private Vector2 worldCenter = Vector2.zero;
        [SerializeField] private Vector2 worldSize = new(40f, 30f);
        [SerializeField, Min(64)] private int baseResolution = 256;

        [Header("Reveal")]
        [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.92f;
        [SerializeField, Range(0f, 1f)] private float visibleAlpha = 0.04f;
        [SerializeField, Min(0.1f)] private float revealRadius = 3.2f;
        [SerializeField, Min(0f)] private float revealSoftness = 1.4f;
        [SerializeField, Min(0f)] private float refogPerSecond = 0.08f;

        [Header("Flashlight Reveal")]
        [SerializeField] private bool includeFlashlightCone = true;
        [SerializeField, Min(0f)] private float flashlightExtraRange = 2f;
        [SerializeField, Min(0f)] private float flashlightConeSoftness = 12f;

        [Header("Adaptive Tuning")]
        [SerializeField] private bool adaptiveRevealByWorldSize = true;
        [SerializeField, Min(6f)] private float adaptiveReferenceSpan = 40f;
        [SerializeField, Range(0.6f, 1.5f)] private float adaptiveRevealMinScale = 0.9f;
        [SerializeField, Range(1f, 3f)] private float adaptiveRevealMaxScale = 1.6f;
        [SerializeField] private bool adaptiveResolutionByWorldSize = true;
        [SerializeField, Min(2f)] private float adaptivePixelsPerUnit = 7f;
        [SerializeField, Min(64)] private int maxAdaptiveResolution = 1024;

        [Header("Performance")]
        [SerializeField, Min(0.01f)] private float updateInterval = 0.05f;

        private Texture2D fogTexture;
        private Sprite fogSprite;
        private SpriteRenderer fogRenderer;
        private Color32[] colorBuffer;
        private float[] alphaBuffer;

        private int textureWidth;
        private int textureHeight;
        private float elapsed;

        private float effectiveRevealRadius;
        private float effectiveRevealSoftness;
        private float effectiveFlashlightExtraRange;
        private float effectiveRefogPerSecond;

        private float runtimeRevealRadiusMultiplier = 1f;
        private float runtimeRevealSoftnessMultiplier = 1f;
        private float runtimeFlashlightExtraRangeMultiplier = 1f;
        private float runtimeRefogMultiplier = 1f;

        [Header("Runtime Style")]
        [SerializeField] private Color runtimeFogTint = new(0.031f, 0.039f, 0.055f, 1f);
        [SerializeField, Range(0.65f, 1.35f)] private float runtimeHiddenAlphaMultiplier = 1f;
        [SerializeField, Range(0.4f, 1.3f)] private float runtimeVisibleAlphaMultiplier = 1f;

        private float effectiveHiddenAlpha;
        private float effectiveVisibleAlpha;

        public float EffectiveRevealRadius => effectiveRevealRadius;
        public float EffectiveRevealSoftness => effectiveRevealSoftness;
        public float EffectiveFlashlightExtraRange => effectiveFlashlightExtraRange;
        public float EffectiveRefogPerSecond => effectiveRefogPerSecond;
        public float RuntimeRevealRadiusMultiplier => runtimeRevealRadiusMultiplier;
        public float RuntimeRevealSoftnessMultiplier => runtimeRevealSoftnessMultiplier;
        public float RuntimeFlashlightExtraRangeMultiplier => runtimeFlashlightExtraRangeMultiplier;
        public float RuntimeRefogMultiplier => runtimeRefogMultiplier;
        public Color RuntimeFogTint => runtimeFogTint;
        public float RuntimeHiddenAlphaMultiplier => runtimeHiddenAlphaMultiplier;
        public float RuntimeVisibleAlphaMultiplier => runtimeVisibleAlphaMultiplier;
        public float EffectiveHiddenAlpha => effectiveHiddenAlpha;
        public float EffectiveVisibleAlpha => effectiveVisibleAlpha;
        public Vector2 WorldSize => worldSize;
        public string TextureResolutionLabel => $"{textureWidth}x{textureHeight}";

        private void Awake()
        {
            RegisterAsActiveInstance();
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RegisterAsActiveInstance();
            EnsureInitialized();
            ForceApply();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveInstance, this))
            {
                ActiveInstance = null;
            }

            if (fogTexture != null)
            {
                Destroy(fogTexture);
            }

            if (fogSprite != null)
            {
                Destroy(fogSprite);
            }
        }

        private void RegisterAsActiveInstance()
        {
            ActiveInstance = this;
        }

        public void SetTargetForEditor(Transform playerTarget, PlayerVisibilitySource playerVisibility)
        {
            target = playerTarget;
            visibilitySource = playerVisibility;
        }

        public void SetWorldBounds(Vector2 center, Vector2 size)
        {
            worldCenter = center;
            worldSize = new Vector2(Mathf.Max(4f, size.x), Mathf.Max(4f, size.y));
            EvaluateAdaptiveTuning();
            EnsureInitialized(true);
            ForceApply();
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < updateInterval)
            {
                return;
            }

            float dt = elapsed;
            elapsed = 0f;

            EnsureInitialized();
            TryFindTarget();
            EvaluateAdaptiveTuning();

            Refog(dt, effectiveRefogPerSecond);

            if (target != null)
            {
                RevealCircle((Vector2)target.position, effectiveRevealRadius, effectiveRevealSoftness);

                if (includeFlashlightCone && visibilitySource != null && visibilitySource.FlashlightEnabled)
                {
                    float range = visibilitySource.FlashlightRange + effectiveFlashlightExtraRange;
                    RevealCone((Vector2)target.position, visibilitySource.CurrentForward, range, visibilitySource.FlashlightAngle, flashlightConeSoftness);
                }
            }

            ApplyTexture();
        }

        private void TryFindTarget()
        {
            if (target == null)
            {
                PlayerDummyController playerController = PlayerDummyController.ActiveInstance;
                if (playerController != null)
                {
                    target = playerController.transform;
                }
                else
                {
                    try
                    {
                        GameObject player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            target = player.transform;
                        }
                    }
                    catch (UnityException)
                    {
                        // Ignore when tag list is not initialized in current scene.
                    }
                }
            }

            if (visibilitySource == null && target != null)
            {
                visibilitySource = target.GetComponent<PlayerVisibilitySource>();
            }
        }

        private void EnsureInitialized(bool forceRebuild = false)
        {
            worldSize.x = Mathf.Max(4f, worldSize.x);
            worldSize.y = Mathf.Max(4f, worldSize.y);
            baseResolution = Mathf.Max(64, baseResolution);
            maxAdaptiveResolution = Mathf.Max(baseResolution, maxAdaptiveResolution);
            EvaluateAdaptiveTuning();

            int desiredWidth = baseResolution;
            if (adaptiveResolutionByWorldSize)
            {
                int adaptiveWidth = Mathf.RoundToInt(worldSize.x * Mathf.Max(2f, adaptivePixelsPerUnit));
                desiredWidth = Mathf.Clamp(Mathf.Max(baseResolution, adaptiveWidth), 64, maxAdaptiveResolution);
            }

            int desiredHeight = Mathf.Max(64, Mathf.RoundToInt(desiredWidth * (worldSize.y / worldSize.x)));
            desiredHeight = Mathf.Min(desiredHeight, maxAdaptiveResolution);

            bool needsRebuild = forceRebuild || fogTexture == null || colorBuffer == null || alphaBuffer == null || desiredWidth != textureWidth || desiredHeight != textureHeight;
            if (needsRebuild)
            {
                BuildTexture(desiredWidth, desiredHeight);
            }

            if (fogRenderer == null)
            {
                fogRenderer = GetComponent<SpriteRenderer>();
                if (fogRenderer == null)
                {
                    fogRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            fogRenderer.sprite = fogSprite;
            fogRenderer.sortingOrder = 300;
            fogRenderer.color = Color.white;

            transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        }

        private void BuildTexture(int width, int height)
        {
            textureWidth = width;
            textureHeight = height;

            if (fogTexture != null)
            {
                Destroy(fogTexture);
            }

            if (fogSprite != null)
            {
                Destroy(fogSprite);
            }

            fogTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "FogOfWarTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float ppu = textureWidth / worldSize.x;
            fogSprite = Sprite.Create(fogTexture, new Rect(0f, 0f, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), ppu);
            fogSprite.name = "FogOfWarSprite";

            colorBuffer = new Color32[textureWidth * textureHeight];
            alphaBuffer = new float[textureWidth * textureHeight];

            for (int i = 0; i < alphaBuffer.Length; i++)
            {
                alphaBuffer[i] = effectiveHiddenAlpha;
                colorBuffer[i] = ToFogColor(effectiveHiddenAlpha);
            }

            ApplyTexture();
        }

        private void Refog(float dt, float refogRatePerSecond)
        {
            float step = Mathf.Max(0f, refogRatePerSecond * dt);
            if (step <= 0f)
            {
                return;
            }

            for (int i = 0; i < alphaBuffer.Length; i++)
            {
                alphaBuffer[i] = Mathf.MoveTowards(alphaBuffer[i], effectiveHiddenAlpha, step);
            }
        }

        private void RevealCircle(Vector2 worldPosition, float radius, float softness)
        {
            float revealRadiusWithSoftness = Mathf.Max(0.01f, radius + softness);
            GetPixelBounds(worldPosition, revealRadiusWithSoftness, out int xMin, out int xMax, out int yMin, out int yMax);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int index = ToIndex(x, y);
                    Vector2 pixelWorld = PixelToWorld(x, y);
                    float distance = Vector2.Distance(pixelWorld, worldPosition);
                    if (distance > revealRadiusWithSoftness)
                    {
                        continue;
                    }

                    float weight = 1f - Mathf.InverseLerp(radius, revealRadiusWithSoftness, distance);
                    float desiredAlpha = Mathf.Lerp(effectiveHiddenAlpha, effectiveVisibleAlpha, Mathf.Clamp01(weight));
                    if (desiredAlpha < alphaBuffer[index])
                    {
                        alphaBuffer[index] = desiredAlpha;
                    }
                }
            }
        }

        private void RevealCone(Vector2 origin, Vector2 forward, float range, float angle, float coneSoftness)
        {
            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            float halfAngle = angle * 0.5f;
            float maxRange = Mathf.Max(0.1f, range + effectiveRevealSoftness);
            GetPixelBounds(origin, maxRange, out int xMin, out int xMax, out int yMin, out int yMax);

            forward.Normalize();

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 pixelWorld = PixelToWorld(x, y);
                    Vector2 toPixel = pixelWorld - origin;
                    float distance = toPixel.magnitude;
                    if (distance > maxRange || distance < 0.001f)
                    {
                        continue;
                    }

                    Vector2 dir = toPixel / distance;
                    float currentAngle = Vector2.Angle(forward, dir);
                    float angleWeight = 1f - Mathf.InverseLerp(halfAngle, halfAngle + coneSoftness, currentAngle);
                    if (angleWeight <= 0f)
                    {
                        continue;
                    }

                    float rangeWeight = 1f - Mathf.InverseLerp(range, maxRange, distance);
                    float weight = Mathf.Clamp01(Mathf.Min(angleWeight, rangeWeight));
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    int index = ToIndex(x, y);
                    float desiredAlpha = Mathf.Lerp(effectiveHiddenAlpha, effectiveVisibleAlpha * 0.5f, weight);
                    if (desiredAlpha < alphaBuffer[index])
                    {
                        alphaBuffer[index] = desiredAlpha;
                    }
                }
            }
        }

        private void ApplyTexture()
        {
            for (int i = 0; i < alphaBuffer.Length; i++)
            {
                colorBuffer[i] = ToFogColor(alphaBuffer[i]);
            }

            fogTexture.SetPixels32(colorBuffer);
            fogTexture.Apply(false, false);
        }

        public void ApplyEchoRevealPulse(Vector2 worldPosition, float radius, float softnessBoost = 0.6f)
        {
            EnsureInitialized();
            EvaluateAdaptiveTuning();

            float safeRadius = Mathf.Max(0.1f, radius);
            float safeSoftness = Mathf.Max(0f, effectiveRevealSoftness + softnessBoost);
            RevealCircle(worldPosition, safeRadius, safeSoftness);
            ApplyTexture();
        }

        public float SampleFogAlpha01AtWorldPosition(Vector2 worldPosition)
        {
            EnsureInitialized();
            if (alphaBuffer == null || alphaBuffer.Length == 0 || textureWidth <= 0 || textureHeight <= 0)
            {
                return effectiveHiddenAlpha;
            }

            WorldToPixel(worldPosition, out int x, out int y);
            x = Mathf.Clamp(x, 0, textureWidth - 1);
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            int index = ToIndex(x, y);
            if ((uint)index >= (uint)alphaBuffer.Length)
            {
                return effectiveHiddenAlpha;
            }

            return alphaBuffer[index];
        }

        public bool IsWorldPositionHidden(Vector2 worldPosition, float hiddenThreshold01 = 0.72f)
        {
            float threshold = Mathf.Clamp01(hiddenThreshold01);
            return SampleFogAlpha01AtWorldPosition(worldPosition) >= threshold;
        }

        public void ResetFogToHidden()
        {
            EnsureInitialized();
            ForceApply();
        }

        public void ApplyRuntimeRevealTuningForEditor(float revealRadiusMultiplier, float revealSoftnessMultiplier, float flashlightExtraRangeMultiplier, float refogMultiplier)
        {
            runtimeRevealRadiusMultiplier = Mathf.Clamp(revealRadiusMultiplier, 0.45f, 2.2f);
            runtimeRevealSoftnessMultiplier = Mathf.Clamp(revealSoftnessMultiplier, 0.45f, 2.2f);
            runtimeFlashlightExtraRangeMultiplier = Mathf.Clamp(flashlightExtraRangeMultiplier, 0.45f, 2.4f);
            runtimeRefogMultiplier = Mathf.Clamp(refogMultiplier, 0.2f, 2.4f);
            EvaluateAdaptiveTuning();
        }

        public void ResetRuntimeRevealTuningForEditor()
        {
            runtimeRevealRadiusMultiplier = 1f;
            runtimeRevealSoftnessMultiplier = 1f;
            runtimeFlashlightExtraRangeMultiplier = 1f;
            runtimeRefogMultiplier = 1f;
            EvaluateAdaptiveTuning();
        }

        public void ApplyRuntimeStyleTuningForEditor(Color fogTint, float hiddenAlphaMultiplier, float visibleAlphaMultiplier)
        {
            runtimeFogTint = new Color(
                Mathf.Clamp01(fogTint.r),
                Mathf.Clamp01(fogTint.g),
                Mathf.Clamp01(fogTint.b),
                1f);

            runtimeHiddenAlphaMultiplier = Mathf.Clamp(hiddenAlphaMultiplier, 0.65f, 1.35f);
            runtimeVisibleAlphaMultiplier = Mathf.Clamp(visibleAlphaMultiplier, 0.4f, 1.3f);
            EvaluateAdaptiveTuning();

            if (alphaBuffer != null)
            {
                ApplyTexture();
            }
        }

        public void ResetRuntimeStyleTuningForEditor()
        {
            runtimeFogTint = new Color(0.031f, 0.039f, 0.055f, 1f);
            runtimeHiddenAlphaMultiplier = 1f;
            runtimeVisibleAlphaMultiplier = 1f;
            EvaluateAdaptiveTuning();

            if (alphaBuffer != null)
            {
                ApplyTexture();
            }
        }

        private void ForceApply()
        {
            if (alphaBuffer == null)
            {
                return;
            }

            for (int i = 0; i < alphaBuffer.Length; i++)
            {
                alphaBuffer[i] = effectiveHiddenAlpha;
            }

            ApplyTexture();
        }

        private void EvaluateAdaptiveTuning()
        {
            float safeSpan = Mathf.Max(4f, Mathf.Max(worldSize.x, worldSize.y));
            float referenceSpan = Mathf.Max(6f, adaptiveReferenceSpan);

            float scale = 1f;
            if (adaptiveRevealByWorldSize)
            {
                float rawScale = safeSpan / referenceSpan;
                scale = Mathf.Clamp(rawScale, adaptiveRevealMinScale, adaptiveRevealMaxScale);
            }

            effectiveRevealRadius = Mathf.Max(0.1f, revealRadius * scale * runtimeRevealRadiusMultiplier);

            float softnessScale = Mathf.Lerp(0.85f, 1.3f, Mathf.InverseLerp(adaptiveRevealMinScale, adaptiveRevealMaxScale, scale));
            effectiveRevealSoftness = Mathf.Max(0f, revealSoftness * softnessScale * runtimeRevealSoftnessMultiplier);

            float flashlightScale = Mathf.Lerp(0.95f, 1.35f, Mathf.InverseLerp(adaptiveRevealMinScale, adaptiveRevealMaxScale, scale));
            effectiveFlashlightExtraRange = Mathf.Max(0f, flashlightExtraRange * flashlightScale * runtimeFlashlightExtraRangeMultiplier);

            runtimeHiddenAlphaMultiplier = Mathf.Clamp(runtimeHiddenAlphaMultiplier, 0.65f, 1.35f);
            runtimeVisibleAlphaMultiplier = Mathf.Clamp(runtimeVisibleAlphaMultiplier, 0.4f, 1.3f);

            effectiveHiddenAlpha = Mathf.Clamp01(hiddenAlpha * runtimeHiddenAlphaMultiplier);
            float desiredVisible = Mathf.Clamp01(visibleAlpha * runtimeVisibleAlphaMultiplier);
            effectiveVisibleAlpha = Mathf.Min(desiredVisible, Mathf.Max(0f, effectiveHiddenAlpha - 0.015f));

            effectiveRefogPerSecond = Mathf.Max(0f, refogPerSecond * runtimeRefogMultiplier);
        }

        private Color32 ToFogColor(float alpha)
        {
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(runtimeFogTint.r) * 255f), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(runtimeFogTint.g) * 255f), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(runtimeFogTint.b) * 255f), 0, 255);
            return new Color32(r, g, b, a);
        }

        private void GetPixelBounds(Vector2 center, float radius, out int xMin, out int xMax, out int yMin, out int yMax)
        {
            Vector2 min = center - Vector2.one * radius;
            Vector2 max = center + Vector2.one * radius;

            WorldToPixel(min, out xMin, out yMin);
            WorldToPixel(max, out xMax, out yMax);

            int lowX = Mathf.Min(xMin, xMax);
            int highX = Mathf.Max(xMin, xMax);
            int lowY = Mathf.Min(yMin, yMax);
            int highY = Mathf.Max(yMin, yMax);

            xMin = Mathf.Clamp(lowX, 0, textureWidth - 1);
            xMax = Mathf.Clamp(highX, 0, textureWidth - 1);
            yMin = Mathf.Clamp(lowY, 0, textureHeight - 1);
            yMax = Mathf.Clamp(highY, 0, textureHeight - 1);
        }

        private void WorldToPixel(Vector2 world, out int x, out int y)
        {
            Vector2 min = worldCenter - worldSize * 0.5f;
            float u = Mathf.InverseLerp(min.x, min.x + worldSize.x, world.x);
            float v = Mathf.InverseLerp(min.y, min.y + worldSize.y, world.y);

            x = Mathf.RoundToInt(u * (textureWidth - 1));
            y = Mathf.RoundToInt(v * (textureHeight - 1));
        }

        private Vector2 PixelToWorld(int x, int y)
        {
            Vector2 min = worldCenter - worldSize * 0.5f;
            float u = x / (float)(textureWidth - 1);
            float v = y / (float)(textureHeight - 1);
            return new Vector2(min.x + worldSize.x * u, min.y + worldSize.y * v);
        }

        private int ToIndex(int x, int y)
        {
            return y * textureWidth + x;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(worldCenter, new Vector3(worldSize.x, worldSize.y, 0f));

            if (target != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.8f);
                Gizmos.DrawWireSphere(target.position, Mathf.Max(0.1f, effectiveRevealRadius));
            }
        }
    }
}




