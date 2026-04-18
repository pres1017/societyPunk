namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

/// <summary>
/// Manages hauler task generation, assignment, and execution.
/// Haulers move goods from output buffers to input buffers/stockpiles.
/// </summary>
public class HaulerSystem
{
    // TODO: tune in playtesting — equipment level modifies this
    public const float BaseCarryCapacity = 10f;

    // Cached paths per hauler — recomputed if missing (e.g., after deserialization)
    private readonly Dictionary<Guid, List<(int X, int Y)>> _paths = new();
    private readonly Dictionary<Guid, int> _pathIndex = new();

    public void Tick(List<Building> buildings, List<Pop> pops, List<HaulerTask> tasks,
        GameData data, TileMap map)
    {
        var buildingsById = new Dictionary<Guid, Building>(buildings.Count);
        foreach (var b in buildings)
            buildingsById[b.Id] = b;

        GenerateTasks(buildings, tasks, data, buildingsById);
        AssignTasks(pops, tasks, buildingsById, map);
        ExecuteHaulers(pops, tasks, buildingsById, map);
    }

    private void GenerateTasks(List<Building> buildings, List<HaulerTask> tasks,
        GameData data, Dictionary<Guid, Building> buildingsById)
    {
        // Track existing active tasks to avoid duplicates
        var activeTasks = new HashSet<(Guid pickupId, string goodId, Guid deliveryId)>();
        foreach (var t in tasks)
        {
            if (t.Phase != HaulerTaskPhase.Completed && t.Phase != HaulerTaskPhase.Cancelled)
                activeTasks.Add((t.PickupBuildingId, t.GoodId, t.DeliveryBuildingId));
        }

        // Collect buildings with input deficits (need goods delivered)
        var deficits = new List<(Building building, string goodId, float needed)>();
        foreach (var b in buildings)
        {
            if (!b.IsConstructed || !b.IsOperational) continue;
            if (string.IsNullOrEmpty(b.ActiveRecipeId)) continue;
            if (!data.Recipes.TryGetValue(b.ActiveRecipeId, out var recipe)) continue;

            foreach (var input in recipe.Inputs)
            {
                float current = 0f;
                b.InputBuffer.TryGetValue(input.GoodId, out current);
                // Request delivery if below one cycle's worth
                if (current < input.Quantity)
                {
                    float needed = input.Quantity - current;
                    deficits.Add((b, input.GoodId, needed));
                }
            }
        }

        // Scan buildings with dirty output buffers — create tasks to move goods
        foreach (var source in buildings)
        {
            if (!source.OutputBufferDirty) continue;
            source.OutputBufferDirty = false;

            foreach (var kvp in source.OutputBuffer)
            {
                if (kvp.Value <= 0f) continue;
                var goodId = kvp.Key;

                // Try to match with a deficit first
                bool matched = false;
                foreach (var (dest, defGoodId, needed) in deficits)
                {
                    if (defGoodId != goodId) continue;
                    if (dest.Id == source.Id) continue;
                    if (activeTasks.Contains((source.Id, goodId, dest.Id))) continue;

                    float qty = Math.Min(kvp.Value, Math.Min(needed, BaseCarryCapacity));
                    float distance = Math.Abs(source.TileX - dest.TileX)
                                   + Math.Abs(source.TileY - dest.TileY);
                    float deficitRatio = needed / Math.Max(qty, 0.01f);
                    float priority = deficitRatio / Math.Max(distance, 1f);

                    tasks.Add(new HaulerTask
                    {
                        PickupBuildingId = source.Id,
                        GoodId = goodId,
                        Quantity = qty,
                        DeliveryBuildingId = dest.Id,
                        Priority = priority,
                    });
                    activeTasks.Add((source.Id, goodId, dest.Id));
                    matched = true;
                    break;
                }

                // If no production deficit, deliver to nearest warehouse
                if (!matched)
                {
                    Building? bestWarehouse = null;
                    float bestDist = float.MaxValue;

                    foreach (var wb in buildings)
                    {
                        if (wb.Id == source.Id) continue;
                        if (!wb.IsConstructed) continue;
                        if (!data.Buildings.TryGetValue(wb.DefId, out var wbDef)) continue;
                        if (wbDef.Role != BuildingRole.Storage) continue;
                        if (activeTasks.Contains((source.Id, goodId, wb.Id))) continue;

                        float dist = Math.Abs(source.TileX - wb.TileX)
                                   + Math.Abs(source.TileY - wb.TileY);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestWarehouse = wb;
                        }
                    }

                    if (bestWarehouse != null)
                    {
                        float qty = Math.Min(kvp.Value, BaseCarryCapacity);
                        tasks.Add(new HaulerTask
                        {
                            PickupBuildingId = source.Id,
                            GoodId = goodId,
                            Quantity = qty,
                            DeliveryBuildingId = bestWarehouse.Id,
                            Priority = 1f / Math.Max(bestDist, 1f),
                        });
                        activeTasks.Add((source.Id, goodId, bestWarehouse.Id));
                    }
                }
            }
        }

