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
        private SpriteRenderer doorGlowRenderer;
        private SpriteRenderer windowGlowRenderer;
        private Vector3 baseScale;
        private bool houseThresholdHint;
        private float houseHintStartedAt = -1f;
        private static Sprite houseGlowSprite;

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
                    baseAlpha = Mathf.Lerp(0.16f, 0.28f, house01);
                    alphaPulse = Mathf.Lerp(0.04f, 0.08f, house01);
                }

                Color color = beaconRenderer.color;
                color.a = Mathf.Clamp01(baseAlpha + Mathf.Sin(Time.time * (pulseSpeed * 0.9f)) * alphaPulse);
                beaconRenderer.color = color;
            }

            if (houseThresholdHint)
            {
                ApplyColor();
                PulseHouseOpenings(house01);
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
            if (enabled)
            {
                EnsureHouseGlowParts();
            }

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

            PulseHouseOpenings(EvaluateHouseHint01());
        }

        private Color EvaluatePortalColor()
        {
            if (!houseThresholdHint)
            {
                return unlocked ? unlockedColor : lockedColor;
            }

            // Painted cottage art already carries door/window light; keep tint white.
            if (MapReadableArt.TryGetHouseThresholdExitSprite() != null)
            {
                return Color.white;
            }

            float house01 = EvaluateHouseHint01();
            Color cottage = new(0.28f, 0.14f, 0.08f, 0.96f);
            Color litCottage = new(0.42f, 0.2f, 0.1f, 0.98f);
            return Color.Lerp(cottage, litCottage, house01);
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
            return new Color(1f, 0.62f, 0.2f, Mathf.Lerp(0.1f, 0.2f, house01));
        }

        private void EnsureHouseGlowParts()
        {
            Sprite glowSprite = GetHouseGlowSprite();
            if (spriteRenderer != null && glowSprite != null)
            {
                spriteRenderer.sprite = glowSprite;
            }

            if (beaconRenderer != null)
            {
                beaconRenderer.transform.localPosition = new Vector3(0f, -0.18f, 0f);
                beaconRenderer.transform.localScale = new Vector3(1.8f, 0.55f, 1f);
                if (glowSprite != null)
                {
                    beaconRenderer.sprite = glowSprite;
                }
            }

            doorGlowRenderer = EnsureGlowChild("ExitHouse_Door", new Vector3(0f, -0.08f, 0f), new Vector3(0.34f, 0.58f, 1f), 122);
            windowGlowRenderer = EnsureGlowChild("ExitHouse_Window", new Vector3(0.22f, 0.18f, 0f), new Vector3(0.2f, 0.2f, 1f), 123);
        }

        private SpriteRenderer EnsureGlowChild(string childName, Vector3 localPosition, Vector3 localScale, int sortingOrder)
        {
            Transform existing = transform.Find(childName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = GetHouseGlowSprite();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void PulseHouseOpenings(float house01)
        {
            if (!houseThresholdHint)
            {
                return;
            }

            float breath = 0.5f + Mathf.Sin(Time.time * 2.1f) * 0.5f;
            float doorAlpha = Mathf.Lerp(0.42f, 0.92f, house01) * Mathf.Lerp(0.82f, 1f, breath);
            float windowAlpha = Mathf.Lerp(0.55f, 1f, house01) * Mathf.Lerp(0.75f, 1f, breath);
            if (doorGlowRenderer != null)
            {
                doorGlowRenderer.color = new Color(1f, 0.72f, 0.28f, doorAlpha);
            }

            if (windowGlowRenderer != null)
            {
                windowGlowRenderer.color = new Color(1f, 0.86f, 0.42f, windowAlpha);
            }
        }

        private static Sprite GetHouseGlowSprite()
        {
            if (houseGlowSprite != null)
            {
                return houseGlowSprite;
            }

            Sprite artSprite = MapReadableArt.TryGetHouseThresholdExitSprite();
            if (artSprite != null)
            {
                houseGlowSprite = artSprite;
                return houseGlowSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "HouseGlowTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            houseGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            houseGlowSprite.name = "HouseGlowSprite";
            houseGlowSprite.hideFlags = HideFlags.HideAndDontSave;
            return houseGlowSprite;
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