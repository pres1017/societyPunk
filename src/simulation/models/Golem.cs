namespace SocietyPunk.Simulation.Models;

/// <summary>
/// A magical construct that performs automated hauling on a fixed route.
/// Not a pop — no needs, no morale, no schedule.
/// </summary>
public class Golem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TileX { get; set; }
    public int TileY { get; set; }

    // Route
    public GolemRoute? AssignedRoute { get; set; }

    // Cargo
    public float CarryCapacity { get; set; } = 15.0f;
    public string? CarriedGoodId { get; set; }
    public float CarriedQuantity { get; set; }

    // Maintenance
    public float EssenceLevel { get; set; } = 1.0f;
    public float EssenceDrainPerTick { get; set; } = 0.002f;

    public bool IsActive => EssenceLevel > 0 && AssignedRoute != null;
}

public class GolemRoute
{
    public Guid PickupBuildingId { get; set; }
    public Guid DeliveryBuildingId { get; set; }
    public string GoodId { get; set; } = string.Empty;
    public float QuantityPerTrip { get; set; }
}
