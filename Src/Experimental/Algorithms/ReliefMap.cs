// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Experimental.Geometry;

namespace Experimental.Algorithms;

public record ReliefMap
{
    public float[] Data { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    private bool _hasBeenTraced;

    public ReliefMap(float[] probabilities, int width, int height)
    {
        if (probabilities.Length != width * height)
        {
            throw new ArgumentException(
                $"Expected width x height = length, got {width} x {height} = {probabilities.Length}");
        }

        if (Tensor.Min<float>(probabilities) < 0 || Tensor.Max<float>(probabilities) > 1)
            throw new ArgumentException("All probabilities must be in [0, 1]");

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

    private Span2D<float> Span2D => Data.AsSpan().AsSpan2D(Height, Width);

    internal float this[int x, int y]
    {
        get => Span2D[y, x];
        set => Span2D[y, x] = value;
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

    private static Point[] Neighbors(this ReliefMap map, Point point) => _eightConnectivity
        .Select(dir => new Point { X = point.X + dir.dx, Y = point.Y + dir.dy })
        .Where(p => p.X >= 0 && p.Y >= 0 && p.X < map.Width && p.Y < map.Height)
        .ToArray();
}
