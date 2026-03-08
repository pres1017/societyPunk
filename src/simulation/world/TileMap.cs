namespace SocietyPunk.Simulation.World;

using SocietyPunk.Simulation.Models;

/// <summary>
/// A single tile on the 2D grid.
/// </summary>
public struct Tile
{
    public TerrainType Terrain;
    public RoadType Road;
    public Guid? BuildingId;

    public bool IsPassable =>
        Terrain != TerrainType.Mountain && Terrain != TerrainType.Water;
}

/// <summary>
/// Flat 2D tile grid (cache-friendly row-major array).
/// </summary>
public class TileMap
{
    public int Width { get; }
    public int Height { get; }
    public Tile[] Tiles { get; }

    /// <summary>
    /// Incremented whenever roads or buildings change — used to invalidate path caches.
    /// </summary>
    public int RoadVersion { get; private set; }

    public TileMap(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new Tile[width * height];
    }

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public ref Tile GetTile(int x, int y) => ref Tiles[y * Width + x];

    public void PlaceRoad(int x, int y, RoadType road)
    {
        ref var tile = ref GetTile(x, y);
        tile.Road = road;
        RoadVersion++;
    }

    public void PlaceBuilding(int x, int y, Guid buildingId)
    {
        ref var tile = ref GetTile(x, y);
        tile.BuildingId = buildingId;
        RoadVersion++;
    }

    public void RemoveBuilding(int x, int y)
    {
        ref var tile = ref GetTile(x, y);
        tile.BuildingId = null;
        RoadVersion++;
    }

    /// <summary>
    /// Movement cost for pathfinding. Returns float.MaxValue for impassable tiles.
    /// </summary>
    public float GetMovementCost(int x, int y)
    {
        if (!InBounds(x, y)) return float.MaxValue;

        ref var tile = ref GetTile(x, y);
        if (!tile.IsPassable) return float.MaxValue;

        return (tile.Terrain, tile.Road) switch
        {
            // Grass
            (TerrainType.Grass, RoadType.None) => 2.0f,
            (TerrainType.Grass, RoadType.DirtPath) => 1.5f,
            (TerrainType.Grass, RoadType.GravelRoad) => 1.0f,
            (TerrainType.Grass, RoadType.Cobblestone) => 0.7f,
            (TerrainType.Grass, RoadType.CartTrack) => 0.5f,
            (TerrainType.Grass, RoadType.SteamRoad) => 0.4f,
            (TerrainType.Grass, RoadType.Rail) => 0.2f,

            // Forest
            (TerrainType.Forest, RoadType.None) => 4.0f,
            (TerrainType.Forest, RoadType.DirtPath) => 2.5f,
            (TerrainType.Forest, RoadType.GravelRoad) => 1.5f,
            (TerrainType.Forest, RoadType.Cobblestone) => 1.0f,
            (TerrainType.Forest, RoadType.CartTrack) => 0.7f,
            (TerrainType.Forest, RoadType.SteamRoad) => 0.5f,
            (TerrainType.Forest, RoadType.Rail) => 0.2f,

            // Hills
            (TerrainType.Hills, RoadType.None) => 5.0f,
            (TerrainType.Hills, RoadType.DirtPath) => 3.0f,
            (TerrainType.Hills, RoadType.GravelRoad) => 2.0f,
            (TerrainType.Hills, RoadType.Cobblestone) => 1.2f,
            (TerrainType.Hills, RoadType.CartTrack) => 0.8f,
            (TerrainType.Hills, RoadType.SteamRoad) => 0.6f,
            (TerrainType.Hills, RoadType.Rail) => 0.3f,

            // Desert
            (TerrainType.Desert, RoadType.None) => 3.0f,
            (TerrainType.Desert, RoadType.DirtPath) => 2.0f,
            (TerrainType.Desert, RoadType.GravelRoad) => 1.2f,
            (TerrainType.Desert, RoadType.Cobblestone) => 0.8f,
            (TerrainType.Desert, RoadType.CartTrack) => 0.6f,
            (TerrainType.Desert, RoadType.SteamRoad) => 0.4f,
            (TerrainType.Desert, RoadType.Rail) => 0.2f,

            // Wetland
            (TerrainType.Wetland, RoadType.None) => 6.0f,
            (TerrainType.Wetland, RoadType.DirtPath) => 4.0f,
            (TerrainType.Wetland, RoadType.GravelRoad) => 2.5f,
            (TerrainType.Wetland, RoadType.Cobblestone) => 1.5f,
            (TerrainType.Wetland, RoadType.CartTrack) => 1.0f,
            (TerrainType.Wetland, RoadType.SteamRoad) => 0.7f,
            (TerrainType.Wetland, RoadType.Rail) => 0.3f,

            // Coast
            (TerrainType.Coast, RoadType.None) => 3.0f,
            (TerrainType.Coast, RoadType.DirtPath) => 2.0f,
            (TerrainType.Coast, RoadType.GravelRoad) => 1.2f,
            (TerrainType.Coast, RoadType.Cobblestone) => 0.8f,
            (TerrainType.Coast, RoadType.CartTrack) => 0.6f,
            (TerrainType.Coast, RoadType.SteamRoad) => 0.4f,
            (TerrainType.Coast, RoadType.Rail) => 0.2f,

            // Mountain/Water are impassable
            _ => float.MaxValue,
        };
    }
}
