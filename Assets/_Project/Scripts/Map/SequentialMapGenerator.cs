using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public static class SequentialMapGenerator
    {
        public static List<GeneratedMapCell> Generate(int stageIndex, SequentialMapConfig config, int variationSalt = 0)
        {
            if (config == null)
            {
                return new List<GeneratedMapCell>();
            }

            int safeStage = Mathf.Max(1, stageIndex);
            int seed = config.useFixedSeed
                ? config.fixedSeed + safeStage * 97 + variationSalt * 7919
                : Environment.TickCount + safeStage * 7919 + variationSalt * 104729;

            System.Random random = new(seed);
            return GenerateInternal(safeStage, config, random);
        }

        private static List<GeneratedMapCell> GenerateInternal(int stageIndex, SequentialMapConfig config, System.Random random)
        {
            List<GeneratedMapCell> cells = new();
            HashSet<Vector2Int> occupied = new();

            Vector2Int current = Vector2Int.zero;
            Vector2Int direction = Vector2Int.up;
            int order = 0;
            int forkCount = 0;

            AddCell(cells, occupied, current, MapCellKind.Start, true, ref order);

            int mainPathLength = config.baseMainPathLength + (stageIndex - 1) * config.mainPathIncreasePerStage;
            mainPathLength = Mathf.Max(4, mainPathLength);

            for (int step = 1; step <= mainPathLength; step++)
            {
                if (Roll(random, config.turnChance))
                {
                    direction = Roll(random, 0.5f) ? RotateLeft(direction) : RotateRight(direction);
                }

                if (!TryPickNextCell(current, direction, occupied, config.maxGenerationRadius, random, out Vector2Int next, out Vector2Int usedDirection))
                {
                    break;
                }

                current = next;
                direction = usedDirection;

                MapCellKind kind = SelectMainPathKind(step, mainPathLength, stageIndex, config, random);
                AddCell(cells, occupied, current, kind, true, ref order);

                bool canFork = step < mainPathLength - 1 && forkCount < config.maxForkCount;
                if (canFork && Roll(random, config.forkChance))
                {
                    GenerateBranch(cells, occupied, current, direction, stageIndex, config, random, ref order);
                    forkCount++;

                    if (kind == MapCellKind.Corridor)
                    {
                        ReplaceLastMainPathCell(cells, MapCellKind.Fork);
                    }
                }
            }

            if (cells.Count > 0)
            {
                int lastMainPathIndex = FindLastMainPathIndex(cells);
                if (lastMainPathIndex >= 0)
                {
                    GeneratedMapCell cell = cells[lastMainPathIndex];
                    cell.kind = MapCellKind.Exit;
                    cells[lastMainPathIndex] = cell;
                }
            }

            EnsureMinimumExitDistance(cells, occupied, stageIndex, config, random, ref order);
            EnsureAtLeastOneHideout(cells, random);
            ExpandSpatialFootprint(cells, occupied, stageIndex, config, random, ref order);
            return cells;
        }

        private static void GenerateBranch(
            List<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            Vector2Int branchStart,
            Vector2Int mainDirection,
            int stageIndex,
            SequentialMapConfig config,
            System.Random random,
            ref int order)
        {
            int minLength = Mathf.Max(1, config.minBranchLength);
            int maxLength = Mathf.Max(minLength, config.maxBranchLength + (stageIndex > config.lateStageStart ? 1 : 0));
            int targetLength = random.Next(minLength, maxLength + 1);

            Vector2Int direction = Roll(random, 0.5f) ? RotateLeft(mainDirection) : RotateRight(mainDirection);
            Vector2Int current = branchStart;

            for (int i = 0; i < targetLength; i++)
            {
                if (!TryPickNextCell(current, direction, occupied, config.maxGenerationRadius, random, out Vector2Int next, out Vector2Int usedDirection))
                {
                    break;
                }

                current = next;
                direction = usedDirection;

                MapCellKind kind = MapCellKind.Corridor;
                if (i == targetLength - 1 && Roll(random, config.branchHideoutChance))
                {
                    kind = MapCellKind.Hideout;
                }
                else if (Roll(random, Mathf.Lerp(config.riskChanceEarly, config.riskChanceLate, 0.5f)))
                {
                    kind = MapCellKind.Risk;
                }

                AddCell(cells, occupied, current, kind, false, ref order);
            }
        }

        private static MapCellKind SelectMainPathKind(int step, int maxStep, int stageIndex, SequentialMapConfig config, System.Random random)
        {
            if (step == maxStep)
            {
                return MapCellKind.Exit;
            }

            float progress = Mathf.Clamp01((float)step / Mathf.Max(1, maxStep));
            float riskChance = EvaluateRiskChance(stageIndex, config, progress);

            if (Roll(random, config.roomChance * (1f - progress * 0.35f)))
            {
                return MapCellKind.Room;
            }

            if (Roll(random, riskChance))
            {
                return MapCellKind.Risk;
            }

            return MapCellKind.Corridor;
        }

        private static void ExpandSpatialFootprint(
            List<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            int stageIndex,
            SequentialMapConfig config,
            System.Random random,
            ref int order)
        {
            bool expansionLikelyUninitialized = config.roomExpansionChance <= 0f
                                                && config.hideoutExpansionChance <= 0f
                                                && config.forkExpansionChance <= 0f
                                                && config.corridorExpansionChance <= 0f;

            bool shouldExpand = config.enableSpatialExpansion || expansionLikelyUninitialized;
            if (!shouldExpand || cells == null || cells.Count == 0)
            {
                return;
            }

            float roomChance = expansionLikelyUninitialized ? 0.92f : Mathf.Clamp01(config.roomExpansionChance);
            float hideoutChance = expansionLikelyUninitialized ? 0.88f : Mathf.Clamp01(config.hideoutExpansionChance);
            float forkChance = expansionLikelyUninitialized ? 0.42f : Mathf.Clamp01(config.forkExpansionChance);
            float corridorChance = expansionLikelyUninitialized ? 0.26f : Mathf.Clamp01(config.corridorExpansionChance);

            int minRadius = config.expansionMinRadius > 0 ? config.expansionMinRadius : 1;
            int maxRadiusRaw = config.expansionMaxRadius > 0 ? config.expansionMaxRadius : 2;
            int maxRadius = Mathf.Max(minRadius, maxRadiusRaw);
            int stageBonusInterval = config.stageExpansionBonusInterval > 0 ? config.stageExpansionBonusInterval : 3;
            int radiusBonus = Mathf.Max(0, (stageIndex - 1) / stageBonusInterval);
            int stageMaxRadius = Mathf.Max(minRadius, maxRadius + Mathf.Min(1, radiusBonus));
            int maxPerAnchor = config.maxExpansionCellsPerAnchor > 0 ? config.maxExpansionCellsPerAnchor : 8;
            int maxTotalBase = config.maxTotalExpansionCells > 0 ? config.maxTotalExpansionCells : 96;
            int maxTotal = Mathf.Max(maxPerAnchor, maxTotalBase + radiusBonus * 12);

            List<GeneratedMapCell> anchors = new(cells);
            Shuffle(anchors, random);

            int totalAdded = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (totalAdded >= maxTotal)
                {
                    break;
                }

                GeneratedMapCell anchor = anchors[i];
                if (!TryGetExpansionPlan(anchor, roomChance, hideoutChance, forkChance, corridorChance, maxPerAnchor, minRadius, stageMaxRadius,
                        out MapCellKind expansionKind,
                        out float chance,
                        out int anchorCellLimit,
                        out int radiusMin,
                        out int radiusMax))
                {
                    continue;
                }

                if (!Roll(random, chance))
                {
                    continue;
                }

                int radius = random.Next(radiusMin, radiusMax + 1);
                int added = ExpandAroundAnchor(cells, occupied, anchor.position, expansionKind, config.maxGenerationRadius, radius, anchorCellLimit, maxTotal, totalAdded, random, ref order);
                totalAdded += added;
            }
        }

        private static bool TryGetExpansionPlan(
            GeneratedMapCell anchor,
            float roomChance,
            float hideoutChance,
            float forkChance,
            float corridorChance,
            int maxPerAnchor,
            int minRadius,
            int maxRadius,
            out MapCellKind expansionKind,
            out float chance,
            out int anchorCellLimit,
            out int radiusMin,
            out int radiusMax)
        {
            expansionKind = MapCellKind.Corridor;
            chance = 0f;
            anchorCellLimit = maxPerAnchor;
            radiusMin = minRadius;
            radiusMax = maxRadius;

            switch (anchor.kind)
            {
                case MapCellKind.Room:
                    expansionKind = MapCellKind.Room;
                    chance = roomChance;
                    anchorCellLimit = maxPerAnchor;
                    return true;

                case MapCellKind.Hideout:
                    expansionKind = MapCellKind.Hideout;
                    chance = hideoutChance;
                    anchorCellLimit = Mathf.Max(2, Mathf.RoundToInt(maxPerAnchor * 0.85f));
                    return true;

                case MapCellKind.Fork:
                    expansionKind = MapCellKind.Room;
                    chance = forkChance;
                    anchorCellLimit = Mathf.Max(2, Mathf.RoundToInt(maxPerAnchor * 0.65f));
                    radiusMin = 1;
                    radiusMax = Mathf.Max(1, maxRadius - 1);
                    return true;

                case MapCellKind.Corridor:
                    expansionKind = MapCellKind.Corridor;
                    chance = corridorChance;
                    anchorCellLimit = Mathf.Max(1, Mathf.RoundToInt(maxPerAnchor * 0.35f));
                    radiusMin = 1;
                    radiusMax = 1;
                    return true;

                default:
                    return false;
            }
        }

        private static int ExpandAroundAnchor(
            List<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            Vector2Int anchor,
            MapCellKind kind,
            int maxGenerationRadius,
            int radius,
            int maxPerAnchor,
            int maxTotal,
            int currentTotal,
            System.Random random,
            ref int order)
        {
            int added = 0;
            List<Vector2Int> offsets = BuildExpansionOffsets(radius);
            Shuffle(offsets, random);
            offsets.Sort((a, b) => Manhattan(a).CompareTo(Manhattan(b)));

            for (int i = 0; i < offsets.Count; i++)
            {
                if (added >= maxPerAnchor || currentTotal + added >= maxTotal)
                {
                    break;
                }

                Vector2Int candidate = anchor + offsets[i];
                if (Mathf.Abs(candidate.x) > maxGenerationRadius || Mathf.Abs(candidate.y) > maxGenerationRadius)
                {
                    continue;
                }

                if (occupied.Contains(candidate))
                {
                    continue;
                }

                if (!HasCardinalNeighbor(candidate, occupied))
                {
                    continue;
                }

                AddCell(cells, occupied, candidate, kind, false, ref order);
                added++;
            }

            return added;
        }

        private static List<Vector2Int> BuildExpansionOffsets(int radius)
        {
            radius = Mathf.Max(1, radius);
            List<Vector2Int> offsets = new();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    int chebyshev = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    if (chebyshev > radius)
                    {
                        continue;
                    }

                    offsets.Add(new Vector2Int(x, y));
                }
            }

            return offsets;
        }

        private static int Manhattan(Vector2Int value)
        {
            return Mathf.Abs(value.x) + Mathf.Abs(value.y);
        }

        private static bool HasCardinalNeighbor(Vector2Int position, HashSet<Vector2Int> occupied)
        {
            return occupied.Contains(position + Vector2Int.up)
                   || occupied.Contains(position + Vector2Int.down)
                   || occupied.Contains(position + Vector2Int.left)
                   || occupied.Contains(position + Vector2Int.right);
        }

        private static float EvaluateRiskChance(int stageIndex, SequentialMapConfig config, float progress)
        {
            float stageFactor = Mathf.Clamp01((float)(stageIndex - 1) / Mathf.Max(1, config.lateStageStart - 1));
            float baseRisk = Mathf.Lerp(config.riskChanceEarly, config.riskChanceLate, stageFactor);
            return Mathf.Clamp01(baseRisk * Mathf.Lerp(0.65f, 1.25f, progress));
        }

        private static bool TryPickNextCell(
            Vector2Int current,
            Vector2Int preferredDirection,
            HashSet<Vector2Int> occupied,
            int maxRadius,
            System.Random random,
            out Vector2Int next,
            out Vector2Int usedDirection)
        {
            List<Vector2Int> candidates = new()
            {
                preferredDirection,
                RotateLeft(preferredDirection),
                RotateRight(preferredDirection),
                -preferredDirection
            };

            for (int i = 1; i < candidates.Count; i++)
            {
                int swapIndex = random.Next(i, candidates.Count);
                Vector2Int temp = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temp;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int direction = candidates[i];
                if (direction == Vector2Int.zero)
                {
                    continue;
                }

                Vector2Int candidate = current + direction;
                if (Mathf.Abs(candidate.x) > maxRadius || Mathf.Abs(candidate.y) > maxRadius)
                {
                    continue;
                }

                if (occupied.Contains(candidate))
                {
                    continue;
                }

                next = candidate;
                usedDirection = direction;
                return true;
            }

            next = current;
            usedDirection = preferredDirection;
            return false;
        }

        private static void AddCell(
            List<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            Vector2Int position,
            MapCellKind kind,
            bool isMainPath,
            ref int order)
        {
            if (occupied.Contains(position))
            {
                return;
            }

            occupied.Add(position);
            cells.Add(new GeneratedMapCell(position, kind, isMainPath, order));
            order++;
        }

        private static int FindLastMainPathIndex(List<GeneratedMapCell> cells)
        {
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                if (cells[i].isMainPath)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ReplaceLastMainPathCell(List<GeneratedMapCell> cells, MapCellKind kind)
        {
            int index = FindLastMainPathIndex(cells);
            if (index < 0)
            {
                return;
            }

            GeneratedMapCell cell = cells[index];
            if (cell.kind == MapCellKind.Start || cell.kind == MapCellKind.Exit)
            {
                return;
            }

            cell.kind = kind;
            cells[index] = cell;
        }

        private static void EnsureAtLeastOneHideout(List<GeneratedMapCell> cells, System.Random random)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == MapCellKind.Hideout)
                {
                    return;
                }
            }

            List<int> roomLikeIndexes = new();
            for (int i = 0; i < cells.Count; i++)
            {
                if (!cells[i].isMainPath && cells[i].kind != MapCellKind.Exit && cells[i].kind != MapCellKind.Start)
                {
                    roomLikeIndexes.Add(i);
                }
            }

            if (roomLikeIndexes.Count == 0)
            {
                return;
            }

            int selected = roomLikeIndexes[random.Next(0, roomLikeIndexes.Count)];
            GeneratedMapCell cell = cells[selected];
            cell.kind = MapCellKind.Hideout;
            cells[selected] = cell;
        }

        private static Vector2Int RotateLeft(Vector2Int direction)
        {
            return new Vector2Int(-direction.y, direction.x);
        }

        private static Vector2Int RotateRight(Vector2Int direction)
        {
            return new Vector2Int(direction.y, -direction.x);
        }

        private static bool Roll(System.Random random, float probability)
        {
            return random.NextDouble() <= Mathf.Clamp01(probability);
        }

        private static void EnsureMinimumExitDistance(
            List<GeneratedMapCell> cells,
            HashSet<Vector2Int> occupied,
            int stageIndex,
            SequentialMapConfig config,
            System.Random random,
            ref int order)
        {
            if (cells == null || cells.Count <= 1 || occupied == null || config == null || random == null)
            {
                return;
            }

            int exitIndex = FindCellIndexByKind(cells, MapCellKind.Exit);
            if (exitIndex < 0)
            {
                return;
            }

            int startIndex = FindCellIndexByKind(cells, MapCellKind.Start);
            Vector2Int startPosition = startIndex >= 0 ? cells[startIndex].position : Vector2Int.zero;
            Vector2Int exitPosition = cells[exitIndex].position;

            int baseDistance = config.minStartToExitDistance > 0 ? config.minStartToExitDistance : 10;
            int distancePerStage = Mathf.Max(0, config.exitDistanceIncreasePerStage);
            int requiredDistance = baseDistance + Mathf.Max(0, stageIndex - 1) * distancePerStage;

            int extensionBudgetBase = config.maxExitExtensionCells > 0 ? config.maxExitExtensionCells : 8;
            int extensionBudget = extensionBudgetBase + Mathf.Max(0, (stageIndex - 1) / 2);

            Vector2Int travelDirection = EstimateExitTravelDirection(cells, exitPosition, startPosition);
            int extensionCount = 0;

            while (extensionCount < extensionBudget
                   && EvaluateDistance(startPosition, exitPosition) < requiredDistance)
            {
                Vector2Int outwardDirection = DetermineOutwardDirection(exitPosition, startPosition, travelDirection);
                Vector2Int preferredDirection = outwardDirection;

                float turnChance = Mathf.Clamp01(config.turnChance * 0.5f);
                if (Roll(random, turnChance))
                {
                    preferredDirection = Roll(random, 0.5f)
                        ? RotateLeft(outwardDirection)
                        : RotateRight(outwardDirection);
                }

                if (!TryPickNextCell(
                        exitPosition,
                        preferredDirection,
                        occupied,
                        config.maxGenerationRadius,
                        random,
                        out Vector2Int next,
                        out Vector2Int usedDirection))
                {
                    break;
                }

                ReplaceCellKindAtPosition(cells, exitPosition, MapCellKind.Corridor);
                AddCell(cells, occupied, next, MapCellKind.Exit, true, ref order);

                exitPosition = next;
                travelDirection = usedDirection;
                extensionCount++;
            }
        }

        private static int FindCellIndexByKind(List<GeneratedMapCell> cells, MapCellKind kind)
        {
            if (cells == null)
            {
                return -1;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ReplaceCellKindAtPosition(List<GeneratedMapCell> cells, Vector2Int position, MapCellKind kind)
        {
            if (cells == null)
            {
                return;
            }

            for (int i = cells.Count - 1; i >= 0; i--)
            {
                GeneratedMapCell cell = cells[i];
                if (cell.position != position)
                {
                    continue;
                }

                if (cell.kind == MapCellKind.Start)
                {
                    return;
                }

                cell.kind = kind;
                cells[i] = cell;
                return;
            }
        }

        private static Vector2Int EstimateExitTravelDirection(List<GeneratedMapCell> cells, Vector2Int exitPosition, Vector2Int startPosition)
        {
            if (cells == null)
            {
                return DetermineOutwardDirection(exitPosition, startPosition, Vector2Int.up);
            }

            int exitOrder = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].position == exitPosition)
                {
                    exitOrder = cells[i].order;
                    break;
                }
            }

            if (exitOrder == int.MinValue)
            {
                return DetermineOutwardDirection(exitPosition, startPosition, Vector2Int.up);
            }

            GeneratedMapCell? previousMain = null;
            for (int i = 0; i < cells.Count; i++)
            {
                GeneratedMapCell cell = cells[i];
                if (!cell.isMainPath || cell.order >= exitOrder || cell.position == exitPosition)
                {
                    continue;
                }

                if (previousMain == null || cell.order > previousMain.Value.order)
                {
                    previousMain = cell;
                }
            }

            if (previousMain.HasValue)
            {
                Vector2Int delta = exitPosition - previousMain.Value.position;
                Vector2Int normalized = NormalizeCardinal(delta);
                if (normalized != Vector2Int.zero)
                {
                    return normalized;
                }
            }

            return DetermineOutwardDirection(exitPosition, startPosition, Vector2Int.up);
        }

        private static Vector2Int DetermineOutwardDirection(Vector2Int position, Vector2Int center, Vector2Int fallback)
        {
            Vector2Int delta = position - center;
            Vector2Int cardinal = NormalizeCardinal(delta);
            if (cardinal != Vector2Int.zero)
            {
                return cardinal;
            }

            return fallback == Vector2Int.zero ? Vector2Int.up : NormalizeCardinal(fallback);
        }

        private static Vector2Int NormalizeCardinal(Vector2Int delta)
        {
            if (delta == Vector2Int.zero)
            {
                return Vector2Int.zero;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x >= 0 ? Vector2Int.right : Vector2Int.left;
            }

            return delta.y >= 0 ? Vector2Int.up : Vector2Int.down;
        }

        private static int EvaluateDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}

