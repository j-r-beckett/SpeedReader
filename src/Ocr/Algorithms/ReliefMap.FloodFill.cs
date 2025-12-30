// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Ocr.Geometry;

namespace SpeedReader.Ocr.Algorithms;

public static partial class ReliefMapExtensions
{
    // Fills all pixels > 0 that are connected to start with -1
    public static void FloodFill(this ReliefMap map, Point start)
    {
        if (map[start.X, start.Y] <= 0)
            return;

        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((start.X, start.Y));
        map[start.X, start.Y] = -1;

        (int dx, int dy)[] neighbors = [(-1, 0), (1, 0), (0, -1), (0, 1)];

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            foreach (var (dx, dy) in neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height && map[nx, ny] > 0)
                {
                    map[nx, ny] = -1;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }
}
