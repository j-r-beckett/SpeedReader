// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using SpeedReader.Ocr.Geometry;

namespace SpeedReader.Ocr.Algorithms;

public record ReliefMap
{
    public float[] Data { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    private bool _hasBeenTraced;

    public ReliefMap(float[] probabilities, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0, nameof(width));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0, nameof(height));

        if (probabilities.Length != width * height)
        {
            throw new ArgumentException(
                $"Expected width x height = length, got {width} x {height} = {probabilities.Length}");
        }

        Debug.Assert(Tensor.Min<float>(probabilities) >= 0 && Tensor.Max<float>(probabilities) <= 1, "All probabilities must be in [0, 1]");

        Data = probabilities;
        Width = width;
        Height = height;
    }

    public List<Polygon> TraceAllBoundaries()
    {
        if (_hasBeenTraced)
            throw new InvalidOperationException($"{nameof(TraceAllBoundaries)} can only be called once");

        _hasBeenTraced = true;

        return this.TraceAllBoundariesInternal();  // This mutates the relief map, so we can only do it once
    }

    internal float this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }
}

public static partial class ReliefMapExtensions
{
    private static readonly List<(int dx, int dy)> _eightConnectivity =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), /*          */ (1, 0),
        (-1, 1), (0, 1), (1, 1)
    ];

    private static ReadOnlySpan<Point> GetNeighbors(this ReliefMap map, Point point, Span<Point> buffer)
    {
        int count = 0;
        foreach (var (dx, dy) in _eightConnectivity)
        {
            int nx = point.X + dx;
            int ny = point.Y + dy;
            if (nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height)
                buffer[count++] = new Point { X = nx, Y = ny };
        }
        return buffer[..count];
    }
}
