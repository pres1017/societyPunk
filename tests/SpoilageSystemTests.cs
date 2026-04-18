using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;

namespace SocietyPunk.Tests;

[TestFixture]
public class SpoilageSystemTests
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

    [Test]
    public void PerishableGood_DecaysInOutputBuffer()
    {
        // bread: spoilage_rate = 0.01
        var building = new Building
        {
            DefId = "bakery",
            IsConstructed = true,
            IsOperational = true
        };
        building.OutputBuffer["bread"] = 100f;

        var buildings = new List<Building> { building };
        SpoilageSystem.Tick(buildings, _data);

        Assert.That(building.OutputBuffer["bread"], Is.LessThan(100f));
        // After 1 tick: 100 * (1 - 0.01) = 99
        Assert.That(building.OutputBuffer["bread"], Is.EqualTo(99f).Within(0.01f));
    }

    [Test]
    public void PerishableGood_DecaysInInputBuffer()
    {
        var building = new Building { DefId = "test", IsConstructed = true };
        building.InputBuffer["bread"] = 50f;

        SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.InputBuffer["bread"], Is.LessThan(50f));
    }

    [Test]
    public void PerishableGood_DecaysInStockpile()
    {
        var building = new Building { DefId = "warehouse", IsConstructed = true };
        building.Stockpile["bread"] = 50f;

        SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.Stockpile["bread"], Is.LessThan(50f));
    }

    [Test]
    public void NonPerishableGood_DoesNotDecay()
    {
        var building = new Building { DefId = "warehouse", IsConstructed = true };
        building.Stockpile["pig_iron"] = 100f;
        building.Stockpile["planks"] = 50f;
        building.Stockpile["charcoal"] = 75f;

        for (int tick = 0; tick < 100; tick++)
            SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.Stockpile["pig_iron"], Is.EqualTo(100f));
        Assert.That(building.Stockpile["planks"], Is.EqualTo(50f));
        Assert.That(building.Stockpile["charcoal"], Is.EqualTo(75f));
    }

    [Test]
    public void TinyAmount_RemovedFromBuffer()
    {
        var building = new Building { DefId = "test", IsConstructed = true };
        // Amount small enough that after spoilage it goes below threshold
        building.OutputBuffer["bread"] = 0.0005f;

        SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.OutputBuffer.ContainsKey("bread"), Is.False,
            "Very small amounts should be removed entirely");
    }

    [Test]
    public void MultipleTicks_CumulativeSpoilage()
    {
        var building = new Building { DefId = "warehouse", IsConstructed = true };
        building.Stockpile["bread"] = 100f;

        for (int tick = 0; tick < 10; tick++)
            SpoilageSystem.Tick(new List<Building> { building }, _data);

        // After 10 ticks: 100 * (1 - 0.01)^10 ≈ 90.44
        float expected = 100f * MathF.Pow(1f - 0.01f, 10);
        Assert.That(building.Stockpile["bread"], Is.EqualTo(expected).Within(0.1f));
    }

    [Test]
    public void HighSpoilageRate_DecaysFaster()
    {
        // fish: spoilage_rate = 0.03 (higher than bread's 0.01)
        var building = new Building { DefId = "warehouse", IsConstructed = true };
        building.Stockpile["fish"] = 100f;
        building.Stockpile["bread"] = 100f;

        for (int tick = 0; tick < 10; tick++)
            SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.Stockpile["fish"], Is.LessThan(building.Stockpile["bread"]),
            "Fish (3% spoilage) should decay faster than bread (1% spoilage)");
    }

    [Test]
    public void GameMeat_Spoils()
    {
        // game_meat: spoilage_rate = 0.02
        var building = new Building { DefId = "warehouse", IsConstructed = true };
        building.Stockpile["game_meat"] = 100f;

        SpoilageSystem.Tick(new List<Building> { building }, _data);

        Assert.That(building.Stockpile["game_meat"], Is.EqualTo(98f).Within(0.01f));
    }

    [Test]
    public void EmptyBuilding_NoErrors()
    {
        var building = new Building { DefId = "test", IsConstructed = true };
        Assert.DoesNotThrow(() =>
            SpoilageSystem.Tick(new List<Building> { building }, _data));
    }
}
