using Godot;
using System.Linq;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

public partial class Node2d : Node2D
{
    private RichTextLabel _log = null!;

    public override void _Ready()
    {
        _log = GetNode<RichTextLabel>("Log");
        RunTests();
    }

    private void RunTests()
    {
        Log("[b]═══ SocietyPunk Step 1 — Data Model Test ═══[/b]\n");

        // 1. Load game data from JSON
        var dataDir = ProjectSettings.GlobalizePath("res://data");
        Log("[b]Loading game data...[/b]");
        var data = GameData.LoadFromDirectory(dataDir);

        Log($"  Goods loaded:     [color=green]{data.Goods.Count}[/color]");
        Log($"  Recipes loaded:   [color=green]{data.Recipes.Count}[/color]");
        Log($"  Buildings loaded: [color=green]{data.Buildings.Count}[/color]");
        Log($"  Techs loaded:     [color=green]{data.Techs.Count}[/color]");
        Log($"  Races loaded:     [color=green]{data.Races.Count}[/color]");

        // 2. Display production chains
        Log("\n[b]═══ Production Chains (Agrarian) ═══[/b]");
        var chains = new[] { "grain_farming", "milling", "baking", "charcoal_burning", "iron_smelting", "smithing" };
        foreach (var id in chains)
        {
            if (!data.Recipes.TryGetValue(id, out var recipe)) continue;
            var inputs = string.Join(" + ", recipe.Inputs.ConvertAll(i => $"{i.Quantity} {i.GoodId}"));
            var outputs = string.Join(" + ", recipe.Outputs.ConvertAll(o => $"{o.Quantity} {o.GoodId}"));
            Log($"  [color=yellow]{id}[/color]: {inputs} → {outputs}  ({recipe.CycleDuration}h, {recipe.Labor.WorkerCount} workers)");
        }

        // 3. TileMap test
        Log("\n[b]═══ TileMap (1000x1000) ═══[/b]");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var map = new SocietyPunk.Simulation.World.TileMap(1000, 1000);
        sw.Stop();
        Log($"  Created 1M tile grid in [color=green]{sw.ElapsedMilliseconds}ms[/color]");
        Log($"  Memory: ~{map.Tiles.Length * 12 / 1024}KB (12 bytes/tile)");

        // Place some terrain
        for (int x = 100; x < 200; x++)
            for (int y = 100; y < 200; y++)
                map.GetTile(x, y).Terrain = TerrainType.Forest;

        for (int x = 300; x < 320; x++)
            for (int y = 300; y < 320; y++)
                map.GetTile(x, y).Terrain = TerrainType.Mountain;

        // Place roads
        for (int x = 0; x < 500; x++)
            map.PlaceRoad(x, 50, RoadType.Cobblestone);

        Log($"  Movement costs:");
        Log($"    Grass (no road):      [color=white]{map.GetMovementCost(0, 0)}[/color]");
        Log($"    Grass (cobblestone):  [color=white]{map.GetMovementCost(50, 50)}[/color]");
        Log($"    Forest (no road):     [color=white]{map.GetMovementCost(150, 150)}[/color]");
        Log($"    Mountain:             [color=white]{(map.GetMovementCost(310, 310) == float.MaxValue ? "IMPASSABLE" : map.GetMovementCost(310, 310).ToString())}[/color]");
        Log($"  Road version after changes: {map.RoadVersion}");

        // 4. Create some test entities
        Log("\n[b]═══ Entity Construction ═══[/b]");

        var warehouse = new Building
        {
            DefId = "warehouse",
            TileX = 50, TileY = 50,
            IsConstructed = true, IsOperational = true
        };
        warehouse.Stockpile["grain"] = 100f;
        warehouse.Stockpile["flour"] = 50f;
        warehouse.Stockpile["bread"] = 25f;
        Log($"  Warehouse [{warehouse.Id.ToString()[..8]}]: grain={warehouse.Stockpile["grain"]}, flour={warehouse.Stockpile["flour"]}, bread={warehouse.Stockpile["bread"]}");

        var bakery = new Building
        {
            DefId = "bakery",
            TileX = 55, TileY = 50,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking"
        };
        bakery.InputBuffer["flour"] = 10f;
        bakery.InputBuffer["charcoal"] = 5f;
        bakery.OutputBuffer["bread"] = 3f;
        bakery.OutputBufferDirty = true;
        Log($"  Bakery [{bakery.Id.ToString()[..8]}]: inputs=[flour:{bakery.InputBuffer["flour"]}, charcoal:{bakery.InputBuffer["charcoal"]}] outputs=[bread:{bakery.OutputBuffer["bread"]}] dirty={bakery.OutputBufferDirty}");

        var pop = new Pop
        {
            Name = "Grukk the Baker",
            Age = 28,
            Race = Race.Orc,
            Profession = ProfessionType.Baker,
            SkillLevel = 0.6f,
            FoodLevel = 0.9f,
            RestLevel = 0.7f,
            State = PopState.Working,
            AssignedBuildingId = bakery.Id,
            TileX = 55, TileY = 50
        };
        Log($"  Pop: [color=cyan]{pop.Name}[/color] ({pop.Race} {pop.Profession}) efficiency={pop.Efficiency:F2}");

        var hauler = new Pop
        {
            Name = "Skritt Fastpaws",
            Age = 19,
            Race = Race.Goblin,
            Profession = ProfessionType.Hauler,
            SkillLevel = 0.3f,
            FoodLevel = 1.0f,
            RestLevel = 1.0f,
            State = PopState.Hauling,
            TileX = 50, TileY = 50,
            CargoGoodId = "flour",
            CargoAmount = 5.0f
        };
        Log($"  Pop: [color=cyan]{hauler.Name}[/color] ({hauler.Race} {hauler.Profession}) carrying {hauler.CargoAmount} {hauler.CargoGoodId}");

        var golem = new Golem
        {
            TileX = 48, TileY = 50,
            CarryCapacity = 15f,
            EssenceLevel = 0.85f,
            AssignedRoute = new GolemRoute
            {
                PickupBuildingId = warehouse.Id,
                DeliveryBuildingId = bakery.Id,
                GoodId = "flour",
                QuantityPerTrip = 10f
            }
        };
        Log($"  Golem [{golem.Id.ToString()[..8]}]: active={golem.IsActive}, essence={golem.EssenceLevel:P0}, route={golem.AssignedRoute.GoodId}");

        // 5. Race modifiers
        Log("\n[b]═══ Race Definitions ═══[/b]");
        foreach (var race in data.Races.Values)
        {
            var mods = race.LaborModifiers.Count > 0
                ? string.Join(", ", race.LaborModifiers.Select(kv => $"{kv.Key}:{kv.Value:F1}"))
                : "none";
            Log($"  [color=yellow]{race.Name}[/color]: speed={race.MoveSpeed:F1}, modifiers=[{mods}]");
        }

        // 6. Tech tree preview
        Log("\n[b]═══ Tech Tree ═══[/b]");
        foreach (var tech in data.Techs.Values)
        {
            var prereqs = tech.Prerequisites.Count > 0 ? string.Join(", ", tech.Prerequisites) : "none";
            var effects = string.Join(", ", tech.Effects.ConvertAll(e => $"{e.EffectType}({e.TargetId})"));
            Log($"  [{tech.Era}] [color=yellow]{tech.Name}[/color]: prereqs=[{prereqs}], cost={tech.ResearchCost}, effects=[{effects}]");
        }

        // 7. Serialization round-trip
        Log("\n[b]═══ JSON Serialization ═══[/b]");
        var json = GameData.Serialize(pop);
        var roundTripped = GameData.Deserialize<Pop>(json)!;
        var match = roundTripped.Name == pop.Name && roundTripped.Race == pop.Race && roundTripped.Profession == pop.Profession;
        Log($"  Pop round-trip: [color={(match ? "green" : "red")}]{(match ? "PASS" : "FAIL")}[/color]");

        json = GameData.Serialize(golem);
        var golemRT = GameData.Deserialize<Golem>(json)!;
        var golemMatch = golemRT.EssenceLevel == golem.EssenceLevel && golemRT.AssignedRoute?.GoodId == "flour";
        Log($"  Golem round-trip: [color={(golemMatch ? "green" : "red")}]{(golemMatch ? "PASS" : "FAIL")}[/color]");

        Log("\n[b][color=green]All tests complete. Step 1 verified in Godot.[/color][/b]");
    }

    private void Log(string msg)
    {
        _log.AppendText(msg + "\n");
    }
}
