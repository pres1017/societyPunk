namespace SocietyPunk.Simulation.World;

using SocietyPunk.Simulation.Models;

/// <summary>
/// Container for all mutable simulation state.
/// Passed to SimulationRunner to wire all systems together.
/// </summary>
public class WorldState
{
    public TileMap Map { get; set; } = null!;
    public GameData Data { get; set; } = null!;
    public List<Building> Buildings { get; set; } = new();
    public List<Pop> Pops { get; set; } = new();
    public List<Golem> Golems { get; set; } = new();
    public List<HaulerTask> HaulerTasks { get; set; } = new();
    public ResearchState Research { get; set; } = new();
    public int CurrentTick { get; set; }
}
