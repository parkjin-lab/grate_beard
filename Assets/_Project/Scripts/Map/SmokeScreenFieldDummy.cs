using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public sealed class SmokeScreenFieldDummy : MonoBehaviour
    {
        private static readonly List<SmokeScreenFieldDummy> ActiveFields = new();

        [SerializeField, Min(0.5f)] private float radius = 2.4f;
        [SerializeField, Min(0.2f)] private float lifetimeSeconds = 5.5f;
        [SerializeField, Range(0f, 1f)] private float visionBlockStrength = 0.82f;
        [SerializeField, Range(0f, 0.95f)] private float noiseDampenStrength = 0.42f;
        [SerializeField, Min(0.1f)] private float breatheSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float minScale = 0.88f;
        [SerializeField, Min(0.1f)] private float maxScale = 1.12f;
        [SerializeField] private Color smokeColor = new(0.62f, 0.66f, 0.73f, 0.58f);

        private float despawnTime;
        private SpriteRenderer spriteRenderer;

        public float Radius => Mathf.Max(0.01f, radius);
        public float VisionBlockStrength => Mathf.Clamp01(visionBlockStrength);
        public float NoiseDampenStrength => Mathf.Clamp(noiseDampenStrength, 0f, 0.95f);

        public void Configure(float configuredRadius, float configuredLifetime, float configuredVisionBlockStrength)
        {
            radius = Mathf.Max(0.5f, configuredRadius);
            lifetimeSeconds = Mathf.Max(0.2f, configuredLifetime);
            visionBlockStrength = Mathf.Clamp01(configuredVisionBlockStrength);
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.sprite == null)
            {
                Sprite puffSprite = MapReadableArt.TryGetSmokeSprite();
                if (puffSprite != null)
                {
                    spriteRenderer.sprite = puffSprite;
                    spriteRenderer.color = new Color(1f, 1f, 1f, smokeColor.a);
                    return;
                }
            }

            // Prefer not to re-tint painted mist that was already assigned with white RGB.
            if (spriteRenderer.sprite != null
                && spriteRenderer.sprite.name == "ForestEchoSmokePuff")
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, smokeColor.a);
                return;
            }

            spriteRenderer.color = smokeColor;
        }

        private void OnEnable()
        {
            if (!ActiveFields.Contains(this))
            {
                ActiveFields.Add(this);
            }

            despawnTime = Time.time + lifetimeSeconds;
        }

        private void OnDisable()
        {
            ActiveFields.Remove(this);
        }

        private void Update()
        {
            if (Time.time >= despawnTime)
            {
                Destroy(gameObject);
                return;
            }

            TickVisual();
        }

        private void TickVisual()
        {
            float breathe = 0.5f + Mathf.Sin(Time.time * breatheSpeed) * 0.5f;
            float scale = Mathf.Lerp(minScale, maxScale, breathe);
            transform.localScale = Vector3.one * (Radius * 2f * scale);
        }

        public static float EvaluateVisionBlock(Vector2 observer, Vector2 target)
        {
            if (ActiveFields.Count == 0)
            {
                return 0f;
            }

            float strongestBlock = 0f;
            for (int i = ActiveFields.Count - 1; i >= 0; i--)
            {
                SmokeScreenFieldDummy field = ActiveFields[i];
                if (field == null)
                {
                    ActiveFields.RemoveAt(i);
                    continue;
                }

                if (!field.enabled || !field.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 center = field.transform.position;
                if (!IntersectsSegment(observer, target, center, field.Radius))
                {
                    continue;
                }

                strongestBlock = Mathf.Max(strongestBlock, field.VisionBlockStrength);
            }

            return strongestBlock;
        }

        public static float EvaluateNoiseDampenAt(Vector2 point)
        {
            if (ActiveFields.Count == 0)
            {
                return 0f;
            }

            float strongestDampen = 0f;
            for (int i = ActiveFields.Count - 1; i >= 0; i--)
            {
                SmokeScreenFieldDummy field = ActiveFields[i];
                if (field == null)
                {
                    ActiveFields.RemoveAt(i);
                    continue;
                }

                if (!field.enabled || !field.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = Vector2.Distance(point, field.transform.position);
                if (distance > field.Radius)
                {
                    continue;
                }

                strongestDampen = Mathf.Max(strongestDampen, field.NoiseDampenStrength);
            }

            return strongestDampen;
        }

        public static float EvaluateNoiseMultiplierAt(Vector2 point)
        {
            float dampen = EvaluateNoiseDampenAt(point);
            return Mathf.Max(0.12f, 1f - dampen);
        }

        private static bool IntersectsSegment(Vector2 a, Vector2 b, Vector2 center, float checkRadius)
        {
            Vector2 ab = b - a;
            float abLengthSq = ab.sqrMagnitude;
            if (abLengthSq <= 0.0001f)
            {
                return Vector2.Distance(a, center) <= checkRadius;
            }

            float t = Mathf.Clamp01(Vector2.Dot(center - a, ab) / abLengthSq);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(closest, center) <= checkRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.72f, 0.78f, 0.84f, 0.72f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
