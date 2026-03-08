namespace SocietyPunk.Simulation.Models;

public class HaulerTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PickupBuildingId { get; set; }
    public string GoodId { get; set; } = string.Empty;
    public float Quantity { get; set; }
    public Guid DeliveryBuildingId { get; set; }
    public float Priority { get; set; }
    public Guid? AssignedHaulerId { get; set; }
    public HaulerTaskPhase Phase { get; set; } = HaulerTaskPhase.Pending;
}

public enum HaulerTaskPhase
{
    Pending,
    MovingToPickup,
    PickingUp,
    MovingToDelivery,
    DroppingOff,
    Completed,
    Cancelled
}
