namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;
using SocietyPunk.Simulation.World;

/// <summary>
/// Manages builder pops who roam to construction sites, deliver materials,
/// and work on buildings until complete.
/// </summary>
public class ConstructionSystem
{
    // TODO: tune in playtesting — base work per tick per builder
    public const float WorkPerTick = 1.0f;

    private enum BuilderPhase { Idle, MovingToSource, PickingUp, MovingToSite, Delivering, MovingToBuild, Building }

    private readonly Dictionary<Guid, BuilderPhase> _phase = new();
    private readonly Dictionary<Guid, Guid> _targetSiteId = new();
    private readonly Dictionary<Guid, List<(int X, int Y)>> _paths = new();
    private readonly Dictionary<Guid, int> _pathIndex = new();

    public void Tick(List<Building> buildings, List<Pop> pops, GameData data, TileMap map)
    {
        var buildingsById = new Dictionary<Guid, Building>(buildings.Count);
        foreach (var b in buildings)
            buildingsById[b.Id] = b;

        foreach (var pop in pops)
        {
            if (pop.Profession != ProfessionType.Builder) continue;

            if (!_phase.TryGetValue(pop.Id, out var phase))
                phase = BuilderPhase.Idle;

            switch (phase)
            {
                case BuilderPhase.Idle:
                    TryAssignSite(pop, buildings, data, buildingsById, map);
                    break;

                case BuilderPhase.MovingToSource:
                    if (MoveAlongPath(pop))
                        SetPhase(pop, BuilderPhase.PickingUp);
                    break;

                case BuilderPhase.PickingUp:
                    PickUpMaterials(pop, buildingsById, data, map);
                    break;

                case BuilderPhase.MovingToSite:
                    if (MoveAlongPath(pop))
                        SetPhase(pop, BuilderPhase.Delivering);
                    break;

                case BuilderPhase.Delivering:
                    DeliverMaterials(pop, buildingsById, data, map);
                    break;

                case BuilderPhase.MovingToBuild:
                    if (MoveAlongPath(pop))
                    {
                        pop.State = PopState.Constructing;
                        SetPhase(pop, BuilderPhase.Building);
                    }
                    break;

                case BuilderPhase.Building:
                    DoBuildWork(pop, buildingsById, data, map);
                    break;
            }
        }
    }

    private void TryAssignSite(Pop builder, List<Building> buildings, GameData data,
        Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        // Find nearest unconstructed building
        Building? bestSite = null;
        float bestDist = float.MaxValue;

        foreach (var b in buildings)
        {
            if (b.IsConstructed) continue;
            if (!data.Buildings.ContainsKey(b.DefId)) continue;

            float dist = Math.Abs(builder.TileX - b.TileX) + Math.Abs(builder.TileY - b.TileY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestSite = b;
            }
        }

        if (bestSite == null) return;

        _targetSiteId[builder.Id] = bestSite.Id;

        // Check if site needs materials
        if (!data.Buildings.TryGetValue(bestSite.DefId, out var def)) return;

        string? neededGood = FindMissingMaterial(bestSite, def);

        if (neededGood != null)
        {
            // Find a warehouse/stockpile with the needed good
            if (TryFindMaterialSource(builder, neededGood, buildings, data, buildingsById, map))
                return; // MovingToSource phase set

            // No source found — go to site and wait
        }

        // All materials present (or nothing to fetch) — go build
        if (NavigateTo(builder, bestSite.TileX, bestSite.TileY, map))
        {
            SetPhase(builder, BuilderPhase.MovingToBuild);
            builder.State = PopState.Walking;
        }
    }

    private static string? FindMissingMaterial(Building site, BuildingDef def)
    {
        foreach (var cost in def.ConstructionCost)
        {
            site.InputBuffer.TryGetValue(cost.GoodId, out float have);
            if (have < cost.Quantity)
                return cost.GoodId;
        }
        return null;
    }

