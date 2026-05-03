using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Managers
{
    public enum NoiseKind
    {
        Footstep,
        Echo,
        FlashlightToggle,
        ItemUse,
        Decoy
    }

    public readonly struct NoiseEvent
    {
        public NoiseEvent(Vector2 position, float loudness, float radius, NoiseKind kind, GameObject source)
        {
            Position = position;
            Loudness = Mathf.Max(0f, loudness);
            Radius = Mathf.Max(0f, radius);
            Kind = kind;
            Source = source;
            TimeStamp = Time.time;
        }

        public Vector2 Position { get; }
        public float Loudness { get; }
        public float Radius { get; }
        public NoiseKind Kind { get; }
        public GameObject Source { get; }
        public float TimeStamp { get; }
    }

    [Serializable]
    public struct RecentNoiseRecord
    {
        public Vector2 position;
        public float loudness;
        public float radius;
        public NoiseKind kind;
        public float expiryTime;
    }

    public sealed class NoiseManager : ManagerBase
    {
        public static NoiseManager Instance { get; private set; }
        public static event Action<NoiseEvent> NoiseRaised;

        [Header("Debug")]
        [SerializeField, Min(1)] private int maxRecentEntries = 24;
        [SerializeField, Min(0.1f)] private float debugLifetime = 2.5f;

        private readonly List<RecentNoiseRecord> recentRecords = new();

        public IReadOnlyList<RecentNoiseRecord> RecentRecords => recentRecords;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (recentRecords.Count == 0)
            {
                return;
            }

            float now = Time.time;
            for (int i = recentRecords.Count - 1; i >= 0; i--)
            {
                if (recentRecords[i].expiryTime <= now)
                {
                    recentRecords.RemoveAt(i);
                }
            }
        }

        public void EmitNoise(Vector2 position, float loudness, float radius, NoiseKind kind, GameObject source = null)
        {
            NoiseEvent noiseEvent = new(position, loudness, radius, kind, source);
            AddDebugRecord(noiseEvent);
            NoiseRaised?.Invoke(noiseEvent);
        }

        public void EmitNoise(Vector2 position, float loudness, NoiseKind kind, GameObject source = null)
        {
            float inferredRadius = Mathf.Max(1f, loudness * 3f);
            EmitNoise(position, loudness, inferredRadius, kind, source);
        }

        private void AddDebugRecord(NoiseEvent noiseEvent)
        {
            if (recentRecords.Count >= maxRecentEntries)
            {
                recentRecords.RemoveAt(0);
            }

            recentRecords.Add(new RecentNoiseRecord
            {
                position = noiseEvent.Position,
                loudness = noiseEvent.Loudness,
                radius = noiseEvent.Radius,
                kind = noiseEvent.Kind,
                expiryTime = Time.time + debugLifetime
            });
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < recentRecords.Count; i++)
            {
                RecentNoiseRecord record = recentRecords[i];
                Gizmos.color = GetNoiseColor(record.kind);
                Gizmos.DrawWireSphere(record.position, record.radius);
            }
        }

        private static Color GetNoiseColor(NoiseKind kind)
        {
            return kind switch
            {
                NoiseKind.Echo => new Color(0.2f, 0.6f, 1f),
                NoiseKind.FlashlightToggle => new Color(1f, 0.8f, 0.2f),
                NoiseKind.ItemUse => new Color(1f, 0.4f, 0.2f),
                NoiseKind.Decoy => new Color(1f, 0.2f, 0.8f),
                _ => new Color(0.7f, 0.9f, 1f)
            };
        }
    }
}
