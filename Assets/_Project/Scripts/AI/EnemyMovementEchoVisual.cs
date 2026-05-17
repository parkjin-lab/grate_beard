using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Map;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyMovementEchoVisual : MonoBehaviour
    {
        private const string EchoVfxRootPath = "Scene_Root/GameRoot/Runtime/VFX/EnemyMoveEchoes";

        [SerializeField, Min(0.05f)] private float pulseInterval = 0.64f;
        [SerializeField, Min(0.01f)] private float minMovementSpeed = 0.32f;
        [SerializeField] private Color pulseColor = new(0.36f, 0.9f, 1f, 0.9f);
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.92f;
        [SerializeField, Range(1, 4)] private int arcCount = 3;
        [SerializeField, Range(40f, 170f)] private float arcAngleDegrees = 102f;
        [SerializeField, Min(0.05f)] private float arcBaseRadius = 0.22f;
        [SerializeField, Min(0.01f)] private float arcRadiusStep = 0.14f;
        [SerializeField, Min(0.005f)] private float arcThickness = 0.07f;
        [SerializeField] private int sortingOrder = 38;
        [SerializeField, Min(0.01f)] private float minTravelDistanceBetweenPulses = 0.34f;
        [SerializeField, Range(1, 16)] private int maxAlivePulsesPerEmitter = 5;
        [SerializeField, Range(16, 512)] private int maxAlivePulsesGlobal = 180;
        [SerializeField, Range(0.01f, 1f)] private float speedSmoothing = 0.24f;
        [SerializeField] private bool requireEnemyControllerOwner = true;
        [SerializeField] private bool showOnlyWhenHiddenByFog = true;
        [SerializeField, Range(0f, 1f)] private float fogHiddenThreshold = 0.72f;
        [SerializeField, Range(0f, 0.4f)] private float fogHiddenHysteresis = 0.08f;
        [SerializeField, Min(0f)] private float fogHiddenGraceSeconds = 0.12f;
        [SerializeField] private bool clampFogThresholdToRuntimeFogRange = true;
        [SerializeField] private bool sampleFogFromBodyBounds = true;
        [SerializeField, Range(0.3f, 1f)] private float bodyBoundsSampleInset = 0.6f;
        [SerializeField] private bool requireAllBodySamplesHiddenByFog = true;
        [SerializeField] private bool showEchoWhenFogSystemUnavailable = false;
        [SerializeField, Min(0.2f)] private float fogSystemLookupInterval = 1f;
        [SerializeField, Min(0.01f)] private float fogVisibilityEvaluationInterval = 0.06f;
        [SerializeField, Min(0.01f)] private float fogVisibilityCacheDistance = 0.35f;
        [SerializeField] private bool clearActivePulsesWhenVisible = true;

        private bool runtimeEnabled = true;
        private float nextPulseTime;
        private Vector2 lastPosition;
        private Vector2 lastDirection = Vector2.right;
        private Vector2 lastPulsePosition;
        private bool hasLastPulsePosition;
        private float smoothedSpeed;
        private int alivePulseCount;
        private Rigidbody2D body2D;
        private SpriteRenderer spriteRenderer;
        private Collider2D echoCollider2D;
        private EnemyController ownerController;
        private FogOfWarSystem fogOfWarSystem;
        private float nextFogLookupTime;
        private bool hasFogHiddenState;
        private bool cachedFogHiddenState;
        private bool hasHiddenByFogSinceTime;
        private float hiddenByFogSinceTime;
        private bool hasCachedShouldShowEcho;
        private bool cachedShouldShowEcho;
        private float nextFogVisibilityEvaluationTime;
        private bool hasCachedVisibilityPosition;
        private Vector2 cachedVisibilityPosition;
        private static Transform cachedEchoVfxRoot;
        private static int lastPrewarmCheckFrame = -1;
        private static int lastPrewarmAvailableCount = -1;
        private readonly HashSet<EnemyMovementEchoWifiPulse> activePulses = new();
        private readonly List<EnemyMovementEchoWifiPulse> pulseCleanupBuffer = new();

        public void Configure(
            float interval,
            float minimumSpeed,
            Color color,
            float duration,
            int wifiArcCount,
            float wifiArcAngleDegrees,
            float wifiArcBaseRadius,
            float wifiArcRadiusStep,
            float wifiArcThickness,
            int wifiSortingOrder)
        {
            pulseInterval = Mathf.Max(0.05f, interval);
            minMovementSpeed = Mathf.Max(0.01f, minimumSpeed);
            pulseColor = color;
            pulseDuration = Mathf.Max(0.05f, duration);
            arcCount = Mathf.Clamp(wifiArcCount, 1, 4);
            arcAngleDegrees = Mathf.Clamp(wifiArcAngleDegrees, 45f, 165f);
            arcBaseRadius = Mathf.Max(0.05f, wifiArcBaseRadius);
            arcRadiusStep = Mathf.Max(0.01f, wifiArcRadiusStep);
            arcThickness = Mathf.Max(0.001f, wifiArcThickness);
            sortingOrder = wifiSortingOrder;
        }

        public void SetEnabledForRuntime(bool enabled)
        {
            runtimeEnabled = enabled;
            InvalidateEchoVisibilityCache();
            if (!runtimeEnabled)
            {
                hasLastPulsePosition = false;
                smoothedSpeed = 0f;
                hasFogHiddenState = false;
                cachedFogHiddenState = false;
                hasHiddenByFogSinceTime = false;
                hiddenByFogSinceTime = 0f;
                ClearActivePulses();
            }
        }

        public void SetOwnerController(EnemyController controller)
        {
            ownerController = controller;
            if (requireEnemyControllerOwner && ownerController == null)
            {
                runtimeEnabled = false;
            }
        }

        public void ConfigureFogVisibility(
            bool onlyWhenHiddenByFog,
            float hiddenThreshold01,
            float hiddenHysteresis01,
            float hiddenGraceSeconds,
            bool clampThresholdToRuntimeFogRange,
            bool allowWhenFogSystemUnavailable,
            float lookupIntervalSeconds,
            float visibilityEvaluationIntervalSeconds,
            float visibilityCacheDistanceUnits)
        {
            showOnlyWhenHiddenByFog = onlyWhenHiddenByFog;
            fogHiddenThreshold = Mathf.Clamp01(hiddenThreshold01);
            fogHiddenHysteresis = Mathf.Clamp(hiddenHysteresis01, 0f, 0.4f);
            fogHiddenGraceSeconds = Mathf.Max(0f, hiddenGraceSeconds);
            clampFogThresholdToRuntimeFogRange = clampThresholdToRuntimeFogRange;
            showEchoWhenFogSystemUnavailable = allowWhenFogSystemUnavailable;
            fogSystemLookupInterval = Mathf.Max(0.2f, lookupIntervalSeconds);
            fogVisibilityEvaluationInterval = Mathf.Max(0.01f, visibilityEvaluationIntervalSeconds);
            fogVisibilityCacheDistance = Mathf.Max(0.01f, visibilityCacheDistanceUnits);
            InvalidateEchoVisibilityCache();
        }

        public void ConfigureFogSampling(bool useBodyBoundsSampling, float boundsSampleInset01, bool requireAllSamplesHidden)
        {
            sampleFogFromBodyBounds = useBodyBoundsSampling;
            bodyBoundsSampleInset = Mathf.Clamp(boundsSampleInset01, 0.3f, 1f);
            requireAllBodySamplesHiddenByFog = requireAllSamplesHidden;
            InvalidateEchoVisibilityCache();
        }

        public void ConfigureVisibilityCleanup(bool clearPulsesWhenVisible)
        {
            clearActivePulsesWhenVisible = clearPulsesWhenVisible;
        }

        public void EnsurePulsePoolPrewarmed(bool prewarmPool, int prewarmTargetCount)
        {
            if (!prewarmPool)
            {
                return;
            }

            int safeTarget = Mathf.Clamp(prewarmTargetCount, 0, 256);
            if (safeTarget <= 0)
            {
                return;
            }

            int availableCount;
            int frame = Time.frameCount;
            if (lastPrewarmCheckFrame == frame && lastPrewarmAvailableCount >= 0)
            {
                availableCount = lastPrewarmAvailableCount;
            }
            else
            {
                availableCount = EnemyMovementEchoWifiPulse.GetAvailablePooledCount();
                lastPrewarmCheckFrame = frame;
                lastPrewarmAvailableCount = availableCount;
            }

            if (availableCount >= safeTarget)
            {
                return;
            }

            EnemyMovementEchoWifiPulse.PrewarmToSize(safeTarget, GetEchoVfxRoot());
            lastPrewarmCheckFrame = frame;
            lastPrewarmAvailableCount = safeTarget;
        }

        private void OnEnable()
        {
            if (ownerController == null)
            {
                ownerController = GetComponent<EnemyController>();
            }

            body2D = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            echoCollider2D = GetComponent<Collider2D>();
            lastPosition = body2D != null ? body2D.position : (Vector2)transform.position;
            smoothedSpeed = 0f;
            hasLastPulsePosition = false;
            alivePulseCount = 0;
            nextFogLookupTime = 0f;
            hasFogHiddenState = false;
            cachedFogHiddenState = false;
            hasHiddenByFogSinceTime = false;
            hiddenByFogSinceTime = 0f;
            activePulses.Clear();
            pulseCleanupBuffer.Clear();
            InvalidateEchoVisibilityCache();
            TryResolveFogSystem(true);
            nextPulseTime = Time.time + Random.Range(0f, Mathf.Max(0.05f, pulseInterval) * 0.4f);
        }

        private void OnDisable()
        {
            alivePulseCount = 0;
            hasLastPulsePosition = false;
            smoothedSpeed = 0f;
            hasHiddenByFogSinceTime = false;
            InvalidateEchoVisibilityCache();
            ClearActivePulses();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!runtimeEnabled)
            {
                if (clearActivePulsesWhenVisible)
                {
                    ClearActivePulses();
                }

                return;
            }

            if (requireEnemyControllerOwner)
            {
                if (ownerController == null || ownerController.gameObject != gameObject || !ownerController.isActiveAndEnabled)
                {
                    if (clearActivePulsesWhenVisible)
                    {
                        ClearActivePulses();
                    }

                    return;
                }
            }

            Vector2 current = body2D != null ? body2D.position : (Vector2)transform.position;
            Vector2 delta = current - lastPosition;
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector2 velocity = body2D != null ? body2D.linearVelocity : delta / dt;
            float speed = velocity.magnitude;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, Mathf.Clamp01(speedSmoothing));

            if (velocity.sqrMagnitude > 0.00001f)
            {
                lastDirection = velocity.normalized;
            }
            else if (delta.sqrMagnitude > 0.00001f)
            {
                lastDirection = delta.normalized;
            }

            float minTravelDistance = Mathf.Max(0.01f, minTravelDistanceBetweenPulses);
            bool movedEnoughSinceLastPulse = !hasLastPulsePosition
                                             || Vector2.SqrMagnitude(current - lastPulsePosition) >= minTravelDistance * minTravelDistance;
            bool wantsSpawnAttempt = smoothedSpeed >= minMovementSpeed
                                     && movedEnoughSinceLastPulse
                                     && Time.time >= nextPulseTime;
            bool wantsCleanupCheck = clearActivePulsesWhenVisible && activePulses.Count > 0;

            if (wantsSpawnAttempt || wantsCleanupCheck)
            {
                bool shouldShowEchoNow = EvaluateShouldShowEchoAt(current);
                if (wantsCleanupCheck && !shouldShowEchoNow)
                {
                    ClearActivePulses();
                }

                if (wantsSpawnAttempt)
                {
                    if (!shouldShowEchoNow)
                    {
                        nextPulseTime = Time.time + Mathf.Max(0.08f, pulseInterval * 0.65f);
                    }
                    else if (CanSpawnPulse())
                    {
                        SpawnWifiPulse(current, lastDirection, smoothedSpeed);
                        hasLastPulsePosition = true;
                        lastPulsePosition = current;
                        float speedFactor = Mathf.InverseLerp(minMovementSpeed, minMovementSpeed * 3f, smoothedSpeed);
                        float interval = Mathf.Lerp(pulseInterval, pulseInterval * 0.9f, speedFactor);
                        nextPulseTime = Time.time + Mathf.Max(0.05f, interval);
                    }
                    else
                    {
                        // Back off briefly when at cap to prevent tight spawn checks.
                        nextPulseTime = Time.time + 0.16f;
                    }
                }
            }

            lastPosition = current;
        }

        private bool CanSpawnPulse()
        {
            int safePerEmitterCap = Mathf.Clamp(maxAlivePulsesPerEmitter, 1, 16);
            int safeGlobalCap = Mathf.Clamp(maxAlivePulsesGlobal, 16, 512);
            if (alivePulseCount >= safePerEmitterCap)
            {
                return false;
            }

            if (EnemyMovementEchoWifiPulse.GlobalAliveCount >= safeGlobalCap)
            {
                return false;
            }

            return true;
        }

        private bool ShouldShowEchoAt(Vector2 worldPosition)
        {
            if (!showOnlyWhenHiddenByFog)
            {
                return true;
            }

            TryResolveFogSystem();
            if (fogOfWarSystem == null || !fogOfWarSystem.isActiveAndEnabled)
            {
                hasFogHiddenState = false;
                hasHiddenByFogSinceTime = false;
                return showEchoWhenFogSystemUnavailable;
            }

            float threshold = Mathf.Clamp01(fogHiddenThreshold);
            float hysteresis = Mathf.Clamp(fogHiddenHysteresis, 0f, 0.4f);

            if (clampFogThresholdToRuntimeFogRange)
            {
                float minAlpha = Mathf.Min(fogOfWarSystem.EffectiveVisibleAlpha, fogOfWarSystem.EffectiveHiddenAlpha);
                float maxAlpha = Mathf.Max(fogOfWarSystem.EffectiveVisibleAlpha, fogOfWarSystem.EffectiveHiddenAlpha);

                if (maxAlpha - minAlpha > 0.0001f)
                {
                    float margin = Mathf.Min(0.03f, (maxAlpha - minAlpha) * 0.45f);
                    threshold = Mathf.Clamp(threshold, minAlpha + margin, maxAlpha - margin);
                    hysteresis = Mathf.Min(hysteresis, (maxAlpha - minAlpha) * 0.8f);
                }
            }

            float enterThreshold = Mathf.Clamp01(threshold + hysteresis * 0.5f);
            float exitThreshold = Mathf.Clamp01(threshold - hysteresis * 0.5f);

            float alpha = SampleFogAlphaForBody(worldPosition);

            if (!hasFogHiddenState)
            {
                cachedFogHiddenState = alpha >= threshold;
                hasFogHiddenState = true;
                return cachedFogHiddenState;
            }

            if (cachedFogHiddenState)
            {
                // Stay hidden until alpha clearly exits the hidden band.
                if (alpha <= exitThreshold)
                {
                    cachedFogHiddenState = false;
                }
            }
            else
            {
                // Stay visible until alpha clearly enters the hidden band.
                if (alpha >= enterThreshold)
                {
                    cachedFogHiddenState = true;
                }
            }

            if (!cachedFogHiddenState)
            {
                hasHiddenByFogSinceTime = false;
                return false;
            }

            if (!hasHiddenByFogSinceTime)
            {
                hiddenByFogSinceTime = Time.time;
                hasHiddenByFogSinceTime = true;
            }

            float grace = Mathf.Max(0f, fogHiddenGraceSeconds);
            if (grace <= 0f)
            {
                return true;
            }

            return Time.time >= hiddenByFogSinceTime + grace;
        }

        private float SampleFogAlphaForBody(Vector2 fallbackWorldPosition)
        {
            if (fogOfWarSystem == null)
            {
                return 0f;
            }

            if (!sampleFogFromBodyBounds)
            {
                return fogOfWarSystem.SampleFogAlpha01AtWorldPosition(fallbackWorldPosition);
            }

            if (!TryGetBodyBounds(out Bounds bounds))
            {
                return fogOfWarSystem.SampleFogAlpha01AtWorldPosition(fallbackWorldPosition);
            }

            Vector2 center = bounds.center;
            float inset = Mathf.Clamp(bodyBoundsSampleInset, 0.3f, 1f);
            float ex = Mathf.Max(0.02f, bounds.extents.x * inset);
            float ey = Mathf.Max(0.02f, bounds.extents.y * inset);

            float c = fogOfWarSystem.SampleFogAlpha01AtWorldPosition(center);
            float r = fogOfWarSystem.SampleFogAlpha01AtWorldPosition(center + new Vector2(ex, 0f));
            float l = fogOfWarSystem.SampleFogAlpha01AtWorldPosition(center + new Vector2(-ex, 0f));
            float u = fogOfWarSystem.SampleFogAlpha01AtWorldPosition(center + new Vector2(0f, ey));
            float d = fogOfWarSystem.SampleFogAlpha01AtWorldPosition(center + new Vector2(0f, -ey));

            if (requireAllBodySamplesHiddenByFog)
            {
                // Conservative mode: if any sampled body point is clearly visible,
                // treat the enemy as visible and suppress echo.
                return Mathf.Min(c, r, l, u, d);
            }

            return (c + r + l + u + d) / 5f;
        }

        private bool TryGetBodyBounds(out Bounds bounds)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                bounds = spriteRenderer.bounds;
                if (bounds.size.sqrMagnitude > 0.000001f)
                {
                    return true;
                }
            }

            if (echoCollider2D == null)
            {
                echoCollider2D = GetComponent<Collider2D>();
            }

            if (echoCollider2D != null)
            {
                bounds = echoCollider2D.bounds;
                if (bounds.size.sqrMagnitude > 0.000001f)
                {
                    return true;
                }
            }

            bounds = default;
            return false;
        }

        private void TryResolveFogSystem(bool force = false)
        {
            FogOfWarSystem activeFog = FogOfWarSystem.ActiveInstance;
            if (activeFog != null && activeFog.isActiveAndEnabled)
            {
                fogOfWarSystem = activeFog;
                return;
            }

            if (!force && fogOfWarSystem != null && fogOfWarSystem.isActiveAndEnabled)
            {
                return;
            }

            if (!force && Time.unscaledTime < nextFogLookupTime)
            {
                return;
            }

            nextFogLookupTime = Time.unscaledTime + Mathf.Max(0.2f, fogSystemLookupInterval);
            fogOfWarSystem = Object.FindFirstObjectByType<FogOfWarSystem>();
        }

        private bool EvaluateShouldShowEchoAt(Vector2 worldPosition)
        {
            float now = Time.time;
            bool withinTimeWindow = now < nextFogVisibilityEvaluationTime;
            bool withinDistanceWindow = false;
            if (hasCachedVisibilityPosition)
            {
                float maxDistance = Mathf.Max(0.01f, fogVisibilityCacheDistance);
                withinDistanceWindow = Vector2.SqrMagnitude(worldPosition - cachedVisibilityPosition) <= maxDistance * maxDistance;
            }

            if (hasCachedShouldShowEcho && withinTimeWindow && (hasCachedVisibilityPosition && withinDistanceWindow))
            {
                return cachedShouldShowEcho;
            }

            cachedShouldShowEcho = ShouldShowEchoAt(worldPosition);
            hasCachedShouldShowEcho = true;
            nextFogVisibilityEvaluationTime = now + Mathf.Max(0.01f, fogVisibilityEvaluationInterval);
            cachedVisibilityPosition = worldPosition;
            hasCachedVisibilityPosition = true;
            return cachedShouldShowEcho;
        }

        private void InvalidateEchoVisibilityCache()
        {
            hasCachedShouldShowEcho = false;
            cachedShouldShowEcho = false;
            nextFogVisibilityEvaluationTime = 0f;
            hasCachedVisibilityPosition = false;
            cachedVisibilityPosition = Vector2.zero;
        }

        internal void NotifyPulseDestroyed(EnemyMovementEchoWifiPulse pulse)
        {
            if (pulse != null)
            {
                activePulses.Remove(pulse);
            }

            alivePulseCount = Mathf.Max(0, alivePulseCount - 1);
        }

        private void SpawnWifiPulse(Vector2 origin, Vector2 direction, float speed)
        {
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.right;
            }

            Transform vfxRoot = GetEchoVfxRoot();
            EnemyMovementEchoWifiPulse pulse = EnemyMovementEchoWifiPulse.Acquire(vfxRoot);
            pulse.transform.position = new Vector3(origin.x, origin.y, 0f);
            pulse.BindOwner(this);
            pulse.gameObject.SetActive(true);
            alivePulseCount++;
            activePulses.Add(pulse);
            float speedFactor = Mathf.InverseLerp(minMovementSpeed, minMovementSpeed * 3f, speed);
            float speedScale = Mathf.Lerp(0.95f, 1.38f, speedFactor);
            float dynamicAngle = Mathf.Lerp(arcAngleDegrees * 0.92f, Mathf.Min(160f, arcAngleDegrees * 1.1f), speedFactor);
            pulse.Configure(
                direction,
                pulseColor,
                pulseDuration,
                arcCount,
                dynamicAngle,
                arcBaseRadius * speedScale,
                arcRadiusStep * speedScale,
                arcThickness * Mathf.Lerp(1f, 1.08f, speedFactor),
                sortingOrder);
        }

        private void ClearActivePulses()
        {
            if (activePulses.Count <= 0)
            {
                return;
            }

            pulseCleanupBuffer.Clear();
            foreach (EnemyMovementEchoWifiPulse pulse in activePulses)
            {
                if (pulse != null)
                {
                    pulseCleanupBuffer.Add(pulse);
                }
            }

            for (int i = 0; i < pulseCleanupBuffer.Count; i++)
            {
                EnemyMovementEchoWifiPulse pulse = pulseCleanupBuffer[i];
                if (pulse != null)
                {
                    pulse.ForceRelease();
                }
            }

            pulseCleanupBuffer.Clear();
            activePulses.Clear();
        }

        private static Transform GetEchoVfxRoot()
        {
            if (cachedEchoVfxRoot != null)
            {
                return cachedEchoVfxRoot;
            }

            cachedEchoVfxRoot = EnsureScenePath(EchoVfxRootPath);
            return cachedEchoVfxRoot;
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

    public sealed class EnemyMovementEchoWifiPulse : MonoBehaviour
    {
        private const int MaxPoolSize = 256;

        [SerializeField] private Color color = new(0.36f, 0.9f, 1f, 0.9f);
        [SerializeField, Min(0.05f)] private float duration = 0.92f;
        [SerializeField, Range(1, 4)] private int arcCount = 3;
        [SerializeField, Range(30f, 180f)] private float arcAngleDegrees = 102f;
        [SerializeField, Min(0.05f)] private float baseRadius = 0.22f;
        [SerializeField, Min(0.01f)] private float radiusStep = 0.14f;
        [SerializeField, Min(0.001f)] private float thickness = 0.07f;
        [SerializeField] private int sortingOrder = 38;
        [SerializeField, Range(8, 64)] private int segmentsPerArc = 34;
        [SerializeField, Range(0.05f, 0.7f)] private float tipWidthScale = 0.22f;
        [SerializeField, Range(0f, 0.8f)] private float forwardDrift = 0.68f;
        [SerializeField, Range(0f, 0.8f)] private float glowBoost = 0.25f;

        private readonly List<LineRenderer> arcs = new();
        private float spawnTime;
        private float despawnTime;
        private float arcDelay;
        private Vector3 baseWorldPosition;
        private EnemyMovementEchoVisual owner;
        private bool countedInGlobal;
        private bool hasNotifiedOwnerRelease;
        private bool inPool;

        private static Material sharedLineMaterial;
        private static int globalAliveCount;
        private static readonly Stack<EnemyMovementEchoWifiPulse> pool = new();
        private static readonly List<EnemyMovementEchoWifiPulse> poolCompactionBuffer = new();
        public static int GlobalAliveCount => Mathf.Max(0, globalAliveCount);

        public static void PrewarmToSize(int targetCount, Transform parent)
        {
            int safeTarget = Mathf.Clamp(targetCount, 0, MaxPoolSize);
            int validCount = CountValidPoolEntries();
            int toCreate = safeTarget - validCount;
            for (int i = 0; i < toCreate; i++)
            {
                GameObject pulseObject = new("EnemyMoveEcho");
                if (parent != null)
                {
                    pulseObject.transform.SetParent(parent, true);
                }

                pulseObject.SetActive(false);
                EnemyMovementEchoWifiPulse pulse = pulseObject.AddComponent<EnemyMovementEchoWifiPulse>();
                pulse.inPool = true;
                pool.Push(pulse);
            }
        }

        public static int GetAvailablePooledCount()
        {
            return CountValidPoolEntries();
        }

        public static EnemyMovementEchoWifiPulse Acquire(Transform parent)
        {
            EnemyMovementEchoWifiPulse pulse = null;
            while (pool.Count > 0 && pulse == null)
            {
                pulse = pool.Pop();
            }

            if (pulse == null)
            {
                GameObject pulseObject = new("EnemyMoveEcho");
                pulseObject.SetActive(false);
                pulse = pulseObject.AddComponent<EnemyMovementEchoWifiPulse>();
            }

            Transform pulseTransform = pulse.transform;
            if (parent != null)
            {
                pulseTransform.SetParent(parent, true);
            }

            pulse.inPool = false;
            pulse.enabled = true;
            if (pulse.gameObject.activeSelf)
            {
                pulse.gameObject.SetActive(false);
            }

            return pulse;
        }

        private static int CountValidPoolEntries()
        {
            poolCompactionBuffer.Clear();
            int validCount = 0;
            while (pool.Count > 0)
            {
                EnemyMovementEchoWifiPulse pulse = pool.Pop();
                if (pulse == null)
                {
                    continue;
                }

                validCount++;
                poolCompactionBuffer.Add(pulse);
            }

            for (int i = 0; i < poolCompactionBuffer.Count; i++)
            {
                pool.Push(poolCompactionBuffer[i]);
            }

            poolCompactionBuffer.Clear();
            return validCount;
        }

        public void BindOwner(EnemyMovementEchoVisual pulseOwner)
        {
            owner = pulseOwner;
        }

        public void ForceRelease()
        {
            if (inPool)
            {
                return;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            TryReturnToPool();
        }

        private void OnEnable()
        {
            inPool = false;
            hasNotifiedOwnerRelease = false;

            if (!countedInGlobal)
            {
                globalAliveCount++;
                countedInGlobal = true;
            }
        }

        private void OnDisable()
        {
            if (countedInGlobal)
            {
                globalAliveCount = Mathf.Max(0, globalAliveCount - 1);
                countedInGlobal = false;
            }

            NotifyOwnerReleased();
            TryReturnToPool();
        }

        private void OnDestroy()
        {
            if (countedInGlobal)
            {
                globalAliveCount = Mathf.Max(0, globalAliveCount - 1);
                countedInGlobal = false;
            }

            NotifyOwnerReleased();
        }

        public void Configure(
            Vector2 direction,
            Color pulseColor,
            float pulseDuration,
            int pulseArcCount,
            float pulseArcAngleDegrees,
            float pulseBaseRadius,
            float pulseRadiusStep,
            float pulseThickness,
            int pulseSortingOrder)
        {
            color = pulseColor;
            duration = Mathf.Max(0.05f, pulseDuration);
            arcCount = Mathf.Clamp(pulseArcCount, 1, 4);
            arcAngleDegrees = Mathf.Clamp(pulseArcAngleDegrees, 45f, 165f);
            baseRadius = Mathf.Max(0.05f, pulseBaseRadius);
            radiusStep = Mathf.Max(0.01f, pulseRadiusStep);
            thickness = Mathf.Max(0.001f, pulseThickness);
            sortingOrder = pulseSortingOrder;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = Vector3.one;

            arcDelay = duration * 0.07f;
            spawnTime = Time.time;
            despawnTime = spawnTime + duration + arcDelay * Mathf.Max(0, arcCount - 1) + 0.06f;
            baseWorldPosition = transform.position;

            BuildArcs();
        }

        private void Update()
        {
            if (arcs.Count <= 0)
            {
                if (Time.time >= despawnTime)
                {
                    ForceRelease();
                }

                return;
            }

            bool anyActive = false;
            float elapsed = Time.time - spawnTime;
            int activeArcCount = Mathf.Clamp(arcCount, 1, arcs.Count);
            for (int i = 0; i < arcs.Count; i++)
            {
                LineRenderer arc = arcs[i];
                if (arc == null)
                {
                    continue;
                }

                if (i >= activeArcCount)
                {
                    arc.enabled = false;
                    continue;
                }

                float localTime = elapsed - arcDelay * i;
                if (localTime < 0f || localTime > duration)
                {
                    arc.enabled = false;
                    continue;
                }

                anyActive = true;
                arc.enabled = true;

                float t = Mathf.Clamp01(localTime / Mathf.Max(0.01f, duration));
                float alpha = Mathf.Clamp01(Mathf.Pow(1f - t, 1.15f) * (1f - i * 0.1f));
                Color currentColor = Color.Lerp(color, Color.white, glowBoost * (1f - t));
                currentColor.a *= alpha;
                arc.startColor = currentColor;
                arc.endColor = currentColor;

                float width = Mathf.Lerp(thickness * 1.08f, thickness * 0.2f, t);
                arc.widthMultiplier = width;
            }

            float normalizedElapsed = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float pulseScale = Mathf.Lerp(0.86f, 1.36f, normalizedElapsed);
            transform.localScale = Vector3.one * pulseScale;
            float driftDistance = Mathf.Lerp(0f, baseRadius * forwardDrift, normalizedElapsed);
            transform.position = baseWorldPosition + transform.right * driftDistance;

            if (!anyActive && Time.time >= despawnTime)
            {
                ForceRelease();
            }
        }

        private void NotifyOwnerReleased()
        {
            if (hasNotifiedOwnerRelease)
            {
                return;
            }

            hasNotifiedOwnerRelease = true;
            owner?.NotifyPulseDestroyed(this);
            owner = null;
        }

        private void TryReturnToPool()
        {
            if (inPool)
            {
                return;
            }

            if (pool.Count >= MaxPoolSize)
            {
                Destroy(gameObject);
                return;
            }

            inPool = true;
            pool.Push(this);
        }

        private void BuildArcs()
        {
            while (arcs.Count < arcCount)
            {
                GameObject arcObject = new($"Arc_{arcs.Count:00}");
                arcObject.transform.SetParent(transform, false);
                LineRenderer line = arcObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = false;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.positionCount = Mathf.Clamp(segmentsPerArc, 8, 64);
                line.numCornerVertices = 3;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                line.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;
                line.widthCurve = BuildWifiWidthCurve(Mathf.Clamp(tipWidthScale, 0.05f, 0.7f));
                line.sharedMaterial = GetSharedLineMaterial();
                arcs.Add(line);
            }

            for (int i = 0; i < arcs.Count; i++)
            {
                LineRenderer line = arcs[i];
                if (line == null)
                {
                    continue;
                }

                if (i >= arcCount)
                {
                    line.enabled = false;
                    continue;
                }

                float radius = baseRadius + radiusStep * i;
                float forwardOffset = radius * 0.34f;
                BuildArcGeometry(line, radius, forwardOffset, arcAngleDegrees);
                line.sortingOrder = sortingOrder - i;
                line.enabled = false;
            }
        }

        private static void BuildArcGeometry(LineRenderer line, float radius, float forwardOffset, float angleDegrees)
        {
            if (line == null)
            {
                return;
            }

            int count = Mathf.Max(2, line.positionCount);
            float halfAngle = angleDegrees * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (float)(count - 1);
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius + forwardOffset;
                float y = Mathf.Sin(angle) * radius;
                line.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private static Material GetSharedLineMaterial()
        {
            if (sharedLineMaterial != null)
            {
                return sharedLineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Default-Line");
            }

            if (shader == null)
            {
                return null;
            }

            sharedLineMaterial = new Material(shader)
            {
                name = "EnemyMoveEchoLineMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedLineMaterial;
        }

        private static AnimationCurve BuildWifiWidthCurve(float tipScale)
        {
            return new AnimationCurve(
                new Keyframe(0f, tipScale),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, tipScale));
        }
    }
}
