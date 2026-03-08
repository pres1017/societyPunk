# Colony Game — Claude Code Agent File v2
## Project: Untitled Dieselpunk Colony Sim
### Focus: Logistics & Automation Core

---

## AGENT MISSION

You are a systems engineering agent building a colony management game in Godot 4
with C#. Your job right now is narrow and specific: build the logistics and
automation core that everything else will grow from. Do not implement economy
systems, markets, wages, or faction politics. The data models should have
placeholder fields for these (a `wage` on Pop, a `price` on Good) but nothing
acts on them yet.

Read this document fully before writing any code.

---

## THE LAYERED CAKE

This project is built in deliberate layers. You are building Layer 1. Do not
build Layer 2 or 3 yet.

**Layer 1 — Logistics & Automation (BUILD NOW)**
Production chains, physical goods movement, worker assignment, transport
networks, automation unlocks. The core loop: build a farm, get grain to a mill,
get flour to a bakery, get bread to your workers so they can keep working.

**Layer 2 — Labor & Needs (BUILD NEXT)**
Pops with individual needs, morale, skill progression, and social dynamics.
Workers who get tired, hungry, and have opinions. RimWorld depth applied to
labor.

**Layer 3 — Economy & Emergence (BUILD LAST)**
Wages, emergent market prices, private ownership, factions, political
consequences. The Victoria 3 layer. The bones exist in the data models from day
one but nothing acts on them until this layer is built.

---

## PLATFORM & TECH STACK

- **Engine:** Godot 4.x (.NET version specifically)
- **Simulation language:** C# — all game logic, data models, and systems
- **UI/scene language:** GDScript — thin layer only, no game logic
- **Architecture:** Simulation layer is pure C# with zero Godot imports.
  Godot Nodes are a presentation wrapper around simulation state.
- **View:** Top-down 2D tile grid (RimWorld / Songs of Syx perspective)
- **Time:** Real-time with pause. Five speeds:
  - 0: Paused
  - 1: Slow (1x) — default
  - 2: Normal (3x)
  - 3: Fast (6x)
  - 4: Very Fast (12x)
  - 5: Maximum (24x)
- **Tick invariance:** Speed only changes ticks-per-real-second. Never skip or
  approximate simulation steps.

---

## SETTING

Starts Napoleonic/pre-industrial. Advances through steam to dieselpunk. Tone is
ASOIAF-to-Scythe: gritty and grounded early, increasingly industrial and strange
late. Magic exists but is rare, low-power, and expensive. Light fantasy elements
(golems, fantastical creatures) are present but fall off as industrialization
advances.

The player is an invisible civic spirit — no physical presence on the map.
Pure management via a cursor, like RimWorld or Dwarf Fortress.

---

## THE CORE LOOP (What Must Feel Good)

This is the game. Everything else is elaboration.

1. Player places a **Grain Farm**. Workers are assigned. Farm produces grain on
   a cycle.
2. Grain sits in the farm's output buffer. A **Hauler pop** picks it up and
   carries it to a **Warehouse** or directly to a **Mill**.
3. Mill consumes grain, produces flour. Flour sits in output buffer.
4. Hauler carries flour to **Bakery**. Bakery produces bread.
5. Bread is distributed to workers. Workers who eat work at full efficiency.
   Workers who don't eat slow down, then stop.
6. Player must **scale** this chain: more farms, better roads, more haulers,
   eventually automated transport.

The scaling problem is the game: **balancing food supply, job slots, and
throughput simultaneously**. Adding 20 new workers means 20 more mouths to
feed, which means more farms, which means more haulers, which means more road
infrastructure, which means more construction workers temporarily, which means
more food pressure. Every scale-up creates a cascade the player must manage.

---

## EARLY GAME (The First Hour)

The player starts with:
- A small group of starting pops (8–12)
- A minimal tile map with terrain already generated
- A stockpile of basic starting goods (some grain, timber, stone)
- No buildings yet

