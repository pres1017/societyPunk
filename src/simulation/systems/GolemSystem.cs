namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

/// <summary>
/// Manages golem route execution, essence drain, and essence refill.
/// Golems are magical constructs that repeat a fixed pickup→delivery route.
/// </summary>
public class GolemSystem
{
    // TODO: tune in playtesting — threshold at which golem seeks essence refill
    public const float EssenceSeekThreshold = 0.1f;

    private readonly Dictionary<Guid, List<(int X, int Y)>> _paths = new();
    private readonly Dictionary<Guid, int> _pathIndex = new();

    public void Tick(List<Building> buildings, List<Golem> golems, GameData data, TileMap map)
    {
        var buildingsById = new Dictionary<Guid, Building>(buildings.Count);
        foreach (var b in buildings)
            buildingsById[b.Id] = b;

        foreach (var golem in golems)
        {
            // Drain essence every tick regardless of phase
            if (golem.EssenceLevel > 0)
            {
                golem.EssenceLevel -= golem.EssenceDrainPerTick;
                if (golem.EssenceLevel < 0)
                    golem.EssenceLevel = 0;
            }

            // Stop if out of essence
            if (golem.EssenceLevel <= 0)
            {
                golem.Phase = GolemPhase.Idle;
                continue;
            }

            // No route assigned — stay idle
            if (golem.AssignedRoute == null)
            {
                golem.Phase = GolemPhase.Idle;
                continue;
            }

            // Check if essence is low and golem should seek refill
            if (golem.EssenceLevel <= EssenceSeekThreshold &&
                golem.Phase != GolemPhase.SeekingEssence &&
                golem.CarriedQuantity == 0) // Don't abandon cargo
            {
                TrySeekEssence(golem, buildings, data, map);
            }

            ExecutePhase(golem, buildingsById, buildings, data, map);
        }
    }

    private void ExecutePhase(Golem golem, Dictionary<Guid, Building> buildingsById,
        List<Building> buildings, GameData data, TileMap map)
    {
        switch (golem.Phase)
        {
            case GolemPhase.Idle:
                // Start the route cycle
                if (golem.AssignedRoute != null && golem.EssenceLevel > 0)
                {
                    var moveResult = StartMoveTo(golem, golem.AssignedRoute.PickupBuildingId, buildingsById, map);
                    if (moveResult == MoveResult.AlreadyThere)
                        golem.Phase = GolemPhase.Loading;
                    else if (moveResult == MoveResult.PathSet)
                        golem.Phase = GolemPhase.MovingToPickup;
                    // MoveResult.Failed => stay Idle
                }
                break;

            case GolemPhase.MovingToPickup:
                if (MoveAlongPath(golem))
                    golem.Phase = GolemPhase.Loading;
                break;

            case GolemPhase.Loading:
                LoadCargo(golem, buildingsById, map);
                break;

            case GolemPhase.MovingToDelivery:
                if (MoveAlongPath(golem))
                    golem.Phase = GolemPhase.Unloading;
                break;

            case GolemPhase.Unloading:
                UnloadCargo(golem, buildingsById, map);
                break;

            case GolemPhase.SeekingEssence:
                if (MoveAlongPath(golem))
                    TryRefillEssence(golem, buildings, data);
                break;
        }
    }

    private void LoadCargo(Golem golem, Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        var route = golem.AssignedRoute!;
        if (!buildingsById.TryGetValue(route.PickupBuildingId, out var pickup))
        {
            golem.Phase = GolemPhase.Idle;
            return;
        }

        // Take from output buffer
        float available = 0f;
        pickup.OutputBuffer.TryGetValue(route.GoodId, out available);

        if (available <= 0f)
        {
            // Nothing to pick up — wait (stay in Loading, will retry next tick)
            return;
        }

        float toLoad = Math.Min(available, Math.Min(route.QuantityPerTrip, golem.CarryCapacity));
        pickup.OutputBuffer[route.GoodId] = available - toLoad;
        if (pickup.OutputBuffer[route.GoodId] <= 0f)
            pickup.OutputBuffer.Remove(route.GoodId);

        golem.CarriedGoodId = route.GoodId;
        golem.CarriedQuantity = toLoad;

        // Start moving to delivery
        if (!buildingsById.TryGetValue(route.DeliveryBuildingId, out var delivery))
        {
            // Put goods back if delivery is gone
            pickup.OutputBuffer.TryGetValue(route.GoodId, out float current);
            pickup.OutputBuffer[route.GoodId] = current + toLoad;
            golem.CarriedGoodId = null;
            golem.CarriedQuantity = 0;
            golem.Phase = GolemPhase.Idle;
            return;
        }

        var path = Pathfinder.FindPath(map, golem.TileX, golem.TileY, delivery.TileX, delivery.TileY);
        if (path == null)
        {
            // Can't reach delivery — put goods back
            pickup.OutputBuffer.TryGetValue(route.GoodId, out float current);
            pickup.OutputBuffer[route.GoodId] = current + toLoad;
            golem.CarriedGoodId = null;
            golem.CarriedQuantity = 0;
            golem.Phase = GolemPhase.Idle;
            return;
        }

        _paths[golem.Id] = path.Steps;
        _pathIndex[golem.Id] = 1;
        golem.Phase = GolemPhase.MovingToDelivery;
    }

