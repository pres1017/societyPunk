#nullable enable
using Godot;
using SocietyPunk.Simulation.Models;
using SimTileMap = SocietyPunk.Simulation.World.TileMap;

/// <summary>
/// Renders the simulation tile grid as colored rectangles.
/// No game logic — purely reads TileMap state and draws.
/// </summary>
public partial class TileMapNode : Node2D
{
    [Export] public int TileSize { get; set; } = 32;

    public SimTileMap? SimTileMap { get; set; }

    private static readonly Color GrassColor = new(0.3f, 0.6f, 0.2f);
    private static readonly Color ForestColor = new(0.1f, 0.4f, 0.1f);
    private static readonly Color HillsColor = new(0.5f, 0.45f, 0.3f);
    private static readonly Color MountainColor = new(0.4f, 0.4f, 0.4f);
    private static readonly Color DesertColor = new(0.85f, 0.8f, 0.5f);
    private static readonly Color WetlandColor = new(0.2f, 0.4f, 0.35f);
    private static readonly Color WaterColor = new(0.15f, 0.3f, 0.7f);
    private static readonly Color CoastColor = new(0.6f, 0.75f, 0.85f);

    private static readonly Color DirtRoadColor = new(0.6f, 0.5f, 0.3f);
    private static readonly Color GravelRoadColor = new(0.65f, 0.6f, 0.55f);
    private static readonly Color CobblestoneColor = new(0.55f, 0.55f, 0.55f);
    private static readonly Color CartTrackColor = new(0.7f, 0.65f, 0.5f);
    private static readonly Color RailColor = new(0.3f, 0.3f, 0.35f);

    public override void _Draw()
    {
        if (SimTileMap == null) return;

        // Only draw tiles visible on screen
        var viewRect = GetViewportRect();
        var transform = GetGlobalTransform().AffineInverse();
        var visibleRect = transform * viewRect;

        int startX = Mathf.Max(0, (int)(visibleRect.Position.X / TileSize) - 1);
        int startY = Mathf.Max(0, (int)(visibleRect.Position.Y / TileSize) - 1);
        int endX = Mathf.Min(SimTileMap.Width, (int)((visibleRect.Position.X + visibleRect.Size.X) / TileSize) + 2);
        int endY = Mathf.Min(SimTileMap.Height, (int)((visibleRect.Position.Y + visibleRect.Size.Y) / TileSize) + 2);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                ref var tile = ref SimTileMap.GetTile(x, y);
                var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);

                // Draw terrain
                DrawRect(rect, GetTerrainColor(tile.Terrain));

                // Draw road overlay
                if (tile.Road != RoadType.None)
                {
                    var roadRect = new Rect2(
                        x * TileSize + TileSize * 0.25f,
                        y * TileSize + TileSize * 0.25f,
                        TileSize * 0.5f, TileSize * 0.5f);
                    DrawRect(roadRect, GetRoadColor(tile.Road));
                }
            }
        }
    }

    public void Refresh()
    {
        QueueRedraw();
    }

    private static Color GetTerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => GrassColor,
        TerrainType.Forest => ForestColor,
        TerrainType.Hills => HillsColor,
        TerrainType.Mountain => MountainColor,
        TerrainType.Desert => DesertColor,
        TerrainType.Wetland => WetlandColor,
        TerrainType.Water => WaterColor,
        TerrainType.Coast => CoastColor,
        _ => GrassColor,
    };

    private static Color GetRoadColor(RoadType road) => road switch
    {
        RoadType.DirtPath => DirtRoadColor,
        RoadType.GravelRoad => GravelRoadColor,
        RoadType.Cobblestone => CobblestoneColor,
        RoadType.CartTrack => CartTrackColor,
        RoadType.SteamRoad => CobblestoneColor,
        RoadType.Rail => RailColor,
        _ => Colors.Transparent,
    };
}
