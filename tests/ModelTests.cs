using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

namespace SocietyPunk.Tests;

[TestFixture]
public class ModelSerializationTests
{
    [Test]
    public void Good_RoundTrips_Json()
    {
        var good = new Good
        {
            Id = "bread",
            Name = "Bread",
            Category = GoodCategory.ManufacturedGood,
            Tier = 3,
            BaseWeight = 0.5f,
            IsPerishable = true,
            SpoilageRate = 0.01f,
            EraRequired = Era.Agrarian,
            BasePrice = 5.0f
        };

        var json = GameData.Serialize(good);
        var deserialized = GameData.Deserialize<Good>(json)!;

        Assert.That(deserialized.Id, Is.EqualTo(good.Id));
        Assert.That(deserialized.Name, Is.EqualTo(good.Name));
        Assert.That(deserialized.Category, Is.EqualTo(good.Category));
        Assert.That(deserialized.Tier, Is.EqualTo(good.Tier));
        Assert.That(deserialized.BaseWeight, Is.EqualTo(good.BaseWeight));
        Assert.That(deserialized.IsPerishable, Is.EqualTo(good.IsPerishable));
        Assert.That(deserialized.SpoilageRate, Is.EqualTo(good.SpoilageRate));
        Assert.That(deserialized.EraRequired, Is.EqualTo(good.EraRequired));
        Assert.That(deserialized.BasePrice, Is.EqualTo(good.BasePrice));
    }

    [Test]
    public void Recipe_RoundTrips_Json()
    {
        var recipe = new Recipe
        {
            Id = "baking",
            BuildingType = "bakery",
            EraRequired = Era.Agrarian,
            Inputs = new List<GoodQuantity>
            {
                new() { GoodId = "flour", Quantity = 2.0f },
                new() { GoodId = "charcoal", Quantity = 1.0f }
            },
            Outputs = new List<GoodQuantity>
            {
                new() { GoodId = "bread", Quantity = 8.0f }
            },
            Labor = new LaborRequirement
            {
                WorkerCount = 2,
                Profession = ProfessionType.Baker,
                MinSkill = 0.0f
            },
            CycleDuration = 2.0f,
            BaseEfficiency = 1.0f
        };

        var json = GameData.Serialize(recipe);
        var deserialized = GameData.Deserialize<Recipe>(json)!;

        Assert.That(deserialized.Id, Is.EqualTo(recipe.Id));
        Assert.That(deserialized.Inputs, Has.Count.EqualTo(2));
        Assert.That(deserialized.Outputs[0].GoodId, Is.EqualTo("bread"));
        Assert.That(deserialized.Outputs[0].Quantity, Is.EqualTo(8.0f));
        Assert.That(deserialized.Labor.WorkerCount, Is.EqualTo(2));
        Assert.That(deserialized.Labor.Profession, Is.EqualTo(ProfessionType.Baker));
        Assert.That(deserialized.CycleDuration, Is.EqualTo(2.0f));
    }

    [Test]
    public void BuildingDef_RoundTrips_Json()
    {
        var def = new BuildingDef
        {
            Id = "warehouse",
            Name = "Warehouse",
            Role = BuildingRole.Storage,
            EraRequired = Era.Agrarian,
            MaxWorkers = 1,
            FootprintX = 3,
            FootprintY = 3,
            StorageCapacity = 500.0f,
            WarehouseRadius = 10,
            ConstructionCost = new List<GoodQuantity>
            {
                new() { GoodId = "planks", Quantity = 20.0f }
            },
            ConstructionTime = 36
        };

        var json = GameData.Serialize(def);
        var deserialized = GameData.Deserialize<BuildingDef>(json)!;

        Assert.That(deserialized.Id, Is.EqualTo(def.Id));
        Assert.That(deserialized.Role, Is.EqualTo(BuildingRole.Storage));
        Assert.That(deserialized.StorageCapacity, Is.EqualTo(500.0f));
        Assert.That(deserialized.WarehouseRadius, Is.EqualTo(10));
    }

