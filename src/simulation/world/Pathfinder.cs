namespace SocietyPunk.Simulation.World;

/// <summary>
/// Result of a pathfinding query.
/// </summary>
public class PathResult
{
    public List<(int X, int Y)> Steps { get; }
    public float TotalCost { get; }

    public PathResult(List<(int X, int Y)> steps, float totalCost)
    {
        Steps = steps;
        TotalCost = totalCost;
    }
}

/// <summary>
/// A* pathfinding on the tile grid.
/// Neighbor directions are parameterized — swap Directions4 for Directions8 to enable diagonal movement.
/// </summary>
public static class Pathfinder
{
    public static readonly (int DX, int DY)[] Directions4 =
    {
        (0, -1), (1, 0), (0, 1), (-1, 0)
    };

    public static readonly (int DX, int DY)[] Directions8 =
    {
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    /// <summary>
    /// Current active direction set. Change this to Directions8 for diagonal movement.
    /// </summary>
    public static (int DX, int DY)[] Directions { get; set; } = Directions4;

    /// <summary>
    /// Find a path from (startX, startY) to (goalX, goalY) using A*.
    /// Returns null if no path exists.
    /// </summary>
    public static PathResult? FindPath(TileMap map, int startX, int startY, int goalX, int goalY)
    {
        if (!map.InBounds(startX, startY) || !map.InBounds(goalX, goalY))
            return null;

        if (map.GetMovementCost(startX, startY) == float.MaxValue ||
            map.GetMovementCost(goalX, goalY) == float.MaxValue)
            return null;

        // Same tile
        if (startX == goalX && startY == goalY)
            return new PathResult(new List<(int, int)> { (startX, startY) }, 0f);

        int w = map.Width;
        int h = map.Height;
        int size = w * h;

        var gCost = new float[size];
        var cameFrom = new int[size];
        Array.Fill(gCost, float.MaxValue);
        Array.Fill(cameFrom, -1);

        int startIdx = startY * w + startX;
        int goalIdx = goalY * w + goalX;
        gCost[startIdx] = 0f;

        // Priority queue: (fCost, tileIndex)
        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIdx, Heuristic(startX, startY, goalX, goalY));

        while (open.Count > 0)
        {
            int currentIdx = open.Dequeue();
            if (currentIdx == goalIdx)
                return BuildResult(cameFrom, gCost, w, startIdx, goalIdx);

            int cx = currentIdx % w;
            int cy = currentIdx / w;
            float currentG = gCost[currentIdx];

            foreach (var (dx, dy) in Directions)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!map.InBounds(nx, ny))
                    continue;

                float moveCost = map.GetMovementCost(nx, ny);
                if (moveCost == float.MaxValue)
                    continue;

                // Diagonal moves cost sqrt(2) × tile cost
                if (dx != 0 && dy != 0)
                    moveCost *= 1.41421356f;

                float newG = currentG + moveCost;
                int neighborIdx = ny * w + nx;

                if (newG < gCost[neighborIdx])
                {
                    gCost[neighborIdx] = newG;
                    cameFrom[neighborIdx] = currentIdx;
                    float f = newG + Heuristic(nx, ny, goalX, goalY);
                    open.Enqueue(neighborIdx, f);
                }
            }
        }

        return null; // No path found
    }

    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        // Chebyshev distance — admissible for both 4-dir and 8-dir
        int dx = Math.Abs(ax - bx);
        int dy = Math.Abs(ay - by);
        return Math.Max(dx, dy);
    }

    private static PathResult BuildResult(int[] cameFrom, float[] gCost, int w, int startIdx, int goalIdx)
    {
        var path = new List<(int X, int Y)>();
        int idx = goalIdx;
        while (idx != startIdx)
        {
            path.Add((idx % w, idx / w));
            idx = cameFrom[idx];
        }
        path.Add((startIdx % w, startIdx / w));
        path.Reverse();
        return new PathResult(path, gCost[goalIdx]);
    }
}
