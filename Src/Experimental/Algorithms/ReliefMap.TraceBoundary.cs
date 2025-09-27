// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    // Moore neighborhood
    private static readonly (int dx, int dy)[] _neighborhood = [
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1)
    ];

    // This method assumes a smooth boundary without any holes, self-intersections, or other gnarly features.
    // To achieve this, the caller can/should use morphological opening as a preprocessing step.
    public static Polygon TraceBoundary(this ReliefMap map, Point start)
    {
        List<Point> boundary = [];

        int currentX = start.X, currentY = start.Y;

        int direction = GetStartingDirection(currentX, currentY);
        if (direction == -1)
        {
            // Boundary is a single pixel
            return new Polygon { Points = [start] };
        }

        do
        {
            boundary.Add((currentX, currentY));

            // Find next boundary pixel using Moore neighborhood
            var next = FindNextBoundaryPixel(currentX, currentY, direction);
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

            // Stop tracing once we've worked our way back around to the starting pixel.
            // This effectively gives us a polygon that approximates the boundary, which is exactly what we want.
            if (boundary.Count > 2)
            {
                // Check if we're on the starting pixel
                if (currentX == start.X && currentY == start.Y)
                    break;

                // Check if we're on one of the starting pixel's neighbors
                int dx = Math.Abs(currentX - start.X);
                int dy = Math.Abs(currentY - start.Y);
                if (dx <= 1 && dy <= 1)
                    break;
            }
        } while (true);

        return new Polygon { Points = boundary.ToImmutableList() };

        // Helper method call graph:
        //   GetStartingDirection -> IsBackground
        //   FindNextBoundaryPixel -> IsBoundaryPixel -> IsBackground
        //                                            -> IsForeground

        // Walks around the Moore neighborhood to find the first direction that leads to a background pixel
        int GetStartingDirection(int x, int y)
        {
            for (int i = 0; i < 8; i++)
            {
                var (dx, dy) = _neighborhood[i];
                if (IsBackground(x + dx, y + dy))
                    return i;
            }
            return -1; // No background neighbor found
        }

        (int x, int y, int direction)? FindNextBoundaryPixel(int x, int y, int startDirection)
        {
            int searchStart = (startDirection + 6) % 8; // Back up 2 positions

            for (int i = 0; i < 8; i++)
            {
                int dir = (searchStart + i) % 8;
                var (dx, dy) = _neighborhood[dir];
                var (nx, ny) = (x + dx, y + dy);

                if (IsBoundaryPixel(nx, ny))
                    return (nx, ny, dir);
            }

            return null; // No next boundary pixel found
        }

        bool IsBoundaryPixel(int x, int y)
        {
            if (!IsForeground(x, y))
                return false;

            foreach (var (dx, dy) in _neighborhood)
            {
                if (IsBackground(x + dx, y + dy))
                    return true;
            }

            return false;
        }

        // In bounds and equal to 1
        bool IsForeground(int x, int y) =>
            x >= 0 && x < map.Width && y >= 0 && y < map.Height && map[x, y] == 1;

        // Out of bounds or less than or equal to 0
        bool IsBackground(int x, int y) =>
            x < 0 || x >= map.Width || y < 0 || y >= map.Height || map[x, y] <= 0;
    }
}
