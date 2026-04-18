namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;

/// <summary>
/// Generates research points from Scholar's Lodge buildings with assigned scholars,
/// applies tech effects on completion, and advances the research queue.
/// </summary>
public class ResearchSystem
{
    // TODO: tune in playtesting — base research points per scholar per tick
    public const float BaseResearchPerTick = 0.5f;

    public void Tick(List<Building> buildings, List<Pop> pops, GameData data, ResearchState state)
    {
        // Advance queue if no current research
        if (state.CurrentResearchId == null && state.ResearchQueue.Count > 0)
        {
            AdvanceQueue(state, data);
        }

        if (state.CurrentResearchId == null) return;
        if (!data.Techs.TryGetValue(state.CurrentResearchId, out var tech)) return;

        // Calculate total research output from all scholar's lodges
        float totalResearch = 0f;

        foreach (var building in buildings)
        {
            if (!building.IsConstructed || !building.IsOperational) continue;
            if (!data.Buildings.TryGetValue(building.DefId, out var def)) continue;
            if (def.Role != BuildingRole.Research) continue;

            // Count assigned scholars working at this building
            foreach (var workerId in building.AssignedWorkerIds)
            {
                var scholar = pops.Find(p => p.Id == workerId);
                if (scholar == null) continue;
                if (scholar.Profession != ProfessionType.Scholar) continue;

                float output = BaseResearchPerTick * scholar.Efficiency;
                // Skill level bonus
                output *= (1f + scholar.SkillLevel * 0.5f);
                totalResearch += output;
            }
        }

        if (totalResearch <= 0f) return;

        state.ResearchProgress += totalResearch;

        if (state.ResearchProgress >= tech.ResearchCost)
        {
            CompleteTech(state, tech, data);
        }
    }

    /// <summary>
    /// Queue a tech for research. Returns false if prerequisites are not met.
    /// </summary>
    public bool QueueTech(string techId, ResearchState state, GameData data)
    {
        if (!data.Techs.TryGetValue(techId, out var tech)) return false;

        // Check prerequisites
        foreach (var prereq in tech.Prerequisites)
        {
            if (!state.UnlockedTechs.Contains(prereq))
                return false;
        }

        // Don't queue duplicates or already-unlocked techs
        if (state.UnlockedTechs.Contains(techId)) return false;
        if (state.ResearchQueue.Contains(techId)) return false;
        if (state.CurrentResearchId == techId) return false;

        state.ResearchQueue.Add(techId);

        // If nothing is being researched, start immediately
        if (state.CurrentResearchId == null)
            AdvanceQueue(state, data);

        return true;
    }

    private void AdvanceQueue(ResearchState state, GameData data)
    {
        while (state.ResearchQueue.Count > 0)
        {
            var nextId = state.ResearchQueue[0];
            state.ResearchQueue.RemoveAt(0);

            if (!data.Techs.TryGetValue(nextId, out var tech)) continue;

            // Re-check prerequisites (may have changed)
            bool prereqsMet = true;
            foreach (var prereq in tech.Prerequisites)
            {
                if (!state.UnlockedTechs.Contains(prereq))
                {
                    prereqsMet = false;
                    break;
                }
            }

            if (!prereqsMet) continue;
            if (state.UnlockedTechs.Contains(nextId)) continue;

            state.CurrentResearchId = nextId;
            state.ResearchProgress = 0f;
            return;
        }

        state.CurrentResearchId = null;
        state.ResearchProgress = 0f;
    }

    private static void CompleteTech(ResearchState state, Tech tech, GameData data)
    {
        state.UnlockedTechs.Add(tech.Id);
        state.CurrentResearchId = null;
        state.ResearchProgress = 0f;

        // Apply effects
        foreach (var effect in tech.Effects)
        {
            switch (effect.EffectType)
            {
                case "unlock_building":
                    state.UnlockedBuildings.Add(effect.TargetId);
                    break;
                case "unlock_road":
                    state.UnlockedRoads.Add(effect.TargetId);
                    break;
                case "production_bonus":
                    state.ProductionBonuses[effect.TargetId] = effect.Value;
                    break;
                case "hauler_capacity_bonus":
                    state.HaulerCapacityMultiplier = effect.Value;
                    break;
            }
        }

        // Advance queue to next tech
        if (state.ResearchQueue.Count > 0)
        {
            // Re-enter AdvanceQueue on next tick naturally
        }
    }
}
