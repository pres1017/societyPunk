namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;

/// <summary>
/// Advances production cycles for all active buildings.
/// Each tick: validate inputs + workers, calculate efficiency, advance progress,
/// consume inputs and produce outputs on cycle completion.
/// </summary>
public static class ProductionSystem
{
    public static void Tick(List<Building> buildings, List<Pop> pops, GameData data)
    {
        var popsById = new Dictionary<Guid, Pop>(pops.Count);
        foreach (var p in pops)
            popsById[p.Id] = p;

        foreach (var building in buildings)
        {
            if (!building.IsConstructed || !building.IsOperational)
                continue;
            if (string.IsNullOrEmpty(building.ActiveRecipeId))
                continue;
            if (!data.Recipes.TryGetValue(building.ActiveRecipeId, out var recipe))
                continue;

            if (!HasRequiredWorkers(building, recipe, popsById))
                continue;
            if (!HasRequiredInputs(building, recipe))
                continue;

            var efficiency = CalculateEfficiency(building, recipe, popsById);
            if (efficiency <= 0f)
                continue;

            // Advance progress: 1 tick of work scaled by efficiency
            building.ProductionProgress += efficiency;

            if (building.ProductionProgress >= recipe.CycleDuration)
            {
                ConsumeInputs(building, recipe);
                ProduceOutputs(building, recipe);
                building.ProductionProgress -= recipe.CycleDuration;
            }
        }
    }

    private static bool HasRequiredWorkers(
        Building building, Recipe recipe, Dictionary<Guid, Pop> popsById)
    {
        int qualifiedCount = 0;
        foreach (var workerId in building.AssignedWorkerIds)
        {
            if (!popsById.TryGetValue(workerId, out var worker))
                continue;
            if (worker.Profession != recipe.Labor.Profession)
                continue;
            if (worker.SkillLevel < recipe.Labor.MinSkill)
                continue;
            qualifiedCount++;
        }
        return qualifiedCount >= recipe.Labor.WorkerCount;
    }

    private static bool HasRequiredInputs(Building building, Recipe recipe)
    {
        foreach (var input in recipe.Inputs)
        {
            if (!building.InputBuffer.TryGetValue(input.GoodId, out var available))
                return false;
            if (available < input.Quantity)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Efficiency = recipe base × average worker skill × building condition.
    /// </summary>
    // TODO: tune in playtesting — consider weighting skill vs condition differently
    internal static float CalculateEfficiency(
        Building building, Recipe recipe, Dictionary<Guid, Pop> popsById)
    {
        float skillSum = 0f;
        int qualifiedCount = 0;

        foreach (var workerId in building.AssignedWorkerIds)
        {
            if (!popsById.TryGetValue(workerId, out var worker))
                continue;
            if (worker.Profession != recipe.Labor.Profession)
                continue;
            if (worker.SkillLevel < recipe.Labor.MinSkill)
                continue;

            // Worker efficiency accounts for food/rest via Pop.Efficiency
            skillSum += worker.SkillLevel * worker.Efficiency;
            qualifiedCount++;

            if (qualifiedCount >= recipe.Labor.WorkerCount)
                break;
        }

        if (qualifiedCount == 0)
            return 0f;

        float avgSkillEfficiency = skillSum / qualifiedCount;
        return recipe.BaseEfficiency * avgSkillEfficiency * building.Condition;
    }

    private static void ConsumeInputs(Building building, Recipe recipe)
    {
        foreach (var input in recipe.Inputs)
        {
            building.InputBuffer[input.GoodId] -= input.Quantity;
            if (building.InputBuffer[input.GoodId] <= 0f)
                building.InputBuffer.Remove(input.GoodId);
        }
    }

    private static void ProduceOutputs(Building building, Recipe recipe)
    {
        foreach (var output in recipe.Outputs)
        {
            if (!building.OutputBuffer.TryGetValue(output.GoodId, out var current))
                current = 0f;
            building.OutputBuffer[output.GoodId] = current + output.Quantity;
        }
        building.OutputBufferDirty = true;
    }
}