    private void UnloadCargo(Golem golem, Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        var route = golem.AssignedRoute!;
        if (!buildingsById.TryGetValue(route.DeliveryBuildingId, out var delivery))
        {
            // Delivery building gone — drop cargo, go idle
            golem.CarriedGoodId = null;
            golem.CarriedQuantity = 0;
            golem.Phase = GolemPhase.Idle;
            return;
        }

        if (golem.CarriedQuantity > 0 && golem.CarriedGoodId != null)
        {
            delivery.InputBuffer.TryGetValue(golem.CarriedGoodId, out float current);
            delivery.InputBuffer[golem.CarriedGoodId] = current + golem.CarriedQuantity;
        }

        golem.CarriedGoodId = null;
        golem.CarriedQuantity = 0;

        // Loop back to pickup
        var moveResult = StartMoveTo(golem, route.PickupBuildingId, buildingsById, map);
        if (moveResult == MoveResult.AlreadyThere)
            golem.Phase = GolemPhase.Loading;
        else if (moveResult == MoveResult.PathSet)
            golem.Phase = GolemPhase.MovingToPickup;
        else
            golem.Phase = GolemPhase.Idle;
    }

    private void TrySeekEssence(Golem golem, List<Building> buildings, GameData data, TileMap map)
    {
        // Find nearest golem_workshop with magical_essence in its input buffer or stockpile
        Building? bestWorkshop = null;
        float bestDist = float.MaxValue;

        foreach (var b in buildings)
        {
            if (!b.IsConstructed || !b.IsOperational) continue;
            if (!data.Buildings.TryGetValue(b.DefId, out var def)) continue;
            if (def.Role != BuildingRole.Magic) continue;

            // Check if workshop has magical_essence available
            float essenceAvailable = 0f;
            b.InputBuffer.TryGetValue("magical_essence", out essenceAvailable);
            if (essenceAvailable <= 0f)
            {
                b.Stockpile.TryGetValue("magical_essence", out essenceAvailable);
            }
            if (essenceAvailable <= 0f) continue;

            float dist = Math.Abs(golem.TileX - b.TileX) + Math.Abs(golem.TileY - b.TileY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestWorkshop = b;
            }
        }

        if (bestWorkshop == null) return;

        var path = Pathfinder.FindPath(map, golem.TileX, golem.TileY,
            bestWorkshop.TileX, bestWorkshop.TileY);
        if (path == null) return;

        _paths[golem.Id] = path.Steps;
        _pathIndex[golem.Id] = 1;
        golem.Phase = GolemPhase.SeekingEssence;
    }

    private void TryRefillEssence(Golem golem, List<Building> buildings, GameData data)
    {
        // Find the workshop at golem's current location
        foreach (var b in buildings)
        {
            if (b.TileX != golem.TileX || b.TileY != golem.TileY) continue;
            if (!b.IsConstructed || !b.IsOperational) continue;
            if (!data.Buildings.TryGetValue(b.DefId, out var def)) continue;
            if (def.Role != BuildingRole.Magic) continue;

            // Consume 1 magical_essence from input buffer or stockpile
            if (b.InputBuffer.TryGetValue("magical_essence", out float inputEssence) && inputEssence >= 1f)
            {
                b.InputBuffer["magical_essence"] = inputEssence - 1f;
                if (b.InputBuffer["magical_essence"] <= 0f)
                    b.InputBuffer.Remove("magical_essence");
                golem.EssenceLevel = 1.0f;
                golem.Phase = GolemPhase.Idle; // Will restart route next tick
                return;
            }

            if (b.Stockpile.TryGetValue("magical_essence", out float stockEssence) && stockEssence >= 1f)
            {
                b.Stockpile["magical_essence"] = stockEssence - 1f;
                if (b.Stockpile["magical_essence"] <= 0f)
                    b.Stockpile.Remove("magical_essence");
                golem.EssenceLevel = 1.0f;
                golem.Phase = GolemPhase.Idle;
                return;
            }
        }

        // No essence available — go idle
        golem.Phase = GolemPhase.Idle;
    }

    private enum MoveResult { Failed, AlreadyThere, PathSet }

    private MoveResult StartMoveTo(Golem golem, Guid targetBuildingId,
        Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        if (!buildingsById.TryGetValue(targetBuildingId, out var target))
            return MoveResult.Failed;

        // Already at target
        if (golem.TileX == target.TileX && golem.TileY == target.TileY)
        {
            _paths.Remove(golem.Id);
            _pathIndex.Remove(golem.Id);
            return MoveResult.AlreadyThere;
        }

        var path = Pathfinder.FindPath(map, golem.TileX, golem.TileY, target.TileX, target.TileY);
        if (path == null)
            return MoveResult.Failed;

        _paths[golem.Id] = path.Steps;
        _pathIndex[golem.Id] = 1;
        return MoveResult.PathSet;
    }

    private bool MoveAlongPath(Golem golem)
    {
        if (!_paths.TryGetValue(golem.Id, out var path) ||
            !_pathIndex.TryGetValue(golem.Id, out var idx))
            return true; // No path = already there

        if (idx >= path.Count)
            return true; // Arrived

        var (nx, ny) = path[idx];
        golem.TileX = nx;
        golem.TileY = ny;
        _pathIndex[golem.Id] = idx + 1;

        return idx + 1 >= path.Count;
    }
}