**First priorities in order:**
1. Get food production running (Grain Farm → Mill → Bakery)
2. Get shelter built (basic housing so pops have somewhere to sleep)
3. Get a fuel supply going (wood → charcoal) so buildings can operate
4. Begin expanding the workforce and production capacity

The first hour should feel like RimWorld's opening — urgency without panic,
observable problems with clear solutions, and the satisfaction of a working
supply chain coming together.

---

## PRODUCTION CHAINS (Agrarian Era)

These five chains exist at game start. All others unlock via research and era
progression.

### Food
```
Grain Farm → [grain] → Windmill/Mill → [flour] → Bakery → [bread]
```
Bread is the primary food source. It is perishable. Grain is not.
Secondary food: Hunting Lodge → [game meat], Fishing Dock → [fish]
Both are simpler chains but less scalable.

### Fuel
```
Woodcutter → [logs] → Charcoal Kiln → [charcoal]
(Later) Coal Mine → [coal] → Coke Oven → [coke]
```
Fuel is consumed by buildings that need heat (bakery, smelter, kiln).
Without fuel, those buildings stop production.

### Construction Materials
```
Woodcutter → [logs] → Sawmill → [planks]
Stone Quarry → [stone] → (used directly for stone buildings)
Clay Pit → [clay] → Brick Kiln → [bricks]
```
Planks and bricks are consumed by construction jobs.
Construction is a task, not a building — workers go to the site.

### Tools & Equipment
```
Iron Mine → [iron ore] → Smelter → [pig iron] → Blacksmith → [tools]
```
Tools are consumed by farms, mines, and workshops as an ongoing input.
A building without tools runs at reduced efficiency.

### Clothing
```
Flax Field / Sheep Ranch → [fiber / wool] → Weaver → [fabric] →
Tailor → [clothes]
```
Clothes are a pop need. Pops without clothes work at reduced efficiency
and have lower morale. Clothes are not perishable but wear out slowly
(degradation rate per pop per tick).

---

## GOODS SYSTEM

```csharp
public class Good
{
    public string Id;               // "bread", "iron_ore", "planks"
    public string Name;
    public GoodCategory Category;
    public int Tier;                // 1 (raw) to 5 (advanced)
    public float BaseWeight;        // affects transport capacity
    public bool IsPerishable;
    public float SpoilageRate;      // fraction lost per tick if perishable
    public Era EraRequired;
    public float BasePrice;         // stub for Layer 3 economy, not used yet
}
```

### Good Categories
- `RawResource` — grain, logs, iron ore, clay, fiber, coal
- `ProcessedMaterial` — flour, planks, pig iron, fabric, charcoal, bricks
- `ManufacturedGood` — bread, clothes, tools, candles
- `FuelGood` — charcoal, coal, coke, firewood
- `ConstructionGood` — planks, bricks, stone
- `MagicalGood` — golem cores, spell components (stub, era-gated)

---

## BUILDINGS SYSTEM

### Building Definition

```csharp
public class Building
{
    public Guid Id;
    public BuildingType Type;
    public Vector2I TilePosition;
    public Vector2I Footprint;          // size in tiles

    public BuildingRole Role;           // Production, Storage, Housing,
                                        // Logistics, Magic
    public Era EraRequired;
    public List<Tech> ResearchRequired;

    public float Condition;             // 0.0–1.0; degrades, needs maintenance
    public bool IsOperational;          // false if no fuel, no workers, damaged

    // Staffing
    public List<Pop> AssignedWorkers;
    public int MaxWorkers;
    public ProfessionType RequiredProfession;

    // Production
    public Recipe ActiveRecipe;
    public Dictionary<Good, float> InputBuffer;   // local input stockpile
    public Dictionary<Good, float> OutputBuffer;  // local output stockpile
    public float ProductionProgress;              // 0.0–1.0 within current cycle

    // Stubs for Layer 3
    public Pop Owner;                   // null = player-owned
    public float Wage;                  // stub, not used in Layer 1
}
```

### Building Roles

