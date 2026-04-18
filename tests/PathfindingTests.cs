using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class PathfinderTests
{
    [SetUp]
    public void ResetDirections()
    {
        // Ensure we're testing with 4-dir
        Pathfinder.Directions = Pathfinder.Directions4;
    }

    [Test]
    public void SameTile_ReturnsZeroCostPath()
    {
        var map = new TileMap(10, 10);
        var result = Pathfinder.FindPath(map, 5, 5, 5, 5);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Steps, Has.Count.EqualTo(1));
        Assert.That(result.TotalCost, Is.EqualTo(0f));
    }

    [Test]
    public void StraightLine_GrassNoRoad()
    {
        var map = new TileMap(10, 10);
        var result = Pathfinder.FindPath(map, 0, 0, 5, 0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Steps[0], Is.EqualTo((0, 0)));
        Assert.That(result.Steps[^1], Is.EqualTo((5, 0)));
        // 5 steps on grass (cost 2.0 each) = 10.0
        Assert.That(result.TotalCost, Is.EqualTo(10.0f).Within(0.01f));
    }

    [Test]
    public void RoadPath_ExistsAndFollowsRoad()
    {
        var map = new TileMap(20, 20);
        // Place a cobblestone road from (0,5) to (10,5)
        for (int x = 0; x <= 10; x++)
            map.PlaceRoad(x, 5, RoadType.Cobblestone);

        var result = Pathfinder.FindPath(map, 0, 5, 10, 5);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Steps[0], Is.EqualTo((0, 5)));
        Assert.That(result.Steps[^1], Is.EqualTo((10, 5)));
        // All tiles on road: 10 steps × 0.7 = 7.0
        Assert.That(result.TotalCost, Is.EqualTo(7.0f).Within(0.01f));
    }

    [Test]
    public void RoadPath_CheaperThanGrass()
    {
        var map = new TileMap(20, 20);
        // Road along y=5
        for (int x = 0; x <= 10; x++)
            map.PlaceRoad(x, 5, RoadType.Cobblestone);

        var roadResult = Pathfinder.FindPath(map, 0, 5, 10, 5);
        // Force grass-only by using a map without roads
        var grassMap = new TileMap(20, 20);
        var grassResult = Pathfinder.FindPath(grassMap, 0, 5, 10, 5);

        Assert.That(roadResult, Is.Not.Null);
        Assert.That(grassResult, Is.Not.Null);
        Assert.That(roadResult!.TotalCost, Is.LessThan(grassResult!.TotalCost));
    }

    [Test]
    public void BlockedRoad_Reroutes()
    {
        var map = new TileMap(20, 10);
        // Road along y=5 from x=0 to x=15
        for (int x = 0; x <= 15; x++)
            map.PlaceRoad(x, 5, RoadType.Cobblestone);

        // Block the road at x=8 with a mountain
        ref var tile = ref map.GetTile(8, 5);
        tile.Terrain = TerrainType.Mountain;

        var result = Pathfinder.FindPath(map, 0, 5, 15, 5);

        Assert.That(result, Is.Not.Null, "Should find an alternate route around the blockage");
        Assert.That(result!.Steps[0], Is.EqualTo((0, 5)));
        Assert.That(result.Steps[^1], Is.EqualTo((15, 5)));
        // Path should NOT pass through the blocked tile
        Assert.That(result.Steps, Does.Not.Contain((8, 5)));
    }

    [Test]
    public void CompletelyBlocked_ReturnsNull()
    {
        var map = new TileMap(10, 10);
        // Wall of mountains across the middle
        for (int x = 0; x < 10; x++)
        {
            ref var tile = ref map.GetTile(x, 5);
            tile.Terrain = TerrainType.Mountain;
        }

        var result = Pathfinder.FindPath(map, 5, 0, 5, 9);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ImpassableStart_ReturnsNull()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(0, 0);
        tile.Terrain = TerrainType.Mountain;

        var result = Pathfinder.FindPath(map, 0, 0, 5, 5);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ImpassableGoal_ReturnsNull()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(5, 5);
        tile.Terrain = TerrainType.Water;

        var result = Pathfinder.FindPath(map, 0, 0, 5, 5);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void OutOfBounds_ReturnsNull()
    {
        var map = new TileMap(10, 10);
        Assert.That(Pathfinder.FindPath(map, -1, 0, 5, 5), Is.Null);
        Assert.That(Pathfinder.FindPath(map, 0, 0, 10, 5), Is.Null);
    }

    [Test]
    public void PathAroundForest_PrefersRoad()
    {
        var map = new TileMap(10, 10);
        // Forest block in the direct path
        for (int x = 3; x <= 6; x++)
            for (int y = 3; y <= 6; y++)
            {
                ref var tile = ref map.GetTile(x, y);
                tile.Terrain = TerrainType.Forest;
            }
        // Road that goes around the forest
        for (int x = 0; x <= 9; x++)
            map.PlaceRoad(x, 1, RoadType.Cobblestone);
        map.PlaceRoad(9, 2, RoadType.Cobblestone);
        map.PlaceRoad(9, 3, RoadType.Cobblestone);
        map.PlaceRoad(9, 4, RoadType.Cobblestone);
        map.PlaceRoad(9, 5, RoadType.Cobblestone);
        for (int x = 9; x >= 0; x--)
            map.PlaceRoad(x, 5, RoadType.Cobblestone);

        // Path from (0,1) to (0,5) — should prefer road around rather than through forest
        var result = Pathfinder.FindPath(map, 0, 1, 0, 5);
        Assert.That(result, Is.Not.Null);

        // Verify no forest tiles without roads are in the path
        foreach (var (px, py) in result!.Steps)
        {
            ref var t = ref map.GetTile(px, py);
            if (t.Terrain == TerrainType.Forest)
                Assert.That(t.Road, Is.Not.EqualTo(RoadType.None),
                    $"Path should not go through unroaded forest at ({px},{py})");
        }
    }

    [Test]
    public void LargeMap_PerformanceReasonable()
    {
        var map = new TileMap(200, 200);
        // Place a road corridor
        for (int x = 0; x < 200; x++)
            map.PlaceRoad(x, 100, RoadType.Cobblestone);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = Pathfinder.FindPath(map, 0, 0, 199, 199);
        sw.Stop();

        Assert.That(result, Is.Not.Null);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            "A* on 200x200 should complete in under 500ms");
    }

    [Test]
    public void EightDir_CanBeEnabled()
    {
        Pathfinder.Directions = Pathfinder.Directions8;

        var map = new TileMap(10, 10);
        var result = Pathfinder.FindPath(map, 0, 0, 3, 3);

        Assert.That(result, Is.Not.Null);
        // With 8-dir, diagonal path is shorter than 4-dir
        // 4-dir would need 6 steps, 8-dir needs 4 (3 diagonal + start)
        Assert.That(result!.Steps, Has.Count.LessThanOrEqualTo(4));
    }
}

