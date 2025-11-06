// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Buffers;
using System.Numerics;
using Ocr.Geometry;

namespace Ocr.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Erode(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var data = map.Data.AsSpan();

        Span<Point> neighborBuffer = stackalloc Point[8];

        var vectorSize = Vector<float>.Count;

        for (int y = 0; y < height; y++)
        {
            // Set left and right edges to -0.1 if they are 1
            if (data[y * width] == 1)
                data[y * width] = -0.1f;
            if (data[y * width + width - 1] == 1)
                data[y * width + width - 1] = -0.1f;

            int x = 1;
            if (Vector.IsHardwareAccelerated && width - 2 >= vectorSize)
            {
                for (; x <= width - 1 - vectorSize; x += vectorSize)
                {
                    var vec = new Vector<float>(data.Slice(y * width + x, vectorSize));
                    if (Vector.GreaterThanAny(vec, Vector<float>.Zero))
                    {
                        for (int i = 0; i < vectorSize; i++)
                        {
                            int idx = y * width + x + i;
                            if (data[idx] == 1)
                            {
                                // Set to -0.1 if any neighbor is 0
                                foreach (var (nx, ny) in map.GetNeighbors((x + i, y), neighborBuffer))
                                {
                                    if (data[ny * width + nx] == 0)
                                    {
                                        data[idx] = -0.1f;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (; x < width - 1; x++)
            {
                int idx = y * width + x;

                if (data[idx] == 0)
                    continue;

                // Set to -0.1 if any neighbor is 0
                foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                {
                    if (data[ny * width + nx] == 0)
                    {
                        data[idx] = -0.1f;
                        break;
                    }
                }
            }
        }

        // Set top and bottom edges to 0
        for (int x = 0; x < width; x++)
        {
            data[x] = 0;
            data[width * (height - 1) + x] = 0;
        }

        // Flip -0.1 to 0
        int j = 0;
        if (Vector.IsHardwareAccelerated && data.Length >= vectorSize)
        {
            for (; j <= data.Length - vectorSize; j += vectorSize)
            {
                var vec = new Vector<float>(data.Slice(j, vectorSize));
                var ceil = Vector.Ceiling(vec);
                ceil.CopyTo(data.Slice(j, vectorSize));
            }
        }

        for (; j < data.Length; j++)
        {
            if (data[j] < 0)
                data[j] = 0;
        }
    }

    public static void Dilate(this ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var data = map.Data.AsSpan();

        var vectorSize = Vector<float>.Count;

        Span<Point> neighborBuffer = stackalloc Point[8];

        for (int y = 0; y < height; y++)
        {
            int x = 0;
            if (Vector.IsHardwareAccelerated && width >= vectorSize)
            {
                for (; x <= width - vectorSize; x += vectorSize)
                {
                    var vec = new Vector<float>(data.Slice(y * width + x, vectorSize));
                    if (Vector.GreaterThanAny(vec, Vector<float>.Zero))
                    {
                        for (int i = 0; i < vectorSize; i++)
                        {
                            int idx = y * width + x + i;

                            // If pixel is 1, set neighbors to -1
                            if (data[idx] == 1)
                            {
                                foreach (var (nx, ny) in map.GetNeighbors((x + i, y), neighborBuffer))
                                {
                                    if (data[ny * width + nx] == 0)
                                        data[ny * width + nx] = -1;
                                }
                            }
                        }
                    }
                }
            }

            for (; x < width; x++)
            {
                int idx = y * width + x;

                // If pixel is 1, set neighbors to -1
                if (data[idx] == 1)
                {
                    foreach (var (nx, ny) in map.GetNeighbors((x, y), neighborBuffer))
                    {
                        if (data[ny * width + nx] == 0)
                            data[ny * width + nx] = -1;
                    }
                }
            }
        }

        // Flip -1 to 1
        int j = 0;
        if (Vector.IsHardwareAccelerated && data.Length >= vectorSize)
        {
            for (; j <= data.Length - vectorSize; j += vectorSize)
            {
                var vec = new Vector<float>(data.Slice(j, vectorSize));
                var ceil = Vector.Abs(vec);
                ceil.CopyTo(data.Slice(j, vectorSize));
            }
        }

        for (; j < data.Length; j++)
        {
            if (data[j] < 0)
                data[j] = 1;
        }
    }
}