**Production buildings** — have a recipe, consume inputs, produce outputs.
Workers are building-bound (assigned to the building, work there each shift).
Examples: Farm, Mill, Bakery, Smelter, Blacksmith, Weaver, Tailor.

**Storage buildings** — Warehouses and stockpiles. Accept goods from output
buffers, supply goods to input buffers. The logistics hub of the supply chain.

**Housing** — Pops sleep here. Capacity limits how many pops can be housed.
No workers assigned — pops choose housing based on proximity and quality.

**Logistics buildings** — Manage transport workers. Cart Depot houses Haulers.
Later: Golem Workshop, Canal Dock, Rail Depot.

**Construction sites** — Temporary. Workers go to the site, consume planks/
bricks/stone, complete the building, site disappears.

### Building-Bound vs. Roaming Workers

| Role | Type | Behavior |
|------|------|----------|
| Farmer | Building-bound | Assigned to farm, works there each shift |
| Miller | Building-bound | Assigned to mill |
| Baker | Building-bound | Assigned to bakery |
| Smelter | Building-bound | Assigned to smelter |
| Blacksmith | Building-bound | Assigned to smithy |
| Hauler | Roaming | Picks up goods from output buffers, delivers to input buffers or warehouses |
| Builder | Roaming | Goes to construction sites, consumes materials, builds |
| Woodcutter | Roaming | Goes to trees on map, cuts, returns with logs |
| Miner | Building-bound | Assigned to mine |

---

## POP SYSTEM (Layer 1 Scope)

In Layer 1, pops are functional agents — they work, eat, sleep, and haul.
The deep individual simulation (morale, loyalty, political factions) is Layer 2.
The stubs must exist in the data model.

```csharp
public class Pop
{
    // Identity
    public Guid Id;
    public string Name;
    public int Age;
    public Race Race;               // Human, Orc, Goblin, Elf, Gnoll

    // Work
    public ProfessionType Profession;
    public Building AssignedBuilding;   // null if roaming role
    public float SkillLevel;           // 0.0–1.0; affects production efficiency

    // Basic needs (Layer 1: just food and shelter)
    public float FoodLevel;            // 0.0–1.0; drains per tick, refilled by eating
    public float RestLevel;            // 0.0–1.0; drains while working, refills while sleeping
    public Building Home;              // assigned housing

    // State
    public PopState State;             // Working, Hauling, Eating, Sleeping,
                                       // Idle, Constructing, Resting
    public Vector2I CurrentTile;
    public Vector2I Destination;

    // Layer 2/3 stubs (exist but unused in Layer 1)
    public float Happiness;
    public float Loyalty;
    public float Savings;
    public float Wage;
    public WealthClass WealthClass;
    public Faction Faction;
}
```

### Pop Schedule (Layer 1)

Pops operate on a simple daily schedule:

- **Work shift** (configurable hours): Go to assigned building or roam for tasks.
  Building-bound workers produce. Haulers pick up and deliver goods.
- **Eat break**: Pop goes to the nearest food source (bakery output, stockpile)
  and consumes bread or other food. FoodLevel refills.
- **Sleep**: Pop goes home. RestLevel refills. If no home, pop sleeps on the
  ground (rest refill penalty).

If FoodLevel hits 0: pop works at 25% efficiency.
If RestLevel hits 0: pop works at 50% efficiency.
Both at 0: pop stops working entirely and seeks food/rest.

### Races (Layer 1 Relevant Traits Only)

| Race | Labor Bonus | Notes |
|------|------------|-------|
| Human | None | Adaptable; no modifiers |
| Orc | +20% mining, smelting | Physically strong |
| Goblin | +20% machinery, crafting | Natural engineers |
| Elf | +20% farming, scholarship | Long-lived |
| Gnoll | +20% hauling, scouting | Fast movers |

---

## LOGISTICS SYSTEM

This is the heart of Layer 1. Get it right.

### The Two Transport Modes

**Mode 1 — Hauler Pops (early game, always present)**

