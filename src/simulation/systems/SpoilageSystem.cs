namespace SocietyPunk.Simulation.Systems;

using SocietyPunk.Simulation.Models;

/// <summary>
/// Reduces perishable goods in all building buffers each tick.
/// Spoilage rate comes from Good definitions in GameData.
/// </summary>
public static class SpoilageSystem
{
    public static void Tick(List<Building> buildings, GameData data)
    {
        foreach (var building in buildings)
        {
            ApplySpoilage(building.InputBuffer, data);
            ApplySpoilage(building.OutputBuffer, data);
            ApplySpoilage(building.Stockpile, data);
        }
    }

    private static void ApplySpoilage(Dictionary<string, float> buffer, GameData data)
    {
        if (buffer.Count == 0)
            return;

        // Collect keys to remove after iteration
        List<string>? toRemove = null;

        foreach (var goodId in buffer.Keys)
        {
            if (!data.Goods.TryGetValue(goodId, out var good))
                continue;
            if (!good.IsPerishable || good.SpoilageRate <= 0f)
                continue;

            var newAmount = buffer[goodId] * (1f - good.SpoilageRate);
            if (newAmount < 0.001f)
            {
                toRemove ??= new List<string>();
                toRemove.Add(goodId);
            }
            else
            {
                buffer[goodId] = newAmount;
            }
        }

        if (toRemove != null)
        {
            foreach (var key in toRemove)
                buffer.Remove(key);
        }
    }
}
