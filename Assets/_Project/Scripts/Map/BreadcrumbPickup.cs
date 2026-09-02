using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class BreadcrumbPickup : MonoBehaviour
    {
        public event Action<BreadcrumbPickup> Collected;
        public event Action<BreadcrumbPickup> ErasedByForest;

        [SerializeField] private int value = 1;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseScale = 0.08f;

        private const float PulseCullDistance = 18f;
        private const int FaintTrailEraseStage = 4;
        private const float FaintTrailLifetimeMin = 11f;
        private const float FaintTrailLifetimeMax = 17f;
        private const float ForestLickInterval = 3.6f;
        private const float ForestLickSeconds = 2.1f;
        private const float ForestEraseFadeSeconds = 0.85f;

        private Vector3 initialScale;
        private Vector3 restLocalPosition;
        private SpriteRenderer bodyRenderer;
        private Color baseColor = Color.white;
        private float releaseGlow01;
        private Coroutine releaseGlowRoutine;
        private float faintLifeRemaining = -1f;
        private bool forestEraseStarted;
        private static float sharedPulseWave;
        private static int sharedPulseFrame = -1;
        private static float nextForestLickTime;

        public int Value => value;
        public bool IsCorrupted { get; private set; }
        public bool IsDense { get; private set; }

        private static readonly List<BreadcrumbPickup> activePickups = new(16);

        public static void CopyActivePickups(List<BreadcrumbPickup> output)
        {
            CopyActive(activePickups, output);
        }

        private static void CopyActive<T>(List<T> source, List<T> output) where T : Behaviour
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            for (int i = source.Count - 1; i >= 0; i--)
            {
                T item = source[i];
                if (item == null)
                {
                    source.RemoveAt(i);
                    continue;
                }

                if (!item.isActiveAndEnabled)
                {
                    continue;
                }

                output.Add(item);
            }
        }

        public static void PulseActiveTrail(float durationSeconds)
        {
            float duration = Mathf.Clamp(durationSeconds, 1f, 1.5f);
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup == null)
                {
                    activePickups.RemoveAt(i);
                    continue;
                }

                pickup.PlayReleaseTrailGlow(duration);
            }
        }

        public static void TryDensifyNear(Vector3 origin, float radius)
        {
            float safeRadius = Mathf.Max(0.2f, radius);
            float radiusSqr = safeRadius * safeRadius;
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup == null)
                {
                    activePickups.RemoveAt(i);
                    continue;
                }

                if (pickup.IsCorrupted || pickup.IsDense)
                {
                    continue;
                }

                if (((Vector2)pickup.transform.position - (Vector2)origin).sqrMagnitude <= radiusSqr)
                {
                    pickup.MarkDense();
                }
            }
        }

        public static void TickForestLick()
        {
            if (RegressionChecklistRunner.IsRegressionRunActive
                || Time.timeScale <= 0.0001f
                || StageManager.ResolvedStageIndex < FaintTrailEraseStage)
            {
                return;
            }

            if (Time.time < nextForestLickTime)
            {
                return;
            }

            nextForestLickTime = Time.time + ForestLickInterval;
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                BreadcrumbPickup pickup = activePickups[i];
                if (pickup == null)
                {
                    activePickups.RemoveAt(i);
                    continue;
                }

                pickup.ApplyForestLick(ForestLickSeconds);
            }
        }

        public void ConfigureCorrupted(bool corrupted)
        {
            IsCorrupted = corrupted;
            if (!corrupted || bodyRenderer == null)
            {
                return;
            }

            faintLifeRemaining = -1f;
            forestEraseStarted = false;
            Sprite corruptedSprite = MapReadableArt.TryGetCorruptedBreadcrumbSprite();
            if (corruptedSprite != null)
            {
                bodyRenderer.sprite = corruptedSprite;
                baseColor = Color.white;
            }
            else
            {
                baseColor = new Color(0.62f, 0.38f, 0.86f, 0.78f);
            }

            bodyRenderer.color = baseColor;
            initialScale = Vector3.one * 0.42f;
            transform.localScale = initialScale;
        }

        public void MarkDense()
        {
            if (IsCorrupted)
            {
                return;
            }

            IsDense = true;
            faintLifeRemaining = -1f;
            forestEraseStarted = false;
            if (bodyRenderer != null)
            {
                Sprite denseSprite = MapReadableArt.TryGetBreadcrumbSprite();
                if (denseSprite != null)
                {
                    bodyRenderer.sprite = denseSprite;
                    baseColor = Color.white;
                }
                else
                {
                    baseColor = new Color(1f, 0.84f, 0.22f, 1f);
                }

                bodyRenderer.color = baseColor;
            }

            initialScale = Vector3.one * 0.58f;
            transform.localScale = initialScale;
        }

        public void PlayReleaseTrailGlow(float durationSeconds)
        {
            if (!isActiveAndEnabled || IsCorrupted)
            {
                return;
            }

            if (releaseGlowRoutine != null)
            {
                StopCoroutine(releaseGlowRoutine);
            }

            releaseGlowRoutine = StartCoroutine(ReleaseTrailGlowRoutine(Mathf.Clamp(durationSeconds, 1f, 1.5f)));
        }

        private void Awake()
        {
            initialScale = transform.localScale;
            restLocalPosition = transform.localPosition;
            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sortingOrder = 25;
            }

            Sprite breadSprite = MapReadableArt.TryGetBreadcrumbSprite();
            if (breadSprite != null)
            {
                bodyRenderer.sprite = breadSprite;
                bodyRenderer.color = Color.white;
            }

            baseColor = bodyRenderer.color;
        }

        private void OnEnable()
        {
            if (!activePickups.Contains(this))
            {
                activePickups.Add(this);
            }

            BeginFaintLifetimeIfNeeded();
        }

        private void OnDisable()
        {
            activePickups.Remove(this);
            releaseGlowRoutine = null;
            releaseGlow01 = 0f;
            if (bodyRenderer != null)
            {
                bodyRenderer.color = baseColor;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0001f)
            {
                return;
            }

            TickFaintErase();

            if (releaseGlow01 <= 0f && ShouldSkipDistantPulse())
            {
                return;
            }

            float wave = SharedPulseWave(pulseSpeed) * pulseScale;
            float denseLift = IsDense ? 0.1f : 0f;
            float faintShrink = !IsDense && !IsCorrupted && faintLifeRemaining >= 0f ? -0.08f : 0f;
            Vector3 wobble = Vector3.zero;
            if (IsCorrupted)
            {
                wobble = new Vector3(
                    Mathf.Sin(Time.time * 7.6f + transform.position.y) * 0.045f,
                    Mathf.Cos(Time.time * 5.8f + transform.position.x) * 0.03f,
                    0f);
            }

            transform.localScale = initialScale * (1f + wave + releaseGlow01 * 0.14f + denseLift + faintShrink);
            if (IsCorrupted)
            {
                transform.localPosition = restLocalPosition + wobble;
            }
            if (bodyRenderer != null)
            {
                Color drawn = baseColor;
                if (releaseGlow01 > 0f && !IsCorrupted)
                {
                    Color glow = new Color(1f, 0.86f, 0.22f, 1f);
                    drawn = Color.Lerp(baseColor, glow, releaseGlow01);
                }

                if (IsCorrupted)
                {
                    float flicker = 0.42f + Mathf.Abs(Mathf.Sin(Time.time * 13.2f)) * 0.58f;
                    drawn.a *= flicker;
                    drawn = Color.Lerp(drawn, new Color(0.28f, 0.22f, 0.4f, drawn.a), 0.35f);
                }
                else if (!IsDense && faintLifeRemaining >= 0f)
                {
                    drawn.a *= Mathf.Lerp(0.22f, 0.7f, Mathf.Clamp01(faintLifeRemaining / FaintTrailLifetimeMax));
                }

                if (forestEraseStarted && faintLifeRemaining >= 0f)
                {
                    drawn.a *= Mathf.Clamp01(faintLifeRemaining / ForestEraseFadeSeconds);
                }

                bodyRenderer.color = drawn;
            }
        }

        private static float SharedPulseWave(float speed)
        {
            int frame = Time.frameCount;
            if (sharedPulseFrame != frame)
            {
                sharedPulseFrame = frame;
                sharedPulseWave = Mathf.Sin(Time.time * speed);
            }

            return sharedPulseWave;
        }

        private bool ShouldSkipDistantPulse()
        {
            Vector2 pos = transform.position;
            PlayerDummyController player = PlayerDummyController.ActiveInstance;
            if (player != null)
            {
                Vector2 delta = (Vector2)player.transform.position - pos;
                if (delta.sqrMagnitude > PulseCullDistance * PulseCullDistance)
                {
                    return true;
                }
            }

            FogOfWarSystem fog = FogOfWarSystem.ActiveInstance;
            return fog != null && fog.IsWorldPositionHidden(pos);
        }

        private System.Collections.IEnumerator ReleaseTrailGlowRoutine(float duration)
        {
            float startedAt = Time.time;
            float safeDuration = Mathf.Max(0.1f, duration);
            while (Time.time < startedAt + safeDuration)
            {
                float t = Mathf.Clamp01((Time.time - startedAt) / safeDuration);
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
                float fall = 1f - Mathf.SmoothStep(0.55f, 1f, t);
                releaseGlow01 = Mathf.Clamp01(rise * fall);
                yield return null;
            }

            releaseGlow01 = 0f;
            if (bodyRenderer != null)
            {
                bodyRenderer.color = baseColor;
            }

            releaseGlowRoutine = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (!other.CompareTag("Player"))
            {
                return;
            }

            Collected?.Invoke(this);
            Destroy(gameObject);
        }

        private void BeginFaintLifetimeIfNeeded()
        {
            if (IsDense
                || IsCorrupted
                || forestEraseStarted
                || RegressionChecklistRunner.IsRegressionRunActive
                || StageManager.ResolvedStageIndex < FaintTrailEraseStage)
            {
                return;
            }

            if (faintLifeRemaining >= 0f)
            {
                return;
            }

            float hash = Mathf.Abs((transform.position.x * 12.7f) + (transform.position.y * 3.1f));
            float blend = Mathf.Repeat(hash, 1f);
            faintLifeRemaining = Mathf.Lerp(FaintTrailLifetimeMin, FaintTrailLifetimeMax, blend);
            initialScale = Vector3.one * 0.28f;
            transform.localScale = initialScale;
            if (bodyRenderer != null && !IsDense)
            {
                Sprite faintSprite = MapReadableArt.TryGetFaintBreadcrumbSprite();
                if (faintSprite != null)
                {
                    bodyRenderer.sprite = faintSprite;
                    baseColor = Color.white;
                }
                else
                {
                    baseColor = new Color(0.78f, 0.7f, 0.58f, 0.42f);
                }

                bodyRenderer.color = baseColor;
            }
        }

        private void ApplyForestLick(float seconds)
        {
            if (IsDense || IsCorrupted || RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            BeginFaintLifetimeIfNeeded();
            if (faintLifeRemaining < 0f)
            {
                return;
            }

            faintLifeRemaining = Mathf.Max(0f, faintLifeRemaining - Mathf.Max(0.1f, seconds));
        }

        private void TickFaintErase()
        {
            if (IsDense || IsCorrupted || faintLifeRemaining < 0f)
            {
                return;
            }

            faintLifeRemaining -= Time.deltaTime;
            if (faintLifeRemaining > 0f)
            {
                return;
            }

            if (!forestEraseStarted)
            {
                forestEraseStarted = true;
                faintLifeRemaining = ForestEraseFadeSeconds;
                return;
            }

            ErasedByForest?.Invoke(this);
            Destroy(gameObject);
        }
    }
}