    private bool TryFindMaterialSource(Pop builder, string goodId,
        List<Building> buildings, GameData data,
        Dictionary<Guid, Building> buildingsById, TileMap map)
    {
        Building? bestSource = null;
        float bestDist = float.MaxValue;

        foreach (var b in buildings)
        {
            if (!b.IsConstructed) continue;

            float stock = 0f;
            // Check stockpile (warehouses)
            b.Stockpile.TryGetValue(goodId, out stock);
            // Also check output buffer (production buildings)
            if (stock <= 0f)
                b.OutputBuffer.TryGetValue(goodId, out stock);
            if (stock <= 0f) continue;

            float dist = Math.Abs(builder.TileX - b.TileX) + Math.Abs(builder.TileY - b.TileY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestSource = b;
            }
        }

        if (bestSource == null) return false;

        if (NavigateTo(builder, bestSource.TileX, bestSource.TileY, map))
        {
            builder.CargoGoodId = goodId; // Remember what we're going to pick up
            builder.CargoAmount = 0f;
            SetPhase(builder, BuilderPhase.MovingToSource);
            builder.State = PopState.Walking;
            return true;
        }

        return false;
    }

    private void PickUpMaterials(Pop builder, Dictionary<Guid, Building> buildingsById,
        GameData data, TileMap map)
    {
        if (!_targetSiteId.TryGetValue(builder.Id, out var siteId)) { ResetBuilder(builder); return; }
        if (!buildingsById.TryGetValue(siteId, out var site)) { ResetBuilder(builder); return; }
        if (!data.Buildings.TryGetValue(site.DefId, out var def)) { ResetBuilder(builder); return; }

        var goodId = builder.CargoGoodId;
        if (goodId == null) { ResetBuilder(builder); return; }

        // Find how much the site still needs
        float needed = 0f;
        foreach (var cost in def.ConstructionCost)
        {
            if (cost.GoodId == goodId)
            {
                site.InputBuffer.TryGetValue(goodId, out float have);
                needed = cost.Quantity - have;
                break;
            }
        }

        if (needed <= 0f)
        {
            // Site no longer needs this — go back to idle to reassess
            builder.CargoGoodId = null;
            ResetBuilder(builder);
            return;
        }

        // Pick up from whatever building we're standing on
        float picked = 0f;
        foreach (var b in buildingsById.Values)
        {
            if (b.TileX != builder.TileX || b.TileY != builder.TileY) continue;

            // Try stockpile first
            if (b.Stockpile.TryGetValue(goodId, out float stockVal) && stockVal > 0f)
            {
                float take = Math.Min(stockVal, needed);
                b.Stockpile[goodId] = stockVal - take;
                if (b.Stockpile[goodId] <= 0f) b.Stockpile.Remove(goodId);
                picked = take;
                break;
            }

            // Try output buffer
            if (b.OutputBuffer.TryGetValue(goodId, out float outVal) && outVal > 0f)
            {
                float take = Math.Min(outVal, needed);
                b.OutputBuffer[goodId] = outVal - take;
                if (b.OutputBuffer[goodId] <= 0f) b.OutputBuffer.Remove(goodId);
                picked = take;
                break;
            }
        }

        if (picked <= 0f)
        {
            // Source is empty — go idle to reassess
            builder.CargoGoodId = null;
            ResetBuilder(builder);
            return;
        }

        builder.CargoGoodId = goodId;
        builder.CargoAmount = picked;

        // Navigate to the site
        if (NavigateTo(builder, site.TileX, site.TileY, map))
        {
            SetPhase(builder, BuilderPhase.MovingToSite);
            builder.State = PopState.Walking;
        }
        else
        {
            ResetBuilder(builder);
        }
    }

