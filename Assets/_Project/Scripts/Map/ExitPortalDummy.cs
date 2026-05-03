using System;
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

        public bool IsUnlocked => unlocked;

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

        private void Update()
        {
            float pulseSpeed = unlocked ? unlockedPulseSpeed : lockedPulseSpeed;
            float pulseScale = unlocked ? unlockedPulseScale : lockedPulseScale;
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            transform.localScale = baseScale * pulse;

            if (beaconRenderer != null)
            {
                float baseAlpha = unlocked ? 0.3f : 0.22f;
                float alphaPulse = unlocked ? 0.12f : 0.08f;
                Color color = beaconRenderer.color;
                color.a = Mathf.Clamp01(baseAlpha + Mathf.Sin(Time.time * (pulseSpeed * 0.9f)) * alphaPulse);
                beaconRenderer.color = color;
            }
        }

        public void SetUnlocked(bool isUnlocked)
        {
            unlocked = isUnlocked;
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
                spriteRenderer.color = unlocked ? unlockedColor : lockedColor;
            }

            if (beaconRenderer != null)
            {
                Color beaconColor = unlocked
                    ? new Color(0.35f, 1f, 0.58f, 0.3f)
                    : new Color(1f, 0.52f, 0.18f, 0.24f);
                beaconRenderer.color = beaconColor;
            }
        }
    }
}