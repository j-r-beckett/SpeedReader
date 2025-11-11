// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using Ocr.Algorithms;
using Ocr.Geometry;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.Algorithms;

public class FloodFillTests
{
    private readonly TestLogger _logger;

    public FloodFillTests(ITestOutputHelper outputHelper) => _logger = new TestLogger(outputHelper);

    [Fact]
    public void FloodFill_MatchesNaiveFloodFill()
    {
        var random = new Random(0);
        var numIterations = 10000;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            int width = random.Next(1, 10);
            int height = random.Next(1, 10);
            var data = new float[width * height];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = random.NextSingle() > 0.5f ? 1f : 0f;
            }

            int startX = random.Next(0, width);
            int startY = random.Next(0, height);

            var mapActual = new ReliefMap([.. data], width, height);
            var mapExpected = new ReliefMap([.. data], width, height);

            mapActual.FloodFill(new Point { X = startX, Y = startY });
            NaiveFloodFill(mapExpected, startX, startY);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mapExpected[x, y] != mapActual[x, y])
                    {
                        _logger.LogInformation($"Mismatch at iteration {iteration}, position ({x}, {y})");
                        _logger.LogInformation($"Width: {width}, Height: {height}, Start: ({startX}, {startY})");
                        _logger.LogInformation("Original map:");
                        LogMap(data, width, height);
                        _logger.LogInformation("Expected (naive BFS):");
                        LogMap(mapExpected.Data, width, height);
                        _logger.LogInformation("Actual (scanline):");
                        LogMap(mapActual.Data, width, height);
                        Assert.Fail($"Mismatch at ({x}, {y}): Expected {mapExpected[x, y]}, Actual {mapActual[x, y]}");
                    }
                }
            }
        }
    }

    private void LogMap(float[] data, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            var line = "";
            for (int x = 0; x < width; x++)
            {
                var val = data[y * width + x];
                line += val == -1 ? "F " : (val > 0 ? "1 " : "0 ");
            }
            _logger.LogInformation(line);
        }
    }

    private static void NaiveFloodFill(ReliefMap map, int startX, int startY)
    {
        if (map[startX, startY] <= 0)
            return;

        var original = new float[map.Data.Length];
        Array.Copy(map.Data, original, map.Data.Length);

        var queue = new Queue<(int x, int y)>();
        var visited = new HashSet<(int x, int y)>();

        queue.Enqueue((startX, startY));
        visited.Add((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var idx = y * map.Width + x;

            if (original[idx] <= 0)
                continue;

            map[x, y] = -1;

            (int dx, int dy)[] neighbors = [(-1, 0), (1, 0), (0, -1), (0, 1)];

            foreach (var (dx, dy) in neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height)
                {
                    int nidx = ny * map.Width + nx;
                    if (!visited.Contains((nx, ny)) && original[nidx] > 0)
                    {
                        visited.Add((nx, ny));
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }
    }
}