    private void DeliverMaterials(Pop builder, Dictionary<Guid, Building> buildingsById,
        GameData data, TileMap map)
    {
        if (!_targetSiteId.TryGetValue(builder.Id, out var siteId)) { ResetBuilder(builder); return; }
        if (!buildingsById.TryGetValue(siteId, out var site)) { ResetBuilder(builder); return; }

        // Deposit cargo into site's input buffer
        if (builder.CargoAmount > 0f && builder.CargoGoodId != null)
        {
            site.InputBuffer.TryGetValue(builder.CargoGoodId, out float current);
            site.InputBuffer[builder.CargoGoodId] = current + builder.CargoAmount;
        }

        builder.CargoGoodId = null;
        builder.CargoAmount = 0f;

        // Check if site still needs more materials
        if (!data.Buildings.TryGetValue(site.DefId, out var def)) { ResetBuilder(builder); return; }

        string? neededGood = FindMissingMaterial(site, def);
        if (neededGood != null)
        {
            // Go fetch more
            var buildings = new List<Building>(buildingsById.Values);
            if (TryFindMaterialSource(builder, neededGood, buildings, data, buildingsById, map))
                return;
        }

        // All materials delivered — start building
        if (builder.TileX == site.TileX && builder.TileY == site.TileY)
        {
            builder.State = PopState.Constructing;
            SetPhase(builder, BuilderPhase.Building);
        }
        else if (NavigateTo(builder, site.TileX, site.TileY, map))
        {
            SetPhase(builder, BuilderPhase.MovingToBuild);
            builder.State = PopState.Walking;
        }
        else
        {
            ResetBuilder(builder);
        }
    }

    private void DoBuildWork(Pop builder, Dictionary<Guid, Building> buildingsById,
        GameData data, TileMap map)
    {
        if (!_targetSiteId.TryGetValue(builder.Id, out var siteId)) { ResetBuilder(builder); return; }
        if (!buildingsById.TryGetValue(siteId, out var site)) { ResetBuilder(builder); return; }
        if (!data.Buildings.TryGetValue(site.DefId, out var def)) { ResetBuilder(builder); return; }

        // Check builder is at the site
        if (builder.TileX != site.TileX || builder.TileY != site.TileY)
        {
            if (NavigateTo(builder, site.TileX, site.TileY, map))
            {
                SetPhase(builder, BuilderPhase.MovingToBuild);
                builder.State = PopState.Walking;
            }
            else
            {
                ResetBuilder(builder);
            }
            return;
        }

        // Check if materials are still needed (another builder may have delivered while we walked)
        string? neededGood = FindMissingMaterial(site, def);
        if (neededGood != null)
        {
            // Need more materials — go fetch
            var buildings = new List<Building>(buildingsById.Values);
            if (TryFindMaterialSource(builder, neededGood, buildings, data, buildingsById, map))
                return;
            // Can't find source — wait at site
            return;
        }

        // Do construction work
        builder.State = PopState.Constructing;
        float work = WorkPerTick * builder.Efficiency;
        site.ConstructionProgress += work;

        if (site.ConstructionProgress >= def.ConstructionTime)
        {
            // Construction complete!
            site.IsConstructed = true;
            site.IsOperational = true;
            site.ConstructionProgress = def.ConstructionTime;

            // Clear construction materials from input buffer
            foreach (var cost in def.ConstructionCost)
            {
                site.InputBuffer.Remove(cost.GoodId);
            }

            ResetBuilder(builder);
        }
    }

    private bool NavigateTo(Pop pop, int targetX, int targetY, TileMap map)
    {
        if (pop.TileX == targetX && pop.TileY == targetY)
            return true; // Already there

        var path = Pathfinder.FindPath(map, pop.TileX, pop.TileY, targetX, targetY);
        if (path == null) return false;

        _paths[pop.Id] = path.Steps;
        _pathIndex[pop.Id] = 1;
        return true;
    }

    private bool MoveAlongPath(Pop pop)
    {
        if (!_paths.TryGetValue(pop.Id, out var path) ||
            !_pathIndex.TryGetValue(pop.Id, out var idx))
            return true;

        if (idx >= path.Count)
            return true;

        var (nx, ny) = path[idx];
        pop.TileX = nx;
        pop.TileY = ny;
        _pathIndex[pop.Id] = idx + 1;

        return idx + 1 >= path.Count;
    }

    private void SetPhase(Pop pop, BuilderPhase phase)
    {
        _phase[pop.Id] = phase;
    }

    private void ResetBuilder(Pop builder)
    {
        builder.State = PopState.Idle;
        builder.CargoGoodId = null;
        builder.CargoAmount = 0f;
        _phase.Remove(builder.Id);
        _targetSiteId.Remove(builder.Id);
        _paths.Remove(builder.Id);
        _pathIndex.Remove(builder.Id);
    }
}
