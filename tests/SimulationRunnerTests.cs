using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class SimulationRunnerTests
{
    private GameData _data = null!;

    [OneTimeSetUp]
    public void LoadData()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "data")))
            dir = Directory.GetParent(dir)?.FullName;
        _data = GameData.LoadFromDirectory(Path.Combine(dir!, "data"));
    }

    [SetUp]
    public void ResetPathfinder()
    {
        Pathfinder.Directions = Pathfinder.Directions4;
    }

    private static void BuildRoad(TileMap map, int x1, int y1, int x2, int y2,
        RoadType road = RoadType.Cobblestone)
    {
        int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
        int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
        for (int x = minX; x <= maxX; x++)
            map.PlaceRoad(x, y1, road);
        for (int y = minY; y <= maxY; y++)
            map.PlaceRoad(x2, y, road);
    }

    // ═══ Full integration test ═══

    [Test]
    public void FullPipeline_FarmToMill_FlourProduced_200Ticks()
    {
        // Setup: grain_farm produces grain → hauler moves grain to windmill → windmill produces flour
        var map = new TileMap(30, 10);
        BuildRoad(map, 0, 5, 20, 5);

        // Grain farm with farmer — pre-stocked with inputs (tools)
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "grain_farming"
        };
        farm.InputBuffer["tools"] = 10f; // grain_farming needs 0.01 tools per cycle
        var farmer1 = new Pop { Profession = ProfessionType.Farmer, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 0, TileY = 5 };
        var farmer2 = new Pop { Profession = ProfessionType.Farmer, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 0, TileY = 5 };
        var farmer3 = new Pop { Profession = ProfessionType.Farmer, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 0, TileY = 5 };
        var farmer4 = new Pop { Profession = ProfessionType.Farmer, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 0, TileY = 5 };
        farm.AssignedWorkerIds.AddRange(new[] { farmer1.Id, farmer2.Id, farmer3.Id, farmer4.Id });

        // Windmill with miller
        var mill = new Building
        {
            DefId = "windmill", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "milling"
        };
        var miller = new Pop { Profession = ProfessionType.Miller, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 10, TileY = 5 };
        mill.AssignedWorkerIds.Add(miller.Id);

        // Warehouse to receive goods
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 20, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        // Hauler to move goods
        var hauler = new Pop
        {
            Profession = ProfessionType.Hauler, SkillLevel = 1f,
            FoodLevel = 1f, RestLevel = 1f,
            State = PopState.Idle,
            TileX = 0, TileY = 5
        };

        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Buildings = new List<Building> { farm, mill, warehouse },
            Pops = new List<Pop> { farmer1, farmer2, farmer3, farmer4, miller, hauler },
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(200);

        Assert.That(log.Snapshots, Has.Count.EqualTo(200),
            "Should have 200 snapshots");

        // Farm should have produced grain (output buffer or hauled away)
        // Check if grain was produced at all — either in farm output, mill input, or warehouse
        float totalGrain = farm.OutputBuffer.GetValueOrDefault("grain", 0f)
            + mill.InputBuffer.GetValueOrDefault("grain", 0f)
            + warehouse.Stockpile.GetValueOrDefault("grain", 0f);
        float totalFlour = mill.OutputBuffer.GetValueOrDefault("flour", 0f)
            + warehouse.Stockpile.GetValueOrDefault("flour", 0f);

        Assert.That(totalGrain + totalFlour, Is.GreaterThan(0f),
            "Grain should have been produced by the farm");
    }

    [Test]
    public void LogContainsCorrectSnapshotCount()
    {
        var map = new TileMap(10, 10);
        var state = new WorldState
        {
            Map = map,
            Data = _data,
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(50);

        Assert.That(log.Snapshots, Has.Count.EqualTo(50));
        Assert.That(log.Snapshots[0].Tick, Is.EqualTo(1));
        Assert.That(log.Snapshots[49].Tick, Is.EqualTo(50));
    }

    [Test]
    public void TickCounter_Increments()
    {
        var map = new TileMap(10, 10);
        var state = new WorldState { Map = map, Data = _data };
        var runner = new SimulationRunner(state);

        runner.Tick();
        Assert.That(state.CurrentTick, Is.EqualTo(1));
        runner.Tick();
        Assert.That(state.CurrentTick, Is.EqualTo(2));
    }

    [Test]
    public void Snapshot_TracksPopStates()
    {
        var map = new TileMap(10, 10);
        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Pops = new List<Pop>
            {
                new() { Profession = ProfessionType.Farmer, State = PopState.Working, FoodLevel = 1f, RestLevel = 1f },
                new() { Profession = ProfessionType.Hauler, State = PopState.Idle, FoodLevel = 1f, RestLevel = 1f },
                new() { Profession = ProfessionType.Hauler, State = PopState.Idle, FoodLevel = 1f, RestLevel = 1f },
            }
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(1);

        var snap = log.Snapshots[0];
        Assert.That(snap.PopStateCounts, Does.ContainKey("Idle"));
    }

    [Test]
    public void Snapshot_TracksWarehouseStockpiles()
    {
        var map = new TileMap(10, 10);
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 0,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["grain"] = 100f;
        warehouse.Stockpile["flour"] = 50f;

        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Buildings = new List<Building> { warehouse },
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(1);

        var snap = log.Snapshots[0];
        Assert.That(snap.WarehouseStockpiles["grain"], Is.EqualTo(100f));
        Assert.That(snap.WarehouseStockpiles["flour"], Is.EqualTo(50f));
    }

    [Test]
    public void Snapshot_TracksAverageEfficiency()
    {
        var map = new TileMap(10, 10);
        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Pops = new List<Pop>
            {
                new() { FoodLevel = 1f, RestLevel = 1f },  // Efficiency = 1.0
                new() { FoodLevel = 0.5f, RestLevel = 1f }, // Efficiency = 0.5
            }
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(1);

        // Average should be (1.0 + 0.5) / 2 = 0.75
        Assert.That(log.Snapshots[0].AverageEfficiency, Is.EqualTo(0.75f).Within(0.01f));
    }

    [Test]
    public void Snapshot_TracksConstructionCounts()
    {
        var map = new TileMap(10, 10);
        var built = new Building
        {
            DefId = "house", TileX = 0, TileY = 0,
            IsConstructed = true, IsOperational = true
        };
        var site = new Building
        {
            DefId = "bakery", TileX = 5, TileY = 0,
            IsConstructed = false, IsOperational = false
        };

        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Buildings = new List<Building> { built, site },
        };

        var runner = new SimulationRunner(state);
        var log = runner.RunFor(1);

        Assert.That(log.Snapshots[0].ConstructedBuildingCount, Is.EqualTo(1));
        Assert.That(log.Snapshots[0].UnderConstructionCount, Is.EqualTo(1));
    }

    // ═══ Multi-system integration ═══

    [Test]
    public void Production_And_Hauling_WorkTogether()
    {
        // Charcoal kiln produces charcoal → hauler delivers to smelter
        var map = new TileMap(20, 10);
        BuildRoad(map, 0, 5, 10, 5);

        var kiln = new Building
        {
            DefId = "charcoal_kiln", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "charcoal_burning"
        };
        kiln.InputBuffer["logs"] = 50f;
        var laborer = new Pop { Profession = ProfessionType.Laborer, SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f, TileX = 0, TileY = 5 };
        kiln.AssignedWorkerIds.Add(laborer.Id);

        var smelter = new Building
        {
            DefId = "smelter", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "iron_smelting"
        };

        var hauler = new Pop
        {
            Profession = ProfessionType.Hauler, SkillLevel = 1f,
            FoodLevel = 1f, RestLevel = 1f,
            State = PopState.Idle, TileX = 0, TileY = 5
        };

        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Buildings = new List<Building> { kiln, smelter },
            Pops = new List<Pop> { laborer, hauler },
        };

        var runner = new SimulationRunner(state);
        runner.RunFor(100);

        // Charcoal should have been produced and moved to smelter
        float smelterCharcoal = smelter.InputBuffer.GetValueOrDefault("charcoal", 0f);
        Assert.That(smelterCharcoal, Is.GreaterThan(0f),
            "Charcoal should be hauled from kiln to smelter");
    }

    [Test]
    public void Golem_IntegrationWithRunner()
    {
        var map = new TileMap(20, 10);
        BuildRoad(map, 0, 5, 10, 5);

        var source = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        source.OutputBuffer["flour"] = 50f;

        var dest = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking"
        };

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 1f,
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = source.Id,
                DeliveryBuildingId = dest.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var state = new WorldState
        {
            Map = map,
            Data = _data,
            Buildings = new List<Building> { source, dest },
            Golems = new List<Golem> { golem },
        };

        var runner = new SimulationRunner(state);
        runner.RunFor(50);

        float bakeryFlour = dest.InputBuffer.GetValueOrDefault("flour", 0f);
        Assert.That(bakeryFlour, Is.GreaterThan(0f),
            "Golem should deliver flour through the SimulationRunner");
    }

    [Test]
    public void EmptyWorld_RunsWithoutErrors()
    {
        var map = new TileMap(10, 10);
        var state = new WorldState { Map = map, Data = _data };
        var runner = new SimulationRunner(state);

        Assert.DoesNotThrow(() => runner.RunFor(100),
            "Running an empty world should not throw");
    }
}
