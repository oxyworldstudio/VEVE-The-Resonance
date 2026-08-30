using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEVE.Procedural
{
    /// <summary>
    /// Tuning parameters for tactical layout evaluation.
    /// </summary>
    [System.Serializable]
    public struct TacticalEvaluationConfig
    {
        /// <summary>
        /// Maximum distance in tiles considered when measuring sightlines.
        /// </summary>
        public int sightlineRadius;

        /// <summary>
        /// Sample stride used when scanning vantage tiles; 1 scans every walkable tile.
        /// </summary>
        public int sightlineSampleStride;

        /// <summary>
        /// A spawn pair line of sight is unsafe when it crosses more exposed tiles than this.
        /// </summary>
        public int maxExposedTilesPerSpawnLine;

        /// <summary>
        /// Minimum Chebyshev distance required between opposing spawn tiles.
        /// </summary>
        public int minSpawnSeparation;

        /// <summary>
        /// Walkable tiles with at most this many orthogonal walkable neighbors count as chokepoints.
        /// </summary>
        public int chokepointMaxNeighbors;

        /// <summary>
        /// Half-width in tiles of the central axis band excluded from flanking paths.
        /// </summary>
        public int flankingBandWidth;

        /// <summary>
        /// Radius around target spawn where re-entry from a flanking path is allowed.
        /// </summary>
        public int flankingApproachRadius;

        /// <summary>
        /// Returns sensible defaults for small-to-medium generated maps.
        /// </summary>
        /// <returns>Default configuration.</returns>
        public static TacticalEvaluationConfig Default()
        {
            return new TacticalEvaluationConfig
            {
                sightlineRadius = 20,
                sightlineSampleStride = 2,
                maxExposedTilesPerSpawnLine = 3,
                minSpawnSeparation = 8,
                chokepointMaxNeighbors = 2,
                flankingBandWidth = 2,
                flankingApproachRadius = 3
            };
        }
    }

    /// <summary>
    /// Outcome of spawn-safety analysis between two opposing spawn sets.
    /// </summary>
    [System.Serializable]
    public struct SpawnSafetyReport
    {
        /// <summary>
        /// True when no opposing pair has an exposed line of sight and all pairs respect separation.
        /// </summary>
        public bool safe;

        /// <summary>
        /// Number of opposing pairs tested on the same floor.
        /// </summary>
        public int pairsTested;

        /// <summary>
        /// Opposing pairs with unobstructed line of sight between them.
        /// </summary>
        public int directLosPairs;

        /// <summary>
        /// Highest count of cover-less tiles found along a cleared spawn line.
        /// </summary>
        public int maxExposedTilesOnDirectLine;

        /// <summary>
        /// Pairs violating the configured minimum separation.
        /// </summary>
        public int separationViolations;

        /// <summary>
        /// Indices of pairs that failed the safety gate as "teamAIndex-teamBIndex" descriptors.
        /// </summary>
        public string[] unsafePairs;
    }

    /// <summary>
    /// Aggregate tactical score for a generated map consumed by map vetting and replay tooling.
    /// </summary>
    [System.Serializable]
    public struct MapTacticalScore
    {
        /// <summary>
        /// 0-1 measure of how strongly the best vantage points dominate map visibility. High values
        /// indicate sniper-dominant sightlines over an exposed layout.
        /// </summary>
        public float sightlineDominance;

        /// <summary>
        /// Cover tiles per traversable tile across all floors.
        /// </summary>
        public float coverDensity;

        /// <summary>
        /// Average number of flanking routes (perpendicular approaches avoiding the central axis)
        /// available between opposing spawn pairs.
        /// </summary>
        public float flankingRoutes;

        /// <summary>
        /// Total structural pinch points detected across all floors.
        /// </summary>
        public int chokepointCount;

        /// <summary>
        /// Opposing-spawn exposure analysis using the supplied spawn sets.
        /// </summary>
        public SpawnSafetyReport spawnSafety;

        /// <summary>
        /// 0-1 layout legibility estimate: open ground visible from spawns penalized by decision
        /// point density, approximating orientation clarity without human playtest.
        /// </summary>
        public float readability;

        /// <summary>
        /// Walkable tiles counted across all floors.
        /// </summary>
        public int walkableTiles;
    }

    /// <summary>
    /// Static, raycast-free tactical scorer over the TileData grid. Uses Bresenham line-of-sight
    /// approximation against tile semantics: Walls, high cover, and non-walkable solid tiles block
    /// sight; doors, windows, low cover, stairs, and traversable debris do not.
    /// </summary>
    public static class TacticalLayoutEvaluator
    {
        private static readonly Vector2Int[] OrthogonalOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        private static readonly Vector2Int[] AllNeighborOffsets =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        /// <summary>
        /// Convenience config for large generated maps: the dominance sweep samples every sixth
        /// tile over a 12-tile radius, keeping vetting passes tractable during scene build.
        /// </summary>
        /// <returns>Coarse high-performance configuration.</returns>
        public static TacticalEvaluationConfig DefaultFastConfig()
        {
            var config = TacticalEvaluationConfig.Default();
            config.sightlineRadius = 12;
            config.sightlineSampleStride = 6;
            return config;
        }

        /// <summary>
        /// Determines whether a tile stops line of sight under the coarse ballistic model.
        /// </summary>
        /// <param name="tile">Tile to test.</param>
        /// <returns>True when sight does not pass through the tile.</returns>
        public static bool IsLosBlocking(TileData tile)
        {
            switch (tile.type)
            {
                case TileType.Wall:
                case TileType.CoverHigh:
                    return true;
                case TileType.Door:
                case TileType.Window:
                case TileType.StairsUp:
                case TileType.StairsDown:
                case TileType.Ladder:
                case TileType.Vent:
                    return false;
                case TileType.CoverLow:
                case TileType.Debris:
                    return !tile.walkable;
                case TileType.Empty:
                    return true;
                default:
                    return tile.providesCover && !tile.walkable;
            }
        }

        /// <summary>
        /// Integer Bresenham cell trace including both endpoints.
        /// </summary>
        /// <param name="from">Start cell.</param>
        /// <param name="to">End cell.</param>
        /// <returns>Ordered list of cells along the line.</returns>
        public static List<Vector2Int> TraceLineCells(Vector2Int from, Vector2Int to)
        {
            var cells = new List<Vector2Int>();
            int x = from.x;
            int y = from.y;
            int dx = Mathf.Abs(to.x - x);
            int dy = Mathf.Abs(to.y - y);
            int stepX = x < to.x ? 1 : -1;
            int stepY = y < to.y ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                cells.Add(new Vector2Int(x, y));
                if (x == to.x && y == to.y) break;

                int doubled = 2 * err;
                if (doubled > -dy)
                {
                    err -= dy;
                    x += stepX;
                }
                if (doubled < dx)
                {
                    err += dx;
                    y += stepY;
                }
            }

            return cells;
        }

        /// <summary>
        /// Raycast-free line-of-sight test on a single floor slice.
        /// </summary>
        /// <param name="grid">Tile grid indexed [x, y, floor].</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floor">Floor slice to trace on.</param>
        /// <param name="from">Origin cell.</param>
        /// <param name="to">Target cell.</param>
        /// <returns>True when the line is unobstructed between endpoints.</returns>
        public static bool LineOfSightClear(TileData[,,] grid, int width, int height, int floor, Vector2Int from, Vector2Int to)
        {
            if (grid == null) return false;
            if (!InBounds(from, width, height) || !InBounds(to, width, height)) return false;

            var cells = TraceLineCells(from, to);
            for (int i = 1; i < cells.Count - 1; i++)
            {
                var c = cells[i];
                if (!InBounds(c, width, height)) return false;
                if (IsLosBlocking(grid[c.x, c.y, floor])) return false;
            }
            return true;
        }

        /// <summary>
        /// Counts walkable tiles visible from a vantage cell within a Chebyshev radius.
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floor">Floor slice.</param>
        /// <param name="from">Vantage cell.</param>
        /// <param name="radius">Maximum look distance in tiles.</param>
        /// <returns>Number of visible walkable cells.</returns>
        public static int CountVisibleTiles(TileData[,,] grid, int width, int height, int floor, Vector2Int from, int radius)
        {
            if (grid == null) return 0;

            int visible = 0;
            int xMin = Mathf.Max(0, from.x - radius);
            int xMax = Mathf.Min(width - 1, from.x + radius);
            int yMin = Mathf.Max(0, from.y - radius);
            int yMax = Mathf.Min(height - 1, from.y + radius);

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == from) continue;
                    if (!grid[x, y, floor].walkable) continue;
                    if (LineOfSightClear(grid, width, height, floor, from, cell)) visible++;
                }
            }
            return visible;
        }

        /// <summary>
        /// Fraction of traversable ground backed by cover, computed across all floors.
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floors">Floor count.</param>
        /// <returns>Density in [0,1]; 0 for empty maps.</returns>
        public static float CoverDensity(TileData[,,] grid, int width, int height, int floors)
        {
            if (grid == null) return 0f;

            int coverTiles = 0;
            int relevantTiles = 0;

            for (int z = 0; z < floors; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var tile = grid[x, y, z];
                        bool traversable = tile.walkable || tile.providesCover;
                        if (!traversable) continue;

                        relevantTiles++;
                        if (tile.providesCover) coverTiles++;
                    }
                }
            }

            return relevantTiles == 0 ? 0f : (float)coverTiles / relevantTiles;
        }

        /// <summary>
        /// Structural chokepoint approximation: traversable non-door cells pinched by their
        /// neighborhood (at most chokepointMaxNeighbors orthogonal walkable exits).
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floors">Floor count.</param>
        /// <param name="config">Tuning configuration.</param>
        /// <returns>Chokepoint cell count across all floors.</returns>
        public static int CountChokepoints(TileData[,,] grid, int width, int height, int floors, TacticalEvaluationConfig config)
        {
            if (grid == null) return 0;

            int count = 0;
            for (int z = 0; z < floors; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var tile = grid[x, y, z];
                        if (!tile.walkable || tile.type == TileType.Door) continue;

                        int exits = 0;
                        foreach (var offset in OrthogonalOffsets)
                        {
                            int nx = x + offset.x;
                            int ny = y + offset.y;
                            if (InBounds(new Vector2Int(nx, ny), width, height) && grid[nx, ny, z].walkable) exits++;
                        }

                        if (exits >= 1 && exits <= config.chokepointMaxNeighbors) count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Sightline dominance: blend of the best vantage's map coverage and the average vantage
        /// coverage, normalized by traversable tile count. Higher means a few spots see too much.
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floors">Floor count.</param>
        /// <param name="config">Tuning configuration.</param>
        /// <returns>Dominance score in [0,1].</returns>
        public static float SightlineDominance(TileData[,,] grid, int width, int height, int floors, TacticalEvaluationConfig config)
        {
            if (grid == null) return 0f;

            int stride = Mathf.Max(1, config.sightlineSampleStride);
            float best = 0f;
            float sum = 0f;
            int samples = 0;

            for (int z = 0; z < floors; z++)
            {
                int walkableOnFloor = 0;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (grid[x, y, z].walkable) walkableOnFloor++;
                    }
                }
                if (walkableOnFloor == 0) continue;

                for (int x = 0; x < width; x += stride)
                {
                    for (int y = 0; y < height; y += stride)
                    {
                        if (!grid[x, y, z].walkable) continue;

                        int visible = CountVisibleTiles(grid, width, height, z, new Vector2Int(x, y), config.sightlineRadius);
                        float ratio = Mathf.Clamp01((float)visible / walkableOnFloor);
                        if (ratio > best) best = ratio;
                        sum += ratio;
                        samples++;
                    }
                }
            }

            if (samples == 0) return 0f;
            float avg = sum / samples;
            return Mathf.Clamp01(0.65f * best + 0.35f * avg);
        }

        /// <summary>
        /// Counts flanking approaches between two same-floor spawns: reachable sides (perpendicular
        /// to the spawn axis) of routes that avoid the central band for most of their distance.
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="spawnA">Origin spawn (x, y, floor).</param>
        /// <param name="spawnB">Target spawn (x, y, floor).</param>
        /// <param name="config">Tuning configuration.</param>
        /// <returns>Number of usable flanking sides: 0, 1, or 2.</returns>
        public static int CountFlankingRoutes(TileData[,,] grid, int width, int height, Vector3Int spawnA, Vector3Int spawnB, TacticalEvaluationConfig config)
        {
            if (grid == null || spawnA.z != spawnB.z) return 0;

            int z = spawnA.z;
            var a = new Vector2Int(spawnA.x, spawnA.y);
            var b = new Vector2Int(spawnB.x, spawnB.y);
            if (!InBounds(a, width, height) || !InBounds(b, width, height)) return 0;

            var axisCells = TraceLineCells(a, b);
            var central = new HashSet<Vector2Int>();
            foreach (var cell in axisCells)
            {
                for (int dx = -config.flankingBandWidth; dx <= config.flankingBandWidth; dx++)
                {
                    for (int dy = -config.flankingBandWidth; dy <= config.flankingBandWidth; dy++)
                    {
                        central.Add(new Vector2Int(cell.x + dx, cell.y + dy));
                    }
                }
            }

            int dirX = b.x - a.x;
            int dirY = b.y - a.y;

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(a);
            queue.Enqueue(a);

            int leftFlanked = 0;
            int rightFlanked = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current != a && current != b && grid[current.x, current.y, z].walkable)
                {
                    if (!central.Contains(current))
                    {
                        int cross = dirX * (current.y - a.y) - dirY * (current.x - a.x);
                        if (cross > 0) leftFlanked++;
                        else if (cross < 0) rightFlanked++;
                    }
                }

                foreach (var offset in OrthogonalOffsets)
                {
                    var next = new Vector2Int(current.x + offset.x, current.y + offset.y);
                    if (!InBounds(next, width, height)) continue;
                    if (visited.Contains(next)) continue;
                    if (!grid[next.x, next.y, z].walkable && next != b) continue;

                    bool nearTarget = next == b
                        || (Mathf.Abs(next.x - b.x) <= config.flankingApproachRadius && Mathf.Abs(next.y - b.y) <= config.flankingApproachRadius);

                    if (!central.Contains(next) || nearTarget)
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            if (!visited.Contains(b)) return 0;

            int routes = 0;
            if (leftFlanked > 0) routes++;
            if (rightFlanked > 0) routes++;
            return routes;
        }

        /// <summary>
        /// Opposing-spawn exposure gate: no direct line of sight with excessive exposed tiles and
        /// minimum separation respected on every tested pair.
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="teamASpawns">Team A spawn tiles (x, y, floor).</param>
        /// <param name="teamBSpawns">Team B spawn tiles (x, y, floor).</param>
        /// <param name="config">Tuning configuration.</param>
        /// <returns>Aggregated safety report.</returns>
        public static SpawnSafetyReport EvaluateSpawnSafety(TileData[,,] grid, int width, int height,
            IReadOnlyList<Vector3Int> teamASpawns, IReadOnlyList<Vector3Int> teamBSpawns, TacticalEvaluationConfig config)
        {
            var report = new SpawnSafetyReport { unsafePairs = Array.Empty<string>() };
            if (grid == null || teamASpawns == null || teamBSpawns == null) return report;

            var unsafeList = new List<string>();
            int directPairs = 0;
            int maxExposed = 0;
            int pairs = 0;
            int separations = 0;

            for (int i = 0; i < teamASpawns.Count; i++)
            {
                for (int j = 0; j < teamBSpawns.Count; j++)
                {
                    var a = teamASpawns[i];
                    var b = teamBSpawns[j];
                    if (a.z != b.z) continue;

                    pairs++;

                    int separation = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
                    if (separation < config.minSpawnSeparation)
                    {
                        separations++;
                        unsafeList.Add(i + "-" + j + ":separation");
                    }

                    var from = new Vector2Int(a.x, a.y);
                    var to = new Vector2Int(b.x, b.y);
                    if (!LineOfSightClear(grid, width, height, a.z, from, to)) continue;

                    directPairs++;

                    var line = TraceLineCells(from, to);
                    int exposed = 0;
                    for (int c = 1; c < line.Count - 1; c++)
                    {
                        if (!HasCoverNear(grid, width, height, a.z, line[c])) exposed++;
                    }

                    if (exposed > maxExposed) maxExposed = exposed;

                    if (exposed > config.maxExposedTilesPerSpawnLine)
                    {
                        unsafeList.Add(i + "-" + j + ":exposure-" + exposed);
                    }
                }
            }

            report.pairsTested = pairs;
            report.directLosPairs = directPairs;
            report.maxExposedTilesOnDirectLine = maxExposed;
            report.separationViolations = separations;
            report.unsafePairs = unsafeList.ToArray();
            report.safe = unsafeList.Count == 0;
            return report;
        }

        /// <summary>
        /// Layout readability heuristic: orientation (average visible ground from spawns inside a
        /// radius) discounted by decision-point density (3+ exit junctions per walkable tile).
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="width">Grid width.</param>
        /// <param name="height">Grid height.</param>
        /// <param name="floors">Floor count.</param>
        /// <param name="spawns">Representative camera/spawn positions, may be empty.</param>
        /// <returns>Readability score in [0,1].</returns>
        public static float Readability(TileData[,,] grid, int width, int height, int floors, IReadOnlyList<Vector3Int> spawns)
        {
            if (grid == null) return 0f;

            int walkable = 0;
            int junctions = 0;

            for (int z = 0; z < floors; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (!grid[x, y, z].walkable) continue;
                        walkable++;

                        int exits = 0;
                        foreach (var offset in OrthogonalOffsets)
                        {
                            var n = new Vector2Int(x + offset.x, y + offset.y);
                            if (InBounds(n, width, height) && grid[n.x, n.y, z].walkable) exits++;
                        }
                        if (exits >= 3) junctions++;
                    }
                }
            }

            if (walkable == 0) return 0f;

            float junctionPenalty = Mathf.Clamp01(4f * junctions / walkable);

            float orientation = 0.5f;
            if (spawns != null && spawns.Count > 0)
            {
                float sum = 0f;
                int counted = 0;
                int radius = 8;
                foreach (var spawn in spawns)
                {
                    int z = spawn.z;
                    if (z < 0 || z >= floors) continue;
                    var cell = new Vector2Int(spawn.x, spawn.y);
                    if (!InBounds(cell, width, height)) continue;

                    int visible = CountVisibleTiles(grid, width, height, z, cell, radius);
                    int potential = Mathf.Min(radius * 2 + 1, width) * Mathf.Min(radius * 2 + 1, height);
                    sum += (float)visible / potential;
                    counted++;
                }
                if (counted > 0) orientation = Mathf.Clamp01(sum / counted * 2.2f);
            }

            return Mathf.Clamp01(0.6f * orientation + 0.4f * (1f - junctionPenalty));
        }

        /// <summary>
        /// Full-map tactical evaluation combining every metric for a set of opposing spawns.
        /// </summary>
        /// <param name="grid">Tile grid produced by ProceduralMapGenerator.</param>
        /// <param name="teamASpawns">Team A spawn tiles.</param>
        /// <param name="teamBSpawns">Team B spawn tiles.</param>
        /// <param name="config">Tuning configuration.</param>
        /// <returns>Aggregate tactical score.</returns>
        public static MapTacticalScore Evaluate(TileData[,,] grid, IReadOnlyList<Vector3Int> teamASpawns,
            IReadOnlyList<Vector3Int> teamBSpawns, TacticalEvaluationConfig config)
        {
            var score = new MapTacticalScore();
            if (grid == null) return score;

            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            int floors = grid.GetLength(2);

            int walkable = 0;
            for (int z = 0; z < floors; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (grid[x, y, z].walkable) walkable++;
                    }
                }
            }

            score.walkableTiles = walkable;
            score.coverDensity = CoverDensity(grid, width, height, floors);
            score.chokepointCount = CountChokepoints(grid, width, height, floors, config);
            score.sightlineDominance = SightlineDominance(grid, width, height, floors, config);

            var spawns = new List<Vector3Int>();
            if (teamASpawns != null) spawns.AddRange(teamASpawns);
            if (teamBSpawns != null) spawns.AddRange(teamBSpawns);
            score.readability = Readability(grid, width, height, floors, spawns);

            score.spawnSafety = EvaluateSpawnSafety(grid, width, height, teamASpawns, teamBSpawns, config);

            int routeSamples = 0;
            float routeSum = 0f;
            if (teamASpawns != null && teamBSpawns != null)
            {
                foreach (var a in teamASpawns)
                {
                    foreach (var b in teamBSpawns)
                    {
                        if (a.z != b.z) continue;
                        routeSum += CountFlankingRoutes(grid, width, height, a, b, config);
                        routeSamples++;
                        if (routeSamples >= 8) break;
                    }
                    if (routeSamples >= 8) break;
                }
            }
            score.flankingRoutes = routeSamples == 0 ? 0f : routeSum / routeSamples;

            return score;
        }

        /// <summary>
        /// Convenience overload using TacticalEvaluationConfig.Default().
        /// </summary>
        /// <param name="grid">Tile grid.</param>
        /// <param name="teamASpawns">Team A spawn tiles.</param>
        /// <param name="teamBSpawns">Team B spawn tiles.</param>
        /// <returns>Aggregate tactical score.</returns>
        public static MapTacticalScore Evaluate(TileData[,,] grid, IReadOnlyList<Vector3Int> teamASpawns, IReadOnlyList<Vector3Int> teamBSpawns)
        {
            return Evaluate(grid, teamASpawns, teamBSpawns, TacticalEvaluationConfig.Default());
        }

        private static bool HasCoverNear(TileData[,,] grid, int width, int height, int floor, Vector2Int cell)
        {
            foreach (var offset in AllNeighborOffsets)
            {
                var n = new Vector2Int(cell.x + offset.x, cell.y + offset.y);
                if (!InBounds(n, width, height)) continue;
                var tile = grid[n.x, n.y, floor];
                if (tile.providesCover || tile.type == TileType.Wall || tile.type == TileType.CoverHigh) return true;
            }
            return false;
        }

        private static bool InBounds(Vector2Int cell, int width, int height)
        {
            return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        }
    }
}
