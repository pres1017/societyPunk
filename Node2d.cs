using Godot;
using System.Collections.Generic;
using System.Linq;
using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;
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

        // ═══ Step 2: Production System ═══
        Log("\n[b]═══ SocietyPunk Step 2 — Production System ═══[/b]\n");

        // Set up a bakery with 2 bakers and inputs for baking
        var testBakery = new Building
        {
            DefId = "bakery",
            TileX = 60, TileY = 50,
            IsConstructed = true, IsOperational = true,
            ActiveRecipeId = "baking",
            Condition = 1.0f
        };
        var bakingRecipe = data.Recipes["baking"];
        foreach (var input in bakingRecipe.Inputs)
            testBakery.InputBuffer[input.GoodId] = input.Quantity * 10;

        var baker1 = new Pop
        {
            Name = "Grukk the Baker",
            Race = Race.Orc, Profession = ProfessionType.Baker,
            SkillLevel = 1.0f, FoodLevel = 1.0f, RestLevel = 1.0f,
            State = PopState.Working, AssignedBuildingId = testBakery.Id
        };
        var baker2 = new Pop
        {
            Name = "Mira Doughknead",
            Race = Race.Human, Profession = ProfessionType.Baker,
            SkillLevel = 0.8f, FoodLevel = 1.0f, RestLevel = 1.0f,
            State = PopState.Working, AssignedBuildingId = testBakery.Id
        };
        testBakery.AssignedWorkerIds.Add(baker1.Id);
        testBakery.AssignedWorkerIds.Add(baker2.Id);

        var prodBuildings = new List<Building> { testBakery };
        var prodPops = new List<Pop> { baker1, baker2 };

        Log("[b]Bakery production (10 ticks):[/b]");
        Log($"  Initial inputs: flour={GetAmount(testBakery.InputBuffer, "flour")}, charcoal={GetAmount(testBakery.InputBuffer, "charcoal")}");

        for (int tick = 1; tick <= 10; tick++)
        {
            ProductionSystem.Tick(prodBuildings, prodPops, data);
            var bread = GetAmount(testBakery.OutputBuffer, "bread");
            var flour = GetAmount(testBakery.InputBuffer, "flour");
            var charcoal = GetAmount(testBakery.InputBuffer, "charcoal");
            Log($"  Tick {tick,2}: bread=[color=green]{bread:F1}[/color]  flour={flour:F1}  charcoal={charcoal:F1}  progress={testBakery.ProductionProgress:F2}");
        }

        var bakeryPass = GetAmount(testBakery.OutputBuffer, "bread") > 0;
        Log($"  Result: [color={(bakeryPass ? "green" : "red")}]{(bakeryPass ? "PASS" : "FAIL")}[/color] — bread produced");

        // Test all five chains briefly
        Log("\n[b]All five production chains (20 ticks each):[/b]");
        var chainTests = new[]
        {
            ("baking", ProfessionType.Baker, "bread", "Food"),
            ("charcoal_burning", ProfessionType.Laborer, "charcoal", "Fuel"),
            ("brick_making", ProfessionType.Laborer, "bricks", "Construction"),
            ("smithing", ProfessionType.Blacksmith, "tools", "Tools"),
            ("tailoring", ProfessionType.Tailor, "clothes", "Clothing"),
        };

        foreach (var (recipeId, profession, outputGood, chainName) in chainTests)
        {
            var r = data.Recipes[recipeId];
            var b = new Building
            {
                DefId = r.BuildingType,
                IsConstructed = true, IsOperational = true,
                ActiveRecipeId = recipeId, Condition = 1.0f
            };
            foreach (var input in r.Inputs)
                b.InputBuffer[input.GoodId] = input.Quantity * 20;

            var workers = new List<Pop>();
            for (int i = 0; i < r.Labor.WorkerCount; i++)
            {
                var w = new Pop
                {
                    Profession = profession, SkillLevel = 1.0f,
                    FoodLevel = 1.0f, RestLevel = 1.0f, State = PopState.Working
                };
                b.AssignedWorkerIds.Add(w.Id);
                workers.Add(w);
            }

            for (int tick = 0; tick < 20; tick++)
                ProductionSystem.Tick(new List<Building> { b }, workers, data);

            var output = GetAmount(b.OutputBuffer, outputGood);
            var pass = output > 0;
            Log($"  {chainName,-14} ({recipeId}): {outputGood}=[color={(pass ? "green" : "red")}]{output:F1}[/color] [{(pass ? "PASS" : "FAIL")}]");
        }

        // Spoilage test
        Log("\n[b]Spoilage system (bread in warehouse, 50 ticks):[/b]");
        var spoilWarehouse = new Building { DefId = "warehouse", IsConstructed = true };
        spoilWarehouse.Stockpile["bread"] = 100f;
        spoilWarehouse.Stockpile["pig_iron"] = 100f;

        Log($"  Before: bread={spoilWarehouse.Stockpile["bread"]:F1}, pig_iron={spoilWarehouse.Stockpile["pig_iron"]:F1}");
        for (int tick = 0; tick < 50; tick++)
            SpoilageSystem.Tick(new List<Building> { spoilWarehouse }, data);

        var breadAfter = GetAmount(spoilWarehouse.Stockpile, "bread");
        var ironAfter = spoilWarehouse.Stockpile["pig_iron"];
        Log($"  After:  bread=[color=yellow]{breadAfter:F1}[/color], pig_iron=[color=green]{ironAfter:F1}[/color]");

        var spoilPass = breadAfter < 100f && ironAfter == 100f;
        Log($"  Result: [color={(spoilPass ? "green" : "red")}]{(spoilPass ? "PASS" : "FAIL")}[/color] — bread decayed, iron unchanged");

        Log("\n[b][color=green]All tests complete. Step 2 verified in Godot.[/color][/b]");

        // ═══ Step 3: Pathfinding & Flow Fields ═══
        Log("\n[b]═══ SocietyPunk Step 3 — Pathfinding ═══[/b]\n");

        // A* pathfinding on a small map
        var pfMap = new SocietyPunk.Simulation.World.TileMap(50, 50);

        // Place a cobblestone road from (0,25) to (40,25)
        for (int rx = 0; rx <= 40; rx++)
            pfMap.PlaceRoad(rx, 25, RoadType.Cobblestone);

        Log("[b]A* Pathfinding:[/b]");
        var pathResult = Pathfinder.FindPath(pfMap, 0, 25, 40, 25);
        var pfPass1 = pathResult != null && pathResult.Steps[^1] == (40, 25);
        Log($"  Road path (0,25)→(40,25): {pathResult?.Steps.Count} steps, cost={pathResult?.TotalCost:F1}  [{(pfPass1 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Path across open grass
        var grassPath = Pathfinder.FindPath(pfMap, 0, 0, 20, 0);
        var pfPass2 = grassPath != null;
        Log($"  Grass path (0,0)→(20,0):  {grassPath?.Steps.Count} steps, cost={grassPath?.TotalCost:F1}  [{(pfPass2 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Road cheaper than grass
        var grassMap2 = new SocietyPunk.Simulation.World.TileMap(50, 50);
        var grassOnly = Pathfinder.FindPath(grassMap2, 0, 25, 40, 25);
        var pfPass3 = pathResult != null && grassOnly != null && pathResult.TotalCost < grassOnly.TotalCost;
        Log($"  Road cost ({pathResult?.TotalCost:F1}) < Grass cost ({grassOnly?.TotalCost:F1}): [{(pfPass3 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Blocked path reroutes
        ref var blockTile = ref pfMap.GetTile(20, 25);
        blockTile.Terrain = TerrainType.Mountain;
        var reroutePath = Pathfinder.FindPath(pfMap, 0, 25, 40, 25);
        var pfPass4 = reroutePath != null && !reroutePath.Steps.Contains((20, 25));
        Log($"  Blocked road reroutes:    {reroutePath?.Steps.Count} steps, avoids (20,25)  [{(pfPass4 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Completely blocked returns null
        for (int by = 0; by < 50; by++)
        {
            ref var wallTile = ref pfMap.GetTile(25, by);
            wallTile.Terrain = TerrainType.Mountain;
        }
        var blockedPath = Pathfinder.FindPath(pfMap, 0, 0, 49, 49);
        var pfPass5 = blockedPath == null;
        Log($"  Full wall blocks path:    null={blockedPath == null}  [{(pfPass5 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Flow field test
        Log("\n[b]Flow Field:[/b]");
        var ffMap = new SocietyPunk.Simulation.World.TileMap(30, 30);
        for (int rx = 0; rx <= 20; rx++)
            ffMap.PlaceRoad(rx, 15, RoadType.Cobblestone);

        var ffSw = System.Diagnostics.Stopwatch.StartNew();
        var field = FlowField.Generate(ffMap, 20, 15);
        ffSw.Stop();
        Log($"  Generated 30x30 flow field in [color=green]{ffSw.ElapsedMilliseconds}ms[/color]");

        // Follow directions from (0, 10) to goal (20, 15)
        int fx = 0, fy = 10;
        int fSteps = 0;
        while ((fx != 20 || fy != 15) && fSteps < 200)
        {
            var (ddx, ddy) = field.GetDirection(fx, fy);
            if (ddx == 0 && ddy == 0) break;
            fx += ddx;
            fy += ddy;
            fSteps++;
        }
        var ffPass1 = fx == 20 && fy == 15;
        Log($"  Follow (0,10)→(20,15): reached ({fx},{fy}) in {fSteps} steps  [{(ffPass1 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Staleness check
        var ffPass2 = !field.IsStale(ffMap);
        ffMap.PlaceRoad(5, 5, RoadType.DirtPath);
        var ffPass3 = field.IsStale(ffMap);
        Log($"  Stale before road change: {!ffPass2} → after: {ffPass3}  [{(ffPass2 && ffPass3 ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        // Large map performance
        var bigMap = new SocietyPunk.Simulation.World.TileMap(200, 200);
        for (int rx = 0; rx < 200; rx++)
            bigMap.PlaceRoad(rx, 100, RoadType.Cobblestone);

        var bigSw = System.Diagnostics.Stopwatch.StartNew();
        var bigPath = Pathfinder.FindPath(bigMap, 0, 0, 199, 199);
        bigSw.Stop();
        Log($"  A* 200x200: {bigSw.ElapsedMilliseconds}ms, {bigPath?.Steps.Count} steps  [{(bigPath != null ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        var bigFfSw = System.Diagnostics.Stopwatch.StartNew();
        var bigField = FlowField.Generate(bigMap, 100, 100);
        bigFfSw.Stop();
        Log($"  FlowField 200x200: {bigFfSw.ElapsedMilliseconds}ms  [{(bigField.GetCost(0, 0) < float.MaxValue ? "[color=green]PASS[/color]" : "[color=red]FAIL[/color]")}]");

        Log("\n[b][color=green]All tests complete. Step 3 verified in Godot.[/color][/b]");

        // ═══ Step 4: Hauler System ═══
        Log("\n[b]═══ SocietyPunk Step 4 — Hauler System ═══[/b]\n");

        var haulerMap = new SocietyPunk.Simulation.World.TileMap(30, 30);
        for (int rx = 0; rx <= 25; rx++)
            haulerMap.PlaceRoad(rx, 10, RoadType.Cobblestone);

        var haulerSystem = new HaulerSystem();

        // --- Test 1: Production output → Warehouse ---
        Log("[b]Test 1: Output buffer → Warehouse[/b]");
        {
            var hWarehouse = new Building
            {
                DefId = "warehouse", TileX = 0, TileY = 10,
                IsConstructed = true, IsOperational = true
            };
            var hBakery = new Building
            {
                DefId = "bakery", TileX = 10, TileY = 10,
                IsConstructed = true, IsOperational = true,
                ActiveRecipeId = "baking"
            };
            hBakery.OutputBuffer["bread"] = 8f;
            hBakery.OutputBufferDirty = true;

            var hPop1 = new Pop
            {
                Name = "Hauler One", Profession = ProfessionType.Hauler,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Idle, TileX = 5, TileY = 10
            };

            var hBuildings = new List<Building> { hWarehouse, hBakery };
            var hPops = new List<Pop> { hPop1 };
            var hTasks = new List<HaulerTask>();

            for (int t = 0; t < 30; t++)
                haulerSystem.Tick(hBuildings, hPops, hTasks, data, haulerMap);

            var whBread1 = GetAmount(hWarehouse.Stockpile, "bread");
            var bkBread1 = GetAmount(hBakery.OutputBuffer, "bread");
            var pass1 = whBread1 > 0 && bkBread1 < 8f;
            Log($"  Warehouse bread: [color=green]{whBread1:F1}[/color], Bakery remaining: {bkBread1:F1}");
            Log($"  Hauler state: {hPop1.State}, tasks: {hTasks.Count}");
            Log($"  Result: [color={(pass1 ? "green" : "red")}]{(pass1 ? "PASS" : "FAIL")}[/color] — bread moved to warehouse");
        }

        // --- Test 2: Warehouse → Production input ---
        Log("\n[b]Test 2: Warehouse → Production input[/b]");
        {
            var hs2 = new HaulerSystem();
            var hWh2 = new Building
            {
                DefId = "warehouse", TileX = 0, TileY = 10,
                IsConstructed = true, IsOperational = true
            };
            hWh2.Stockpile["flour"] = 20f;
            hWh2.Stockpile["charcoal"] = 10f;

            var hBk2 = new Building
            {
                DefId = "bakery", TileX = 15, TileY = 10,
                IsConstructed = true, IsOperational = true,
                ActiveRecipeId = "baking"
            };

            var hPop2a = new Pop
            {
                Name = "Hauler Two", Profession = ProfessionType.Hauler,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Idle, TileX = 5, TileY = 10
            };
            var hPop2b = new Pop
            {
                Name = "Hauler Three", Profession = ProfessionType.Hauler,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Idle, TileX = 7, TileY = 10
            };

            var hBlds2 = new List<Building> { hWh2, hBk2 };
            var hPops2 = new List<Pop> { hPop2a, hPop2b };
            var hTasks2 = new List<HaulerTask>();

            for (int t = 0; t < 40; t++)
                hs2.Tick(hBlds2, hPops2, hTasks2, data, haulerMap);

            var bkFlour = GetAmount(hBk2.InputBuffer, "flour");
            var bkCharcoal = GetAmount(hBk2.InputBuffer, "charcoal");
            var pass2 = bkFlour > 0 || bkCharcoal > 0;
            Log($"  Bakery inputs: flour=[color=green]{bkFlour:F1}[/color], charcoal=[color=green]{bkCharcoal:F1}[/color]");
            Log($"  Warehouse remaining: flour={GetAmount(hWh2.Stockpile, "flour"):F1}, charcoal={GetAmount(hWh2.Stockpile, "charcoal"):F1}");
            Log($"  Result: [color={(pass2 ? "green" : "red")}]{(pass2 ? "PASS" : "FAIL")}[/color] — goods delivered to bakery input");
        }

        // --- Test 3: Full production + hauling loop ---
        Log("\n[b]Test 3: Production → Hauling → Warehouse loop[/b]");
        {
            var hs3 = new HaulerSystem();
            var hWh3 = new Building
            {
                DefId = "warehouse", TileX = 0, TileY = 10,
                IsConstructed = true, IsOperational = true
            };
            var hBk3 = new Building
            {
                DefId = "bakery", TileX = 12, TileY = 10,
                IsConstructed = true, IsOperational = true,
                ActiveRecipeId = "baking", Condition = 1.0f
            };
            hBk3.InputBuffer["flour"] = 20f;
            hBk3.InputBuffer["charcoal"] = 10f;

            var bkrPop1 = new Pop
            {
                Name = "Baker Bob", Profession = ProfessionType.Baker,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Working, AssignedBuildingId = hBk3.Id
            };
            var bkrPop2 = new Pop
            {
                Name = "Baker Sue", Profession = ProfessionType.Baker,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Working, AssignedBuildingId = hBk3.Id
            };
            hBk3.AssignedWorkerIds.Add(bkrPop1.Id);
            hBk3.AssignedWorkerIds.Add(bkrPop2.Id);

            var hPop3 = new Pop
            {
                Name = "Hauler Fast", Profession = ProfessionType.Hauler,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Idle, TileX = 6, TileY = 10
            };

            var hBlds3 = new List<Building> { hWh3, hBk3 };
            var hPops3 = new List<Pop> { bkrPop1, bkrPop2, hPop3 };
            var hTasks3 = new List<HaulerTask>();

            for (int t = 0; t < 50; t++)
            {
                ProductionSystem.Tick(hBlds3, hPops3, data);
                hs3.Tick(hBlds3, hPops3, hTasks3, data, haulerMap);
            }

            var whBread3 = GetAmount(hWh3.Stockpile, "bread");
            var completed3 = 0;
            foreach (var ht in hTasks3)
                if (ht.Phase == HaulerTaskPhase.Completed) completed3++;

            var pass3 = whBread3 > 0 && completed3 > 0;
            Log($"  Warehouse bread: [color=green]{whBread3:F1}[/color]");
            Log($"  Tasks completed: [color=green]{completed3}[/color] / {hTasks3.Count} total");
            Log($"  Result: [color={(pass3 ? "green" : "red")}]{(pass3 ? "PASS" : "FAIL")}[/color] — production + hauling integrated");
        }

        // --- Test 4: Blocked path cancels task ---
        Log("\n[b]Test 4: Blocked path → task cancelled[/b]");
        {
            var hs4 = new HaulerSystem();
            var blockedHMap = new SocietyPunk.Simulation.World.TileMap(20, 20);
            for (int wy = 0; wy < 20; wy++)
            {
                ref var wt = ref blockedHMap.GetTile(10, wy);
                wt.Terrain = TerrainType.Mountain;
            }

            var hWh4 = new Building
            {
                DefId = "warehouse", TileX = 0, TileY = 5,
                IsConstructed = true, IsOperational = true
            };
            var hBk4 = new Building
            {
                DefId = "bakery", TileX = 15, TileY = 5,
                IsConstructed = true, IsOperational = true,
                ActiveRecipeId = "baking"
            };
            hBk4.OutputBuffer["bread"] = 5f;
            hBk4.OutputBufferDirty = true;

            var hPop4 = new Pop
            {
                Name = "Blocked Hauler", Profession = ProfessionType.Hauler,
                SkillLevel = 1f, FoodLevel = 1f, RestLevel = 1f,
                State = PopState.Idle, TileX = 15, TileY = 5
            };

            var hBlds4 = new List<Building> { hWh4, hBk4 };
            var hPops4 = new List<Pop> { hPop4 };
            var hTasks4 = new List<HaulerTask>();

            for (int t = 0; t < 10; t++)
                hs4.Tick(hBlds4, hPops4, hTasks4, data, blockedHMap);

            var cancelled4 = 0;
            foreach (var ht in hTasks4)
                if (ht.Phase == HaulerTaskPhase.Cancelled) cancelled4++;

            var pass4 = cancelled4 > 0;
            Log($"  Tasks cancelled: [color=yellow]{cancelled4}[/color] / {hTasks4.Count}");
            Log($"  Hauler state: {hPop4.State}");
            Log($"  Result: [color={(pass4 ? "green" : "red")}]{(pass4 ? "PASS" : "FAIL")}[/color] — unreachable path cancels task");
        }

        Log("\n[b][color=green]All tests complete. Step 4 verified in Godot.[/color][/b]");
    }

    private static float GetAmount(Dictionary<string, float> buffer, string key)
    {
        return buffer.TryGetValue(key, out var val) ? val : 0f;
    }

    private void Log(string msg)
    {
        _log.AppendText(msg + "\n");
    }
}
