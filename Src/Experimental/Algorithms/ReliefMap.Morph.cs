// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Buffers;
using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Erode(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var data = map.Data;

        var outputArray = ArrayPool<float>.Shared.Rent(data.Length);
        try
        {
            var output = outputArray.AsSpan()[..data.Length];
            data.CopyTo(output);
            Span<Point> neighborBuffer = stackalloc Point[8];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    if (data[idx] == 0)
                        continue;

                    // Set to 0 if on border
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    {
                        output[idx] = 0;
                        continue;
                    }

                    // Set to zero if any neighbor is 0
                    foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                    {
                        if (data[ny * width + nx] == 0)
                        {
                            output[idx] = 0;
                            break;
                        }
                    }
                }
            }

            output.CopyTo(data);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(outputArray);
        }
    }

    public static void Dilate(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var data = map.Data;

        var outputArray = ArrayPool<float>.Shared.Rent(data.Length);
        try
        {
            var output = outputArray.AsSpan()[..data.Length];
            data.CopyTo(output);
            Span<Point> neighborBuffer = stackalloc Point[8];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    // If pixel is 1, set neighbors to 1
                    if (data[idx] == 1)
                    {
                        output[idx] = 1;
                        foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                        {
                            output[ny * width + nx] = 1;
                        }
                    }
                }
            }

            output.CopyTo(data);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(outputArray);
        }
    }
}
