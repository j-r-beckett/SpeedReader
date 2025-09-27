// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Erode(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip if already 0
                if (map[x, y] == 0.0f)
                    continue;

                // Mark for erosion if any neighbor is out of bounds
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    map[x, y] = -0.5f;
                    continue;
                }

                // Mark for erosion if any neighbor is 0
                foreach (var (nx, ny) in map.Neighbors((x, y)))
                {
                    if (map[nx, ny] == 0.0f)
                    {
                        map[x, y] = -0.5f;
                        break;
                    }
                }
            }
        }

        // Flip -0.5 to 0
        Tensor.Ceiling<float>(map.Data, map.Data);
    }

    public static void Dilate(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip if already 1
                if (map[x, y] == 1.0f)
                    continue;

                // Mark for dilation if any neighbor is 1
                foreach (var (nx, ny) in map.Neighbors((x, y)))
                {
                    if (map[nx, ny] == 1.0f)
                    {
                        map[x, y] = -1.0f;
                        break;
                    }
                }
            }
        }

        // Flip -1 to 1
        Tensor.Abs<float>(map.Data, map.Data);
    }
}
