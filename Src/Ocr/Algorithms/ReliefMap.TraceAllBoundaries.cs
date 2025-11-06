// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Ocr.Geometry;

namespace Ocr.Algorithms;

public static partial class ReliefMapExtensions
{
    // Mutates the relief map
    internal static List<Polygon> TraceAllBoundariesInternal(this ReliefMap map)
    {
        // Binarize using threshold recommended by DBNet paper. All values in map are now either 0 or 1
        map.Binarize(0.2f);

        // Morphological opening to clean up boundaries
        map.Erode();
        map.Dilate();

        // Find all boundaries
        var boundaries = new List<Polygon>();  // Rough estimate of how many words can fit in a 640x640 tile

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map[x, y] == -1)
                    continue;  // Has already been traced, skip

                if (IsOnBoundary(x, y))
                {
                    var boundary = map.TraceBoundary((x, y));  // Moore boundary tracing to get boundary
                    if (boundary.Points.Count > 0)
                    {
                        boundaries.Add(boundary);
                        map.FloodFill((x, y), -1);  // Set boundary + enclosed pixels to -1 to prevent re-tracing
                    }
                }
            }
        }

        return boundaries;

        // Returns true if the given pixel is 1 and has at least one neighbor that is 0 (counting out of bounds as 0)
        bool IsOnBoundary(int x, int y)
        {
            if (map[x, y] == 0)
                return false;

            Debug.Assert(map[x, y] == 1);

            // Check if neighbor is out of bounds
            if (x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1)
                return true;

            Span<Point> neighborBuffer = stackalloc Point[8];

            // Check if neighbor is 0
            foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
            {
                if (map[nx, ny] == 0)
                    return true;
            }

            return false;
        }
    }
}
