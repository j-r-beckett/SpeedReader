// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Geometry;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    // Set all pixels connected to start to the given value using scanline flood fill
    public static void FloodFill(this ReliefMap map, Point start, float value)
    {
        // Quick exit if start is already processed or background
        if (map[start.X, start.Y] <= 0)
            return;

        var stack = new Stack<(int y, int xLeft, int xRight)>();

        // Scan and fill the initial line
        int left = ScanLeft(map, start.X, start.Y);
        int right = ScanRight(map, start.X, start.Y);
        FillScanline(map, start.Y, left, right, value);
        stack.Push((start.Y, left, right));

        while (stack.Count > 0)
        {
            var (y, xLeft, xRight) = stack.Pop();

            // Check line above
            if (y > 0)
                ScanAndPushLines(map, y - 1, xLeft, xRight, value, stack);

            // Check line below
            if (y < map.Height - 1)
                ScanAndPushLines(map, y + 1, xLeft, xRight, value, stack);
        }
    }

    private static int ScanLeft(ReliefMap map, int x, int y)
    {
        while (x > 0 && map[x - 1, y] > 0)
            x--;
        return x;
    }

    private static int ScanRight(ReliefMap map, int x, int y)
    {
        while (x < map.Width - 1 && map[x + 1, y] > 0)
            x++;
        return x;
    }

    private static void FillScanline(ReliefMap map, int y, int xLeft, int xRight, float value)
    {
        for (int x = xLeft; x <= xRight; x++)
            map[x, y] = value;
    }

    private static void ScanAndPushLines(ReliefMap map, int y, int xLeft, int xRight, float value, Stack<(int y, int xLeft, int xRight)> stack)
    {
        int searchLeft = Math.Max(0, xLeft - 1);
        int searchRight = Math.Min(map.Width - 1, xRight + 1);

        int x = searchLeft;
        while (x <= searchRight)
        {
            while (x <= searchRight && map[x, y] <= 0)
                x++;

            if (x > searchRight)
                break;

            // Found a span to fill
            int left = ScanLeft(map, x, y);
            int right = ScanRight(map, x, y);
            FillScanline(map, y, left, right, value);
            stack.Push((y, left, right));

            x = right + 1;
        }
    }
}
