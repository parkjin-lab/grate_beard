using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Events;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class SafeHavenZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 0.7f;

        [Header("Unsafe Dread")]
        [SerializeField] private bool enableUnsafeDread = true;
        [SerializeField, Range(0f, 1f)] private float unsafeDreadPressureThreshold = 0.42f;
        [SerializeField, Min(0.2f)] private float unsafeDreadMinInterval = 5.2f;
        [SerializeField, Min(0.2f)] private float unsafeDreadMaxInterval = 11.5f;
        [SerializeField] private Color unsafeDreadTint = new(0.58f, 0.08f, 0.12f, 0.92f);
        [SerializeField, Range(0f, 1f)] private float unsafeDreadTintStrength = 0.46f;
        [SerializeField, Min(0.1f)] private float unsafeDreadBreatheSpeed = 0.75f;
        [SerializeField] private Color unsafePulseColor = new(0.64f, 0.04f, 0.1f, 0.38f);
        [SerializeField, Min(0.2f)] private float unsafePulseRadius = 2.1f;
        [SerializeField, Min(0.1f)] private float unsafePulseDuration = 2.4f;
        [SerializeField, Range(1, 4)] private int unsafePulseRingCount = 2;
        [SerializeField, Min(0f)] private float unsafePulseRingInterval = 0.34f;
        [SerializeField] private int unsafePulseSortingOrder = 32;
        [SerializeField] private bool emitUnsafeFalseNoise = true;
        [SerializeField, Range(0f, 1f)] private float unsafeFalseNoiseChance = 0.58f;
        [SerializeField, Min(0f)] private float unsafeFalseNoiseDistance = 2.8f;
        [SerializeField, Min(0f)] private float unsafeFalseNoiseLoudness = 0.48f;
        [SerializeField, Min(0f)] private float unsafeFalseNoiseRadius = 3.6f;
        [SerializeField, Range(0f, 1f)] private float unsafeFalsePulseAlphaScale = 0.52f;

        [Header("Overstay Pressure")]
        [SerializeField] private bool enableOverstayPressure = true;
        [SerializeField, Min(0.5f)] private float overstayWarningSeconds = 4.8f;
        [SerializeField, Min(0.5f)] private float overstayBeatInterval = 2.6f;
        [SerializeField, Min(0f)] private float overstayNoiseLoudness = 0.54f;
        [SerializeField, Min(0.1f)] private float overstayNoiseRadius = 4.4f;
        [SerializeField, Min(0f)] private float overstayNoiseGrowthPerBeat = 0.18f;
        [SerializeField] private Color overstayPulseColor = new(1f, 0.28f, 0.14f, 0.42f);
        [SerializeField, Min(0.2f)] private float overstayPulseRadius = 1.75f;
        [SerializeField, Min(0.1f)] private float overstayPulseDuration = 1.55f;
        [SerializeField] private int overstayPulseSortingOrder = 38;

        private PlayerConcealmentState activePlayerConcealment;
        private SpriteRenderer spriteRenderer;
        private Color baseRendererColor;
        private bool hasBaseRendererColor;
        private int configuredStage = 1;
        private float configuredPressure01;
        private float nextUnsafeDreadTime = float.PositiveInfinity;
        private float safeHavenEnteredTime = -999f;
        private float nextOverstayBeatTime = float.PositiveInfinity;
        private int overstayBeatCount;
        private bool overstayWarningRaised;

        public void Configure(float targetRadius)
        {
            Configure(targetRadius, 1, 0f);
        }

        public void Configure(float targetRadius, int stageIndex, float stagePressure01)
        {
            radius = Mathf.Max(0.1f, targetRadius);
            configuredStage = Mathf.Max(1, stageIndex);
            configuredPressure01 = Mathf.Clamp01(stagePressure01);

            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = radius;
                collider.isTrigger = true;
            }

            ResolveRenderer();
        }

        private void Awake()
        {
            ResolveRenderer();
        }

        private void OnEnable()
        {
            nextUnsafeDreadTime = float.PositiveInfinity;
            ResolveRenderer();
        }

        private void Update()
        {
            TickUnsafeDread();
            TickOverstayPressure();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerConcealmentState concealment = ResolvePlayerConcealment(other);
            if (concealment == null)
            {
                return;
            }

            activePlayerConcealment = concealment;
            activePlayerConcealment.EnterSafeHaven();
            safeHavenEnteredTime = Time.time;
            nextOverstayBeatTime = Time.time + Mathf.Max(0.5f, overstayWarningSeconds);
            overstayBeatCount = 0;
            overstayWarningRaised = false;

            float dread = EvaluateUnsafeDread01();
            if (dread > 0f)
            {
                ScheduleNextUnsafeDread(dread, true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            PlayerConcealmentState concealment = ResolvePlayerConcealment(other);
            if (concealment == null)
            {
                return;
            }

            concealment.ExitSafeHaven();
            if (activePlayerConcealment == concealment)
            {
                activePlayerConcealment = null;
                nextUnsafeDreadTime = float.PositiveInfinity;
                ResetOverstayPressure();
            }
        }

        private void TickUnsafeDread()
        {
            float dread = EvaluateUnsafeDread01();
            bool active = enableUnsafeDread
                          && Application.isPlaying
                          && !RegressionChecklistRunner.IsRegressionRunActive
                          && dread > 0f
                          && activePlayerConcealment != null
                          && activePlayerConcealment.IsInsideSafeHaven;

            ApplyUnsafeDreadTint(active ? dread : 0f);
            if (!active || Time.time < nextUnsafeDreadTime)
            {
                return;
            }

            TriggerUnsafeDread(dread);
            ScheduleNextUnsafeDread(dread, false);
        }

        private float EvaluateUnsafeDread01()
        {
            if (!enableUnsafeDread)
            {
                return 0f;
            }

            float threshold = Mathf.Clamp01(unsafeDreadPressureThreshold);
            if (threshold >= 0.995f)
            {
                return configuredPressure01 >= threshold ? 1f : 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold, 1f, configuredPressure01));
        }

        private void ApplyUnsafeDreadTint(float dread)
        {
            ResolveRenderer();
            if (spriteRenderer == null || !hasBaseRendererColor)
            {
                return;
            }

            if (dread <= 0.001f)
            {
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, baseRendererColor, Mathf.Clamp01(Time.deltaTime * 3.5f));
                return;
            }

            float breathe = 0.5f + Mathf.Sin((Time.time + transform.position.sqrMagnitude * 0.07f) * unsafeDreadBreatheSpeed) * 0.5f;
            float blend = Mathf.Clamp01(unsafeDreadTintStrength * dread * Mathf.Lerp(0.35f, 1f, breathe));
            Color target = Color.Lerp(baseRendererColor, unsafeDreadTint, blend);
            target.a = Mathf.Clamp01(Mathf.Lerp(baseRendererColor.a, unsafeDreadTint.a, blend) * Mathf.Lerp(0.9f, 1.08f, breathe * dread));
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, target, Mathf.Clamp01(Time.deltaTime * 2.9f));
        }

        private void ScheduleNextUnsafeDread(float dread, bool firstBeat)
        {
            float interval = Mathf.Lerp(
                Mathf.Max(unsafeDreadMinInterval, unsafeDreadMaxInterval),
                Mathf.Max(0.2f, unsafeDreadMinInterval),
                Mathf.Clamp01(dread));

            if (firstBeat)
            {
                interval *= Mathf.Lerp(0.46f, 0.24f, Mathf.Clamp01(dread));
            }

            nextUnsafeDreadTime = Time.time + Mathf.Max(0.2f, interval * Random.Range(0.82f, 1.18f));
        }

        private void TriggerUnsafeDread(float dread)
        {
            SpawnUnsafePulse(transform.position, dread, 1f);

            if (!emitUnsafeFalseNoise || NoiseManager.Instance == null)
            {
                return;
            }

            float chance = Mathf.Clamp01(unsafeFalseNoiseChance + dread * 0.18f);
            if (Random.value > chance)
            {
                return;
            }

            Vector2 falseNoisePosition = EvaluateUnsafeFalseNoisePosition(dread);
            float intensity = Mathf.Lerp(0.72f, 1.18f, Mathf.Clamp01(dread));
            NoiseManager.Instance.EmitNoise(
                falseNoisePosition,
                unsafeFalseNoiseLoudness * intensity,
                unsafeFalseNoiseRadius * intensity,
                NoiseKind.Decoy,
                gameObject);
            SpawnUnsafePulse(falseNoisePosition, dread, unsafeFalsePulseAlphaScale);
        }

        private void TickOverstayPressure()
        {
            bool active = enableOverstayPressure
                          && Application.isPlaying
                          && !RegressionChecklistRunner.IsRegressionRunActive
                          && activePlayerConcealment != null
                          && activePlayerConcealment.IsInsideSafeHaven;

            if (!active || Time.time < nextOverstayBeatTime)
            {
                return;
            }

            overstayBeatCount++;
            float pressure = Mathf.Clamp01((Time.time - safeHavenEnteredTime - overstayWarningSeconds) / Mathf.Max(0.5f, overstayBeatInterval * 3f));
            TriggerOverstayPressure(pressure);

            float interval = Mathf.Max(0.5f, overstayBeatInterval) * Mathf.Lerp(1f, 0.64f, pressure);
            nextOverstayBeatTime = Time.time + interval;
        }

        private void TriggerOverstayPressure(float pressure)
        {
            if (!overstayWarningRaised)
            {
                overstayWarningRaised = true;
                RuntimeEventBus.Raise(
                    RuntimeEventType.Objective,
                    "Safe haven thinning",
                    this,
                    configuredStage,
                    semantic: RuntimeEventSemantic.SafeHavenThin);
            }

            SpawnOverstayPulse(pressure);
            if (NoiseManager.Instance == null)
            {
                return;
            }

            float beatScale = 1f + Mathf.Max(0, overstayBeatCount - 1) * Mathf.Max(0f, overstayNoiseGrowthPerBeat);
            NoiseManager.Instance.EmitNoise(
                transform.position,
                Mathf.Max(0f, overstayNoiseLoudness) * beatScale,
                Mathf.Max(0.1f, overstayNoiseRadius) * Mathf.Lerp(1f, 1.32f, pressure),
                NoiseKind.ItemUse,
                gameObject);
        }

        private void SpawnOverstayPulse(float pressure)
        {
            Transform vfxRoot = EnsureUnsafeVfxRoot();
            GameObject visualObject = new("SafeHavenOverstayPulse");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = overstayPulseColor;
            color.a *= Mathf.Lerp(0.78f, 1.16f, Mathf.Clamp01(pressure));
            visual.Configure(
                Mathf.Max(0.2f, overstayPulseRadius * Mathf.Lerp(0.92f, 1.28f, Mathf.Clamp01(pressure))),
                color,
                Mathf.Max(0.1f, overstayPulseDuration),
                2,
                Mathf.Max(0.08f, overstayPulseDuration * 0.18f),
                overstayPulseSortingOrder);
        }

        private void ResetOverstayPressure()
        {
            safeHavenEnteredTime = -999f;
            nextOverstayBeatTime = float.PositiveInfinity;
            overstayBeatCount = 0;
            overstayWarningRaised = false;
        }

        private Vector2 EvaluateUnsafeFalseNoisePosition(float dread)
        {
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            float distance = radius + unsafeFalseNoiseDistance * Mathf.Lerp(0.78f, 1.3f, Mathf.Clamp01(dread));
            return (Vector2)transform.position + direction * Mathf.Max(radius + 0.25f, distance);
        }

        private void SpawnUnsafePulse(Vector2 position, float dread, float alphaScale)
        {
            Transform vfxRoot = EnsureUnsafeVfxRoot();
            GameObject visualObject = new("UnsafeHavenPulse");
            if (vfxRoot != null)
            {
                visualObject.transform.SetParent(vfxRoot, false);
            }

            visualObject.transform.position = new Vector3(position.x, position.y, 0f);
            EchoPulseVisualDummy visual = visualObject.AddComponent<EchoPulseVisualDummy>();
            Color color = unsafePulseColor;
            color.a *= Mathf.Clamp01(alphaScale) * Mathf.Lerp(0.76f, 1.16f, Mathf.Clamp01(dread));
            visual.Configure(
                Mathf.Max(0.2f, unsafePulseRadius * Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp01(dread))),
                color,
                Mathf.Max(0.1f, unsafePulseDuration),
                Mathf.Clamp(unsafePulseRingCount, 1, 4),
                Mathf.Max(0f, unsafePulseRingInterval),
                unsafePulseSortingOrder);
        }

        private static Transform EnsureUnsafeVfxRoot()
        {
            GameObject vfxRoot = GameObject.Find("Scene_Root/GameRoot/Runtime/VFX");
            if (vfxRoot == null)
            {
                return null;
            }

            Transform existing = vfxRoot.transform.Find("UnsafeSafeHavens");
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new("UnsafeSafeHavens");
            rootObject.transform.SetParent(vfxRoot.transform, false);
            return rootObject.transform;
        }

        private void ResolveRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && !hasBaseRendererColor)
            {
                baseRendererColor = spriteRenderer.color;
                hasBaseRendererColor = true;
            }
        }

        private static PlayerConcealmentState ResolvePlayerConcealment(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            PlayerConcealmentState concealment = collider.GetComponent<PlayerConcealmentState>();
            if (concealment != null)
            {
                return concealment;
            }

            concealment = collider.GetComponentInParent<PlayerConcealmentState>();
            if (concealment != null)
            {
                return concealment;
            }

            GameObject playerObject = null;
            try
            {
                playerObject = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                // Tag setup may not be complete in edit-time context.
            }

            if (playerObject == null)
            {
                playerObject = collider.gameObject;
            }

            concealment = playerObject.GetComponent<PlayerConcealmentState>();
            if (concealment == null)
            {
                concealment = playerObject.AddComponent<PlayerConcealmentState>();
            }

            return concealment;
        }

        private void OnDisable()
        {
            if (activePlayerConcealment != null)
            {
                activePlayerConcealment.ExitSafeHaven();
                activePlayerConcealment = null;
            }

            nextUnsafeDreadTime = float.PositiveInfinity;
            ResetOverstayPressure();
            if (spriteRenderer != null && hasBaseRendererColor)
            {
                spriteRenderer.color = baseRendererColor;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.85f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