Haulers are roaming worker pops with the `Hauler` profession. Each tick, an
idle Hauler evaluates the world and picks the highest-priority task:

```
HaulerTask {
    PickupBuilding: Building,
    PickupGood: Good,
    PickupQuantity: float,
    DeliveryBuilding: Building,
    Priority: float
}
```

Priority is determined by:
- How depleted the destination's input buffer is
- How full the source's output buffer is
- Distance (closer = higher priority, all else equal)

A Hauler picks up goods from an output buffer, walks to the destination, and
deposits into the input buffer. Carry capacity is limited by the Hauler's
equipment (handcart, horse cart, etc.).

**Mode 2 — Automated Transport (unlocked via research/era)**

Automation progressively frees Haulers from basic tasks, allowing them to focus
on higher-complexity work:

| Era | Automation Unlock | What It Replaces |
|-----|------------------|-----------------|
| Agrarian | Golem Hauler (magic) | Basic point-to-point hauling |
| Early Industrial | Horse-drawn cart track | Fixed-route bulk transport |
| Early Industrial | Pipeline (fluids) | Water, oil, later diesel |
| Industrial | Steam wagon route | Long-distance bulk freight |
| Industrial | Conveyor belt (factory interior) | Inter-machine goods flow |
| Dieselpunk | Diesel truck route | High-capacity road freight |
| Dieselpunk | Rail freight | Colony-scale bulk transport |

Each automation unlock reduces the number of Haulers needed for covered routes.
Freed Haulers become available for other tasks or can be retrained. The player
must manage this transition — displaced workers need new assignments.

### The First Automation Moment

When a player first places a **Cart Track** between their mill and bakery, it
should feel like:

*"I don't need to keep assigning haulers to this route anymore. The cart runs
automatically. Now I can send those haulers to the new mining operation."*

This is the core satisfaction loop of the automation layer: identify a
bottleneck, build the infrastructure to automate it, redeploy freed workers to
the next bottleneck.

### Road Network

Roads are built by construction workers. Road quality affects:
- Hauler movement speed (dirt path < gravel road < cobblestone < rail)
- Automated transport availability (some routes require road grade)
- Seasonal penalties (mud season slows dirt paths dramatically)

```csharp
public class RoadTile
{
	public Vector2I Position;
	public RoadType Type;       // DirtPath, GravelRoad, Cobblestone,
								// CartTrack, SteamRoad, Rail
	public float Condition;     // 0.0–1.0; degrades with use
	public float SpeedModifier; // multiplier on movement speed
}
```

The road network is a graph. Pathfinding uses A* with edge weights modified by
road type and condition.

### Warehouses

Warehouses are the logistics hubs. They:
- Accept goods from any nearby output buffer (auto-pull within radius)
- Supply goods to any nearby input buffer (auto-push within radius)
- Provide a central stockpile the player can inspect

The auto-pull/push radius is the "network access" system. Buildings within
warehouse radius can exchange goods passively (slow rate). Buildings outside
radius depend entirely on Hauler pops.

```csharp
public class Warehouse : Building
{
	public float Radius;                            // tiles
	public Dictionary<Good, float> Stockpile;
	public Dictionary<Good, float> ReserveLevels;  // min stockpile before
												   // accepting more
	public Dictionary<Good, float> OutputLimits;   // max to push per tick
}
```

---

## RECIPE SYSTEM

```csharp
public class Recipe
{
	public string Id;
	public BuildingType BuildingType;
	public Era EraRequired;
	public List<string> ResearchRequired;

	public List<GoodQuantity> Inputs;       // consumed per cycle
	public List<GoodQuantity> Outputs;      // produced per cycle

	public LaborRequirement Labor;
	public float CycleDuration;             // in-game hours
	public float BaseEfficiency;            // 1.0 = standard

	// Efficiency modifiers (applied at runtime):
	// actual_efficiency = base * avg_worker_skill * condition_modifier
	//                   * fuel_modifier * tool_modifier
}

public class LaborRequirement
{
	public int WorkerCount;
	public ProfessionType Profession;
	public float MinSkill;              // 0.0 = any, 1.0 = master only
}

public class GoodQuantity
{
	public string GoodId;
	public float Quantity;
}
```

