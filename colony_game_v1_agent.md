# Colony Game — Claude Code Agent File
## Project: Untitled Dieselpunk Colony City-State Game

---

## AGENT MISSION

You are a game design and systems engineering agent. Your task is to design and implement the **core data models, simulation logic, and foundational systems** for a colony management game with deep economic simulation. This document is your complete briefing. Read it fully before writing any code.

The player is not a dictator or factory owner — they are the **civic spirit of a growing settlement**: a guiding intelligence that zones land, builds public infrastructure, sets tax policy, shapes diplomatic relations, and nudges conditions, while individual colonists ("pops") live, work, earn, spend, and make their own economic decisions. The economy is emergent, not commanded. There is no colonial overlord — the player is building an independent city-state from agrarian roots to dieselpunk industrialism.

---

## THEMATIC CORE — THE METANARRATIVE

This game has a central philosophical tension that should inform every system:

**The heroic and handcrafted giving way to the mass-produced and industrial.**

Early game relies on scarcity and excellence: hero units, master craftsmen, rare wizards, artisan goods. These are powerful but fragile and hard to replace. Late game offers industrial abundance: artillery, mechs, factories, mass-produced goods. These are efficient but coarse, and they erode the culture that produced the heroes.

The player is never forced to choose a side — the tension is **soft and emergent**, arising from what they build, research, and prioritize. A player who clings to the old ways can make it work, but at increasing cost. A player who embraces industrialism gains power but loses something harder to quantify — in faction attitudes, pop culture, and the kinds of stories the colony tells about itself.

This tension should be visible in:
- Faction politics (Traditionalists vs. Industrialists)
- Pop happiness metrics (some pops mourn the old ways)
- Military unit design (hero units vs. mass conscript armies)
- Magic (rare and wondrous early, hybridized or marginalized late)
- Goods (artisan quality vs. factory output)

---

## PLATFORM & TECH STACK

- **Engine:** Godot 4.x
- **Language:** C# for all simulation/data logic; GDScript permitted for UI glue only
- **Architecture:** Simulation-first. All economic and pop simulation must be cleanly separated from rendering so it can be tested headlessly.
- **Target view:** Top-down 2D tile grid (RimWorld / Songs of Syx perspective)
- **Time model:** Real-time with pause. Five simulation speeds:
  - Speed 0: Paused
  - Speed 1: Slow (1x) — default, careful management
  - Speed 2: Normal (3x)
  - Speed 3: Fast (6x)
  - Speed 4: Very Fast (12x) — for idle/stable periods
  - Speed 5: Maximum (24x) — skip through uneventful stretches

All simulation logic must be tick-based and speed-invariant — speeding up just runs more ticks per real second, never skips simulation steps.

---

## SETTING & AESTHETIC

**Tone:** ASOIAF → Scythe. Grounded, gritty, and political early. Strange and wondrous things exist at the edges of the world. As the game progresses the aesthetic shifts: gas lamps, coal smoke, newspapers, flat caps, diesel fumes, arc welding sparks. Magic becomes rarer and stranger against an industrial backdrop.

**Eras of progression** (hybrid: world advances on a global timeline, player colony can lead or lag):

1. **Agrarian Era** — hand tools, animal labor, subsistence farming, cottage industries, melee warfare, hero units common, magic more accepted
2. **Early Industrial Era** — steam power, early mechanization, textile mills, coal mining, ranged weapons (muskets), artillery emerging
3. **Industrial Era** — railways, mass production, factories, urbanization, rifles, early artillery dominance, magic increasingly marginalized
4. **Late Industrial / Dieselpunk Era** — diesel engines, chemical industries, mass-market goods, tanks and mechs, magic rare but potent if hybridized with industrial goods

---

## WORLD MAP & SITE SELECTION

### World Generation
The world map is **procedurally generated each new game** using a seeded algorithm. The map contains:
- Terrain types: plains, forest, hills, mountains, desert, wetlands, coastline
- River systems (affect transport, water access, mill power)
- Mineral deposit regions (iron, coal, stone, rare magical materials)
- Soil quality gradients (arable land for farming)
- Fauna distributions (wildlife, fantastical creatures, dangerous zones)
- Rival colony starting positions
- Indigenous/fantastical faction territories