        // Also generate tasks for warehouses to supply production buildings
        foreach (var (dest, goodId, needed) in deficits)
        {
            // Find a warehouse with stock of this good
            foreach (var wb in buildings)
            {
                if (!wb.IsConstructed) continue;
                if (!data.Buildings.TryGetValue(wb.DefId, out var wbDef)) continue;
                if (wbDef.Role != BuildingRole.Storage) continue;
                if (!wb.Stockpile.TryGetValue(goodId, out var stock) || stock <= 0f) continue;
                if (activeTasks.Contains((wb.Id, goodId, dest.Id))) continue;

                float qty = Math.Min(stock, Math.Min(needed, BaseCarryCapacity));
                float distance = Math.Abs(wb.TileX - dest.TileX)
                               + Math.Abs(wb.TileY - dest.TileY);
                float deficitRatio = needed / Math.Max(qty, 0.01f);

                tasks.Add(new HaulerTask
                {
                    PickupBuildingId = wb.Id,
                    GoodId = goodId,
                    Quantity = qty,
                    DeliveryBuildingId = dest.Id,
                    Priority = deficitRatio / Math.Max(distance, 1f),
                });
                activeTasks.Add((wb.Id, goodId, dest.Id));
                break; // One warehouse per deficit
            }
        }
    }

    private void AssignTasks(List<Pop> pops, List<HaulerTask> tasks,
        Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        // Sort pending tasks by priority (highest first)
        var pending = new List<HaulerTask>();
        foreach (var t in tasks)
        {
            if (t.Phase == HaulerTaskPhase.Pending && t.AssignedHaulerId == null)
                pending.Add(t);
        }
        pending.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        int taskIdx = 0;
        foreach (var hauler in pops)
        {
            if (taskIdx >= pending.Count) break;
            if (hauler.Profession != ProfessionType.Hauler) continue;
            if (hauler.State != PopState.Idle) continue;
            if (hauler.CurrentTaskId != null) continue;

            var task = pending[taskIdx];
            if (!buildingsById.TryGetValue(task.PickupBuildingId, out var pickup))
            {
                task.Phase = HaulerTaskPhase.Cancelled;
                taskIdx++;
                continue;
            }

            // Compute path to pickup
            var path = Pathfinder.FindPath(map, hauler.TileX, hauler.TileY, pickup.TileX, pickup.TileY);
            if (path == null)
            {
                task.Phase = HaulerTaskPhase.Cancelled;
                taskIdx++;
                continue;
            }

            task.AssignedHaulerId = hauler.Id;
            task.Phase = HaulerTaskPhase.MovingToPickup;
            hauler.CurrentTaskId = task.Id;
            hauler.State = PopState.Hauling;
            _paths[hauler.Id] = path.Steps;
            _pathIndex[hauler.Id] = 1; // Skip index 0 (current position)
            taskIdx++;
        }
    }

    private void ExecuteHaulers(List<Pop> pops, List<HaulerTask> tasks,
        Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        var tasksById = new Dictionary<Guid, HaulerTask>(tasks.Count);
        foreach (var t in tasks)
            tasksById[t.Id] = t;

        foreach (var hauler in pops)
        {
            if (hauler.Profession != ProfessionType.Hauler) continue;
            if (hauler.CurrentTaskId == null) continue;
            if (!tasksById.TryGetValue(hauler.CurrentTaskId.Value, out var task)) continue;

            switch (task.Phase)
            {
                case HaulerTaskPhase.MovingToPickup:
                    if (MoveAlongPath(hauler))
                    {
                        // Arrived at pickup
                        task.Phase = HaulerTaskPhase.PickingUp;
                    }
                    break;

                case HaulerTaskPhase.PickingUp:
                    if (!buildingsById.TryGetValue(task.PickupBuildingId, out var pickup))
                    {
                        CancelTask(hauler, task);
                        break;
                    }

                    float available = GetFromBuffer(pickup, task.GoodId);
                    if (available <= 0f)
                    {
                        CancelTask(hauler, task);
                        break;
                    }

                    float toLoad = Math.Min(available, Math.Min(task.Quantity, BaseCarryCapacity));
                    RemoveFromBuffer(pickup, task.GoodId, toLoad);
                    hauler.CargoGoodId = task.GoodId;
                    hauler.CargoAmount = toLoad;

                    // Compute path to delivery
                    if (!buildingsById.TryGetValue(task.DeliveryBuildingId, out var delivery))
                    {
                        CancelTask(hauler, task);
                        break;
                    }

                    var deliveryPath = Pathfinder.FindPath(map, hauler.TileX, hauler.TileY,
                        delivery.TileX, delivery.TileY);
                    if (deliveryPath == null)
                    {
                        // Can't reach delivery — put goods back and cancel
                        AddToBuffer(pickup, task.GoodId, toLoad);
                        hauler.CargoGoodId = null;
                        hauler.CargoAmount = 0f;
                        CancelTask(hauler, task);
                        break;
                    }

                    _paths[hauler.Id] = deliveryPath.Steps;
                    _pathIndex[hauler.Id] = 1;
                    task.Phase = HaulerTaskPhase.MovingToDelivery;
                    break;

                case HaulerTaskPhase.MovingToDelivery:
                    if (MoveAlongPath(hauler))
                    {
                        task.Phase = HaulerTaskPhase.DroppingOff;
                    }
                    break;

                case HaulerTaskPhase.DroppingOff:
                    if (!buildingsById.TryGetValue(task.DeliveryBuildingId, out var dest))
                    {
                        CancelTask(hauler, task);
                        break;
                    }

                    // Deposit into InputBuffer or Stockpile (for warehouses)
                    if (hauler.CargoAmount > 0f && hauler.CargoGoodId != null)
                    {
                        bool isStorage = false;
                        // Check if destination is a warehouse by looking at its stockpile usage
                        // Warehouses use Stockpile, production buildings use InputBuffer
                        if (dest.Stockpile.Count > 0 || dest.InputBuffer.Count == 0)
                        {
                            // Heuristic: if it has a stockpile or no input buffer, treat as storage
                            // Better: check the BuildingDef role, but we don't have GameData here
                            // We'll deposit to InputBuffer if the good is needed there, else Stockpile
                            if (dest.InputBuffer.ContainsKey(hauler.CargoGoodId) ||
                                (!string.IsNullOrEmpty(dest.ActiveRecipeId)))
                            {
                                isStorage = false;
                            }
                            else
                            {
                                isStorage = true;
                            }
                        }

                        if (isStorage)
                        {
                            dest.Stockpile.TryGetValue(hauler.CargoGoodId, out float current);
                            dest.Stockpile[hauler.CargoGoodId] = current + hauler.CargoAmount;
                        }
                        else
                        {
                            dest.InputBuffer.TryGetValue(hauler.CargoGoodId, out float current);
                            dest.InputBuffer[hauler.CargoGoodId] = current + hauler.CargoAmount;
                        }
                    }

                    hauler.CargoGoodId = null;
                    hauler.CargoAmount = 0f;
                    task.Phase = HaulerTaskPhase.Completed;
                    hauler.CurrentTaskId = null;
                    hauler.State = PopState.Idle;
                    _paths.Remove(hauler.Id);
                    _pathIndex.Remove(hauler.Id);
                    break;
            }
        }
    }

    private bool MoveAlongPath(Pop hauler)
    {
        if (!_paths.TryGetValue(hauler.Id, out var path) ||
            !_pathIndex.TryGetValue(hauler.Id, out var idx))
            return true; // No path = already there

        if (idx >= path.Count)
            return true; // Arrived

        var (nx, ny) = path[idx];
        hauler.TileX = nx;
        hauler.TileY = ny;
        _pathIndex[hauler.Id] = idx + 1;

        return idx + 1 >= path.Count;
    }

    private static void CancelTask(Pop hauler, HaulerTask task)
    {
        task.Phase = HaulerTaskPhase.Cancelled;
        hauler.CurrentTaskId = null;
        hauler.State = PopState.Idle;
        hauler.CargoGoodId = null;
        hauler.CargoAmount = 0f;
    }

    private static float GetFromBuffer(Building building, string goodId)
    {
        // Check OutputBuffer first, then Stockpile (for warehouses)
        if (building.OutputBuffer.TryGetValue(goodId, out var outVal) && outVal > 0f)
            return outVal;
        if (building.Stockpile.TryGetValue(goodId, out var stockVal) && stockVal > 0f)
            return stockVal;
        return 0f;
    }

    private static void RemoveFromBuffer(Building building, string goodId, float amount)
    {
        if (building.OutputBuffer.TryGetValue(goodId, out var outVal) && outVal > 0f)
        {
            building.OutputBuffer[goodId] = outVal - amount;
            if (building.OutputBuffer[goodId] <= 0f)
                building.OutputBuffer.Remove(goodId);
            return;
        }
        if (building.Stockpile.TryGetValue(goodId, out var stockVal) && stockVal > 0f)
        {
            building.Stockpile[goodId] = stockVal - amount;
            if (building.Stockpile[goodId] <= 0f)
                building.Stockpile.Remove(goodId);
        }
    }

    private static void AddToBuffer(Building building, string goodId, float amount)
    {
        building.OutputBuffer.TryGetValue(goodId, out float current);
        building.OutputBuffer[goodId] = current + amount;
    }
}