### Sample Recipes (Agrarian Era)

```
Grain Farm:
  inputs:  [tools x0.01 per cycle (wear)]
  outputs: [grain x10]
  labor:   4 Farmers, min skill 0.0
  cycle:   24 hours

Windmill:
  inputs:  [grain x5]
  outputs: [flour x4]
  labor:   1 Miller, min skill 0.0
  cycle:   4 hours

Bakery:
  inputs:  [flour x2, charcoal x1]
  outputs: [bread x8]
  labor:   2 Bakers, min skill 0.0
  cycle:   2 hours

Iron Smelter:
  inputs:  [iron_ore x3, charcoal x2]
  outputs: [pig_iron x2]
  labor:   3 Smelters, min skill 0.0
  cycle:   6 hours

Blacksmith:
  inputs:  [pig_iron x1, charcoal x0.5]
  outputs: [tools x2]
  labor:   1 Blacksmith, min skill 0.1
  cycle:   3 hours

Charcoal Kiln:
  inputs:  [logs x3]
  outputs: [charcoal x2]
  labor:   1 Laborer, min skill 0.0
  cycle:   8 hours

Sawmill:
  inputs:  [logs x2]
  outputs: [planks x3]
  labor:   2 Laborers, min skill 0.0
  cycle:   3 hours

Tailor:
  inputs:  [fabric x2]
  outputs: [clothes x1]
  labor:   1 Tailor, min skill 0.1
  cycle:   6 hours
```

---

## MAGIC SYSTEM (Layer 1 Scope: Golems Only)

Magic is fully designed but mostly deferred. The one magical element that belongs
in Layer 1 is the **Golem Hauler** — the first automation unlock.

### Golem Hauler

A Golem is a magical construct that performs basic hauling tasks. It is not a
pop — it has no needs, no morale, no schedule. It simply executes a fixed
point-to-point route repeatedly.

```csharp
public class Golem
{
	public Guid Id;
	public Vector2I CurrentTile;
	public GolemRoute AssignedRoute;
	public float CarryCapacity;
	public Good CarriedGood;
	public float CarriedQuantity;

	// Maintenance
	public float EssenceLevel;          // 0.0–1.0; drains per tick
	// When EssenceLevel hits 0, Golem stops until refilled
	// EssenceLevel is refilled by a Wizard pop or Golem Workshop
}

public class GolemRoute
{
	public Building Pickup;
	public Building Delivery;
	public Good Good;
	public float QuantityPerTrip;
}
```

Golems require **Golem Cores** (a magical good) to construct and **Magical
Essence** (produced by a Wizard pop or Alchemist) to maintain. They are more
reliable than Hauler pops (never eat, never sleep) but more expensive per unit
of throughput and require magical infrastructure.

This is intentional: early automation requires a magical solution that
eventually gets outcompeted by mechanical solutions. A colony that invested
heavily in Golem infrastructure has sunk costs in the magical supply chain and
will be reluctant to abandon it. This is a soft expression of the
tradition-vs-industry tension.

---

## RESEARCH & TECHNOLOGY (Layer 1 Scope)

Only the techs relevant to logistics and automation are implemented in Layer 1.

```csharp
public class Tech
{
	public string Id;
	public string Name;
	public Era Era;
	public List<string> Prerequisites;
	public List<string> InfrastructureRequired;     // building types that must
													// exist before researching
	public float ResearchCost;
	public List<TechEffect> Effects;
}
```

Research points are generated by a **Scholar's Lodge** building (one per colony
early game). The player spends research points to unlock techs.

### Agrarian Era Logistics Techs

