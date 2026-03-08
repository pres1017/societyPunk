namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Immutable definition — loaded from JSON, never modified at runtime.
/// </summary>
public class Good
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GoodCategory Category { get; set; }
    public int Tier { get; set; }
    public float BaseWeight { get; set; }
    public bool IsPerishable { get; set; }
    public float SpoilageRate { get; set; }
    public Era EraRequired { get; set; }

    // LAYER 3 STUB — not used in Layer 1
    public float BasePrice { get; set; }
}