    [Test]
    public void Building_MutableState_Works()
    {
        var building = new Building
        {
            DefId = "bakery",
            TileX = 5,
            TileY = 10,
            IsOperational = true,
            IsConstructed = true,
            ActiveRecipeId = "baking"
        };

        building.InputBuffer["flour"] = 10.0f;
        building.OutputBuffer["bread"] = 5.0f;
        building.OutputBufferDirty = true;

        Assert.That(building.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(building.InputBuffer["flour"], Is.EqualTo(10.0f));
        Assert.That(building.OutputBuffer["bread"], Is.EqualTo(5.0f));
        Assert.That(building.OutputBufferDirty, Is.True);
    }

    [Test]
    public void Pop_Efficiency_Calculation()
    {
        var pop = new Pop { FoodLevel = 1.0f, RestLevel = 1.0f };
        Assert.That(pop.Efficiency, Is.EqualTo(1.0f));

        pop.FoodLevel = 0.5f;
        pop.RestLevel = 0.5f;
        Assert.That(pop.Efficiency, Is.EqualTo(0.25f));

        pop.FoodLevel = 0f;
        pop.RestLevel = 0f;
        Assert.That(pop.Efficiency, Is.EqualTo(0f));

        pop.FoodLevel = 0f;
        pop.RestLevel = 1.0f;
        Assert.That(pop.Efficiency, Is.EqualTo(0.25f)); // foodMod = 0.25 when food=0

        pop.FoodLevel = 1.0f;
        pop.RestLevel = 0f;
        Assert.That(pop.Efficiency, Is.EqualTo(0.5f)); // restMod = 0.5 when rest=0
    }

    [Test]
    public void Pop_RoundTrips_Json()
    {
        var pop = new Pop
        {
            Name = "TestPop",
            Age = 25,
            Race = Race.Goblin,
            Profession = ProfessionType.Hauler,
            SkillLevel = 0.5f,
            FoodLevel = 0.8f,
            RestLevel = 0.9f,
            State = PopState.Hauling,
            TileX = 10,
            TileY = 20
        };

        var json = GameData.Serialize(pop);
        var deserialized = GameData.Deserialize<Pop>(json)!;

        Assert.That(deserialized.Name, Is.EqualTo("TestPop"));
        Assert.That(deserialized.Race, Is.EqualTo(Race.Goblin));
        Assert.That(deserialized.Profession, Is.EqualTo(ProfessionType.Hauler));
        Assert.That(deserialized.FoodLevel, Is.EqualTo(0.8f));
    }

    [Test]
    public void Tech_RoundTrips_Json()
    {
        var tech = new Tech
        {
            Id = "steam_power",
            Name = "Steam Power",
            Era = Era.EarlyIndustrial,
            Prerequisites = new List<string> { "cart_tracks" },
            InfrastructureRequired = new List<string> { "smelter" },
            ResearchCost = 200.0f,
            Effects = new List<TechEffect>
            {
                new() { EffectType = "unlock_building", TargetId = "steam_engine", Value = 1.0f }
            }
        };

        var json = GameData.Serialize(tech);
        var deserialized = GameData.Deserialize<Tech>(json)!;

        Assert.That(deserialized.Id, Is.EqualTo("steam_power"));
        Assert.That(deserialized.Prerequisites, Has.Count.EqualTo(1));
        Assert.That(deserialized.Effects[0].EffectType, Is.EqualTo("unlock_building"));
    }

    [Test]
    public void Golem_RoundTrips_Json()
    {
        var golem = new Golem
        {
            TileX = 5,
            TileY = 10,
            CarryCapacity = 15.0f,
            EssenceLevel = 0.8f,
            EssenceDrainPerTick = 0.002f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = Guid.NewGuid(),
                DeliveryBuildingId = Guid.NewGuid(),
                GoodId = "grain",
                QuantityPerTrip = 10.0f
            }
        };

        var json = GameData.Serialize(golem);
        var deserialized = GameData.Deserialize<Golem>(json)!;

        Assert.That(deserialized.CarryCapacity, Is.EqualTo(15.0f));
        Assert.That(deserialized.EssenceLevel, Is.EqualTo(0.8f));
        Assert.That(deserialized.AssignedRoute, Is.Not.Null);
        Assert.That(deserialized.AssignedRoute!.GoodId, Is.EqualTo("grain"));
        Assert.That(deserialized.IsActive, Is.True);
    }

    [Test]
    public void Golem_IsActive_RequiresEssenceAndRoute()
    {
        var golem = new Golem();
        Assert.That(golem.IsActive, Is.False); // no route

        golem.AssignedRoute = new GolemRoute { GoodId = "grain" };
        Assert.That(golem.IsActive, Is.True); // has route and essence

        golem.EssenceLevel = 0f;
        Assert.That(golem.IsActive, Is.False); // no essence
    }