```
Golem Crafting
  prereqs: none
  infra: none
  effects: unlocks Golem Workshop, Golem Hauler unit

Improved Roads
  prereqs: none
  infra: Sawmill
  effects: unlocks Gravel Road (2x speed vs dirt path)

Cart Tracks
  prereqs: Improved Roads
  infra: Blacksmith
  effects: unlocks Cart Track (automated fixed-route transport)

Animal Husbandry
  prereqs: none
  infra: none
  effects: unlocks Ranch, draft animals (horse carts for Haulers, +50% carry)

Crop Rotation
  prereqs: none
  infra: Grain Farm
  effects: +25% grain yield on farms

Basic Masonry
  prereqs: none
  infra: Stone Quarry
  effects: unlocks stone buildings (more durable, fireproof)
```

### Early Industrial Era Logistics Techs (unlocked after Agrarian complete)

```
Steam Power
  prereqs: [Cart Tracks]
  infra: [Iron Smelter, Coal Mine]
  effects: unlocks Steam Engine building, steam-powered production buildings

Pipeline Engineering
  prereqs: [Steam Power]
  infra: [Blacksmith]
  effects: unlocks fluid pipelines (water, later oil and diesel)

Railway Engineering
  prereqs: [Steam Power, Cart Tracks]
  infra: [Iron Smelter, Blacksmith]
  effects: unlocks Rail, Locomotive, Rail Depot
```

---

## SIMULATION TICK ARCHITECTURE

| Tick Type | Frequency | What Updates |
|-----------|-----------|-------------|
| **Movement tick** | Every in-game hour | Pop movement, Hauler pathfinding, Golem routes |
| **Production tick** | Every in-game hour | Building production cycles, buffer updates |
| **Needs tick** | Every in-game day | Pop food and rest levels, eating and sleeping behavior |
| **World tick** | Every in-game month | Era advancement check, world events, research progress |

All tick logic is deterministic given the same starting state and random seed.

### Performance Requirements

- Target: 5000 individual pops without frame rate degradation
- Pop pathfinding: flow-field navigation per zone, not per-pop A*
- Production: O(n) over buildings, not over pops
- Use dirty flags: only recalculate pop needs when inputs change
- Hauler task assignment: priority queue updated on buffer change events,
  not polled every tick

---

## PROJECT STRUCTURE

```
/
├── GAME_DESIGN_FULL.md         # Full vision document (north star)
├── CLAUDE.md                   # Points here, tells Claude to read both
├── src/
│   ├── simulation/             # Pure C# — zero Godot imports
│   │   ├── models/
│   │   │   ├── Pop.cs
│   │   │   ├── Good.cs
│   │   │   ├── Recipe.cs
│   │   │   ├── Building.cs
│   │   │   ├── Golem.cs
│   │   │   ├── RoadTile.cs
│   │   │   ├── Warehouse.cs
│   │   │   └── Tech.cs
│   │   ├── systems/
│   │   │   ├── ProductionSystem.cs     # building production cycles
│   │   │   ├── HaulerSystem.cs         # hauler task assignment and execution
│   │   │   ├── GolemSystem.cs          # golem route execution
│   │   │   ├── NeedsSystem.cs          # pop food and rest
│   │   │   ├── ConstructionSystem.cs   # construction job management
│   │   │   ├── ResearchSystem.cs       # research point accumulation and spend
│   │   │   └── TransportNetwork.cs     # road graph, pathfinding
│   │   ├── world/
│   │   │   ├── TileMap.cs
│   │   │   ├── WorldState.cs
│   │   │   └── SimulationClock.cs
│   │   └── SimulationRunner.cs         # tick orchestrator
│   └── godot/                  # Godot-facing layer — thin wrappers only
│       ├── ui/
│       └── scenes/
├── data/                       # JSON definitions — nothing hardcoded
│   ├── goods.json
│   ├── recipes.json
│   ├── buildings.json
│   ├── techs.json
│   └── races.json
└── tests/                      # Headless NUnit tests
    ├── ProductionTests.cs
    ├── HaulerTests.cs
    └── NeedsTests.cs
```

---

## IMPLEMENTATION ORDER

Build in this exact order. Do not skip ahead.