### Site Selection
Before the colony is founded, the player views the world map and selects a starting tile. Site choice has **meaningful, lasting mechanical consequences**:

```
SiteAttributes {
    // Water
    river_access: bool
    river_size: Enum { None, Stream, River, Major }

    // Agriculture
    soil_quality: float             // 0.0–1.0; affects farm yield permanently
    arable_fraction: float

    // Minerals
    deposits: List<MineralDeposit> {
        good: Good
        richness: float
        depth: Enum { Surface, Shallow, Deep }
    }

    // Fauna
    wildlife: List<FaunaPresence> {
        species: CreatureType
        density: float
        disposition: Enum { Passive, Territorial, Hostile }
    }

    // Climate
    climate: ClimateType
    seasonal_hazards: List<HazardType>

    // Strategic
    defensibility: float
    proximity_to_rivals: float
    trade_route_potential: int
}
```

The player can compare sites before committing. Site selection is permanent.

---

## FANTASY RACES

Each pop belongs to one of five races. Race provides **economic and military stat differences and cultural flavor** but does not directly determine political preference — that emerges from wealth class, occupation, and education.

### Race Definitions

| Race | Labor Traits | Military Traits | Magic Relation | Reproduction | Cultural Notes |
|------|-------------|-----------------|----------------|--------------|----------------|
| **Human** | Adaptable; no penalties or bonuses | Average across unit types | Neutral | Standard | Politically flexible; swing vote in most factions |
| **Orc** | +mining, smelting, construction; -scholarly | +melee and ranged; excellent soldiers | Distrust of magic; loyalty penalty if magic prominent in government | Standard | Clan-oriented; respond well to strong military government |
| **Goblin** | +engineering, tinkering, machinery; -farming | Weak individually; excel as artillery crews, engineers, saboteurs | Affinity for artifice; treat enchanted goods as craft | High (fast reproducing) | Natural engineers; thrive in industrial era; form strong guild factions |
| **Elf** | +farming, scholarship, magic; -heavy industry | Skilled archers and hero candidates; poor conscripts | High magic affinity; elven wizards most powerful | Very low (long-lived) | Traditionalist; resist rapid era advancement |
| **Gnoll** | +hunting, scouting, military logistics | Pack tactics; excel in skirmish and cavalry | Distrust of magic | Moderate | Pack loyalty makes them reliable soldiers; suspicious of outsiders |

### Starting Composition
The player selects a **primary founding race**. Other races immigrate over time based on colony reputation, world events, and immigration policy.

---

## FOUNDING IDEOLOGY

At game start, the player selects a **founding ideology** that sets initial conditions without permanently locking anything.

| Ideology | Government | Magic Acceptance | Starting Bonus | Starting Penalty |
|----------|-----------|-----------------|----------------|-----------------|
| **Devout** | Monarchy (Theocratic) | High | Religious buildings grant loyalty bonus; healing magic cheaper | Low dissent tolerance; Reformists appear earlier |
| **Mercantile** | Merchant Oligarchy | Low | Trade routes +20% revenue; Merchant Guild starts organized | Magic stigma; Laborers' Union forms faster |
| **Militarist** | Monarchy (Military) | Low | Standing army from day one; pops tolerate hardship better | Higher ongoing military costs; Reformist tension builds |
| **Pragmatist** | Republic (early form) | Moderate | Balanced start; most flexible reform path | No strong early bonuses; slower initial growth |
| **Arcane** | Monarchy (Scholarly) | Very High | Wizard apprentices from day one; magic research faster | Magic reliance creates vulnerability as eras advance |
| **Communal** | Council of Guilds | Moderate | Pop happiness starts higher; guild factions cooperative | Slower capital accumulation; wealthy pop emergence delayed |

---

## GOVERNMENT SYSTEM

### Government Types
- **Monarchy** — broad unilateral power; marriage diplomacy available; loyalty swings sharper
- **Merchant Oligarchy** — wealthy pops have formal power; economic policies easier; military reforms harder
- **Military Junta** — soldier pops elevated; conscription easier; civil liberties reforms very difficult
- **Council of Guilds** — profession guilds share governance; balanced but slow; strong labor protections
- **Republic** — faction approval required for most policies; most stable long-term; most flexible diplomatically

### Government Reform
The player reforms government via a policy menu. Each reform:
- Requires minimum faction approval ratings
- Has a transition cost (loyalty disruption, treasury, time)
- May trigger opposition events

