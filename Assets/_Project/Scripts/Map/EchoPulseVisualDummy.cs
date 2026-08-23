using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class EchoPulseVisualDummy : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 3f;
        [SerializeField] private Color ringColor = new(0.36f, 0.78f, 1f, 0.78f);
        [SerializeField, Min(0.1f)] private float ringDuration = 1.85f;
        [SerializeField, Range(1, 4)] private int ringCount = 3;
        [SerializeField, Min(0f)] private float ringInterval = 0.34f;
        [SerializeField, Min(0f)] private float startRadius = 0.2f;
        [SerializeField, Range(0.5f, 3f)] private float expansionEase = 1.45f;
        [SerializeField, Range(0f, 0.6f)] private float lingeringAlpha = 0.12f;
        [SerializeField, Range(0f, 0.4f)] private float flickerStrength = 0.08f;
        [SerializeField] private int sortingOrder = 36;

        private readonly List<SpriteRenderer> rings = new();
        private float spawnTime;
        private float despawnTime;

        private static Sprite ringSprite;

        public static Sprite SharedRingSprite => GetRingSprite();

        public void Configure(float targetRadius, Color color, float duration, int count, float interval, int order)
        {
            radius = Mathf.Max(0.3f, targetRadius);
            ringColor = color;
            ringDuration = Mathf.Max(0.1f, duration);
            ringCount = Mathf.Clamp(count, 1, 4);
            ringInterval = Mathf.Clamp(interval, 0f, ringDuration * 0.8f);
            sortingOrder = order;

            BuildRings();
            if (isActiveAndEnabled)
            {
                ResetLifetime();
            }
        }

        private void Awake()
        {
            BuildRings();
        }

        private void OnEnable()
        {
            ResetLifetime();
        }

        private void ResetLifetime()
        {
            spawnTime = Time.time;
            despawnTime = spawnTime + ringDuration + ringInterval * Mathf.Max(0, ringCount - 1) + 0.08f;
        }

        private void Update()
        {
            if (rings.Count == 0)
            {
                return;
            }

            bool anyActive = false;
            float elapsed = Time.time - spawnTime;

            int activeRingCount = Mathf.Min(ringCount, rings.Count);
            for (int i = 0; i < activeRingCount; i++)
            {
                SpriteRenderer ring = rings[i];
                if (ring == null)
                {
                    continue;
                }

                float localTime = elapsed - ringInterval * i;
                if (localTime < 0f || localTime > ringDuration)
                {
                    ring.enabled = false;
                    continue;
                }

                anyActive = true;
                ring.enabled = true;

                float t = Mathf.Clamp01(localTime / Mathf.Max(0.01f, ringDuration));
                float easedT = Mathf.SmoothStep(0f, 1f, Mathf.Pow(t, Mathf.Max(0.5f, expansionEase)));
                float currentRadius = Mathf.Lerp(startRadius, radius, easedT);
                float diameter = currentRadius * 2f;
                ring.transform.localScale = new Vector3(diameter, diameter, 1f);

                Color color = ringColor;
                float breath = 1f - flickerStrength + Mathf.Sin((Time.time + i * 0.37f) * 9.2f) * flickerStrength;
                float tail = Mathf.Lerp(1f, lingeringAlpha, Mathf.SmoothStep(0.08f, 1f, t));
                color.a *= Mathf.Clamp01(tail * breath);
                ring.color = color;
            }

            for (int i = activeRingCount; i < rings.Count; i++)
            {
                if (rings[i] != null)
                {
                    rings[i].enabled = false;
                }
            }

            if (!anyActive && Time.time >= despawnTime)
            {
                Destroy(gameObject);
            }
        }

        private void BuildRings()
        {
            while (rings.Count < ringCount)
            {
                int index = rings.Count;
                GameObject ringObject = new($"Ring_{index:00}");
                ringObject.transform.SetParent(transform, false);

                SpriteRenderer renderer = ringObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetRingSprite();
                renderer.sortingOrder = sortingOrder - index;
                renderer.color = ringColor;
                renderer.enabled = false;
                rings.Add(renderer);
            }

            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null)
                {
                    continue;
                }

                rings[i].sortingOrder = sortingOrder - i;
                rings[i].color = ringColor;
                rings[i].enabled = false;
            }
        }

        private static Sprite GetRingSprite()
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }

            const int size = 128;
            const float outerRadius = 0.48f;
            const float ringHalfThickness = 0.06f;
            const float feather = 0.03f;

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "EchoPulseRingTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedDistance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float edgeDistance = Mathf.Abs(normalizedDistance - outerRadius);
                    float alpha = 1f - Mathf.InverseLerp(ringHalfThickness, ringHalfThickness + feather, edgeDistance);
                    alpha = Mathf.Clamp01(alpha);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    texture.SetPixel(x, y, new Color32(255, 255, 255, a));
                }
            }

            texture.Apply();

            ringSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            ringSprite.name = "EchoPulseRingSprite";
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }
    }
}
