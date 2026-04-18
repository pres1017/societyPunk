namespace SocietyPunk.Simulation.World;

using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.Systems;

/// <summary>
/// Per-tick snapshot of key simulation metrics.
/// </summary>
public class TickSnapshot
{
    public int Tick { get; set; }
    public Dictionary<string, float> WarehouseStockpiles { get; set; } = new();
    public Dictionary<string, int> PopStateCounts { get; set; } = new();
    public Dictionary<string, float> BuildingOutputTotals { get; set; } = new();
    public float AverageEfficiency { get; set; }
    public int ConstructedBuildingCount { get; set; }
    public int UnderConstructionCount { get; set; }
    public int UnlockedTechCount { get; set; }
}

/// <summary>
/// Log of all tick snapshots from a simulation run.
/// </summary>
public class TickLog
{
    public List<TickSnapshot> Snapshots { get; set; } = new();
}

/// <summary>
/// Headless simulation harness that runs all systems in the correct order
/// and captures per-tick snapshots for analysis and balancing.
/// </summary>
public class SimulationRunner
{
    private readonly WorldState _state;

    // Systems (stateful ones need persistent instances)
    private readonly HaulerSystem _hauler = new();
    private readonly GolemSystem _golem = new();
    private readonly ConstructionSystem _construction = new();
    private readonly ResearchSystem _research = new();

    public SimulationRunner(WorldState state)
    {
        _state = state;
    }

    /// <summary>
    /// Advance the simulation by one tick. All systems run in order.
    /// </summary>
    public void Tick()
    {
        // 1. Production — buildings produce goods
        ProductionSystem.Tick(_state.Buildings, _state.Pops, _state.Data);

        // 2. Spoilage — perishable goods decay
        SpoilageSystem.Tick(_state.Buildings, _state.Data);

        // 3. Haulers — pop-based goods transport
        _hauler.Tick(_state.Buildings, _state.Pops, _state.HaulerTasks,
            _state.Data, _state.Map);

        // 4. Golems — automated goods transport
        _golem.Tick(_state.Buildings, _state.Golems, _state.Data, _state.Map);

        // 5. Needs — pop food/rest drain
        NeedsSystem.Tick(_state.Pops, _state.Buildings, _state.Data);

        // 6. Construction — builders work on sites
        _construction.Tick(_state.Buildings, _state.Pops, _state.Data, _state.Map);

        // 7. Research — scholars generate research points
        _research.Tick(_state.Buildings, _state.Pops, _state.Data, _state.Research);

        _state.CurrentTick++;
    }

    /// <summary>
    /// Run N ticks and capture a snapshot after each tick.
    /// </summary>
    public TickLog RunFor(int ticks)
    {
        var log = new TickLog();

        for (int i = 0; i < ticks; i++)
        {
            Tick();
            log.Snapshots.Add(CaptureSnapshot());
        }

        return log;
    }

    private TickSnapshot CaptureSnapshot()
    {
        var snapshot = new TickSnapshot
        {
            Tick = _state.CurrentTick,
            UnlockedTechCount = _state.Research.UnlockedTechs.Count
        };

        // Aggregate building data
        foreach (var b in _state.Buildings)
        {
            if (b.IsConstructed)
            {
                snapshot.ConstructedBuildingCount++;

                if (_state.Data.Buildings.TryGetValue(b.DefId, out var def) &&
                    def.Role == BuildingRole.Storage)
                {
                    foreach (var kvp in b.Stockpile)
                    {
                        snapshot.WarehouseStockpiles.TryGetValue(kvp.Key, out float current);
                        snapshot.WarehouseStockpiles[kvp.Key] = current + kvp.Value;
                    }
                }

                foreach (var kvp in b.OutputBuffer)
                {
                    snapshot.BuildingOutputTotals.TryGetValue(kvp.Key, out float current);
                    snapshot.BuildingOutputTotals[kvp.Key] = current + kvp.Value;
                }
            }
            else
            {
                snapshot.UnderConstructionCount++;
            }
        }

        // Pop state counts
        foreach (var pop in _state.Pops)
        {
            var stateKey = pop.State.ToString();
            snapshot.PopStateCounts.TryGetValue(stateKey, out int count);
            snapshot.PopStateCounts[stateKey] = count + 1;
        }

        // Average efficiency
        if (_state.Pops.Count > 0)
        {
            float totalEff = 0f;
            foreach (var pop in _state.Pops)
                totalEff += pop.Efficiency;
            snapshot.AverageEfficiency = totalEff / _state.Pops.Count;
        }

        return snapshot;
    }
}
