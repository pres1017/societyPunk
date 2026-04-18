using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class GolemSystemTests
{
    private GameData _data = null!;
    private GolemSystem _system = null!;

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
        _system = new GolemSystem();
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

    // ═══ Spec-required test ═══

    [Test]
    public void MillToBakery_FlourMoved_72Ticks_ThenEssenceRunsOut()
    {
        var map = new TileMap(30, 10);

        var mill = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "milling"
        };
        mill.OutputBuffer["flour"] = 50f;

        var bakery = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking"
        };

        BuildRoad(map, 0, 5, 10, 5);

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 1.0f,
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = mill.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var buildings = new List<Building> { mill, bakery };
        var golems = new List<Golem> { golem };

        // Run 72 ticks
        for (int tick = 0; tick < 72; tick++)
            _system.Tick(buildings, golems, _data, map);

        float bakeryFlour = 0f;
        bakery.InputBuffer.TryGetValue("flour", out bakeryFlour);
        Assert.That(bakeryFlour, Is.GreaterThan(0f),
            "Flour should have been moved from mill to bakery");

        // Now let essence run out — keep ticking until it does
        float flourBefore = bakeryFlour;
        while (golem.EssenceLevel > 0)
            _system.Tick(buildings, golems, _data, map);

        Assert.That(golem.EssenceLevel, Is.EqualTo(0f), "Essence should be zero");
        Assert.That(golem.Phase, Is.EqualTo(GolemPhase.Idle), "Golem should be idle when out of essence");

        // Run 10 more ticks — nothing should change
        bakery.InputBuffer.TryGetValue("flour", out float flourAfterStop);
        float millFlourBefore = mill.OutputBuffer.GetValueOrDefault("flour", 0f);

        for (int tick = 0; tick < 10; tick++)
            _system.Tick(buildings, golems, _data, map);

        float flourAfterExtra = 0f;
        bakery.InputBuffer.TryGetValue("flour", out flourAfterExtra);
        Assert.That(flourAfterExtra, Is.EqualTo(flourAfterStop),
            "No flour should move after golem stops");
    }

    // ═══ Essence drain ═══

    [Test]
    public void EssenceDrains_CorrectRate()
    {
        var map = new TileMap(20, 10);
        var mill = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        var bakery = new Building
        {
            DefId = "bakery", TileX = 5, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 5, 5);

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 1.0f,
            EssenceDrainPerTick = 0.01f, // Faster drain for test
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = mill.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 5f
            }
        };

        var buildings = new List<Building> { mill, bakery };
        var golems = new List<Golem> { golem };

        _system.Tick(buildings, golems, _data, map);

        Assert.That(golem.EssenceLevel, Is.EqualTo(0.99f).Within(0.001f),
            "Essence should drain by EssenceDrainPerTick each tick");
    }

    // ═══ Essence zero = inactive ═══

    [Test]
    public void GolemStops_WhenEssenceZero()
    {
        var map = new TileMap(20, 10);
        var mill = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        mill.OutputBuffer["flour"] = 50f;

        var bakery = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 0f, // Already empty
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = mill.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var buildings = new List<Building> { mill, bakery };
        var golems = new List<Golem> { golem };

        for (int tick = 0; tick < 20; tick++)
            _system.Tick(buildings, golems, _data, map);

        Assert.That(golem.Phase, Is.EqualTo(GolemPhase.Idle));
        Assert.That(golem.TileX, Is.EqualTo(0), "Golem should not have moved");
        Assert.That(golem.TileY, Is.EqualTo(5), "Golem should not have moved");

        float bakeryFlour = 0f;
        bakery.InputBuffer.TryGetValue("flour", out bakeryFlour);
        Assert.That(bakeryFlour, Is.EqualTo(0f), "No flour should have been delivered");
    }

    // ═══ Essence refill ═══

    [Test]
    public void GolemRefills_AtWorkshop()
    {
        var map = new TileMap(20, 10);

        var workshop = new Building
        {
            DefId = "golem_workshop", TileX = 5, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        workshop.InputBuffer["magical_essence"] = 5f;

        var mill = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        mill.OutputBuffer["flour"] = 50f;

        var bakery = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);

        var golem = new Golem
        {
            TileX = 5, TileY = 5, // At the workshop
            EssenceLevel = 0.05f, // Below threshold
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = mill.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var buildings = new List<Building> { workshop, mill, bakery };
        var golems = new List<Golem> { golem };

        // Tick enough for the golem to seek and refill
        for (int tick = 0; tick < 20; tick++)
            _system.Tick(buildings, golems, _data, map);

        Assert.That(golem.EssenceLevel, Is.GreaterThan(0.5f),
            "Golem should have refilled essence at workshop");

        float workshopEssence = 0f;
        workshop.InputBuffer.TryGetValue("magical_essence", out workshopEssence);
        Assert.That(workshopEssence, Is.LessThan(5f),
            "Workshop should have consumed magical_essence");
    }

    // ═══ Five production chains ═══

    [Test]
    public void FoodChain_FlourToBackery()
    {
        AssertGolemDelivers("windmill", "bakery", "flour", "baking");
    }

    [Test]
    public void FuelChain_CharcoalToSmelter()
    {
        AssertGolemDelivers("charcoal_kiln", "smelter", "charcoal", "iron_smelting");
    }

    [Test]
    public void ToolsChain_PigIronToBlacksmith()
    {
        AssertGolemDelivers("smelter", "blacksmith", "pig_iron", "smithing");
    }

    [Test]
    public void ClothingChain_FabricToTailor()
    {
        AssertGolemDelivers("weaver", "tailor_shop", "fabric", "tailoring");
    }

    [Test]
    public void ConstructionChain_ClayToBrickKiln()
    {
        AssertGolemDelivers("clay_pit", "brick_kiln", "clay", "brick_making");
    }

    private void AssertGolemDelivers(string sourceDef, string destDef, string goodId, string recipeId)
    {
        var map = new TileMap(30, 10);

        var source = new Building
        {
            DefId = sourceDef, TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        source.OutputBuffer[goodId] = 50f;

        var dest = new Building
        {
            DefId = destDef, TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = recipeId
        };

        BuildRoad(map, 0, 5, 10, 5);

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 1.0f,
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = source.Id,
                DeliveryBuildingId = dest.Id,
                GoodId = goodId,
                QuantityPerTrip = 10f
            }
        };

        var buildings = new List<Building> { source, dest };
        var golems = new List<Golem> { golem };

        for (int tick = 0; tick < 50; tick++)
            _system.Tick(buildings, golems, _data, map);

        float delivered = 0f;
        dest.InputBuffer.TryGetValue(goodId, out delivered);
        Assert.That(delivered, Is.GreaterThan(0f),
            $"{goodId} should have been delivered from {sourceDef} to {destDef}");
    }

    // ═══ Edge cases ═══

    [Test]
    public void GolemWithNoRoute_StaysIdle()
    {
        var map = new TileMap(10, 10);
        var golem = new Golem
        {
            TileX = 5, TileY = 5,
            EssenceLevel = 1.0f,
            AssignedRoute = null
        };

        var golems = new List<Golem> { golem };
        var buildings = new List<Building>();

        for (int tick = 0; tick < 10; tick++)
            _system.Tick(buildings, golems, _data, map);

        Assert.That(golem.Phase, Is.EqualTo(GolemPhase.Idle));
        Assert.That(golem.TileX, Is.EqualTo(5));
        Assert.That(golem.TileY, Is.EqualTo(5));
    }

    [Test]
    public void GolemWaits_WhenPickupEmpty()
    {
        var map = new TileMap(20, 10);
        var mill = new Building
        {
            DefId = "windmill", TileX = 0, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        // No flour in output buffer

        var bakery = new Building
        {
            DefId = "bakery", TileX = 10, TileY = 5,
            IsConstructed = true, IsOperational = true
        };

        BuildRoad(map, 0, 5, 10, 5);

        var golem = new Golem
        {
            TileX = 0, TileY = 5,
            EssenceLevel = 1.0f,
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = mill.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var buildings = new List<Building> { mill, bakery };
        var golems = new List<Golem> { golem };

        // Run enough ticks to arrive at pickup
        for (int tick = 0; tick < 5; tick++)
            _system.Tick(buildings, golems, _data, map);

        // Should be in Loading phase waiting for goods
        Assert.That(golem.Phase, Is.EqualTo(GolemPhase.Loading));
        Assert.That(golem.CarriedQuantity, Is.EqualTo(0f));

        // Add flour to the mill — golem should pick it up
        mill.OutputBuffer["flour"] = 20f;

        for (int tick = 0; tick < 30; tick++)
            _system.Tick(buildings, golems, _data, map);

        float bakeryFlour = 0f;
        bakery.InputBuffer.TryGetValue("flour", out bakeryFlour);
        Assert.That(bakeryFlour, Is.GreaterThan(0f),
            "Golem should deliver flour after source restocks");
    }

    [Test]
    public void Golem_RoundTrips_Json()
    {
        var golem = new Golem
        {
            TileX = 3, TileY = 7,
            EssenceLevel = 0.75f,
            Phase = GolemPhase.MovingToDelivery,
            CarriedGoodId = "flour",
            CarriedQuantity = 10f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = Guid.NewGuid(),
                DeliveryBuildingId = Guid.NewGuid(),
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };

        var json = GameData.Serialize(golem);
        var deserialized = GameData.Deserialize<Golem>(json);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Id, Is.EqualTo(golem.Id));
        Assert.That(deserialized.TileX, Is.EqualTo(3));
        Assert.That(deserialized.TileY, Is.EqualTo(7));
        Assert.That(deserialized.EssenceLevel, Is.EqualTo(0.75f));
        Assert.That(deserialized.Phase, Is.EqualTo(GolemPhase.MovingToDelivery));
        Assert.That(deserialized.CarriedGoodId, Is.EqualTo("flour"));
        Assert.That(deserialized.CarriedQuantity, Is.EqualTo(10f));
        Assert.That(deserialized.AssignedRoute, Is.Not.Null);
        Assert.That(deserialized.AssignedRoute!.GoodId, Is.EqualTo("flour"));
        Assert.That(deserialized.AssignedRoute.QuantityPerTrip, Is.EqualTo(10f));
    }
}
