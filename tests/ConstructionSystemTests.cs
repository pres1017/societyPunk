using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class ConstructionSystemTests
{
    private GameData _data = null!;
    private ConstructionSystem _system = null!;

    [OneTimeSetUp]
    public void LoadData()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "data")))
            dir = Directory.GetParent(dir)?.FullName;
        _data = GameData.LoadFromDirectory(Path.Combine(dir!, "data"));
    }

    [SetUp]
    public void ResetSystem()
    {
        _system = new ConstructionSystem();
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

    private static Pop MakeBuilder(int tileX, int tileY)
    {
        return new Pop
        {
            Profession = ProfessionType.Builder,
            SkillLevel = 1.0f,
            FoodLevel = 1.0f,
            RestLevel = 1.0f,
            State = PopState.Idle,
            TileX = tileX,
            TileY = tileY
        };
    }

    // ═══ Spec-required test ═══

    [Test]
    public void BakeryConstruction_WithWarehouse_2Builders_Completes()
    {
        // Bakery: construction_cost = 8 planks + 10 bricks, construction_time = 30
        var map = new TileMap(30, 10);

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["planks"] = 50f;
        warehouse.Stockpile["bricks"] = 50f;

        var bakerySite = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = false, IsOperational = false,
            ConstructionProgress = 0f
        };

        BuildRoad(map, 0, 5, 10, 5);

        var builder1 = MakeBuilder(0, 5);
        var builder2 = MakeBuilder(0, 5);
        var buildings = new List<Building> { warehouse, bakerySite };
        var pops = new List<Pop> { builder1, builder2 };

        // Run until complete or timeout
        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, pops, _data, map);
            if (bakerySite.IsConstructed) break;
        }

        Assert.That(bakerySite.IsConstructed, Is.True,
            "Bakery should be constructed after sufficient ticks");
        Assert.That(bakerySite.IsOperational, Is.True,
            "Bakery should be operational after construction");
    }

    // ═══ Material delivery ═══

    [Test]
    public void Builder_FetchesMaterials_FromWarehouse()
    {
        var map = new TileMap(30, 10);

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["planks"] = 50f;

        // House: construction_cost = 8 planks, construction_time = 16
        var houseSite = new Building
        {
            DefId = "house", TileX = 10, TileY = 5,
            IsConstructed = false, IsOperational = false
        };

        BuildRoad(map, 0, 5, 10, 5);
        var builder = MakeBuilder(0, 5);
        var buildings = new List<Building> { warehouse, houseSite };
        var pops = new List<Pop> { builder };

        // Run enough ticks for builder to fetch and deliver
        for (int tick = 0; tick < 100; tick++)
        {
            _system.Tick(buildings, pops, _data, map);
            if (houseSite.InputBuffer.GetValueOrDefault("planks", 0f) > 0f)
                break;
        }

        float sitePlanks = houseSite.InputBuffer.GetValueOrDefault("planks", 0f);
        Assert.That(sitePlanks, Is.GreaterThan(0f),
            "Builder should have delivered planks to the construction site");
        Assert.That(warehouse.Stockpile.GetValueOrDefault("planks", 0f), Is.LessThan(50f),
            "Warehouse should have less planks after builder picks up");
    }

    // ═══ Multiple builders ═══

    [Test]
    public void TwoBuilders_FasterThanOne()
    {
        int ticksWithOne = RunConstructionAndCountTicks(1);
        int ticksWithTwo = RunConstructionAndCountTicks(2);

        Assert.That(ticksWithTwo, Is.LessThan(ticksWithOne),
            "Two builders should complete construction faster than one");
    }

    private int RunConstructionAndCountTicks(int builderCount)
    {
        var map = new TileMap(20, 10);

        // House: 8 planks, 16 ticks
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["planks"] = 50f;

        var site = new Building
        {
            DefId = "house", TileX = 5, TileY = 5,
            IsConstructed = false, IsOperational = false
        };

        BuildRoad(map, 0, 5, 5, 5);

        var pops = new List<Pop>();
        for (int i = 0; i < builderCount; i++)
            pops.Add(MakeBuilder(0, 5));

        var buildings = new List<Building> { warehouse, site };
        var system = new ConstructionSystem();

        for (int tick = 0; tick < 500; tick++)
        {
            system.Tick(buildings, pops, _data, map);
            if (site.IsConstructed) return tick;
        }

        return 500; // Didn't complete
    }

    // ═══ No materials ═══

    [Test]
    public void Builder_CannotBuild_WithoutMaterials()
    {
        var map = new TileMap(20, 10);

        // House needs 8 planks — no warehouse with planks
        var site = new Building
        {
            DefId = "house", TileX = 5, TileY = 5,
            IsConstructed = false, IsOperational = false
        };

        BuildRoad(map, 0, 5, 5, 5);
        var builder = MakeBuilder(5, 5);
        var buildings = new List<Building> { site };
        var pops = new List<Pop> { builder };

        for (int tick = 0; tick < 100; tick++)
            _system.Tick(buildings, pops, _data, map);

        Assert.That(site.IsConstructed, Is.False,
            "Building should not be constructed without materials");
        Assert.That(site.ConstructionProgress, Is.EqualTo(0f),
            "No construction progress without materials");
    }

    // ═══ Completion ═══

    [Test]
    public void CompletedBuilding_ClearsConstructionMaterials()
    {
        var map = new TileMap(20, 10);

        // Pre-stock the site with all materials
        // House: 8 planks, 16 ticks
        var site = new Building
        {
            DefId = "house", TileX = 5, TileY = 5,
            IsConstructed = false, IsOperational = false
        };
        site.InputBuffer["planks"] = 8f;

        BuildRoad(map, 0, 5, 5, 5);
        var builder = MakeBuilder(5, 5);
        var buildings = new List<Building> { site };
        var pops = new List<Pop> { builder };

        for (int tick = 0; tick < 100; tick++)
        {
            _system.Tick(buildings, pops, _data, map);
            if (site.IsConstructed) break;
        }

        Assert.That(site.IsConstructed, Is.True);
        Assert.That(site.IsOperational, Is.True);
        // Construction materials should be consumed
        Assert.That(site.InputBuffer.GetValueOrDefault("planks", 0f), Is.EqualTo(0f),
            "Construction materials should be cleared after completion");
    }

    // ═══ Five production chains ═══

    [Test]
    public void FoodChain_ConstructGrainFarm()
    {
        // grain_farm: 10 planks + 5 stone, 48 ticks
        AssertCanConstruct("grain_farm",
            new Dictionary<string, float> { ["planks"] = 20f, ["stone"] = 20f });
    }

    [Test]
    public void FuelChain_ConstructCharcoalKiln()
    {
        // charcoal_kiln: 10 stone + 5 clay, 24 ticks
        AssertCanConstruct("charcoal_kiln",
            new Dictionary<string, float> { ["stone"] = 20f, ["clay"] = 20f });
    }

    [Test]
    public void ToolsChain_ConstructSmelter()
    {
        // smelter: 20 stone + 15 bricks, 60 ticks
        AssertCanConstruct("smelter",
            new Dictionary<string, float> { ["stone"] = 30f, ["bricks"] = 30f });
    }

    [Test]
    public void ClothingChain_ConstructWeaver()
    {
        // weaver: 8 planks, 20 ticks
        AssertCanConstruct("weaver",
            new Dictionary<string, float> { ["planks"] = 20f });
    }

    [Test]
    public void ConstructionChain_ConstructBrickKiln()
    {
        // brick_kiln: 15 stone + 10 clay, 30 ticks
        AssertCanConstruct("brick_kiln",
            new Dictionary<string, float> { ["stone"] = 30f, ["clay"] = 30f });
    }

    private void AssertCanConstruct(string buildingDefId, Dictionary<string, float> warehouseStock)
    {
        var map = new TileMap(30, 10);

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        foreach (var kvp in warehouseStock)
            warehouse.Stockpile[kvp.Key] = kvp.Value;

        var site = new Building
        {
            DefId = buildingDefId, TileX = 10, TileY = 5,
            IsConstructed = false, IsOperational = false
        };

        BuildRoad(map, 0, 5, 10, 5);
        var builder = MakeBuilder(0, 5);
        var buildings = new List<Building> { warehouse, site };
        var pops = new List<Pop> { builder };

        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, pops, _data, map);
            if (site.IsConstructed) break;
        }

        Assert.That(site.IsConstructed, Is.True,
            $"{buildingDefId} should be constructable with provided materials");
        Assert.That(site.IsOperational, Is.True);
    }

    // ═══ Edge cases ═══

    [Test]
    public void NonBuilderPop_DoesNotConstruct()
    {
        var map = new TileMap(20, 10);
        var site = new Building
        {
            DefId = "house", TileX = 5, TileY = 5,
            IsConstructed = false, IsOperational = false
        };
        site.InputBuffer["planks"] = 8f;

        var farmer = new Pop
        {
            Profession = ProfessionType.Farmer,
            State = PopState.Idle,
            TileX = 5, TileY = 5,
            FoodLevel = 1f, RestLevel = 1f
        };

        var buildings = new List<Building> { site };
        var pops = new List<Pop> { farmer };

        for (int tick = 0; tick < 50; tick++)
            _system.Tick(buildings, pops, _data, map);

        Assert.That(site.IsConstructed, Is.False,
            "Non-builder pop should not construct buildings");
    }

    [Test]
    public void Builder_GoesIdle_AfterConstruction()
    {
        var map = new TileMap(20, 10);
        var site = new Building
        {
            DefId = "house", TileX = 5, TileY = 5,
            IsConstructed = false, IsOperational = false
        };
        site.InputBuffer["planks"] = 8f;

        BuildRoad(map, 0, 5, 5, 5);
        var builder = MakeBuilder(5, 5);
        var buildings = new List<Building> { site };
        var pops = new List<Pop> { builder };

        for (int tick = 0; tick < 100; tick++)
        {
            _system.Tick(buildings, pops, _data, map);
            if (site.IsConstructed) break;
        }

        Assert.That(site.IsConstructed, Is.True);
        Assert.That(builder.State, Is.EqualTo(PopState.Idle),
            "Builder should return to idle after construction completes");
    }
}
