using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class HaulerSystemTests
{
    private GameData _data = null!;
    private HaulerSystem _hauler = null!;

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
        _hauler = new HaulerSystem();
        Pathfinder.Directions = Pathfinder.Directions4;
    }

    /// <summary>
    /// Helper: creates a road between two buildings' tile positions.
    /// </summary>
    private static void BuildRoad(TileMap map, int x1, int y1, int x2, int y2, RoadType road = RoadType.Cobblestone)
    {
        // Horizontal then vertical L-shaped road
        int minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
        int minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
        for (int x = minX; x <= maxX; x++)
            map.PlaceRoad(x, y1, road);
        for (int y = minY; y <= maxY; y++)
            map.PlaceRoad(x2, y, road);
    }

    private static Pop MakeHauler(int tileX, int tileY)
    {
        return new Pop
        {
            Profession = ProfessionType.Hauler,
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
    public void FarmToWarehouse_GrainMoved_48Ticks()
    {
        // Place a farm and a warehouse 10 tiles apart with a road
        var map = new TileMap(30, 10);
        var farm = new Building
        {
            DefId = "grain_farm",
            TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 50f;
        farm.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse",
            TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { farm, warehouse };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 48; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float warehouseGrain = 0f;
        warehouse.Stockpile.TryGetValue("grain", out warehouseGrain);

        Assert.That(warehouseGrain, Is.GreaterThan(0f),
            "Grain should have been moved from farm output to warehouse stockpile");
        Assert.That(farm.OutputBuffer.GetValueOrDefault("grain", 0f), Is.LessThan(50f),
            "Farm output buffer should have been depleted");
    }

    // ═══ Five production chains ═══

    [Test]
    public void FoodChain_FlourToBackeryInput()
    {
        var map = new TileMap(30, 10);
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["flour"] = 20f;

        var bakery = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking"
        };
        // Bakery needs flour but has none
        bakery.InputBuffer["charcoal"] = 10f; // Has charcoal, missing flour

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);

        var buildings = new List<Building> { warehouse, bakery };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float bakeryFlour = 0f;
        bakery.InputBuffer.TryGetValue("flour", out bakeryFlour);
        Assert.That(bakeryFlour, Is.GreaterThan(0f),
            "Flour should be delivered from warehouse to bakery input");
    }

    [Test]
    public void FuelChain_CharcoalFromKilnToSmelter()
    {
        var map = new TileMap(30, 10);
        var kiln = new Building
        {
            DefId = "charcoal_kiln", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        kiln.OutputBuffer["charcoal"] = 15f;
        kiln.OutputBufferDirty = true;

        var smelter = new Building
        {
            DefId = "smelter", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "iron_smelting"
        };
        smelter.InputBuffer["iron_ore"] = 10f; // Has ore, missing charcoal

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { kiln, smelter };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float smelterCharcoal = 0f;
        smelter.InputBuffer.TryGetValue("charcoal", out smelterCharcoal);
        Assert.That(smelterCharcoal, Is.GreaterThan(0f),
            "Charcoal should be delivered from kiln to smelter");
    }

    [Test]
    public void ToolsChain_PigIronToBlacksmith()
    {
        var map = new TileMap(30, 10);
        var smelter = new Building
        {
            DefId = "smelter", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        smelter.OutputBuffer["pig_iron"] = 10f;
        smelter.OutputBufferDirty = true;

        var blacksmith = new Building
        {
            DefId = "blacksmith", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "smithing"
        };
        blacksmith.InputBuffer["charcoal"] = 5f; // Has charcoal, missing pig_iron

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { smelter, blacksmith };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float smithIron = 0f;
        blacksmith.InputBuffer.TryGetValue("pig_iron", out smithIron);
        Assert.That(smithIron, Is.GreaterThan(0f),
            "Pig iron should be delivered from smelter to blacksmith");
    }

    [Test]
    public void ClothingChain_FabricToTailor()
    {
        var map = new TileMap(30, 10);
        var weaver = new Building
        {
            DefId = "weaver", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        weaver.OutputBuffer["fabric"] = 10f;
        weaver.OutputBufferDirty = true;

        var tailor = new Building
        {
            DefId = "tailor_shop", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "tailoring"
        };

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { weaver, tailor };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float tailorFabric = 0f;
        tailor.InputBuffer.TryGetValue("fabric", out tailorFabric);
        Assert.That(tailorFabric, Is.GreaterThan(0f),
            "Fabric should be delivered from weaver to tailor");
    }

    [Test]
    public void ConstructionChain_ClayToBrickKiln()
    {
        var map = new TileMap(30, 10);
        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["clay"] = 20f;

        var kiln = new Building
        {
            DefId = "brick_kiln", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "brick_making"
        };
        kiln.InputBuffer["charcoal"] = 5f; // Has charcoal, missing clay

        BuildRoad(map, 0, 5, 10, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { warehouse, kiln };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float kilnCite = 0f;
        kiln.InputBuffer.TryGetValue("clay", out kilnCite);
        Assert.That(kilnCite, Is.GreaterThan(0f),
            "Clay should be delivered from warehouse to brick kiln");
    }

    // ═══ Edge cases ═══

    [Test]
    public void NoTasksGenerated_WhenBuffersClean()
    {
        var map = new TileMap(20, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 50f;
        farm.OutputBufferDirty = false; // Not dirty!

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        var hauler = MakeHauler(0, 5);
        var tasks = new List<HaulerTask>();

        _hauler.Tick(new List<Building> { farm, warehouse }, new List<Pop> { hauler }, tasks, _data, map);

        // No output-buffer-sourced tasks should be created (warehouse supply tasks may still appear for deficits)
        var outputTasks = tasks.FindAll(t => t.PickupBuildingId == farm.Id);
        Assert.That(outputTasks, Has.Count.EqualTo(0),
            "No tasks should be generated from a non-dirty output buffer");
    }

    [Test]
    public void HaulerCancels_WhenPickupEmpty()
    {
        var map = new TileMap(20, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 5f;
        farm.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 5, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 5, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { farm, warehouse };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        // Run 1 tick to generate and assign task
        _hauler.Tick(buildings, pops, tasks, _data, map);
        Assert.That(tasks, Has.Count.GreaterThan(0));

        // Empty the farm output before hauler arrives
        farm.OutputBuffer.Clear();

        // Run enough ticks for hauler to arrive
        for (int tick = 0; tick < 20; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        // Hauler should be idle with the task cancelled
        Assert.That(hauler.State, Is.EqualTo(PopState.Idle));
        Assert.That(hauler.CurrentTaskId, Is.Null);
    }

    [Test]
    public void HaulerRespectsCarryCapacity()
    {
        var map = new TileMap(20, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 100f; // Way more than carry capacity
        farm.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 5, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 5, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { farm, warehouse };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        // Run enough ticks for one delivery
        for (int tick = 0; tick < 20; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float warehouseGrain = 0f;
        warehouse.Stockpile.TryGetValue("grain", out warehouseGrain);

        // Should not exceed carry capacity in a single trip
        Assert.That(warehouseGrain, Is.LessThanOrEqualTo(HaulerSystem.BaseCarryCapacity),
            "Hauler should not carry more than BaseCarryCapacity");
        Assert.That(warehouseGrain, Is.GreaterThan(0f));
    }

    [Test]
    public void MultipleHaulers_WorkSimultaneously()
    {
        var map = new TileMap(30, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 50f;
        farm.OutputBufferDirty = true;

        var kiln = new Building
        {
            DefId = "charcoal_kiln", TileX = 0, TileY = 3,
            IsConstructed = true, IsOperational = true
        };
        kiln.OutputBuffer["charcoal"] = 20f;
        kiln.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);
        BuildRoad(map, 0, 3, 10, 3);
        // Connect the roads
        map.PlaceRoad(10, 4, RoadType.Cobblestone);

        var hauler1 = MakeHauler(0, 5);
        var hauler2 = MakeHauler(0, 3);
        var buildings = new List<Building> { farm, kiln, warehouse };
        var pops = new List<Pop> { hauler1, hauler2 };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 30; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        float warehouseGrain = 0f, warehouseCharcoal = 0f;
        warehouse.Stockpile.TryGetValue("grain", out warehouseGrain);
        warehouse.Stockpile.TryGetValue("charcoal", out warehouseCharcoal);

        // At least one of each should have been delivered
        Assert.That(warehouseGrain, Is.GreaterThan(0f), "Hauler 1 should deliver grain");
        Assert.That(warehouseCharcoal, Is.GreaterThan(0f), "Hauler 2 should deliver charcoal");
    }

    [Test]
    public void HaulerGoesIdle_AfterDelivery()
    {
        var map = new TileMap(10, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 5f;
        farm.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 3, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 3, 5);
        var hauler = MakeHauler(0, 5);
        var buildings = new List<Building> { farm, warehouse };
        var pops = new List<Pop> { hauler };
        var tasks = new List<HaulerTask>();

        // Run enough for a short delivery
        for (int tick = 0; tick < 20; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        Assert.That(hauler.State, Is.EqualTo(PopState.Idle));
        Assert.That(hauler.CurrentTaskId, Is.Null);
        Assert.That(hauler.CargoAmount, Is.EqualTo(0f));
        Assert.That(hauler.CargoGoodId, Is.Null);
    }

    [Test]
    public void NonHaulerPop_NotAssignedTasks()
    {
        var map = new TileMap(20, 10);
        var farm = new Building
        {
            DefId = "grain_farm", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        farm.OutputBuffer["grain"] = 50f;
        farm.OutputBufferDirty = true;

        var warehouse = new Building
        {
            DefId = "warehouse", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);

        // Baker, not a hauler
        var baker = new Pop
        {
            Profession = ProfessionType.Baker,
            State = PopState.Idle,
            TileX = 0, TileY = 5
        };

        var buildings = new List<Building> { farm, warehouse };
        var pops = new List<Pop> { baker };
        var tasks = new List<HaulerTask>();

        for (int tick = 0; tick < 20; tick++)
            _hauler.Tick(buildings, pops, tasks, _data, map);

        // No tasks should be assigned to the baker
        Assert.That(baker.CurrentTaskId, Is.Null);
        Assert.That(baker.State, Is.EqualTo(PopState.Idle));
    }
}