[TestFixture]
public class FlowFieldTests
{
    [SetUp]
    public void ResetDirections()
    {
        Pathfinder.Directions = Pathfinder.Directions4;
    }

    [Test]
    public void GoalTile_HasZeroCostAndNoDirection()
    {
        var map = new TileMap(10, 10);
        var ff = FlowField.Generate(map, 5, 5);

        Assert.That(ff.GetCost(5, 5), Is.EqualTo(0f));
        Assert.That(ff.GetDirection(5, 5), Is.EqualTo((0, 0)));
    }

    [Test]
    public void AdjacentTile_PointsTowardGoal()
    {
        var map = new TileMap(10, 10);
        var ff = FlowField.Generate(map, 5, 5);

        // Tile to the left of goal should point right
        Assert.That(ff.GetDirection(4, 5), Is.EqualTo((1, 0)));
        // Tile above goal should point down
        Assert.That(ff.GetDirection(5, 4), Is.EqualTo((0, 1)));
        // Tile to the right of goal should point left
        Assert.That(ff.GetDirection(6, 5), Is.EqualTo((-1, 0)));
        // Tile below goal should point up
        Assert.That(ff.GetDirection(5, 6), Is.EqualTo((0, -1)));
    }

    [Test]
    public void FollowDirections_ReachesGoal()
    {
        var map = new TileMap(20, 20);
        // Place some roads for interesting routing
        for (int rx = 0; rx <= 15; rx++)
            map.PlaceRoad(rx, 10, RoadType.Cobblestone);

        var ff = FlowField.Generate(map, 15, 10);

        // Follow directions from (0, 8)
        int x = 0, y = 8;
        int maxSteps = 100;
        int steps = 0;

        while ((x != 15 || y != 10) && steps < maxSteps)
        {
            var (dx, dy) = ff.GetDirection(x, y);
            Assert.That((dx, dy), Is.Not.EqualTo((0, 0)),
                $"Hit unreachable tile at ({x},{y}) after {steps} steps");
            x += dx;
            y += dy;
            steps++;
        }

        Assert.That((x, y), Is.EqualTo((15, 10)), "Should reach the goal");
        Assert.That(steps, Is.LessThan(maxSteps), "Should reach goal within reasonable steps");
    }

