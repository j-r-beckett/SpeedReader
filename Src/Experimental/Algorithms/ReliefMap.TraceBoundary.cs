// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    private static readonly (int dx, int dy)[] _directions = {
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    };

    public static Polygon TraceBoundary(this ReliefMap map, Point start)
    {
        List<Point> boundary = [];

        int currentX = start.X, currentY = start.Y;
        int firstX = start.X, firstY = start.Y;

        // Find initial direction by looking for first background neighbor
        int direction = FindInitialDirection(currentX, currentY, map);
        if (direction == -1)
        {
            // Single pixel component
            return new Polygon { Points = [start] };
        }

        do
        {
            boundary.Add((currentX, currentY));

            // Find next boundary pixel using Moore neighborhood
            var next = FindNextBoundaryPixel(currentX, currentY, direction, map);
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

            // Single-trace stopping criterion: stop when boundary.Count > 2 and current pixel
            // is either the starting pixel or one of its 8-connected neighbors
            if (boundary.Count > 2 && IsStartingPixelOrNeighbor(currentX, currentY, firstX, firstY))
                break;

            // Safety check to prevent infinite loops
            if (boundary.Count > map.Width * map.Height)
                break;

        } while (true);

        return new Polygon { Points = boundary.ToImmutableList() };
    }

    private static bool IsStartingPixelOrNeighbor(int currentX, int currentY, int startX, int startY)
    {
        // Check if it's the starting pixel
        if (currentX == startX && currentY == startY)
            return true;

        // Check if it's one of the starting pixel's 8-connected neighbors
        int dx = Math.Abs(currentX - startX);
        int dy = Math.Abs(currentY - startY);
        return dx <= 1 && dy <= 1;
    }

    private static int FindInitialDirection(int x, int y, ReliefMap map)
    {
        for (int i = 0; i < 8; i++)
        {
            var (dx, dy) = _directions[i];
            if (IsBackground(x + dx, y + dy, map))
            {
                return i;
            }
        }
        return -1; // No background neighbor found
    }

    private static (int x, int y, int direction)? FindNextBoundaryPixel(int currentX, int currentY, int startDirection, ReliefMap map)
    {
        // Start searching from the direction opposite to where we came from
        int searchStart = (startDirection + 6) % 8; // Back up 2 positions (opposite direction)

        for (int i = 0; i < 8; i++)
        {
            int dir = (searchStart + i) % 8;
            var (dx, dy) = _directions[dir];
            int nextX = currentX + dx;
            int nextY = currentY + dy;

            if (IsForeground(nextX, nextY, map) && IsBoundaryPixel(nextX, nextY, map))
            {
                return (nextX, nextY, dir);
            }
        }

        return null; // No next boundary pixel found
    }

    private static bool IsBackground(int x, int y, ReliefMap map)
    {
        // Virtual edges: pixels outside bounds are treated as background
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
        {
            return true;
        }

        return map[x, y] <= 0; // 0 or -1 (processed)
    }

    /// <summary>
    /// Checks if a pixel is foreground (value > 0 and within bounds).
    /// </summary>
    private static bool IsForeground(int x, int y, ReliefMap map) =>
        x < 0 || x >= map.Width || y < 0 || y >= map.Height ? false : map[x, y] > 0;

    private static bool IsBoundaryPixel(int x, int y, ReliefMap map)
    {
        if (!IsForeground(x, y, map))
        {
            return false;
        }

        // Check all 8 neighbors - boundary if ANY neighbor is background
        foreach (var (dx, dy) in _directions)
        {
            if (IsBackground(x + dx, y + dy, map))
            {
                return true;
            }
        }
        return false;
    }
}