Reform is gradual — moving from Monarchy to Republic passes through intermediate steps, each unlocking new policy options.

---

## POP SIMULATION

### The Base Unit
Each pop is an **individual simulated entity**. A city of 500 pops has 500 discrete agents.

### Pop Attributes

```
Pop {
    id: UUID
    name: string
    age: int
    race: Race

    wealth_class: WealthClass { Destitute, Poor, WorkingClass, Artisan, MiddleClass, UpperClass, Elite }
    profession: ProfessionType
    employer: Building?
    skill_level: float              // 0.0–1.0

    is_hero: bool
    hero_class: HeroClass?
    hero_condition_progress: float

    savings: float
    wage: float

    needs: NeedState
    health: float                   // 0.0–1.0
    happiness: float

    loyalty: float                  // 0.0–1.0
    political_faction: Faction?

    magic_affinity: float           // 0.0–1.0
    is_magic_user: bool
    magic_school: MagicSchool?

    home: Building?
    workplace: Building?
    current_tile: Vector2i
}
```

### Social Mobility
Pops move between wealth classes based on sustained savings, skill advancement, education access, and housing availability. Movement is gradual.

### Pop Lifecycle
Pops are born, age (child → adult → elder), and die. Immigration and emigration respond to colony conditions vs. world reputation.

---

## NEEDS SYSTEM

| Tier | Wealth Classes | Goods Required |
|------|---------------|----------------|
| **Subsistence** | All | Water, Bread, Firewood, Basic Shelter |
| **Working Class** | WorkingClass+ | Clothing, Lamp Oil, Salt, Tobacco |
| **Artisan** | Artisan+ | Spirits, Tailored Clothes, Basic Medicine, Newspapers |
| **Middle Class** | MiddleClass+ | Quality Furniture, Fine Foods, Books, Quality Medicine |
| **Elite** | UpperClass, Elite | Fine Furniture, Imported Luxuries, Art, Carriages, Fine Spirits |

Unmet needs reduce happiness → reduce loyalty → trigger disloyalty consequences. Severely unmet basic needs cause health decline and death.

---

## GOODS SYSTEM

```
Good {
    id: string
    name: string
    category: GoodCategory
    tier: int                       // 1 (raw) to 5 (advanced manufactured)
    base_weight: float
    is_perishable: bool
    spoilage_rate: float
    era_required: Era
    is_magical: bool
    magic_school: MagicSchool?
    quality: QualityType            // Artisan or Factory (where applicable)
}
```

### Good Categories
- **Raw Resources:** water, grain, coal, iron ore, timber, clay, hemp, cotton, rare herbs, magical crystals
- **Processed Materials:** flour, pig iron, planks, fabric, coke, charcoal, refined magical essence
- **Manufactured Goods:** bread, clothing, tools, furniture, weapons, lamp oil, medicine, spell components
- **Military Goods:** muskets, rifles, cannon, powder, shot, uniforms, rations, artillery shells, tank parts, mech components
- **Magical Goods:** spell components, enchanted weapons, warded armor, alchemical mixtures, refined essence
- **Hybrid Goods:** diesel-enchanted shells, magically-reinforced armor plate, alchemical diesel
- **Luxury Goods:** fine spirits, art, carriages, imported spices, fine clothing
- **Capital Goods:** steam engines, machine parts, rail segments, diesel engines, mech frames
- **Services:** healthcare, education, entertainment, banking (abstracted as goods)

### Artisan vs. Factory Goods
Many goods have two production variants:
- **Artisan:** higher quality, higher pop happiness bonus, skilled labor, slow and expensive
- **Factory:** lower quality, cheaper, fast, less skill required, lower happiness bonus

Pops notice which version they receive. This distinction is a mechanical expression of the metanarrative.

---

## PRODUCTION & RECIPES

```
Recipe {
    id: string
    building_type: BuildingType
    era_required: Era
    research_required: List<Tech>?
    infrastructure_required: List<BuildingType>?
    inputs: List<GoodQuantity>
    outputs: List<GoodQuantity>
    labor_required: LaborRequirement {
        worker_count: int
        profession: ProfessionType
        min_skill: float
    }
    cycle_duration: float
    base_efficiency: float
    output_quality: QualityType
}
```

