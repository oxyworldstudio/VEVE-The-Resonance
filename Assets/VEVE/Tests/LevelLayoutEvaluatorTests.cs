using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VEVE.Procedural;

/// <summary>
/// EditMode coverage for TacticalLayoutEvaluator: line-of-sight blocking behavior,
/// cover-density monotonicity, spawn safety with separation, and metric bounds on generated maps.
/// </summary>
public sealed class LevelLayoutEvaluatorTests
{
    private static TileData[,,] MakeOpenFloor(int width, int height, int floors)
    {
        var grid = new TileData[width, height, floors];
        for (int z = 0; z < floors; z++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y, z] = new TileData
                    {
                        type = TileType.Floor,
                        position = new Vector3Int(x, y, z),
                        walkable = true,
                        providesCover = false,
                        materialIntegrity = 1f,
                        floorLevel = z,
                        noiseValue = 0.5f,
                        isEdge = false
                    };
                }
            }
        }
        return grid;
    }

    private static void SetTile(TileData[,,] grid, int x, int y, int z, TileType type, bool walkable, bool cover)
    {
        var tile = grid[x, y, z];
        tile.type = type;
        tile.walkable = walkable;
        tile.providesCover = cover;
        grid[x, y, z] = tile;
    }

    private static TacticalEvaluationConfig FastConfig()
    {
        var config = TacticalEvaluationConfig.Default();
        config.sightlineRadius = 12;
        config.sightlineSampleStride = 2;
        return config;
    }

    [Test]
    public void LosBlockingWallReducesSpawnLineExposure()
    {
        const int width = 12;
        const int height = 3;
        var grid = MakeOpenFloor(width, height, 1);

        var teamA = new List<Vector3Int> { new Vector3Int(1, 1, 0) };
        var teamB = new List<Vector3Int> { new Vector3Int(10, 1, 0) };
        var config = TacticalEvaluationConfig.Default();

        Assert.IsTrue(TacticalLayoutEvaluator.LineOfSightClear(grid, width, height, 0, new Vector2Int(1, 1), new Vector2Int(10, 1)),
            "Open corridor must expose a direct spawn line initially.");

        var before = TacticalLayoutEvaluator.EvaluateSpawnSafety(grid, width, height, teamA, teamB, config);
        Assert.AreEqual(1, before.directLosPairs);
        Assert.Greater(before.maxExposedTilesOnDirectLine, config.maxExposedTilesPerSpawnLine);
        Assert.IsFalse(before.safe);

        SetTile(grid, 6, 1, 0, TileType.Wall, false, false);

        Assert.IsFalse(TacticalLayoutEvaluator.LineOfSightClear(grid, width, height, 0, new Vector2Int(1, 1), new Vector2Int(10, 1)),
            "A wall tile must block the coarse Bresenham line of sight.");

        var after = TacticalLayoutEvaluator.EvaluateSpawnSafety(grid, width, height, teamA, teamB, config);
        Assert.AreEqual(0, after.directLosPairs);
        Assert.Less(after.maxExposedTilesOnDirectLine, before.maxExposedTilesOnDirectLine);
        Assert.IsTrue(after.safe);
    }

    [Test]
    public void CoverDensityIsMonotonicInCoverTileCount()
    {
        const int width = 10;
        const int height = 10;
        var grid = MakeOpenFloor(width, height, 1);

        Assert.AreEqual(0f, TacticalLayoutEvaluator.CoverDensity(grid, width, height, 1), 0.0001f);

        float previous = 0f;
        for (int added = 1; added <= 5; added++)
        {
            SetTile(grid, added, added, 0, TileType.CoverLow, true, true);
            float density = TacticalLayoutEvaluator.CoverDensity(grid, width, height, 1);
            Assert.Greater(density, previous, "Cover density must strictly increase with each cover tile.");
            Assert.LessOrEqual(density, 1f);
            previous = density;
        }

        Assert.AreEqual(5f / 100f, previous, 0.0001f);
    }

    [Test]
    public void SeparatedSpawnsBehindCoverWallAreDirectLineSafe()
    {
        const int width = 16;
        const int height = 6;
        var grid = MakeOpenFloor(width, height, 1);

        for (int y = 0; y < height; y++)
        {
            SetTile(grid, 8, y, 0, TileType.CoverHigh, false, true);
        }

        var teamA = new List<Vector3Int> { new Vector3Int(1, 1, 0) };
        var teamB = new List<Vector3Int> { new Vector3Int(14, 4, 0) };
        var config = TacticalEvaluationConfig.Default();

        Assert.GreaterOrEqual(Mathf.Max(Mathf.Abs(14 - 1), Mathf.Abs(4 - 1)), config.minSpawnSeparation,
            "Test spawns must respect the minimum separation baseline.");

        var report = TacticalLayoutEvaluator.EvaluateSpawnSafety(grid, width, height, teamA, teamB, config);
        Assert.AreEqual(1, report.pairsTested);
        Assert.AreEqual(0, report.directLosPairs, "A full cover wall must prevent any direct spawn line.");
        Assert.AreEqual(0, report.maxExposedTilesOnDirectLine);
        Assert.IsTrue(report.safe);
    }

    [Test]
    public void EvaluationIsDeterministicForIdenticalGrid()
    {
        const int width = 12;
        const int height = 8;
        var grid = MakeOpenFloor(width, height, 1);

        SetTile(grid, 5, 0, 0, TileType.Wall, false, false);
        SetTile(grid, 5, 1, 0, TileType.Wall, false, false);
        SetTile(grid, 8, 4, 0, TileType.CoverHigh, false, true);
        SetTile(grid, 2, 6, 0, TileType.CoverLow, true, true);

        var teamA = new List<Vector3Int> { new Vector3Int(1, 1, 0) };
        var teamB = new List<Vector3Int> { new Vector3Int(11, 5, 0) };
        var config = FastConfig();

        var first = TacticalLayoutEvaluator.Evaluate(grid, teamA, teamB, config);
        var second = TacticalLayoutEvaluator.Evaluate(grid, teamA, teamB, config);

        Assert.AreEqual(first.sightlineDominance, second.sightlineDominance);
        Assert.AreEqual(first.coverDensity, second.coverDensity);
        Assert.AreEqual(first.chokepointCount, second.chokepointCount);
        Assert.AreEqual(first.readability, second.readability);
        Assert.AreEqual(first.spawnSafety.directLosPairs, second.spawnSafety.directLosPairs);
    }

    [Test]
    public void GeneratedMapProducesBoundedMetricsAndConsistentSpawnSafety()
    {
        var generator = new ProceduralMapGenerator();
        var settings = MapGenerationSettings.Default();
        settings.seed = 20260830;

        TileData[,,] grid = generator.Generate(settings);
        Assert.IsNotNull(grid);

        int width = settings.mapWidth;
        int height = settings.mapHeight;
        int floors = grid.GetLength(2);

        var allSpawns = generator.GetSpawnPoints();
        Assert.Greater(allSpawns.Count, 1, "Generated map must expose candidate spawns.");

        var teamA = new List<Vector3Int>();
        var teamB = new List<Vector3Int>();
        allSpawns.Sort((a, b) => (a.x * 1000 + a.y).CompareTo(b.x * 1000 + b.y));
        for (int i = 0; i < allSpawns.Count; i++)
        {
            if (i % 2 == 0) teamA.Add(allSpawns[i]);
            else teamB.Add(allSpawns[i]);
        }

        Assert.Greater(teamA.Count, 0);
        Assert.Greater(teamB.Count, 0);

        var config = TacticalLayoutEvaluator.DefaultFastConfig();
        float dominance = TacticalLayoutEvaluator.SightlineDominance(grid, width, height, floors, config);
        float density = TacticalLayoutEvaluator.CoverDensity(grid, width, height, floors);
        int chokes = TacticalLayoutEvaluator.CountChokepoints(grid, width, height, floors, config);
        float readability = TacticalLayoutEvaluator.Readability(grid, width, height, floors, allSpawns);
        var safety = TacticalLayoutEvaluator.EvaluateSpawnSafety(grid, width, height, teamA, teamB, config);

        Assert.GreaterOrEqual(dominance, 0f);
        Assert.LessOrEqual(dominance, 1f);
        Assert.GreaterOrEqual(density, 0f);
        Assert.LessOrEqual(density, 1f);
        Assert.GreaterOrEqual(readability, 0f);
        Assert.LessOrEqual(readability, 1f);
        Assert.GreaterOrEqual(chokes, 0);
        Assert.GreaterOrEqual(safety.pairsTested, 0);
        Assert.GreaterOrEqual(safety.directLosPairs, 0);

        if (!safety.safe)
        {
            Assert.Greater(safety.directLosPairs + safety.separationViolations, 0,
                "An unsafe report must be justified by direct exposure or separation violations.");
        }
        else
        {
            Assert.AreEqual(0, safety.unsafePairs.Length);
        }
    }
}
