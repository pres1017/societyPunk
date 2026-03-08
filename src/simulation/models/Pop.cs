namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Mutable state — one per individual colonist.
/// </summary>
public class Pop
{
    // Identity
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public Race Race { get; set; }

    // Work
    public ProfessionType Profession { get; set; }
    public Guid? AssignedBuildingId { get; set; }
    public float SkillLevel { get; set; }

    // Basic needs (Layer 1: food and rest only)
    public float FoodLevel { get; set; } = 1.0f;
    public float RestLevel { get; set; } = 1.0f;
    public Guid? HomeId { get; set; }

    // State & position
    public PopState State { get; set; } = PopState.Idle;
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int DestX { get; set; }
    public int DestY { get; set; }

    // Hauler-specific
    public Guid? CurrentTaskId { get; set; }
    public string? CargoGoodId { get; set; }
    public float CargoAmount { get; set; }

    // LAYER 2/3 STUBS — exist but unused in Layer 1
    public float Happiness { get; set; } = 0.5f;
    public float Loyalty { get; set; } = 0.5f;
    public float Savings { get; set; }
    public float Wage { get; set; }
    public WealthClass WealthClass { get; set; }

    /// <summary>
    /// Work efficiency based on food and rest levels.
    /// </summary>
    public float Efficiency
    {
        get
        {
            if (FoodLevel <= 0 && RestLevel <= 0) return 0f;
            var foodMod = FoodLevel <= 0 ? 0.25f : FoodLevel;
            var restMod = RestLevel <= 0 ? 0.5f : RestLevel;
            return foodMod * restMod;
        }
    }
}