### Sample Production Chains

**Food:** Grain Farm → [grain] → Mill → [flour] → Bakery → [bread]

**Iron & Tools:** Iron Mine → [iron ore] → Smelter → [pig iron] → Blacksmith → [tools/weapons]
*(Industrial: Blast Furnace → Steel Mill → Machine Shop)*

**Military (era-gated):**
- Agrarian: Blacksmith → [melee weapons], Armorer → [armor]
- Early Industrial: Gunsmith → [muskets], Powder Mill → [black powder]
- Industrial: Arsenal → [rifles, cannon, artillery shells]
- Dieselpunk: Ordnance Factory → [tank parts, mech components] + Enchantment Workshop → [hybrid magical-industrial munitions]

**Magic:** Herbalist → [rare herbs] + Alchemist → [spell components] → Wizard's Tower → [spell effects / enchanted goods]
*(Late: Wizard's Tower + Machine Shop → [hybrid magical-industrial goods])*

---

## MILITARY SYSTEM

### Core Philosophy
Military is another profession and production chain. Soldiers are pops. Weapons are goods. Campaigns consume supplies. Combat resolution is **abstract** — the player manages strategy and supply, not individual battles.

### Military Unit Types (Era-Gated)

| Era | Unit Types | Key Input Goods |
|-----|-----------|-----------------|
| Agrarian | Militia, Swordsmen, Archers, Hero Units | Melee weapons, armor, rations |
| Early Industrial | Musketeers, Light Cavalry, Field Artillery | Muskets, powder, shot, uniforms, horses, cannon |
| Industrial | Rifle Infantry, Heavy Cavalry, Siege Artillery | Rifles, artillery shells, rail logistics |
| Dieselpunk | Mechanized Infantry, Tank Squadrons, Mech Units | Diesel, tank parts, mech frames, specialized munitions |

### Squad System
Military units are organized as **squads** — groups of soldier pops with defined composition. Squads consume goods per tick. Effectiveness is a function of: equipment quality, morale, training level, commanding officer skill, and any hero/magical boons.

Artificers and wizards apply effects **at the squad level**, except hero units who are always individual.

### Hero Units
Heroes are exceptional individual pops emerging through **specific conditions**, subject to **permanent death**.

```
Hero {
    pop: Pop
    hero_class: HeroClass {
        Champion,           // survived 3+ battles, max combat skill
        Warlord,            // successfully defended against siege or large raid
        Archmage,           // completed study chain + rare celestial event
        MasterArtificer,    // produced legendary-quality item after years at max skill
        GreatMerchant,      // completed improbably profitable trade series
    }
    boons: List<Boon>
    legendary_items: List<Good>
    is_alive: bool
    death_cause: string?            // recorded in colony history
}
```

Hero death is permanent and recorded. Legendary items persist and can be passed on. Dead heroes can be memorialized for a colony morale boost.

### Abstract Combat Resolution
Battle considers: force size and composition, equipment quality, supply state, terrain, fortifications, hero presence, soldier morale and loyalty, magical support. The player sees outcome and casualties — not a real-time battle.

### Military Supply Chain
Armies consume per tick: rations, ammunition, equipment maintenance goods, morale goods (tobacco, spirits). Undersupplied armies suffer morale and effectiveness penalties.

---

## MAGIC SYSTEM

### Core Philosophy
Magic is rare, costly, and wondrous — and gradually marginalized by industrial progress unless hybridized with it. Magic users consume **input goods** for every magical feat, like any other production chain. Most magical abilities are outclassed by late industrial technology in raw output, but specialized or hybridized wizards remain viable and unique.

### Magic Users as Pops
Rare pops with high `magic_affinity` who train at magical buildings (Apprentice Lodge → Wizard's Tower → Arcane Academy). They consume spell components, rare herbs, magical crystals. They can become Hero units (Archmage class) under specific conditions.

### Magic Schools

| School | Effects | Key Input Goods | Era Viability |
|--------|---------|-----------------|---------------|
| **Combat** | Battlefield spells; squad boons | Spell components, rare herbs, crystals | Strong early; hybridizes with munitions late |
| **Weather** | Rain/drought/storm manipulation; affects crop yields | Rare herbs, sky crystals, silver dust | Always viable |
| **Agricultural** | Fertility spells, pest control, growth acceleration | Rare herbs, earth crystals, enchanted water | Strong early; replaced by chemical fertilizers late |
| **Healing** | Disease treatment, wound recovery, plague prevention | Rare herbs, alchemical mixtures | Always viable; competes with industrial medicine |
| **Divination** | Scouting, intelligence, world event forewarning | Rare herbs, mirror glass, arcane ink | Always viable |
| **Enchantment** | Imbuing goods and weapons with magical properties | Crystals, refined essence + the item | Always viable; hybridizes directly with industrial goods |

### Hybrid Magic-Industrial Goods
In the Industrial and Dieselpunk eras, magic and industry combine:
- **Alchemical shells** — artillery rounds enchanted for accuracy or yield
- **Warded armor plate** — factory steel with enchanted protection
- **Magical diesel** — alchemically treated fuel with higher energy output
- **Enchanted communication devices** — divination-based early telegraph equivalents

These require both industrial production chains AND magical inputs — expensive but uniquely powerful.

### Societal Magic Acceptance
Each colony has a **magic acceptance rating** influenced by founding ideology, racial composition, government type, active policies, and events. Low acceptance means magic users face discrimination and emigrate. High acceptance unlocks more magical buildings and policies. The player nudges this via policy but doesn't fully control it.

---

## FANTASTICAL CREATURES & WORLD THREATS

### Creature Types

**Wildlife-Class (ecological behavior):**
- Giant predators — territorial, attack if encroached
- Magical fauna (fire lizards, storm birds) — rare, avoid settlements unless provoked
- Herd animals — huntable, eventually domesticable

**Faction-Class (rival colony behavior):**
- **Goblin/Orc Tribes** — own settlement, population, resource needs; raid when hungry; can be traded with, allied, or conquered; diminish as industrial era advances
- **Necromancer Covens** — a hero-class magic user with undead minions; undead need magical upkeep, not food; major early-to-mid game threat
- **Dragon** — singular, ancient, territorial; behaves like a natural disaster combined with a faction; has a lair with wealth; can potentially be negotiated with (rare) or driven off; does not disappear with industrial progress but becomes less existentially threatening

Fantastical threats are most dangerous in the Agrarian era. Industrial military capacity makes them more manageable over time.

---

## ECONOMY & MARKET

### Market Model
Single regional market to start; district sub-markets as a late feature. Prices emerge from supply and demand.

```
market_price(good) = base_price * demand_pressure * scarcity_modifier * world_price_influence * tariff_modifier

demand_pressure = total_demand_this_tick / (total_supply_this_tick + stockpile)
scarcity_modifier = if stockpile < safety_stock: 1.0 + scarcity_premium else 1.0
```

Prices update each economy tick (daily). Smoothing factor prevents oscillation.

### Wages & Employment
Buildings post job listings with wage offers. Pops seek employment maximizing wage relative to skill. Player can set minimum wage policy.

### Taxes
Income tax, sales tax, property tax, import/export tariffs. Revenue funds public infrastructure, services, research, military, and treasury.

### Private Ownership
Wealthy pops (MiddleClass+) invest savings to construct buildings in zoned areas when market demand is high. Player can alternatively direct-build public buildings from treasury.

---

## LOGISTICS SYSTEM

Goods must be physically moved across the tile map.

**Auto-routed:** Buildings on road network push/pull goods to/from nearest warehouse at a slow base rate, limited by road quality.

**Hauler pops:** Dedicated Haulers physically transport goods. Capacity and speed scale with era: handcart → horse cart → steam wagon → diesel truck.

Logistics bottlenecks are visible and meaningful.

---

## POLITICAL & SOCIAL SYSTEMS

### Factions
- **Laborers' Union** — low-wage working class; demands minimum wage, better conditions
- **Merchant Guild** — wealthy commercial pops; demands low taxes, free trade
- **Industrialists** — factory owners; demands infrastructure, research
- **Traditionalists** — rural and elven pops; resists rapid era change; demands artisan preservation and magic acceptance
- **Reformists** — demands voting rights, public services, rule of law
- **Military Order** — soldier pops and officers; demands military funding
- **Arcane Society** — magic users and supporters; demands magic acceptance and research

### Disloyalty Consequences (escalating)
1. Work slowdowns (loyalty < 0.4)
2. Strikes (faction-led, loyalty < 0.3)
3. Riots and property destruction (loyalty < 0.2)
4. Emigration (sustained unhappiness)
5. Political faction seizure
6. Military mutiny (extreme conditions)

---

## DIPLOMACY SYSTEM

### Diplomatic Infrastructure
Diplomacy is staffed like a production chain. Build an **Embassy**, assign **Diplomat pops** (profession requiring education). Diplomats consume fine goods, travel supplies, and wages. Each active treaty costs diplomat capacity — overcommitting causes treaty degradation.

### Diplomatic Actions

| Action | Available To | Notes |
|--------|-------------|-------|
| Trade Agreement | All | Reduces tariffs with partner; costs 1 diplomat |
| Non-Aggression Pact | All | Reduces raid/war risk; costs 1 diplomat |
| Military Alliance | All | Mutual defense; costs 2 diplomats; faction approval required |
| Marriage Alliance | Monarchy only | Strong loyalty bond with partner; unique events |
| Vassalage Offer | All | Absorb weakened rival; high faction approval threshold |
| Trade Embargo | All | Economic warfare; strains broader diplomatic relations |
| Bribe Rival Faction | All | Destabilize a rival colony; covert; costs treasury |

---

## RESEARCH & TECHNOLOGY

```
Tech {
    id: string
    era: Era
    prerequisites: List<Tech>
    infrastructure_required: List<BuildingType>?
    research_cost: float
    effects: List<TechEffect>
}
```

Research generated by schools, universities, guild houses, research institutes. Era transitions require both research AND infrastructure prerequisites.

### Sample Tech Chains
- **Agrarian:** Crop Rotation, Animal Husbandry, Basic Masonry, Herbal Medicine, Runic Script
- **Early Industrial:** Steam Power (requires coal mine + blacksmith built), Textile Machinery, Gunpowder Weapons, Early Artillery
- **Industrial:** Blast Furnace Metallurgy, Railway Engineering, Chemical Processes, Rifled Weapons, Mass Production
- **Dieselpunk:** Internal Combustion, Electrical Grid, Mechanized Warfare, Alchemical Engineering (magic-industrial hybrids), Synthetic Materials

---

## THE OUTSIDE WORLD

### World State
- **Rival colonies** — simulated growth, economy, military; buying/selling on world markets
- **Creature factions** — goblin tribes, orc clans, necromancer covens with own population logic
- **Global market** — world supply/demand influenced by events and rival production

### World Events (examples)
- Volcanic eruption → global grain price spike for 1 year
- War between rival colonies → arms demand spike, refugee wave
- Plague in port cities → trade disruption, medicine demand spike
- Dragon awakens → regional threat, rare material deposit revealed
- Goblin tribe displaced → large hostile incursion on your borders
- Magical comet → temporary boost to all magic user abilities
- Great Exhibition → technology sharing opportunity; diplomatic event

---

## SIMULATION TICK ARCHITECTURE

| Tick Type | Frequency | What Updates |
|-----------|-----------|-------------|
| **Fast tick** | Every in-game hour | Pop movement, hauler pathfinding, building production, combat |
| **Economy tick** | Every in-game day | Needs evaluation, purchases, wages, market prices |
| **Social tick** | Every in-game week | Happiness, loyalty drift, faction membership, hero condition progress |
| **World tick** | Every in-game month | World events, rival colony states, global prices, era advancement check |

All tick logic must be **deterministic** given the same starting state and random seed.

---

## IMPLEMENTATION PHASES

### Phase 1: Core Data Models
1. All enums: `Era`, `WealthClass`, `Race`, `ProfessionType`, `GoodCategory`, `FactionType`, `MagicSchool`, `HeroClass`, `GovernmentType`, `QualityType`
2. `Good` model with full attributes including quality flag
3. `Recipe` model
4. `Pop` model with all attributes including race, hero status, magic affinity
5. `NeedState` and `NeedLevel` models
6. `Building` model
7. `SiteAttributes` model for world map site selection
8. JSON serialization for all models
9. Unit tests confirming model construction and serialization

### Phase 2: Economy Simulation (Headless)
1. `Market` — stockpiles, price calculation per tick
2. Pop purchasing behavior (evaluate needs, check prices, buy if affordable)
3. Wage payment loop
4. Building production loop (consume inputs, produce outputs)
5. Private building investment logic (wealthy pop → building construction)
6. Simulation harness: run N ticks, output price/happiness/stockpile log

### Phase 3: Logistics & Map
1. Tile map representation
2. Road network graph
3. Auto-route goods flow along network
4. Hauler pop pathfinding and delivery behavior

### Phase 4: Political & Social Systems
1. Loyalty calculation
2. Faction emergence conditions
3. Disloyalty consequence triggers
4. Government reform policy system

### Phase 5: Military System
1. Squad data model
2. Hero emergence condition tracking
3. Abstract battle resolution algorithm
4. Military supply consumption per tick
5. Artificer/wizard boon application to squads

### Phase 6: Magic System
1. Magic user pop training chain
2. Spell effect system (effects treated as goods/building outputs)
3. Magic acceptance rating and its effects
4. Hybrid magical-industrial goods recipes

### Phase 7: World Map & Site Selection
1. Procedural world generation algorithm
2. Site attribute calculation per tile
3. Site comparison data layer for UI

### Phase 8: Diplomacy
1. Embassy building and diplomat pop profession
2. Treaty and alliance data models
3. Diplomat capacity system
4. World event propagation to diplomatic relations

---

## CODE ARCHITECTURE GUIDELINES

- **Separate simulation from presentation.** All simulation classes are plain C# with no Godot dependencies. Wrap in Godot Nodes only at the presentation layer.
- **Data-driven design.** Goods, recipes, buildings, techs, races, spells defined in JSON resource files loaded at startup. Nothing hardcoded.
- **Event-driven communication.** C# events/delegates for simulation state changes; Godot signals only at the Node layer.
- **Save/load from the start.** All simulation state must be JSON-serializable from Phase 1.
- **Performance at scale.** 500+ individual pops requires O(n) or better per-tick logic. Use dirty flags, cached aggregates, and time-sliced pathfinding.
- **Simulation speed invariance.** Speed multiplier only changes how many ticks run per real second — never skips or approximates simulation steps.

---

## OPEN DESIGN QUESTIONS (Flag and Propose Solutions During Implementation)

1. **Housing model:** Pop occupies residential building (paying rent) vs. shelter as a purchased good. Recommend: building occupancy with rent as financial transaction.
2. **Pop pathfinding at scale:** 500+ pops pathfinding simultaneously. Recommend: flow-field navigation per zone, updated periodically rather than per-pop per-tick.
3. **Market granularity:** Single colony market is the starting point. District sub-markets are a stretch goal.
4. **Hero narrative logging:** Deaths, achievements, and legendary items should feed a colony history log — the foundation for emergent storytelling.
5. **Era transition pacing:** Expose as a tunable config parameter; do not hardcode.
6. **Faction negotiation UI:** Defer until simulation layer is stable.
7. **Artisan vs. factory good distinction:** The `quality` flag must live on `Good` instances, not just on recipes, so the market and pop happiness systems can distinguish them at point of purchase and consumption.

---

## GLOSSARY

| Term | Definition |
|------|-----------|
| Pop | An individual colonist with full simulated state |
| Race | One of five fantasy races; affects stats and culture, not political determinism |
| Hero | An exceptional individual pop, condition-triggered, with permanent death |
| Good | Any tradeable commodity, service, or magical effect in the economy |
| Recipe | A production formula: inputs + labor → outputs |
| Tick | One simulation time step |
| Era | A technology/development epoch (Agrarian through Dieselpunk) |
| Wealth Class | A pop's economic stratum, determining needs and behavior |
| Faction | A political group emerging from pop grievances or shared interests |
| Loyalty | A pop's alignment with the city-state government |
| Magic Acceptance | Colony-wide cultural tolerance for magic users and magical goods |
| Hybrid Good | A good requiring both industrial and magical production inputs |
| Squad | A military unit composed of soldier pops, consuming supply goods |
| Diplomat | A pop profession staffing the embassy and maintaining treaties |
| World Market | The external economy beyond the colony's borders |
| Hauler | A logistics pop responsible for physical transport of goods |
| Artisan Good | High-quality, slow, expensive variant of a manufactured good |
| Factory Good | Mass-produced, cheaper, lower-quality variant of a manufactured good |

---

*End of agent file. Begin with Phase 1: Core Data Models.*
