namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Immutable definition — loaded from JSON, never modified at runtime.
/// </summary>
public class Tech
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Era Era { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string>? InfrastructureRequired { get; set; }
    public float ResearchCost { get; set; }
    public List<TechEffect> Effects { get; set; } = new();
}

public class TechEffect
{
    public string EffectType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public float Value { get; set; }
}