    [Test]
    public void HaulerTask_RoundTrips_Json()
    {
        var task = new HaulerTask
        {
            PickupBuildingId = Guid.NewGuid(),
            GoodId = "flour",
            Quantity = 5.0f,
            DeliveryBuildingId = Guid.NewGuid(),
            Priority = 2.0f,
            Phase = HaulerTaskPhase.MovingToPickup
        };

        var json = GameData.Serialize(task);
        var deserialized = GameData.Deserialize<HaulerTask>(json)!;

        Assert.That(deserialized.GoodId, Is.EqualTo("flour"));
        Assert.That(deserialized.Quantity, Is.EqualTo(5.0f));
        Assert.That(deserialized.Phase, Is.EqualTo(HaulerTaskPhase.MovingToPickup));
    }

    [Test]
    public void RaceDef_RoundTrips_Json()
    {
        var race = new RaceDef
        {
            Race = Race.Orc,
            Name = "Orc",
            LaborModifiers = new Dictionary<string, float>
            {
                ["mining"] = 1.2f,
                ["smelting"] = 1.1f
            },
            MoveSpeed = 1.1f
        };

        var json = GameData.Serialize(race);
        var deserialized = GameData.Deserialize<RaceDef>(json)!;

        Assert.That(deserialized.Race, Is.EqualTo(Race.Orc));
        Assert.That(deserialized.LaborModifiers["mining"], Is.EqualTo(1.2f));
        Assert.That(deserialized.MoveSpeed, Is.EqualTo(1.1f));
    }
}

[TestFixture]
public class TileMapTests
{
    [Test]
    public void TileMap_Creation()
    {
        var map = new TileMap(1000, 1000);
        Assert.That(map.Width, Is.EqualTo(1000));
        Assert.That(map.Height, Is.EqualTo(1000));
        Assert.That(map.Tiles.Length, Is.EqualTo(1_000_000));
    }

    [Test]
    public void TileMap_InBounds()
    {
        var map = new TileMap(10, 10);
        Assert.That(map.InBounds(0, 0), Is.True);
        Assert.That(map.InBounds(9, 9), Is.True);
        Assert.That(map.InBounds(-1, 0), Is.False);
        Assert.That(map.InBounds(10, 0), Is.False);
    }

