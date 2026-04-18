namespace SocietyPunk.Simulation.World;

/// <summary>
/// A grid-wide pathfinding structure: for every reachable tile, stores the
/// direction to move toward the goal. Generated via Dijkstra from the goal outward.
/// Cached and invalidated via RoadVersion.
/// </summary>
public class FlowField
{
    public int Width { get; }
    public int Height { get; }
    public int GoalX { get; }
    public int GoalY { get; }

    /// <summary>
    /// The RoadVersion of the TileMap when this flow field was generated.
    /// </summary>
    public int CachedRoadVersion { get; }

    // Per-tile direction toward goal. (0,0) means unreachable or is the goal itself.
    private readonly sbyte[] _dx;
    private readonly sbyte[] _dy;

    // Per-tile cost to reach goal (for debugging/testing). float.MaxValue = unreachable.
    private readonly float[] _cost;

    private FlowField(int width, int height, int goalX, int goalY, int roadVersion,
        sbyte[] dx, sbyte[] dy, float[] cost)
    {
        Width = width;
        Height = height;
        GoalX = goalX;
        GoalY = goalY;
        CachedRoadVersion = roadVersion;
        _dx = dx;
        _dy = dy;
        _cost = cost;
    }

    /// <summary>
    /// Get the direction to move from (x, y) toward the goal.
    /// Returns (0, 0) if the tile is unreachable or is the goal.
    /// </summary>
    public (int DX, int DY) GetDirection(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return (0, 0);
        int idx = y * Width + x;
        return (_dx[idx], _dy[idx]);
    }

    /// <summary>
    /// Get the total cost from (x, y) to the goal. float.MaxValue if unreachable.
    /// </summary>
    public float GetCost(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return float.MaxValue;
        return _cost[y * Width + x];
    }

    /// <summary>
    /// True if the map's road layout has changed since this flow field was generated.
    /// </summary>
    public bool IsStale(TileMap map) => map.RoadVersion != CachedRoadVersion;

    /// <summary>
    /// Generate a flow field from a single goal tile using Dijkstra.
    /// Uses the same direction set as Pathfinder for consistency.
    /// </summary>
    public static FlowField Generate(TileMap map, int goalX, int goalY)
    {
        int w = map.Width;
        int h = map.Height;
        int size = w * h;

        var cost = new float[size];
        var dx = new sbyte[size];
        var dy = new sbyte[size];
        Array.Fill(cost, float.MaxValue);

        int goalIdx = goalY * w + goalX;

        if (!map.InBounds(goalX, goalY) || map.GetMovementCost(goalX, goalY) == float.MaxValue)
            return new FlowField(w, h, goalX, goalY, map.RoadVersion, dx, dy, cost);

        cost[goalIdx] = 0f;

        var open = new PriorityQueue<int, float>();
        open.Enqueue(goalIdx, 0f);

        var directions = Pathfinder.Directions;

        while (open.Count > 0)
        {
            int currentIdx = open.Dequeue();
            int cx = currentIdx % w;
            int cy = currentIdx / w;
            float currentCost = cost[currentIdx];

            foreach (var (ddx, ddy) in directions)
            {
                int nx = cx + ddx;
                int ny = cy + ddy;
                if (!map.InBounds(nx, ny))
                    continue;

                float moveCost = map.GetMovementCost(nx, ny);
                if (moveCost == float.MaxValue)
                    continue;

                // Diagonal moves cost sqrt(2) × tile cost
                if (ddx != 0 && ddy != 0)
                    moveCost *= 1.41421356f;

                float newCost = currentCost + moveCost;
                int neighborIdx = ny * w + nx;

                if (newCost < cost[neighborIdx])
                {
                    cost[neighborIdx] = newCost;
                    // Direction points FROM neighbor TOWARD current (i.e., toward goal)
                    dx[neighborIdx] = (sbyte)(-ddx);
                    dy[neighborIdx] = (sbyte)(-ddy);
                    open.Enqueue(neighborIdx, newCost);
                }
            }
        }

        return new FlowField(w, h, goalX, goalY, map.RoadVersion, dx, dy, cost);
    }
}
