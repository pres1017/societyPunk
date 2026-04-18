#nullable enable
using Godot;
using SocietyPunk.Simulation.Models;

/// <summary>
/// Renders a building at its tile position.
/// No game logic — purely reads Building state and draws.
/// </summary>
public partial class BuildingNode : Node2D
{
    public Building? SimBuilding { get; set; }
    public BuildingDef? Def { get; set; }
    public int TileSize { get; set; } = 32;

    private static readonly Color ConstructionColor = new(0.8f, 0.6f, 0.2f, 0.6f);
    private static readonly Color ProductionColor = new(0.3f, 0.5f, 0.8f);
    private static readonly Color StorageColor = new(0.6f, 0.5f, 0.3f);
    private static readonly Color HousingColor = new(0.7f, 0.7f, 0.5f);
    private static readonly Color ResearchColor = new(0.6f, 0.3f, 0.7f);
    private static readonly Color MagicColor = new(0.4f, 0.2f, 0.8f);
    private static readonly Color LogisticsColor = new(0.5f, 0.6f, 0.4f);
    private static readonly Color DefaultColor = new(0.5f, 0.5f, 0.5f);

    public override void _Draw()
    {
        if (SimBuilding == null || Def == null) return;

        float w = Def.FootprintX * TileSize;
        float h = Def.FootprintY * TileSize;
        var rect = new Rect2(0, 0, w, h);

        Color color = GetBuildingColor(Def.Role);
        if (!SimBuilding.IsConstructed)
            color = ConstructionColor;

        DrawRect(rect, color);

        // Draw construction progress bar
        if (!SimBuilding.IsConstructed && Def.ConstructionTime > 0)
        {
            float progress = SimBuilding.ConstructionProgress / Def.ConstructionTime;
            var barBg = new Rect2(2, h - 6, w - 4, 4);
            var barFg = new Rect2(2, h - 6, (w - 4) * progress, 4);
            DrawRect(barBg, new Color(0.2f, 0.2f, 0.2f));
            DrawRect(barFg, new Color(0.2f, 0.8f, 0.2f));
        }

        // Draw outline
        DrawRect(rect, new Color(0, 0, 0, 0.4f), false, 1.0f);
    }

    public void Refresh()
    {
        if (SimBuilding == null) return;
        Position = new Vector2(SimBuilding.TileX * TileSize, SimBuilding.TileY * TileSize);
        QueueRedraw();
    }

    private static Color GetBuildingColor(BuildingRole role) => role switch
    {
        BuildingRole.Production => ProductionColor,
        BuildingRole.Storage => StorageColor,
        BuildingRole.Housing => HousingColor,
        BuildingRole.Research => ResearchColor,
        BuildingRole.Magic => MagicColor,
        BuildingRole.Logistics => LogisticsColor,
        _ => DefaultColor,
    };
}
