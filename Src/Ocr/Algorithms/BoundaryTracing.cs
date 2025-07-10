using System.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace Ocr.Algorithms;

public static class BoundaryTracing
{
    // 8 directions: N, NE, E, SE, S, SW, W, NW
    private static readonly (int dx, int dy)[] Directions = {
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    /// <summary>
    /// Finds all boundaries in a binary probability map using morphological opening followed by Moore boundary tracing.
    /// </summary>
    /// <param name="probabilityMap">2D binary probability map where values are either 0 or 1</param>
    /// <returns>List of boundaries, each containing the ordered boundary coordinates of that region</returns>
    /// <remarks>
    /// The input probability map is modified during processing - traced regions are marked as -1.
    /// Uses morphological opening to clean the image, then Moore tracing to get ordered polygons.
    /// </remarks>
    public static List<(int X, int Y)[]> FindBoundaries(Span2D<float> probabilityMap)
    {
        Debug.Assert(probabilityMap.ToArray().Cast<float>().Min() >= 0
                     && probabilityMap.ToArray().Cast<float>().Max() <= 1);

        // Apply morphological opening to clean up the probability map
        MorphologicalOps.OpeningInPlace(probabilityMap, 0.5f);

        List<(int X, int Y)[]> boundaries = [];

        for (int y = 0; y < probabilityMap.Height; y++)
        {
            for (int x = 0; x < probabilityMap.Width; x++)
            {
                // Skip already processed pixels
                if (probabilityMap[y, x] == -1)
                    continue;

                // Look for foreground pixels that are boundary pixels
                if (probabilityMap[y, x] > 0 && IsBoundaryPixel(x, y, probabilityMap))
                {
                    var boundary = TraceMooreBoundary(x, y, probabilityMap);
                    if (boundary.Length > 0)
                    {
                        boundaries.Add(boundary);
                        // Flood fill the entire connected component to prevent re-tracing
                        FloodFillComponent(x, y, probabilityMap);
                    }
                }
            }
        }

        return boundaries;
    }

    /// <summary>
    /// Checks if a pixel is a boundary pixel (foreground adjacent to background).
    /// Uses virtual edges - pixels outside bounds are treated as background.
    /// </summary>
    private static bool IsBoundaryPixel(int x, int y, Span2D<float> probabilityMap)
    {
        if (!IsForeground(x, y, probabilityMap))
            return false;

        // Check all 8 neighbors - boundary if ANY neighbor is background
        foreach (var (dx, dy) in Directions)
        {
            if (IsBackground(x + dx, y + dy, probabilityMap))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a pixel is background (0, -1, or outside image bounds).
    /// </summary>
    private static bool IsBackground(int x, int y, Span2D<float> probabilityMap)
    {
        // Virtual edges: pixels outside bounds are treated as background
        if (x < 0 || x >= probabilityMap.Width || y < 0 || y >= probabilityMap.Height)
            return true;

        return probabilityMap[y, x] <= 0; // 0 or -1 (processed)
    }

    /// <summary>
    /// Checks if a pixel is foreground (value > 0 and within bounds).
    /// </summary>
    private static bool IsForeground(int x, int y, Span2D<float> probabilityMap)
    {
        if (x < 0 || x >= probabilityMap.Width || y < 0 || y >= probabilityMap.Height)
            return false;

        return probabilityMap[y, x] > 0;
    }

    /// <summary>
    /// Traces the boundary of a region using Moore boundary tracing with Jacob's stopping criterion.
    /// Returns an ordered sequence of boundary pixels forming a polygon.
    /// </summary>
    private static (int X, int Y)[] TraceMooreBoundary(int startX, int startY, Span2D<float> probabilityMap)
    {
        List<(int X, int Y)> boundary = [];
        
        int currentX = startX, currentY = startY;
        int firstX = startX, firstY = startY;
        
        // Find initial direction by looking for first background neighbor
        int direction = FindInitialDirection(currentX, currentY, probabilityMap);
        if (direction == -1) 
        {
            // Single pixel component
            return [(startX, startY)];
        }

        do
        {
            boundary.Add((currentX, currentY));
            
            // Find next boundary pixel using Moore neighborhood
            var next = FindNextBoundaryPixel(currentX, currentY, direction, probabilityMap);
            if (next.HasValue)
            {
                currentX = next.Value.x;
                currentY = next.Value.y;
                direction = next.Value.direction;
            }
            else
            {
                break; // No next pixel found
            }
            
            // Jacob's stopping criterion: stop when we return to start with same approach direction
            if (currentX == firstX && currentY == firstY && boundary.Count > 2)
            {
                break;
            }
            
            // Safety check to prevent infinite loops
            if (boundary.Count > probabilityMap.Width * probabilityMap.Height)
            {
                break;
            }
            
        } while (true);

        return boundary.ToArray();
    }

    /// <summary>
    /// Finds the initial direction for Moore tracing by locating the first background neighbor.
    /// </summary>
    private static int FindInitialDirection(int x, int y, Span2D<float> probabilityMap)
    {
        for (int i = 0; i < 8; i++)
        {
            var (dx, dy) = Directions[i];
            if (IsBackground(x + dx, y + dy, probabilityMap))
            {
                return i;
            }
        }
        return -1; // No background neighbor found
    }

    /// <summary>
    /// Finds the next boundary pixel in Moore tracing, starting search from the given direction.
    /// </summary>
    private static (int x, int y, int direction)? FindNextBoundaryPixel(int currentX, int currentY, int startDirection, Span2D<float> probabilityMap)
    {
        // Start searching from the direction opposite to where we came from
        int searchStart = (startDirection + 6) % 8; // Back up 2 positions (opposite direction)
        
        for (int i = 0; i < 8; i++)
        {
            int dir = (searchStart + i) % 8;
            var (dx, dy) = Directions[dir];
            int nextX = currentX + dx;
            int nextY = currentY + dy;
            
            if (IsForeground(nextX, nextY, probabilityMap) && IsBoundaryPixel(nextX, nextY, probabilityMap))
            {
                return (nextX, nextY, dir);
            }
        }
        
        return null; // No next boundary pixel found
    }

    /// <summary>
    /// Flood fills the entire connected component with -1 to mark it as processed.
    /// </summary>
    private static void FloodFillComponent(int startX, int startY, Span2D<float> probabilityMap)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));
        
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            
            // Skip if out of bounds or already processed/background
            if (x < 0 || x >= probabilityMap.Width || y < 0 || y >= probabilityMap.Height || 
                probabilityMap[y, x] <= 0)
                continue;
                
            // Mark as processed
            probabilityMap[y, x] = -1;
            
            // Add all 8-connected foreground neighbors
            foreach (var (dx, dy) in Directions)
            {
                stack.Push((x + dx, y + dy));
            }
        }
    }
}
