namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Immutable definition — loaded from JSON, never modified at runtime.
/// </summary>
public class BuildingDef
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BuildingRole Role { get; set; }
    public Era EraRequired { get; set; }
    public List<string>? ResearchRequired { get; set; }
    public ProfessionType RequiredProfession { get; set; }
    public int MaxWorkers { get; set; }
    public int FootprintX { get; set; } = 1;
    public int FootprintY { get; set; } = 1;
    public List<GoodQuantity> ConstructionCost { get; set; } = new();
    public int ConstructionTime { get; set; }
    public float MaintenanceCost { get; set; }
    public List<string> AvailableRecipes { get; set; } = new();

    // Storage-specific (only relevant when Role == Storage)
    public float StorageCapacity { get; set; }
    public int WarehouseRadius { get; set; }

    // Housing-specific
    public int HousingCapacity { get; set; }
}

/// <summary>
/// Mutable state — one per placed building in the world.
/// </summary>
public class Building
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DefId { get; set; } = string.Empty;
    public int TileX { get; set; }
    public int TileY { get; set; }

    // State
    public float Condition { get; set; } = 1.0f;
    public bool IsOperational { get; set; }
    public bool IsConstructed { get; set; }
    public float ConstructionProgress { get; set; }

    // Staffing
    public List<Guid> AssignedWorkerIds { get; set; } = new();

    // Production
    public string? ActiveRecipeId { get; set; }
    public Dictionary<string, float> InputBuffer { get; set; } = new();
    public Dictionary<string, float> OutputBuffer { get; set; } = new();
    public float ProductionProgress { get; set; }
    public bool OutputBufferDirty { get; set; }

    // Storage (warehouse-specific — populated when Role == Storage)
    public Dictionary<string, float> Stockpile { get; set; } = new();

    // LAYER 3 STUBS
    public Guid? OwnerId { get; set; }
    public float Wage { get; set; }
}
