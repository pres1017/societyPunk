#nullable enable
using Godot;
using SocietyPunk.Simulation.Models;

/// <summary>
/// Renders a pop sprite at their current tile position.
/// No game logic — purely reads Pop state and draws.
/// </summary>
public partial class PopNode : Node2D
{
    public Pop? SimPop { get; set; }
    public int TileSize { get; set; } = 32;

    private static readonly Color IdleColor = new(0.9f, 0.9f, 0.9f);
    private static readonly Color WorkingColor = new(0.2f, 0.7f, 0.2f);
    private static readonly Color HaulingColor = new(0.8f, 0.6f, 0.2f);
    private static readonly Color EatingColor = new(0.9f, 0.4f, 0.4f);
    private static readonly Color SleepingColor = new(0.4f, 0.4f, 0.8f);
    private static readonly Color ConstructingColor = new(0.7f, 0.5f, 0.1f);
    private static readonly Color WalkingColor = new(0.6f, 0.8f, 0.6f);

    public override void _Draw()
    {
        if (SimPop == null) return;

        float radius = TileSize * 0.3f;
        var center = new Vector2(TileSize * 0.5f, TileSize * 0.5f);

        // Draw pop as a colored circle
        DrawCircle(center, radius, GetStateColor(SimPop.State));
        DrawArc(center, radius, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.5f), 1.0f);

        // Draw food/rest indicator bar below
        float barWidth = TileSize * 0.6f;
        float barX = (TileSize - barWidth) * 0.5f;
        float barY = TileSize * 0.8f;

        // Food bar (red/green)
        DrawRect(new Rect2(barX, barY, barWidth, 2), new Color(0.3f, 0.1f, 0.1f));
        DrawRect(new Rect2(barX, barY, barWidth * SimPop.FoodLevel, 2), new Color(0.2f, 0.8f, 0.2f));
    }

    public void Refresh()
    {
        if (SimPop == null) return;
        Position = new Vector2(SimPop.TileX * TileSize, SimPop.TileY * TileSize);
        QueueRedraw();
    }

    private static Color GetStateColor(PopState state) => state switch
    {
        PopState.Idle => IdleColor,
        PopState.Working => WorkingColor,
        PopState.Hauling => HaulingColor,
        PopState.Eating => EatingColor,
        PopState.Sleeping => SleepingColor,
        PopState.Constructing => ConstructingColor,
        PopState.Walking => WalkingColor,
        _ => IdleColor,
    };
}
