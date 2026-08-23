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

        [SerializeField] private int value = 1;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseScale = 0.08f;

        private const float PulseCullDistance = 18f;

        private Vector3 initialScale;
        private SpriteRenderer bodyRenderer;
        private Color baseColor = Color.white;
        private float releaseGlow01;
        private Coroutine releaseGlowRoutine;
        private static float sharedPulseWave;
        private static int sharedPulseFrame = -1;

        public int Value => value;

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

        public void PlayReleaseTrailGlow(float durationSeconds)
        {
            if (!isActiveAndEnabled)
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

            if (releaseGlow01 <= 0f && ShouldSkipDistantPulse())
            {
                return;
            }

            float wave = SharedPulseWave(pulseSpeed) * pulseScale;
            transform.localScale = initialScale * (1f + wave + releaseGlow01 * 0.14f);
            if (bodyRenderer != null && releaseGlow01 > 0f)
            {
                Color glow = new Color(1f, 0.86f, 0.22f, 1f);
                bodyRenderer.color = Color.Lerp(baseColor, glow, releaseGlow01);
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
    }
}



