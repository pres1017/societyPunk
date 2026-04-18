namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Mutable runtime state for the research system.
/// Tracks which techs are unlocked, current research, and the queue.
/// </summary>
public class ResearchState
{
    public HashSet<string> UnlockedTechs { get; set; } = new();
    public string? CurrentResearchId { get; set; }
    public float ResearchProgress { get; set; }
    public List<string> ResearchQueue { get; set; } = new();

    // Applied modifiers from completed techs
    public HashSet<string> UnlockedBuildings { get; set; } = new();
    public HashSet<string> UnlockedRoads { get; set; } = new();
    public Dictionary<string, float> ProductionBonuses { get; set; } = new();
    public float HaulerCapacityMultiplier { get; set; } = 1.0f;
}