### Step 1: Data Models + Serialization
- All enums: `Era`, `Race`, `ProfessionType`, `GoodCategory`, `BuildingRole`,
  `PopState`, `RoadType`, `BuildingType`
- `Good`, `Recipe`, `GoodQuantity`, `LaborRequirement`
- `Pop` with all fields including Layer 2/3 stubs
- `Building` with input/output buffers
- `Warehouse`
- `RoadTile`
- `Tech` and `TechEffect`
- `Golem` and `GolemRoute`
- JSON serialization for all models
- Load all goods, recipes, buildings, techs from the `/data/` JSON files
- **Unit tests:** construct every model, serialize to JSON, deserialize, assert
  field equality

### Step 2: Production System
- Building production cycle: check inputs, check workers, check fuel, advance
  progress, complete cycle, push to output buffer
- Efficiency calculation: base × skill × condition × fuel × tool modifiers
- Spoilage: perishable goods in buffers lose a fraction per tick
- **Unit tests:** run a bakery for 10 ticks with full inputs, assert bread
  appears in output buffer. Run with no flour, assert no bread. Run with no
  fuel, assert no bread.

### Step 3: Tile Map + Road Network
- `TileMap`: 2D grid of tiles with terrain type and road state
- Road network graph: nodes are road tiles, edges weighted by road type
- A* pathfinding on the graph
- Flow field generation per destination zone
- **Unit tests:** build a road between two points, assert path exists. Block
  the road, assert path reroutes or fails gracefully.

### Step 4: Hauler System
- Hauler task queue: scan output buffers for excess, scan input buffers for
  deficit, generate tasks, sort by priority
- Hauler pop behavior: idle → pick task → walk to pickup → load → walk to
  delivery → unload → idle
- Carry capacity limits by equipment level
- **Unit tests:** place a farm and a warehouse 10 tiles apart with a road,
  assign a hauler, run 48 ticks, assert grain moved from farm output to
  warehouse stockpile.

### Step 5: Needs System (Basic)
- Pop food level: drains 0.05 per work tick
- Pop rest level: drains 0.03 per work tick, refills 0.1 per sleep tick
- Eating behavior: pop with food level < 0.3 seeks nearest food source
- Work efficiency modifier: food_level × rest_level applied to production
- **Unit tests:** run a pop for 24 hours with no food access, assert food level
  at 0 and efficiency at minimum. Provide food, assert recovery.

### Step 6: Golem System
- Golem route execution: walk to pickup, load, walk to delivery, unload, repeat
- Essence drain per tick
- Essence refill at Golem Workshop
- Golem stops when essence = 0
- **Simulation test:** place a Golem on a mill→bakery route, run 72 ticks,
  assert flour moved. Let essence run out, assert Golem stops.

### Step 7: Construction System
- Construction site as a temporary building
- Builder pop roaming behavior: find nearest site needing materials, check if
  materials are available in nearby stockpile, carry materials to site,
  work on site until complete
- Site completion: convert to finished building
- **Unit tests:** place a construction site for a bakery, stock a nearby
  warehouse with planks and bricks, assign 2 builders, run until complete.

### Step 8: Research System
- Scholar's Lodge generates research points per tick (modified by worker skill)
- Player can queue techs to research
- On completion: apply TechEffect (unlock building type, unlock recipe, unlock
  road type, etc.)
- **Unit tests:** run Scholar's Lodge for 100 ticks, assert research points
  accumulated. Queue "Cart Tracks", assert it completes and unlocks the recipe.

### Step 9: Headless Simulation Harness
- `SimulationRunner` runs N ticks with a given starting state
- Outputs a log: per-tick snapshot of stockpile levels, pop states, building
  outputs, and efficiency ratings
- This harness is your primary tool for balancing production chains before
  building any UI

### Step 10: Godot Integration (thin layer only)
- `TileMapNode`: renders the tile grid from simulation TileMap state
- `PopNode`: renders a pop sprite at their current tile
- `BuildingNode`: renders building at its tile position
- `SimulationClockNode`: advances simulation ticks at the correct speed
- No game logic in any Node script

