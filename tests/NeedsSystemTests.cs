using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;

namespace SocietyPunk.Tests;

[TestFixture]
public class NeedsSystemTests
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

    private static Pop MakeWorker(float foodLevel = 1f, float restLevel = 1f, Guid? buildingId = null)
    {
        return new Pop
        {
            Name = "Test Worker",
            Profession = ProfessionType.Baker,
            SkillLevel = 1f,
            FoodLevel = foodLevel,
            RestLevel = restLevel,
            State = PopState.Working,
            AssignedBuildingId = buildingId ?? Guid.NewGuid(),
            TileX = 5, TileY = 5
        };
    }

    private static Building MakeWarehouseWithFood(string foodId = "bread", float amount = 10f,
        int tileX = 5, int tileY = 5)
    {
        var b = new Building
        {
            DefId = "warehouse",
            TileX = tileX, TileY = tileY,
            IsConstructed = true, IsOperational = true
        };
        b.Stockpile[foodId] = amount;
        return b;
    }

    [Test]
    public void WorkingPop_DrainsFoodAndRest()
    {
        var pop = MakeWorker(foodLevel: 1f, restLevel: 1f);
        var buildings = new List<Building> { MakeWarehouseWithFood() };

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.FoodLevel, Is.EqualTo(1f - NeedsSystem.FoodDrainPerWorkTick).Within(0.001f));
        Assert.That(pop.RestLevel, Is.EqualTo(1f - NeedsSystem.RestDrainPerWorkTick).Within(0.001f));
    }

    [Test]
    public void IdlePop_NoDrain()
    {
        var pop = MakeWorker(foodLevel: 0.5f, restLevel: 0.5f);
        pop.State = PopState.Idle;
        pop.AssignedBuildingId = null;
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        // Idle pops don't drain, but at 0.5 food level they go to eating
        // (0.5 >= 0.3, so no transition for food; 0.5 >= 0.3, no transition for rest)
        Assert.That(pop.FoodLevel, Is.EqualTo(0.5f));
        Assert.That(pop.RestLevel, Is.EqualTo(0.5f));
    }

    [Test]
    public void LowFood_SwitchesToEating()
    {
        var pop = MakeWorker(foodLevel: 0.29f, restLevel: 1f);
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.State, Is.EqualTo(PopState.Eating));
    }

    [Test]
    public void LowRest_SwitchesToSleeping()
    {
        var pop = MakeWorker(foodLevel: 0.5f, restLevel: 0.29f);
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.State, Is.EqualTo(PopState.Sleeping));
    }

    [Test]
    public void BothZero_SwitchesToEating()
    {
        var pop = MakeWorker(foodLevel: 0f, restLevel: 0f);
        pop.State = PopState.Idle;
        pop.AssignedBuildingId = null;
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.State, Is.EqualTo(PopState.Eating));
    }

    [Test]
    public void EatingPop_ConsumesFoodFromStockpile()
    {
        var pop = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop.State = PopState.Eating;
        var warehouse = MakeWarehouseWithFood("bread", 5f);
        var buildings = new List<Building> { warehouse };

        float foodBefore = pop.FoodLevel;
        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.FoodLevel, Is.GreaterThan(foodBefore));
        Assert.That(warehouse.Stockpile.GetValueOrDefault("bread"), Is.LessThan(5f));
    }

    [Test]
    public void EatingPop_NoFoodAvailable_StaysEating()
    {
        var pop = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop.State = PopState.Eating;
        var buildings = new List<Building>(); // No buildings with food

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.State, Is.EqualTo(PopState.Eating));
        Assert.That(pop.FoodLevel, Is.EqualTo(0.1f));
    }

    [Test]
    public void EatingPop_ConsumesFromOutputBuffer()
    {
        var pop = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop.State = PopState.Eating;
        var bakery = new Building
        {
            DefId = "bakery", TileX = 5, TileY = 5,
            IsConstructed = true, IsOperational = true
        };
        bakery.OutputBuffer["bread"] = 3f;
        var buildings = new List<Building> { bakery };

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.FoodLevel, Is.GreaterThan(0.1f));
        Assert.That(bakery.OutputBuffer.GetValueOrDefault("bread"), Is.LessThan(3f));
    }

    [Test]
    public void SleepingPop_RefillsRest()
    {
        var pop = MakeWorker(foodLevel: 1f, restLevel: 0.1f);
        pop.State = PopState.Sleeping;
        pop.HomeId = Guid.NewGuid();
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.RestLevel, Is.EqualTo(0.1f + NeedsSystem.RestRefillPerSleepTick).Within(0.001f));
    }

    [Test]
    public void SleepingPop_NoHome_ReducedRefill()
    {
        var pop = MakeWorker(foodLevel: 1f, restLevel: 0.1f);
        pop.State = PopState.Sleeping;
        pop.HomeId = null;
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        Assert.That(pop.RestLevel, Is.EqualTo(0.1f + NeedsSystem.RestRefillNoHomeTick).Within(0.001f));
    }

    [Test]
    public void EatingPop_RecoversToWorking()
    {
        var assignedId = Guid.NewGuid();
        var pop = MakeWorker(foodLevel: 0.79f, restLevel: 1f, buildingId: assignedId);
        pop.State = PopState.Eating;
        // Feed enough to cross 0.8 threshold
        var warehouse = MakeWarehouseWithFood("bread", 10f);
        var buildings = new List<Building> { warehouse };

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        // Bread food_value=0.4, so 0.79+0.4 > 0.8 → should return to working
        Assert.That(pop.FoodLevel, Is.GreaterThanOrEqualTo(NeedsSystem.SatisfiedThreshold));
        Assert.That(pop.State, Is.EqualTo(PopState.Working));
    }

    [Test]
    public void SleepingPop_RecoversToWorking()
    {
        var assignedId = Guid.NewGuid();
        var pop = MakeWorker(foodLevel: 1f, restLevel: 0.75f, buildingId: assignedId);
        pop.State = PopState.Sleeping;
        pop.HomeId = Guid.NewGuid();
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        // 0.75 + 0.1 = 0.85 >= 0.8
        Assert.That(pop.RestLevel, Is.GreaterThanOrEqualTo(NeedsSystem.SatisfiedThreshold));
        Assert.That(pop.State, Is.EqualTo(PopState.Working));
    }

    [Test]
    public void Starvation24Ticks_FoodAtZero()
    {
        var pop = MakeWorker(foodLevel: 1f, restLevel: 1f);
        var buildings = new List<Building>(); // No food

        for (int t = 0; t < 24; t++)
        {
            // Force working state each tick to keep draining
            if (pop.State == PopState.Eating) pop.State = PopState.Working;
            if (pop.State == PopState.Sleeping) pop.State = PopState.Working;
            NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);
        }

        // 24 ticks × 0.05 = 1.2 drain → food should be 0
        Assert.That(pop.FoodLevel, Is.EqualTo(0f));
        Assert.That(pop.Efficiency, Is.EqualTo(0.25f * pop.RestLevel).Within(0.01f));
    }

    [Test]
    public void Recovery_AfterStarvation()
    {
        var assignedId = Guid.NewGuid();
        var pop = MakeWorker(foodLevel: 0f, restLevel: 0.5f, buildingId: assignedId);
        pop.State = PopState.Eating;

        var warehouse = MakeWarehouseWithFood("bread", 10f);
        var buildings = new List<Building> { warehouse };

        // Tick until pop recovers from Eating
        for (int t = 0; t < 10; t++)
        {
            NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);
            if (pop.State == PopState.Working) break;
        }

        Assert.That(pop.State, Is.EqualTo(PopState.Working));
        Assert.That(pop.FoodLevel, Is.GreaterThan(0.25f));
        Assert.That(pop.Efficiency, Is.GreaterThan(0.25f));
    }

    [Test]
    public void FoodPriority_PrefersNearestBuilding()
    {
        var pop = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop.State = PopState.Eating;
        pop.TileX = 0; pop.TileY = 0;

        var farWarehouse = MakeWarehouseWithFood("bread", 10f, tileX: 20, tileY: 20);
        var nearWarehouse = MakeWarehouseWithFood("bread", 10f, tileX: 1, tileY: 0);
        var buildings = new List<Building> { farWarehouse, nearWarehouse };

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        // Near warehouse should be consumed from
        Assert.That(nearWarehouse.Stockpile.GetValueOrDefault("bread"), Is.LessThan(10f));
        Assert.That(farWarehouse.Stockpile["bread"], Is.EqualTo(10f));
    }

    [Test]
    public void HaulerMidTask_NotInterrupted()
    {
        var pop = new Pop
        {
            Name = "Busy Hauler",
            Profession = ProfessionType.Hauler,
            FoodLevel = 0.1f, RestLevel = 0.1f,
            State = PopState.Hauling,
            CurrentTaskId = Guid.NewGuid()
        };
        var buildings = new List<Building>();

        NeedsSystem.Tick(new List<Pop> { pop }, buildings, _data);

        // Hauler should keep hauling (drains but doesn't switch state)
        Assert.That(pop.State, Is.EqualTo(PopState.Hauling));
        Assert.That(pop.FoodLevel, Is.EqualTo(0.1f - NeedsSystem.FoodDrainPerWorkTick).Within(0.001f));
    }

    [Test]
    public void DifferentFoodTypes_DifferentValues()
    {
        // Test that game_meat gives different food value than bread
        var pop1 = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop1.State = PopState.Eating;
        var breadWarehouse = MakeWarehouseWithFood("bread", 5f);

        var pop2 = MakeWorker(foodLevel: 0.1f, restLevel: 1f);
        pop2.State = PopState.Eating;
        var meatWarehouse = MakeWarehouseWithFood("game_meat", 5f);

        NeedsSystem.Tick(new List<Pop> { pop1 }, new List<Building> { breadWarehouse }, _data);
        NeedsSystem.Tick(new List<Pop> { pop2 }, new List<Building> { meatWarehouse }, _data);

        // bread food_value=0.4, game_meat food_value=0.3
        Assert.That(pop1.FoodLevel, Is.GreaterThan(pop2.FoodLevel));
    }
}