    [Test]
    public void TileMap_DefaultTerrain_IsGrass()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(5, 5);
        Assert.That(tile.Terrain, Is.EqualTo(TerrainType.Grass));
        Assert.That(tile.Road, Is.EqualTo(RoadType.None));
        Assert.That(tile.IsPassable, Is.True);
    }

    [Test]
    public void TileMap_MovementCost_GrassNoRoad()
    {
        var map = new TileMap(10, 10);
        Assert.That(map.GetMovementCost(5, 5), Is.EqualTo(2.0f));
    }

    [Test]
    public void TileMap_MovementCost_WithRoad()
    {
        var map = new TileMap(10, 10);
        map.PlaceRoad(5, 5, RoadType.Cobblestone);
        Assert.That(map.GetMovementCost(5, 5), Is.EqualTo(0.7f));
    }

    [Test]
    public void TileMap_MovementCost_Impassable()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(5, 5);
        tile.Terrain = TerrainType.Mountain;
        Assert.That(map.GetMovementCost(5, 5), Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void TileMap_MovementCost_OutOfBounds()
    {
        var map = new TileMap(10, 10);
        Assert.That(map.GetMovementCost(-1, 0), Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void TileMap_PlaceRoad_IncrementsVersion()
    {
        var map = new TileMap(10, 10);
        var v0 = map.RoadVersion;
        map.PlaceRoad(3, 3, RoadType.DirtPath);
        Assert.That(map.RoadVersion, Is.EqualTo(v0 + 1));
    }

    [Test]
    public void TileMap_PlaceBuilding()
    {
        var map = new TileMap(10, 10);
        var id = Guid.NewGuid();
        map.PlaceBuilding(4, 4, id);
        ref var tile = ref map.GetTile(4, 4);
        Assert.That(tile.BuildingId, Is.EqualTo(id));
    }

    [Test]
    public void TileMap_RemoveBuilding()
    {
        var map = new TileMap(10, 10);
        var id = Guid.NewGuid();
        map.PlaceBuilding(4, 4, id);
        map.RemoveBuilding(4, 4);
        ref var tile = ref map.GetTile(4, 4);
        Assert.That(tile.BuildingId, Is.Null);
    }

    [Test]
    public void TileMap_ForestWithRoad_CostsCorrectly()
    {
        var map = new TileMap(10, 10);
        ref var tile = ref map.GetTile(3, 3);
        tile.Terrain = TerrainType.Forest;
        Assert.That(map.GetMovementCost(3, 3), Is.EqualTo(4.0f));

        map.PlaceRoad(3, 3, RoadType.GravelRoad);
        Assert.That(map.GetMovementCost(3, 3), Is.EqualTo(1.5f));
    }

    [Test]
    public void TileMap_Rail_IsFastest()
    {
        var map = new TileMap(10, 10);
        map.PlaceRoad(5, 5, RoadType.Rail);
        Assert.That(map.GetMovementCost(5, 5), Is.EqualTo(0.2f));
    }
}

[TestFixture]
public class GameDataLoaderTests
{
    private string _dataDir = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        // Find the data directory relative to the test assembly
        var dir = TestContext.CurrentContext.TestDirectory;
        // Walk up to find the project root (contains data/ folder)
        while (dir != null && !Directory.Exists(Path.Combine(dir, "data")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        _dataDir = Path.Combine(dir!, "data");
    }

    [Test]
    public void LoadFromDirectory_LoadsAllGoods()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        Assert.That(data.Goods, Has.Count.GreaterThanOrEqualTo(20));
        Assert.That(data.Goods.ContainsKey("bread"), Is.True);
        Assert.That(data.Goods["bread"].Category, Is.EqualTo(GoodCategory.ManufacturedGood));
        Assert.That(data.Goods["bread"].IsPerishable, Is.True);
    }

    [Test]
    public void LoadFromDirectory_LoadsAllRecipes()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        Assert.That(data.Recipes, Has.Count.GreaterThanOrEqualTo(10));
        Assert.That(data.Recipes.ContainsKey("baking"), Is.True);
        Assert.That(data.Recipes["baking"].Inputs, Has.Count.EqualTo(2));
        Assert.That(data.Recipes["baking"].Outputs[0].GoodId, Is.EqualTo("bread"));
    }

    [Test]
    public void LoadFromDirectory_LoadsAllBuildings()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        Assert.That(data.Buildings, Has.Count.GreaterThanOrEqualTo(10));
        Assert.That(data.Buildings.ContainsKey("warehouse"), Is.True);
        Assert.That(data.Buildings["warehouse"].StorageCapacity, Is.EqualTo(500.0f));
        Assert.That(data.Buildings["warehouse"].Role, Is.EqualTo(BuildingRole.Storage));
    }

    [Test]
    public void LoadFromDirectory_LoadsAllTechs()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        Assert.That(data.Techs, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(data.Techs.ContainsKey("steam_power"), Is.True);
        Assert.That(data.Techs["steam_power"].Prerequisites, Contains.Item("cart_tracks"));
    }

    [Test]
    public void LoadFromDirectory_LoadsAllRaces()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        Assert.That(data.Races, Has.Count.EqualTo(5));
        Assert.That(data.Races.ContainsKey(Race.Orc), Is.True);
        Assert.That(data.Races[Race.Orc].MoveSpeed, Is.EqualTo(1.1f));
    }

    [Test]
    public void AllRecipeInputs_ReferenceValidGoods()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        foreach (var recipe in data.Recipes.Values)
        {
            foreach (var input in recipe.Inputs)
            {
                Assert.That(data.Goods.ContainsKey(input.GoodId), Is.True,
                    $"Recipe '{recipe.Id}' references unknown input good '{input.GoodId}'");
            }
            foreach (var output in recipe.Outputs)
            {
                Assert.That(data.Goods.ContainsKey(output.GoodId), Is.True,
                    $"Recipe '{recipe.Id}' references unknown output good '{output.GoodId}'");
            }
        }
    }

    [Test]
    public void AllBuildingRecipes_ReferenceValidRecipes()
    {
        var data = GameData.LoadFromDirectory(_dataDir);
        foreach (var building in data.Buildings.Values)
        {
            foreach (var recipeId in building.AvailableRecipes)
            {
                Assert.That(data.Recipes.ContainsKey(recipeId), Is.True,
                    $"Building '{building.Id}' references unknown recipe '{recipeId}'");
            }
        }
    }
}
