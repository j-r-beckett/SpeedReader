// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Ocr.Algorithms;
using SpeedReader.Ocr.Geometry;
using SpeedReader.TestUtils;
using Xunit.Abstractions;

namespace SpeedReader.Ocr.Test.Algorithms;

public class FloodFillTests
{
    private readonly TestLogger _logger;

    public FloodFillTests(ITestOutputHelper outputHelper) => _logger = new TestLogger(outputHelper);

    [Fact]
    public void FloodFill_SinglePixel_FillsOnlyThatPixel()
    {
        float[] data =
        [
            0f, 0f, 0f,
            0f, 1f, 0f,
            0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 3, height: 3);

        map.FloodFill(new Point { X = 1, Y = 1 });

        Assert.Equal(-1f, map[1, 1]);
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                if (x != 1 || y != 1)
                    Assert.Equal(0f, map[x, y]);
            }
        }
    }

    [Fact]
    public void FloodFill_HorizontalLine_FillsEntireLine()
    {
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f,
            0f, 0f, 0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 5, height: 3);

        map.FloodFill(new Point { X = 2, Y = 1 });

        for (int x = 0; x < 5; x++)
            Assert.Equal(-1f, map[x, 1]);
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(0f, map[x, 0]);
            Assert.Equal(0f, map[x, 2]);
        }
    }

    [Fact]
    public void FloodFill_VerticalLine_FillsEntireLine()
    {
        float[] data =
        [
            0f, 1f, 0f,
            0f, 1f, 0f,
            0f, 1f, 0f,
            0f, 1f, 0f,
            0f, 1f, 0f
        ];
        var map = new ReliefMap(data, width: 3, height: 5);

        map.FloodFill(new Point { X = 1, Y = 2 });

        for (int y = 0; y < 5; y++)
            Assert.Equal(-1f, map[1, y]);
        for (int y = 0; y < 5; y++)
        {
            Assert.Equal(0f, map[0, y]);
            Assert.Equal(0f, map[2, y]);
        }
    }

    [Fact]
    public void FloodFill_Rectangle_FillsEntireRectangle()
    {
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 5, height: 5);

        map.FloodFill(new Point { X = 2, Y = 2 });

        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
                Assert.Equal(-1f, map[x, y]);
        }
    }

    [Fact]
    public void FloodFill_DiagonalOnly_DoesNotFill()
    {
        float[] data =
        [
            1f, 0f, 0f,
            0f, 1f, 0f,
            0f, 0f, 1f
        ];
        var map = new ReliefMap(data, width: 3, height: 3);

        map.FloodFill(new Point { X = 1, Y = 1 });

        Assert.Equal(-1f, map[1, 1]);
        Assert.Equal(1f, map[0, 0]);
        Assert.Equal(1f, map[2, 2]);
    }

    [Fact]
    public void FloodFill_DisconnectedComponents_FillsOnlyConnectedComponent()
    {
        float[] data =
        [
            1f, 1f, 0f, 1f, 1f,
            1f, 1f, 0f, 1f, 1f,
            0f, 0f, 0f, 0f, 0f,
            1f, 1f, 0f, 1f, 1f,
            1f, 1f, 0f, 1f, 1f
        ];
        var map = new ReliefMap(data, width: 5, height: 5);

        map.FloodFill(new Point { X = 3, Y = 3 });

        Assert.Equal(-1f, map[3, 3]);
        Assert.Equal(-1f, map[3, 4]);
        Assert.Equal(-1f, map[4, 3]);
        Assert.Equal(-1f, map[4, 4]);

        Assert.Equal(1f, map[0, 0]);
        Assert.Equal(1f, map[1, 0]);
        Assert.Equal(1f, map[3, 0]);
        Assert.Equal(1f, map[0, 3]);
    }

    [Fact]
    public void FloodFill_StartOnBackground_DoesNothing()
    {
        float[] data =
        [
            0f, 1f, 0f,
            1f, 0f, 1f,
            0f, 1f, 0f
        ];
        var map = new ReliefMap([.. data], width: 3, height: 3);

        map.FloodFill(new Point { X = 1, Y = 1 });

        for (int i = 0; i < data.Length; i++)
            Assert.Equal(data[i], map.Data[i]);
    }

    [Fact]
    public void FloodFill_StartOnNegativeValue_DoesNothing()
    {
        float[] data = [0f, 1f, 1f];
        var map = new ReliefMap(data, width: 3, height: 1);

        // Fill first, creating a -1
        map[1, 0] = -1f;

        // Try to flood fill from the -1
        map.FloodFill(new Point { X = 1, Y = 0 });

        Assert.Equal(0f, map[0, 0]);
        Assert.Equal(-1f, map[1, 0]);
        Assert.Equal(1f, map[2, 0]);
    }

    [Fact]
    public void FloodFill_ComplexShape_MaintainsConnectivity()
    {
        // L-shape
        float[] data =
        [
            1f, 0f, 0f,
            1f, 0f, 0f,
            1f, 1f, 1f
        ];
        var map = new ReliefMap(data, width: 3, height: 3);

        map.FloodFill(new Point { X = 0, Y = 0 });

        for (int y = 0; y < 3; y++)
            Assert.Equal(-1f, map[0, y]);
        for (int x = 0; x < 3; x++)
            Assert.Equal(-1f, map[x, 2]);
    }

    [Fact]
    public void FloodFill_PropertyTest_AllFilledPixelsAreConnected()
    {
        var random = new Random(0);
        var numIterations = 1000;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            int width = random.Next(3, 15);
            int height = random.Next(3, 15);
            var data = new float[width * height];

            for (int i = 0; i < data.Length; i++)
                data[i] = random.NextSingle() > 0.5f ? 1f : 0f;

            int startX = random.Next(0, width);
            int startY = random.Next(0, height);

            var map = new ReliefMap([.. data], width, height);
            var originalData = (float[])data.Clone();

            map.FloodFill(new Point { X = startX, Y = startY });

            // Property: All filled pixels must be 4-connected to the start
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (map[x, y] == -1f)
                    {
                        Assert.True(originalData[y * width + x] > 0, $"Filled pixel at ({x},{y}) was not > 0 originally");
                        Assert.True(IsConnectedToStart(originalData, width, height, x, y, startX, startY),
                            $"Filled pixel at ({x},{y}) is not connected to start ({startX},{startY})");
                    }
                }
            }
        }
    }

    private static bool IsConnectedToStart(float[] data, int width, int height, int x, int y, int startX, int startY)
    {
        if (x == startX && y == startY)
            return true;

        if (data[y * width + x] <= 0)
            return false;

        var visited = new bool[width * height];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((x, y));
        visited[y * width + x] = true;

        (int dx, int dy)[] neighbors = [(-1, 0), (1, 0), (0, -1), (0, 1)];

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();

            if (cx == startX && cy == startY)
                return true;

            foreach (var (dx, dy) in neighbors)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                int idx = ny * width + nx;

                if (nx >= 0 && ny >= 0 && nx < width && ny < height &&
                    !visited[idx] && data[idx] > 0)
                {
                    visited[idx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return false;
    }
}
