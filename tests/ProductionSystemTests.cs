using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;

namespace SocietyPunk.Tests;

[TestFixture]
public class ProductionSystemTests
{
    private GameData _data = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "data")))
            dir = Directory.GetParent(dir)?.FullName;
        _data = GameData.LoadFromDirectory(Path.Combine(dir!, "data"));
    }

    /// <summary>
    /// Creates a building + workers ready for production with a given recipe.
    /// Fills input buffer with enough materials for the requested number of cycles.
    /// </summary>
    private (Building building, List<Pop> workers) SetupProduction(
        string recipeId, int cyclesOfInputs = 10, float workerSkill = 1.0f,
        float foodLevel = 1.0f, float restLevel = 1.0f)
    {
        var recipe = _data.Recipes[recipeId];

        var building = new Building
        {
            DefId = recipe.BuildingType,
            IsConstructed = true,
            IsOperational = true,
            ActiveRecipeId = recipeId,
            Condition = 1.0f
        };

        // Fill inputs for N cycles
        foreach (var input in recipe.Inputs)
            building.InputBuffer[input.GoodId] = input.Quantity * cyclesOfInputs;

        // Create qualified workers
        var workers = new List<Pop>();
        for (int i = 0; i < recipe.Labor.WorkerCount; i++)
        {
            var worker = new Pop
            {
                Profession = recipe.Labor.Profession,
                SkillLevel = workerSkill,
                FoodLevel = foodLevel,
                RestLevel = restLevel,
                State = PopState.Working,
                AssignedBuildingId = building.Id
            };
            building.AssignedWorkerIds.Add(worker.Id);
            workers.Add(worker);
        }

        return (building, workers);
    }

    // ═══ Spec-required tests ═══

    [Test]
    public void Bakery_10Ticks_ProducesBread()
    {
        var (building, workers) = SetupProduction("baking", cyclesOfInputs: 10);
        var buildings = new List<Building> { building };

        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.True,
            "Bakery should produce bread after 10 ticks");
        Assert.That(building.OutputBuffer["bread"], Is.GreaterThan(0f));
    }

    [Test]
    public void Bakery_NoFlour_NoBread()
    {
        var (building, workers) = SetupProduction("baking");
        building.InputBuffer.Clear(); // No inputs at all

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False,
            "Bakery with no flour should produce no bread");
    }

    [Test]
    public void Bakery_NoFuel_NoBread()
    {
        var recipe = _data.Recipes["baking"];
        var (building, workers) = SetupProduction("baking");

        // Remove charcoal (fuel) but keep flour
        building.InputBuffer.Remove("charcoal");
        // Keep only flour
        var flourInput = recipe.Inputs.Find(i => i.GoodId == "flour");
        building.InputBuffer["flour"] = flourInput!.Quantity * 10;

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False,
            "Bakery with no charcoal (fuel) should produce no bread");
    }

    // ═══ All five production chains ═══

    [Test]
    public void FoodChain_Baking_ProducesBread()
    {
        // baking: 2 flour + 1 charcoal → 8 bread, 2h cycle, 2 bakers
        var (building, workers) = SetupProduction("baking");
        var buildings = new List<Building> { building };

        // With skill=1.0, food=1.0, rest=1.0, condition=1.0, efficiency=1.0
        // CycleDuration=2.0, so each cycle takes 2 ticks
        // 10 ticks → 5 cycles → 40 bread
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer["bread"], Is.EqualTo(40.0f).Within(0.01f));
    }

    [Test]
    public void FuelChain_CharcoalBurning_ProducesCharcoal()
    {
        // charcoal_burning: 3 logs → 2 charcoal, 8h cycle, 1 laborer
        var (building, workers) = SetupProduction("charcoal_burning");
        var buildings = new List<Building> { building };

        // CycleDuration=8.0, efficiency=1.0 → 1 cycle per 8 ticks
        for (int tick = 0; tick < 16; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("charcoal"), Is.True);
        Assert.That(building.OutputBuffer["charcoal"], Is.EqualTo(4.0f).Within(0.01f));
    }

    [Test]
    public void ConstructionChain_BrickMaking_ProducesBricks()
    {
        // brick_making: 4 clay + 1 charcoal → 3 bricks, 6h cycle, 2 laborers
        var (building, workers) = SetupProduction("brick_making");
        var buildings = new List<Building> { building };

        // CycleDuration=6.0, efficiency=1.0 → 1 cycle per 6 ticks
        for (int tick = 0; tick < 12; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bricks"), Is.True);
        Assert.That(building.OutputBuffer["bricks"], Is.EqualTo(6.0f).Within(0.01f));
    }

    [Test]
    public void ToolsChain_Smithing_ProducesTools()
    {
        // smithing: 1 pig_iron + 0.5 charcoal → 2 tools, 3h cycle, 1 blacksmith (min_skill 0.1)
        var (building, workers) = SetupProduction("smithing");
        var buildings = new List<Building> { building };

        // CycleDuration=3.0, efficiency=1.0 → 1 cycle per 3 ticks
        for (int tick = 0; tick < 9; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("tools"), Is.True);
        Assert.That(building.OutputBuffer["tools"], Is.EqualTo(6.0f).Within(0.01f));
    }

    [Test]
    public void ClothingChain_Tailoring_ProducesClothes()
    {
        // tailoring: 2 fabric → 1 clothes, 6h cycle, 1 tailor (min_skill 0.1)
        var (building, workers) = SetupProduction("tailoring");
        var buildings = new List<Building> { building };

        // CycleDuration=6.0, efficiency=1.0 → 1 cycle per 6 ticks
        for (int tick = 0; tick < 12; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("clothes"), Is.True);
        Assert.That(building.OutputBuffer["clothes"], Is.EqualTo(2.0f).Within(0.01f));
    }

    // ═══ Efficiency tests ═══

    [Test]
    public void HighSkill_ProducesMoreThanLowSkill()
    {
        // High skill worker
        var (highBuilding, highWorkers) = SetupProduction("baking", workerSkill: 1.0f);
        // Low skill worker
        var (lowBuilding, lowWorkers) = SetupProduction("baking", workerSkill: 0.3f);

        for (int tick = 0; tick < 20; tick++)
        {
            ProductionSystem.Tick(new List<Building> { highBuilding }, highWorkers, _data);
            ProductionSystem.Tick(new List<Building> { lowBuilding }, lowWorkers, _data);
        }

        float highOutput = highBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);
        float lowOutput = lowBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);

        Assert.That(highOutput, Is.GreaterThan(lowOutput),
            "High-skill workers should produce more than low-skill workers");
        Assert.That(lowOutput, Is.GreaterThan(0f),
            "Low-skill workers should still produce something");
    }

    [Test]
    public void HungryWorkers_ProduceLess()
    {
        var (fedBuilding, fedWorkers) = SetupProduction("baking", foodLevel: 1.0f);
        var (hungryBuilding, hungryWorkers) = SetupProduction("baking", foodLevel: 0.3f);

        for (int tick = 0; tick < 20; tick++)
        {
            ProductionSystem.Tick(new List<Building> { fedBuilding }, fedWorkers, _data);
            ProductionSystem.Tick(new List<Building> { hungryBuilding }, hungryWorkers, _data);
        }

        float fedOutput = fedBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);
        float hungryOutput = hungryBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);

        Assert.That(fedOutput, Is.GreaterThan(hungryOutput));
    }

    [Test]
    public void DamagedBuilding_ProducesLess()
    {
        var (goodBuilding, goodWorkers) = SetupProduction("baking");
        var (damagedBuilding, damagedWorkers) = SetupProduction("baking");
        damagedBuilding.Condition = 0.5f;

        for (int tick = 0; tick < 20; tick++)
        {
            ProductionSystem.Tick(new List<Building> { goodBuilding }, goodWorkers, _data);
            ProductionSystem.Tick(new List<Building> { damagedBuilding }, damagedWorkers, _data);
        }

        float goodOutput = goodBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);
        float damagedOutput = damagedBuilding.OutputBuffer.GetValueOrDefault("bread", 0f);

        Assert.That(goodOutput, Is.GreaterThan(damagedOutput));
    }

    // ═══ Edge cases ═══

    [Test]
    public void UnconstructedBuilding_DoesNotProduce()
    {
        var (building, workers) = SetupProduction("baking");
        building.IsConstructed = false;

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False);
    }

    [Test]
    public void NoWorkers_DoesNotProduce()
    {
        var (building, _) = SetupProduction("baking");
        building.AssignedWorkerIds.Clear();

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, new List<Pop>(), _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False);
    }

    [Test]
    public void WrongProfession_DoesNotProduce()
    {
        var (building, workers) = SetupProduction("baking");
        // Change all workers to the wrong profession
        foreach (var w in workers)
            w.Profession = ProfessionType.Farmer;

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False);
    }

    [Test]
    public void InsufficientInputs_DoesNotStartCycle()
    {
        var recipe = _data.Recipes["baking"];
        var (building, workers) = SetupProduction("baking");
        // Put in less than one cycle's worth of flour
        building.InputBuffer["flour"] = recipe.Inputs.Find(i => i.GoodId == "flour")!.Quantity * 0.5f;

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False);
    }

    [Test]
    public void InputsConsumedOnCompletion()
    {
        var recipe = _data.Recipes["baking"];
        var (building, workers) = SetupProduction("baking", cyclesOfInputs: 1);

        float initialFlour = building.InputBuffer["flour"];
        float initialCharcoal = building.InputBuffer["charcoal"];

        var buildings = new List<Building> { building };
        // Run enough ticks to complete exactly one cycle (CycleDuration=2, efficiency=1)
        for (int tick = 0; tick < 2; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        // Inputs should be consumed
        float remainingFlour = building.InputBuffer.GetValueOrDefault("flour", 0f);
        float remainingCharcoal = building.InputBuffer.GetValueOrDefault("charcoal", 0f);

        Assert.That(remainingFlour, Is.LessThan(initialFlour));
        Assert.That(remainingCharcoal, Is.LessThan(initialCharcoal));
        Assert.That(building.OutputBufferDirty, Is.True);
    }

    [Test]
    public void SkillBelowMinimum_DoesNotProduce()
    {
        // smithing requires min_skill 0.1
        var (building, workers) = SetupProduction("smithing", workerSkill: 0.05f);

        var buildings = new List<Building> { building };
        for (int tick = 0; tick < 10; tick++)
            ProductionSystem.Tick(buildings, workers, _data);

        Assert.That(building.OutputBuffer.ContainsKey("tools"), Is.False);
    }
}
