using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.AI.Learning
{
    [Serializable]
    public sealed class EnemyMemory
    {
        private readonly Dictionary<Vector2Int, float> hotspotScores = new();
        private readonly Queue<Vector2> playerSamples = new();

        public bool HasLastSeenPosition { get; private set; }
        public bool HasLastHeardPosition { get; private set; }
        public Vector2 LastSeenPosition { get; private set; }
        public Vector2 LastHeardPosition { get; private set; }

        public void Tick(float deltaTime, float decayPerSecond)
        {
            if (hotspotScores.Count == 0 || decayPerSecond <= 0f)
            {
                return;
            }

            float decay = Mathf.Clamp01(decayPerSecond * deltaTime);
            Vector2Int[] keys = hotspotScores.Keys.ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                Vector2Int key = keys[i];
                hotspotScores[key] *= 1f - decay;
                if (hotspotScores[key] < 0.01f)
                {
                    hotspotScores.Remove(key);
                }
            }
        }

        public void RecordSighting(Vector2 position, float cellSize)
        {
            LastSeenPosition = position;
            HasLastSeenPosition = true;
            AddHotspot(position, 1.2f, cellSize);
        }

        public void RecordNoise(Vector2 position, float weight, float cellSize)
        {
            LastHeardPosition = position;
            HasLastHeardPosition = true;
            AddHotspot(position, Mathf.Max(0.2f, weight), cellSize);
        }

        public void RecordPlayerSample(Vector2 position, int maxSamples, float cellSize)
        {
            maxSamples = Mathf.Max(3, maxSamples);
            playerSamples.Enqueue(position);
            AddHotspot(position, 0.4f, cellSize);

            while (playerSamples.Count > maxSamples)
            {
                playerSamples.Dequeue();
            }
        }

        public Vector2 PredictEscapeDirection(float minimumMagnitude = 0.15f)
        {
            if (playerSamples.Count < 2)
            {
                return Vector2.zero;
            }

            Vector2[] snapshot = playerSamples.ToArray();
            Vector2 direction = snapshot[snapshot.Length - 1] - snapshot[0];
            if (direction.magnitude < minimumMagnitude)
            {
                return Vector2.zero;
            }

            return direction.normalized;
        }

        public List<Vector2> GetPreferredSearchPoints(int count, float cellSize)
        {
            count = Mathf.Max(1, count);
            List<Vector2> points = hotspotScores
                .OrderByDescending(pair => pair.Value)
                .Take(count)
                .Select(pair => CellCenter(pair.Key, cellSize))
                .ToList();

            if (HasLastSeenPosition)
            {
                points.Insert(0, LastSeenPosition);
            }

            if (HasLastHeardPosition)
            {
                points.Insert(0, LastHeardPosition);
            }

            return points.Distinct().Take(count).ToList();
        }

        public string BuildDebugSummary(int maxEntries, float cellSize)
        {
            maxEntries = Mathf.Max(1, maxEntries);

            if (hotspotScores.Count == 0)
            {
                return "none";
            }

            IEnumerable<string> entries = hotspotScores
                .OrderByDescending(pair => pair.Value)
                .Take(maxEntries)
                .Select(pair =>
                {
                    Vector2 center = CellCenter(pair.Key, cellSize);
                    return $"({center.x:0.0}, {center.y:0.0})={pair.Value:0.00}";
                });

            return string.Join(", ", entries);
        }

        private void AddHotspot(Vector2 position, float value, float cellSize)
        {
            Vector2Int cell = ToCell(position, cellSize);
            if (hotspotScores.TryGetValue(cell, out float existing))
            {
                hotspotScores[cell] = existing + value;
            }
            else
            {
                hotspotScores[cell] = value;
            }
        }

        private static Vector2Int ToCell(Vector2 position, float cellSize)
        {
            float safeCellSize = Mathf.Max(0.1f, cellSize);
            return new Vector2Int(
                Mathf.RoundToInt(position.x / safeCellSize),
                Mathf.RoundToInt(position.y / safeCellSize));
        }

        private static Vector2 CellCenter(Vector2Int cell, float cellSize)
        {
            float safeCellSize = Mathf.Max(0.1f, cellSize);
            return new Vector2(cell.x * safeCellSize, cell.y * safeCellSize);
        }
    }
}
