# SocietyPunk — Dieselpunk Colony City-State Game

A colony management game with deep economic simulation, built in Godot 4 (.NET/C#).

## Architecture

The project follows a strict **simulation/presentation separation**:

### Simulation Layer (`src/simulation/`)
Pure C# classes with **zero Godot dependencies**. This layer contains all game logic:
- `models/` — Data models: Pop, Good, Recipe, Building, Tech, etc.
- `systems/` — Simulation systems: Market, Production, Needs, Politics, Military
- `world/` — World map, site attributes, world events

The simulation is tick-based and deterministic. Given the same starting state and random seed, it produces identical results.

### Godot Layer (`src/godot/`)
Thin presentation layer that handles rendering and input only:
- `ui/` — UI scenes and scripts
- `scenes/` — Game scenes and node scripts

Godot nodes wrap simulation objects and translate C# events into Godot signals.

### Data (`data/`)
JSON resource files loaded at startup. Nothing is hardcoded:
- `goods/`, `recipes/`, `buildings/`, `races/`, `techs/`

### Tests (`tests/`)
Headless C# unit tests using NUnit. Tests run against the simulation layer directly, with no Godot runtime required.

## Tick Architecture

| Tick Type | Frequency | Updates |
|-----------|-----------|---------|
| Fast | Every in-game hour | Movement, hauling, production, combat |
| Economy | Every in-game day | Needs, purchases, wages, market prices |
| Social | Every in-game week | Happiness, loyalty, factions, hero progress |
| World | Every in-game month | World events, rivals, global prices, era check |

## Building & Testing

```bash
# Build the simulation + Godot project
dotnet build

# Run headless simulation tests (no Godot required)
dotnet test tests/SocietyPunk.Tests.csproj
```
