# CLAUDE.md — Colony Game

## Design Documents
- `colony_game_v2_agent.md` — **active build spec** (Layer 1: logistics core)
- `colony_game_v1_agent.md` — full long-term vision (read only for context)

Read only the section of v2 relevant to your current step.
Do not read the entire document unless asked.

---

## Current Step
**Step: [ ASK THE DEVELOPER BEFORE ASSUMING ]**

Do one step. Stop when its tests pass. Do not advance without being told.

---

## Non-Negotiable Rules

**1. No Godot imports in simulation code.**
`src/simulation/` must never contain `using Godot;`. This is a hard bug if it
appears. Use plain C# types throughout the simulation layer.

**2. Nothing hardcoded.**
Every Good, Recipe, Building type, and Tech is defined in `/data/*.json`.
String literals for game content inside C# code are bugs.

**3. All five production chains are equal.**
Food (grain→flour→bread) is the primary example in the spec for clarity only.
Fuel, Construction, Tools, and Clothing are equally important. Tests must cover
all five chains. Do not over-implement food and sketch the others.

**4. Layer 3 stubs are frozen.**
Fields marked `// LAYER 3 STUB` must not be read or written by any Layer 1
system. Do not implement wages, market prices, happiness, loyalty, factions,
or private ownership. The stubs exist. Leave them alone.

**5. Tests before done.**
A step is not complete until NUnit tests pass headlessly. No exceptions.

**6. Serialize everything.**
Every model must round-trip through JSON correctly before any system that uses
it is built.

---

## Architecture at a Glance

```
src/simulation/     ← Pure C#. Zero Godot imports. Tested headlessly.
  models/           ← Pop, Good, Building, Recipe, Golem, RoadTile, etc.
  systems/          ← ProductionSystem, HaulerSystem, NeedsSystem, etc.
  world/            ← TileMap, WorldState, SimulationClock

src/godot/          ← Thin Godot wrapper only. Zero game logic.

data/               ← JSON definitions. Source of truth for all content.

tests/              ← NUnit headless tests. One file per system.
```

---

## How to Work

**Be concise.** Write the code. No summaries of what you just did. Explain
only things that are non-obvious or where you deviated from the spec.

**Ask before deviating.** If the spec is ambiguous or a better approach
exists, say so briefly and wait for confirmation. Do not silently make
architectural decisions.

**Flag open questions, don't block on them.** For the open questions listed
in v2, implement the recommended approach and leave a
`// TODO: tune in playtesting` comment. Do not ask about balance values.

**Keep file reads scoped.** Only read the files your current step needs.
Do not explore the whole project before starting work.

---

## Orchestrator Mode

If asked to act as an orchestrator:
1. Read the current step from v2.
2. Break it into 2–3 focused subagent tasks.
3. For each task, write a self-contained prompt including only the context
   that subagent needs (relevant models, rules, and the specific task).
4. Do not implement anything. Return the task breakdown and prompts only.
5. Wait for developer approval before any subagent work begins.

---

## Step Completion Checklist

- [ ] Code compiles, no errors or warnings
- [ ] All new files in correct directories per architecture above
- [ ] No `using Godot;` in `src/simulation/`
- [ ] No hardcoded Good IDs, Recipe names, or Building types in C# code
- [ ] JSON serialization round-trips correctly for all new models
- [ ] NUnit tests pass headlessly for all new systems
- [ ] All five production chains represented where applicable
- [ ] Layer 3 stubs present and untouched
- [ ] No Layer 2 or Layer 3 logic implemented