    [Test]
    public void ImpassableTile_HasNoDirection()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(3, 3);
        tile.Terrain = TerrainType.Mountain;

        var ff = FlowField.Generate(map, 5, 5);

        Assert.That(ff.GetDirection(3, 3), Is.EqualTo((0, 0)));
        Assert.That(ff.GetCost(3, 3), Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void UnreachableTile_HasNoDirection()
    {
        var map = new TileMap(10, 10);
        // Wall of mountains isolating top-left corner
        for (int x = 0; x < 10; x++)
        {
            ref var tile = ref map.GetTile(x, 3);
            tile.Terrain = TerrainType.Mountain;
        }

        var ff = FlowField.Generate(map, 5, 5);

        // (1, 1) is isolated above the wall
        Assert.That(ff.GetDirection(1, 1), Is.EqualTo((0, 0)));
        Assert.That(ff.GetCost(1, 1), Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void IsStale_DetectsRoadChanges()
    {
        var map = new TileMap(10, 10);
        var ff = FlowField.Generate(map, 5, 5);

        Assert.That(ff.IsStale(map), Is.False);

        map.PlaceRoad(3, 3, RoadType.DirtPath);

        Assert.That(ff.IsStale(map), Is.True);
    }

    [Test]
    public void RoadPreference_LowerCostAlongRoad()
    {
        var map = new TileMap(20, 20);
        for (int x = 0; x <= 15; x++)
            map.PlaceRoad(x, 10, RoadType.Cobblestone);

        var ff = FlowField.Generate(map, 15, 10);

        // Tile on the road should have lower cost than tile off the road at same distance
        float costOnRoad = ff.GetCost(5, 10);
        float costOffRoad = ff.GetCost(5, 5); // Same X distance but off road

        Assert.That(costOnRoad, Is.LessThan(costOffRoad));
    }

    [Test]
    public void ImpassableGoal_AllUnreachable()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(5, 5);
        tile.Terrain = TerrainType.Mountain;

        var ff = FlowField.Generate(map, 5, 5);

        // Everything should be unreachable since the goal itself is impassable
        Assert.That(ff.GetCost(0, 0), Is.EqualTo(float.MaxValue));
        Assert.That(ff.GetDirection(0, 0), Is.EqualTo((0, 0)));
    }

    [Test]
    public void OutOfBounds_ReturnsDefaults()
    {
        var map = new TileMap(10, 10);
        var ff = FlowField.Generate(map, 5, 5);

        Assert.That(ff.GetDirection(-1, 0), Is.EqualTo((0, 0)));
        Assert.That(ff.GetCost(-1, 0), Is.EqualTo(float.MaxValue));
        Assert.That(ff.GetDirection(10, 0), Is.EqualTo((0, 0)));
    }

    [Test]
    public void LargeMap_PerformanceReasonable()
    {
        var map = new TileMap(200, 200);
        for (int x = 0; x < 200; x++)
            map.PlaceRoad(x, 100, RoadType.Cobblestone);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ff = FlowField.Generate(map, 100, 100);
        sw.Stop();

        Assert.That(ff.GetCost(0, 0), Is.LessThan(float.MaxValue));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500),
            "Flow field generation on 200x200 should complete in under 500ms");
    }
}
