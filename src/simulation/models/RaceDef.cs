namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Immutable definition — loaded from JSON.
/// </summary>
public class RaceDef
{
    public Race Race { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, float> LaborModifiers { get; set; } = new();
    public float MoveSpeed { get; set; } = 1.0f;
}