---

## CODE ARCHITECTURE RULES

1. **No Godot imports in simulation layer.** If a file in `src/simulation/` has
   `using Godot;`, that is a bug.

2. **Data-driven.** Every good, recipe, building type, and tech is defined in
   JSON files in `/data/`. Nothing is hardcoded. The simulation loads
   definitions at startup from these files.

3. **Immutable definitions, mutable state.** `Good`, `Recipe`, `Tech` are
   definition objects — load once, never modify. `Building`, `Pop`, `Golem`
   are state objects — modified every tick.

4. **Events, not polling.** Use C# events to communicate state changes between
   systems. `ProductionSystem` fires `OnOutputBufferChanged`. `HaulerSystem`
   listens and updates its task queue. Don't poll buffers every tick.

5. **Serializable from day one.** All simulation state must produce a valid JSON
   snapshot at any tick. Save/load must work before any feature is considered
   complete.

6. **Test every system headlessly.** Every system in `src/simulation/systems/`
   must have corresponding tests in `/tests/` that run without Godot.

---

## LAYER 3 STUBS (Exist Now, Activated Later)

The following fields exist on models from Step 1 but nothing reads or writes
them in Layer 1. They are commented with `// LAYER 3 STUB`:

- `Pop.Wage` — what this pop earns per tick
- `Pop.Savings` — accumulated wealth
- `Pop.WealthClass` — economic stratum
- `Pop.Happiness` — composite wellbeing
- `Pop.Loyalty` — alignment with government
- `Pop.Faction` — political group membership
- `Good.BasePrice` — starting market price
- `Building.Owner` — pop who owns this building (null = player)
- `Building.Wage` — wage offered to workers at this building

When Layer 3 is built, these stubs become live. The simulation architecture
must not assume they are zero or null in ways that would break when activated.

---

## OPEN QUESTIONS (Flag During Implementation, Don't Block On)

1. **Buffer size limits:** Should building input/output buffers have a maximum
   capacity? Recommend yes — forces players to think about throughput, not just
   connectivity.

2. **Hauler task cancellation:** If a Hauler picks a task and the situation
   changes mid-delivery (destination destroyed, route blocked), how does it
   recover? Recommend: re-evaluate on arrival failure, pick next best task.

3. **Construction material sourcing:** Do builders source materials from any
   warehouse, or only from a designated one? Recommend: nearest warehouse with
   sufficient stock.

4. **Golem vs. Hauler balance:** Golems never sleep or eat but cost magical
   upkeep. Tuning the relative cost is a balance question for playtesting, not
   architecture. Expose the costs as data values in JSON, not hardcoded.

5. **Road degradation rate:** How fast do roads degrade? How expensive is
   repair? Expose as config values.

6. **Day length:** How many real seconds is one in-game hour at Speed 1? This
   affects everything. Recommend starting with 1 in-game hour = 6 real seconds
   at Speed 1, making one in-game day = 2.4 real minutes. Expose as config.

---

## GLOSSARY

| Term | Definition |
|------|-----------|
| Pop | An individual colonist with full simulated state |
| Hauler | A roaming pop whose job is moving goods between buildings |
| Golem | A magical construct performing automated hauling on a fixed route |
| Good | Any tradeable or consumable item in the simulation |
| Recipe | A production formula: inputs + labor + time → outputs |
| Buffer | A building's local storage of input or output goods |
| Warehouse | A central logistics hub with auto-push/pull to nearby buildings |
| Tick | One simulation time step |
| Era | A technology epoch (Agrarian, Early Industrial, Industrial, Dieselpunk) |
| Cart Track | The first mechanical automation unlock; fixed-route bulk transport |
| Flow Field | A pathfinding structure giving every tile a direction toward a goal |
| Throughput | Goods produced or moved per unit of time — the core scaling metric |

---

*End of agent file. Begin with Step 1: Data Models and Serialization.*
