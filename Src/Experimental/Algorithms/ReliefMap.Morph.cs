// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;
using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Erode(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;

        Span<Point> neighborBuffer = stackalloc Point[8];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip if already 0
                if (map[x, y] == 0)
                    continue;

                // Mark for erosion if any neighbor is out of bounds
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    map[x, y] = -1;
                    continue;
                }

                // Mark for erosion if any neighbor is 0
                foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                {
                    if (map[nx, ny] == 0)
                    {
                        map[x, y] = -1;
                        break;
                    }
                }
            }
        }

        // Flip -1 to 0
        var data = map.Data;
        for (int i = 0; i < data.Length; i++)
            data[i] = data[i] == -1 ? 0 : data[i];
    }

    public static void Dilate(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;

        Span<Point> neighborBuffer = stackalloc Point[8];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip if already 1
                if (map[x, y] == 1)
                    continue;

                // Mark for dilation if any neighbor is 1
                foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                {
                    if (map[nx, ny] == 1)
                    {
                        map[x, y] = -1;
                        break;
                    }
                }
            }
        }

        // Flip -1 to 1
        var data = map.Data;
        for (int i = 0; i < data.Length; i++)
            data[i] = data[i] == -1 ? 1 : data[i];
    }
}
