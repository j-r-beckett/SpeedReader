// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    // Set all pixels connected to start to the given value
    public static void FloodFill(this ReliefMap map, Point start, float value)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((start.X, start.Y));

        Span<Point> neighborBuffer = stackalloc Point[8];

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();

            // Skip if already processed or background
            if (map[x, y] <= 0)
                continue;

            // Mark as processed
            map[x, y] = value;

            // Push neighbors to stack
            foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                stack.Push((nx, ny));
        }
    }
}
