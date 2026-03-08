namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Immutable definition — loaded from JSON, never modified at runtime.
/// </summary>
public class Recipe
{
    public string Id { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public Era EraRequired { get; set; }
    public List<string>? ResearchRequired { get; set; }
    public List<GoodQuantity> Inputs { get; set; } = new();
    public List<GoodQuantity> Outputs { get; set; } = new();
    public LaborRequirement Labor { get; set; } = new();
    public float CycleDuration { get; set; }
    public float BaseEfficiency { get; set; } = 1.0f;
}

public class GoodQuantity
{
    public string GoodId { get; set; } = string.Empty;
    public float Quantity { get; set; }
}

public class LaborRequirement
{
    public int WorkerCount { get; set; }
    public ProfessionType Profession { get; set; }
    public float MinSkill { get; set; }
}
