using System;
using System.Collections.Generic;
using LostBreadcrumbs.Runtime.Managers;
using LostBreadcrumbs.Runtime.Player;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class ExitPortalDummy : MonoBehaviour
    {
        public event Action PlayerEntered;

        [SerializeField] private bool unlocked;
        [SerializeField] private Color lockedColor = new(1f, 0.25f, 0.25f, 0.9f);
        [SerializeField] private Color unlockedColor = new(0.2f, 1f, 0.5f, 0.9f);
        [SerializeField, Min(0f)] private float lockedPulseScale = 0.05f;
        [SerializeField, Min(0f)] private float unlockedPulseScale = 0.12f;
        [SerializeField, Min(0.2f)] private float lockedPulseSpeed = 2.4f;
        [SerializeField, Min(0.2f)] private float unlockedPulseSpeed = 4.1f;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer beaconRenderer;
        private Vector3 baseScale;
        private bool houseThresholdHint;
        private float houseHintStartedAt = -1f;

        public bool IsUnlocked => unlocked;

        private static readonly List<ExitPortalDummy> activePortals = new(4);

        public static void CopyActivePortals(List<ExitPortalDummy> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            for (int i = activePortals.Count - 1; i >= 0; i--)
            {
                ExitPortalDummy portal = activePortals[i];
                if (portal == null)
                {
                    activePortals.RemoveAt(i);
                    continue;
                }

                if (!portal.isActiveAndEnabled)
                {
                    continue;
                }

                output.Add(portal);
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Transform beaconTransform = transform.Find("ExitPortal_Beacon");
            if (beaconTransform != null)
            {
                beaconRenderer = beaconTransform.GetComponent<SpriteRenderer>();
            }

            baseScale = transform.localScale;
            ApplyColor();
        }

        private void OnEnable()
        {
            if (!activePortals.Contains(this))
            {
                activePortals.Add(this);
            }
        }

        private void OnDisable()
        {
            activePortals.Remove(this);
        }

        private void Update()
        {
            float house01 = EvaluateHouseHint01();
            float pulseSpeed = unlocked ? unlockedPulseSpeed : lockedPulseSpeed;
            float pulseScale = unlocked ? unlockedPulseScale : lockedPulseScale;
            float houseScale = houseThresholdHint ? Mathf.Lerp(1f, 1.18f, house01) : 1f;
            float pulse = (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale) * houseScale;
            transform.localScale = baseScale * pulse;

            if (beaconRenderer != null)
            {
                float baseAlpha = unlocked ? 0.3f : 0.22f;
                float alphaPulse = unlocked ? 0.12f : 0.08f;
                if (houseThresholdHint)
                {
                    baseAlpha = Mathf.Lerp(0.24f, 0.52f, house01);
                    alphaPulse = Mathf.Lerp(0.08f, 0.18f, house01);
                }

                Color color = beaconRenderer.color;
                color.a = Mathf.Clamp01(baseAlpha + Mathf.Sin(Time.time * (pulseSpeed * 0.9f)) * alphaPulse);
                beaconRenderer.color = color;
            }

            if (houseThresholdHint)
            {
                ApplyColor();
            }
        }

        public void SetUnlocked(bool isUnlocked)
        {
            unlocked = isUnlocked;
            ApplyColor();
        }

        public void SetHouseThresholdHint(bool enabled)
        {
            houseThresholdHint = enabled;
            houseHintStartedAt = enabled ? Time.time : -1f;
            ApplyColor();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (RegressionChecklistRunner.IsRegressionRunActive)
            {
                return;
            }

            if (!unlocked)
            {
                return;
            }

            if (!IsPlayerCollider(other))
            {
                return;
            }

            PlayerEntered?.Invoke();
        }

        private static bool IsPlayerCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            try
            {
                if (other.CompareTag("Player"))
                {
                    return true;
                }
            }
            catch (UnityException)
            {
                // Tag might not exist in project settings.
            }

            return other.GetComponentInParent<PlayerDummyController>() != null;
        }

        private void ApplyColor()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = EvaluatePortalColor();
            }

            if (beaconRenderer != null)
            {
                beaconRenderer.color = EvaluateBeaconColor();
            }
        }

        private Color EvaluatePortalColor()
        {
            if (!houseThresholdHint)
            {
                return unlocked ? unlockedColor : lockedColor;
            }

            float house01 = EvaluateHouseHint01();
            Color houseLocked = new(1f, 0.62f, 0.22f, 0.95f);
            Color houseUnlocked = new(1f, 0.82f, 0.38f, 0.98f);
            Color from = unlocked ? unlockedColor : lockedColor;
            Color to = unlocked ? houseUnlocked : houseLocked;
            return Color.Lerp(from, to, house01);
        }

        private Color EvaluateBeaconColor()
        {
            if (!houseThresholdHint)
            {
                return unlocked
                    ? new Color(0.35f, 1f, 0.58f, 0.3f)
                    : new Color(1f, 0.52f, 0.18f, 0.24f);
            }

            float house01 = EvaluateHouseHint01();
            Color warm = new(1f, 0.72f, 0.28f, Mathf.Lerp(0.28f, 0.55f, house01));
            return warm;
        }

        private float EvaluateHouseHint01()
        {
            if (!houseThresholdHint || houseHintStartedAt < 0f)
            {
                return 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - houseHintStartedAt) / 36f));
        }
    }
}