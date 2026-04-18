namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;

/// <summary>
/// Drains pop food/rest while working, triggers eating/sleeping behavior,
/// and handles food consumption from buildings.
/// </summary>
public static class NeedsSystem
{
    // TODO: tune in playtesting
    public const float FoodDrainPerWorkTick = 0.05f;
    public const float RestDrainPerWorkTick = 0.03f;
    public const float RestRefillPerSleepTick = 0.1f;
    public const float RestRefillNoHomeTick = 0.05f;
    public const float FoodSeekThreshold = 0.3f;
    public const float RestSeekThreshold = 0.3f;
    public const float SatisfiedThreshold = 0.8f;

    public static void Tick(List<Pop> pops, List<Building> buildings, GameData data)
    {
        // Build food good lookup once
        var foodGoods = new HashSet<string>();
        var foodValues = new Dictionary<string, float>();
        foreach (var good in data.Goods.Values)
        {
            if (good.IsFood)
            {
                foodGoods.Add(good.Id);
                foodValues[good.Id] = good.FoodValue;
            }
        }

        foreach (var pop in pops)
        {
            // 1. Drain needs for active states
            if (pop.State == PopState.Working || pop.State == PopState.Hauling ||
                pop.State == PopState.Constructing)
            {
                pop.FoodLevel = Math.Max(0f, pop.FoodLevel - FoodDrainPerWorkTick);
                pop.RestLevel = Math.Max(0f, pop.RestLevel - RestDrainPerWorkTick);
            }

            // 2. State transition checks (skip haulers mid-task)
            if (pop.State != PopState.Eating && pop.State != PopState.Sleeping &&
                pop.State != PopState.Hauling)
            {
                if (pop.FoodLevel <= 0f && pop.RestLevel <= 0f)
                {
                    pop.State = PopState.Eating; // food priority when both empty
                }
                else if (pop.FoodLevel < FoodSeekThreshold)
                {
                    pop.State = PopState.Eating;
                }
                else if (pop.RestLevel < RestSeekThreshold)
                {
                    pop.State = PopState.Sleeping;
                }
            }

            // 3. Eating behavior
            if (pop.State == PopState.Eating)
            {
                TryEat(pop, buildings, foodGoods, foodValues);

                if (pop.FoodLevel >= SatisfiedThreshold)
                {
                    // Check if rest is also low
                    if (pop.RestLevel < RestSeekThreshold)
                        pop.State = PopState.Sleeping;
                    else
                        ReturnToWork(pop);
                }
            }

            // 4. Sleeping behavior
            if (pop.State == PopState.Sleeping)
            {
                bool hasHome = pop.HomeId != null;
                float refill = hasHome ? RestRefillPerSleepTick : RestRefillNoHomeTick;
                pop.RestLevel = Math.Min(1f, pop.RestLevel + refill);

                if (pop.RestLevel >= SatisfiedThreshold)
                {
                    // Check if food dropped while sleeping
                    if (pop.FoodLevel < FoodSeekThreshold)
                        pop.State = PopState.Eating;
                    else
                        ReturnToWork(pop);
                }
            }

            // 5. Clamp
            pop.FoodLevel = Math.Clamp(pop.FoodLevel, 0f, 1f);
            pop.RestLevel = Math.Clamp(pop.RestLevel, 0f, 1f);
        }
    }

    private static void TryEat(Pop pop, List<Building> buildings,
        HashSet<string> foodGoods, Dictionary<string, float> foodValues)
    {
        // Find nearest building with any food
        Building? bestBuilding = null;
        string? bestFoodId = null;
        float bestDist = float.MaxValue;

        foreach (var b in buildings)
        {
            if (!b.IsConstructed) continue;
            float dist = Math.Abs(pop.TileX - b.TileX) + Math.Abs(pop.TileY - b.TileY);
            if (dist >= bestDist) continue;

            // Check Stockpile first, then OutputBuffer
            string? foundFood = null;
            foreach (var kvp in b.Stockpile)
            {
                if (foodGoods.Contains(kvp.Key) && kvp.Value > 0f)
                {
                    foundFood = kvp.Key;
                    break;
                }
            }

            if (foundFood == null)
            {
                foreach (var kvp in b.OutputBuffer)
                {
                    if (foodGoods.Contains(kvp.Key) && kvp.Value > 0f)
                    {
                        foundFood = kvp.Key;
                        break;
                    }
                }
            }

            if (foundFood != null)
            {
                bestBuilding = b;
                bestFoodId = foundFood;
                bestDist = dist;
            }
        }

        if (bestBuilding == null || bestFoodId == null) return; // No food found — stay Eating (starving)

        // Consume 1 unit
        float consumed = 0f;
        if (bestBuilding.Stockpile.TryGetValue(bestFoodId, out var stockAmt) && stockAmt > 0f)
        {
            float toEat = Math.Min(1f, stockAmt);
            bestBuilding.Stockpile[bestFoodId] = stockAmt - toEat;
            if (bestBuilding.Stockpile[bestFoodId] <= 0f)
                bestBuilding.Stockpile.Remove(bestFoodId);
            consumed = toEat;
        }
        else if (bestBuilding.OutputBuffer.TryGetValue(bestFoodId, out var outAmt) && outAmt > 0f)
        {
            float toEat = Math.Min(1f, outAmt);
            bestBuilding.OutputBuffer[bestFoodId] = outAmt - toEat;
            if (bestBuilding.OutputBuffer[bestFoodId] <= 0f)
                bestBuilding.OutputBuffer.Remove(bestFoodId);
            consumed = toEat;
        }

        if (consumed > 0f && foodValues.TryGetValue(bestFoodId, out var foodVal))
        {
            pop.FoodLevel = Math.Min(1f, pop.FoodLevel + foodVal * consumed);
        }
    }

    private static void ReturnToWork(Pop pop)
    {
        if (pop.AssignedBuildingId != null)
            pop.State = PopState.Working;
        else
            pop.State = PopState.Idle;
    }
}
