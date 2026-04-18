using NUnit.Framework;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;

namespace SocietyPunk.Tests;

[TestFixture]
public class ResearchSystemTests
{
    private GameData _data = null!;
    private ResearchSystem _system = null!;

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
        _system = new ResearchSystem();
    }

    private static Pop MakeScholar(Guid assignedBuildingId)
    {
        return new Pop
        {
            Profession = ProfessionType.Scholar,
            SkillLevel = 1.0f,
            FoodLevel = 1.0f,
            RestLevel = 1.0f,
            State = PopState.Working
        };
    }

    private static (Building lodge, List<Pop> scholars) MakeLodgeWithScholars(int scholarCount)
    {
        var lodge = new Building
        {
            DefId = "scholars_lodge",
            IsConstructed = true,
            IsOperational = true
        };

        var scholars = new List<Pop>();
        for (int i = 0; i < scholarCount; i++)
        {
            var scholar = MakeScholar(lodge.Id);
            lodge.AssignedWorkerIds.Add(scholar.Id);
            scholars.Add(scholar);
        }

        return (lodge, scholars);
    }

    // ═══ Spec-required test ═══

    [Test]
    public void ScholarsLodge_100Ticks_AccumulatesResearchPoints()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(2);
        var buildings = new List<Building> { lodge };
        var pops = scholars;
        var state = new ResearchState();

        // Queue "improved_roads" (no prerequisites, cost = 50)
        bool queued = _system.QueueTech("improved_roads", state, _data);
        Assert.That(queued, Is.True, "Should be able to queue improved_roads");

        for (int tick = 0; tick < 100; tick++)
            _system.Tick(buildings, pops, _data, state);

        // Should have completed (2 scholars * 0.5 base * 1.5 skill bonus = 1.5/tick, 100 ticks = 150 > 50 cost)
        Assert.That(state.UnlockedTechs, Does.Contain("improved_roads"),
            "improved_roads should be unlocked after 100 ticks with 2 scholars");
    }

    [Test]
    public void QueueCartTracks_WithoutPrereq_Fails()
    {
        var state = new ResearchState();

        // cart_tracks requires improved_roads
        bool queued = _system.QueueTech("cart_tracks", state, _data);
        Assert.That(queued, Is.False,
            "Should not be able to queue cart_tracks without improved_roads unlocked");
    }

    [Test]
    public void QueueCartTracks_WithPrereq_Succeeds()
    {
        var state = new ResearchState();
        state.UnlockedTechs.Add("improved_roads");

        bool queued = _system.QueueTech("cart_tracks", state, _data);
        Assert.That(queued, Is.True,
            "Should be able to queue cart_tracks after improved_roads is unlocked");
    }

    [Test]
    public void CartTracks_Completes_UnlocksRoad()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(3);
        var buildings = new List<Building> { lodge };
        var pops = scholars;
        var state = new ResearchState();
        state.UnlockedTechs.Add("improved_roads");

        _system.QueueTech("cart_tracks", state, _data);

        // cart_tracks cost = 80, 3 scholars * 0.5 * 1.5 = 2.25/tick → ~36 ticks
        for (int tick = 0; tick < 100; tick++)
            _system.Tick(buildings, pops, _data, state);

        Assert.That(state.UnlockedTechs, Does.Contain("cart_tracks"));
        Assert.That(state.UnlockedRoads, Does.Contain("cart_track"),
            "cart_track road type should be unlocked");
    }

    // ═══ Points generation ═══

    [Test]
    public void MoreScholars_FasterResearch()
    {
        int ticksWith1 = TicksToComplete("improved_roads", 1);
        int ticksWith3 = TicksToComplete("improved_roads", 3);

        Assert.That(ticksWith3, Is.LessThan(ticksWith1),
            "3 scholars should complete research faster than 1");
    }

    private int TicksToComplete(string techId, int scholarCount)
    {
        var (lodge, scholars) = MakeLodgeWithScholars(scholarCount);
        var buildings = new List<Building> { lodge };
        var state = new ResearchState();
        _system.QueueTech(techId, state, _data);

        for (int tick = 0; tick < 1000; tick++)
        {
            _system.Tick(buildings, scholars, _data, state);
            if (state.UnlockedTechs.Contains(techId)) return tick;
        }
        return 1000;
    }

    // ═══ Tech effects ═══

    [Test]
    public void GolemCrafting_UnlocksBuilding()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(3);
        var buildings = new List<Building> { lodge };
        var state = new ResearchState();

        _system.QueueTech("golem_crafting", state, _data);

        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, scholars, _data, state);
            if (state.UnlockedTechs.Contains("golem_crafting")) break;
        }

        Assert.That(state.UnlockedTechs, Does.Contain("golem_crafting"));
        Assert.That(state.UnlockedBuildings, Does.Contain("golem_workshop"),
            "golem_workshop should be unlocked by golem_crafting tech");
    }

    [Test]
    public void CropRotation_AppliesProductionBonus()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(3);
        var buildings = new List<Building> { lodge };
        var state = new ResearchState();

        _system.QueueTech("crop_rotation", state, _data);

        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, scholars, _data, state);
            if (state.UnlockedTechs.Contains("crop_rotation")) break;
        }

        Assert.That(state.ProductionBonuses.ContainsKey("grain_farming"), Is.True);
        Assert.That(state.ProductionBonuses["grain_farming"], Is.EqualTo(1.25f));
    }

    [Test]
    public void AnimalHusbandry_AppliesHaulerBonus()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(3);
        var buildings = new List<Building> { lodge };
        var state = new ResearchState();

        _system.QueueTech("animal_husbandry", state, _data);

        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, scholars, _data, state);
            if (state.UnlockedTechs.Contains("animal_husbandry")) break;
        }

        Assert.That(state.HaulerCapacityMultiplier, Is.EqualTo(1.5f));
    }

    // ═══ Queue behavior ═══

    [Test]
    public void QueueAdvances_AfterCompletion()
    {
        var (lodge, scholars) = MakeLodgeWithScholars(3);
        var buildings = new List<Building> { lodge };
        var state = new ResearchState();

        // Queue two techs with no prereqs
        _system.QueueTech("crop_rotation", state, _data);       // cost 40
        _system.QueueTech("animal_husbandry", state, _data);    // cost 60

        Assert.That(state.CurrentResearchId, Is.EqualTo("crop_rotation"));

        for (int tick = 0; tick < 500; tick++)
        {
            _system.Tick(buildings, scholars, _data, state);
            if (state.UnlockedTechs.Contains("animal_husbandry")) break;
        }

        Assert.That(state.UnlockedTechs, Does.Contain("crop_rotation"));
        Assert.That(state.UnlockedTechs, Does.Contain("animal_husbandry"),
            "Queue should advance and complete second tech");
    }

    [Test]
    public void DuplicateQueue_Rejected()
    {
        var state = new ResearchState();
        bool first = _system.QueueTech("crop_rotation", state, _data);
        bool second = _system.QueueTech("crop_rotation", state, _data);

        Assert.That(first, Is.True);
        Assert.That(second, Is.False, "Should not queue the same tech twice");
    }

    [Test]
    public void AlreadyUnlocked_Rejected()
    {
        var state = new ResearchState();
        state.UnlockedTechs.Add("crop_rotation");

        bool queued = _system.QueueTech("crop_rotation", state, _data);
        Assert.That(queued, Is.False, "Should not queue already-unlocked tech");
    }

    // ═══ Edge cases ═══

    [Test]
    public void NoScholars_NoProgress()
    {
        var lodge = new Building
        {
            DefId = "scholars_lodge",
            IsConstructed = true,
            IsOperational = true
        };
        // No workers assigned

        var state = new ResearchState();
        _system.QueueTech("crop_rotation", state, _data);

        for (int tick = 0; tick < 100; tick++)
            _system.Tick(new List<Building> { lodge }, new List<Pop>(), _data, state);

        Assert.That(state.UnlockedTechs, Does.Not.Contain("crop_rotation"),
            "No progress without scholars");
        Assert.That(state.ResearchProgress, Is.EqualTo(0f));
    }

    [Test]
    public void NonScholarWorker_NoProgress()
    {
        var lodge = new Building
        {
            DefId = "scholars_lodge",
            IsConstructed = true,
            IsOperational = true
        };
        var farmer = new Pop
        {
            Profession = ProfessionType.Farmer,
            FoodLevel = 1f, RestLevel = 1f
        };
        lodge.AssignedWorkerIds.Add(farmer.Id);

        var state = new ResearchState();
        _system.QueueTech("crop_rotation", state, _data);

        for (int tick = 0; tick < 100; tick++)
            _system.Tick(new List<Building> { lodge }, new List<Pop> { farmer }, _data, state);

        Assert.That(state.ResearchProgress, Is.EqualTo(0f),
            "Non-scholar worker should not generate research");
    }

    [Test]
    public void ResearchState_RoundTrips_Json()
    {
        var state = new ResearchState
        {
            CurrentResearchId = "cart_tracks",
            ResearchProgress = 42.5f,
        };
        state.UnlockedTechs.Add("improved_roads");
        state.UnlockedBuildings.Add("golem_workshop");
        state.UnlockedRoads.Add("gravel_road");
        state.ProductionBonuses["grain_farming"] = 1.25f;
        state.HaulerCapacityMultiplier = 1.5f;
        state.ResearchQueue.Add("steam_power");

        var json = GameData.Serialize(state);
        var deserialized = GameData.Deserialize<ResearchState>(json);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.CurrentResearchId, Is.EqualTo("cart_tracks"));
        Assert.That(deserialized.ResearchProgress, Is.EqualTo(42.5f));
        Assert.That(deserialized.UnlockedTechs, Does.Contain("improved_roads"));
        Assert.That(deserialized.UnlockedBuildings, Does.Contain("golem_workshop"));
        Assert.That(deserialized.UnlockedRoads, Does.Contain("gravel_road"));
        Assert.That(deserialized.ProductionBonuses["grain_farming"], Is.EqualTo(1.25f));
        Assert.That(deserialized.HaulerCapacityMultiplier, Is.EqualTo(1.5f));
        Assert.That(deserialized.ResearchQueue, Does.Contain("steam_power"));
    }
}
