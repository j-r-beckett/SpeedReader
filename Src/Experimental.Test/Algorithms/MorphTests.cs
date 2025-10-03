// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Algorithms;

namespace Experimental.Test.Algorithms;

public class MorphTests
{
    [Fact]
    public void Erode_SinglePixel_DisappearsCompletely()
    {
        float[] data =
        [
            0f, 0f, 0f,
            0f, 1f, 0f,
            0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 3, height: 3);

        map.Erode();

        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(0f, data[i]);
        }
    }

    [Fact]
    public void Erode_ThreeByThreeSquare_ReducesToSinglePixel()
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

        map.Erode();

        Assert.Equal(1f, map[2, 2]);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (x != 2 || y != 2)
                {
                    Assert.Equal(0f, map[x, y]);
                }
            }
        }
    }

    [Fact]
    public void Dilate_SinglePixel_ExpandsToThreeByThree()
    {
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 5, height: 5);

        map.Dilate();

        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
            {
                Assert.Equal(1f, map[x, y]);
            }
        }

        Assert.Equal(0f, map[0, 0]);
        Assert.Equal(0f, map[4, 4]);
        Assert.Equal(0f, map[0, 4]);
        Assert.Equal(0f, map[4, 0]);
    }

    [Fact]
    public void Dilate_TwoSeparatePixels_ExpandsToSeparateRegions()
    {
        float[] data =
        [
            1f, 0f, 0f, 0f, 1f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f
        ];
        var map = new ReliefMap(data, width: 5, height: 5);

        map.Dilate();

        Assert.Equal(1f, map[0, 0]);
        Assert.Equal(1f, map[1, 0]);
        Assert.Equal(1f, map[0, 1]);
        Assert.Equal(1f, map[1, 1]);

        Assert.Equal(1f, map[4, 0]);
        Assert.Equal(1f, map[3, 0]);
        Assert.Equal(1f, map[4, 1]);
        Assert.Equal(1f, map[3, 1]);

        Assert.Equal(0f, map[2, 2]);
    }

    [Fact]
    public void ErodeGt_MatchesActualErode()
    {
        var random = new Random(0);

        var numIterations = 100000;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            const int width = 10;
            const int height = 10;
            var data = new float[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = random.NextSingle() > 0.5f ? 1f : 0f;
            }

            var mapActual = new ReliefMap([.. data], width, height);
            var mapExpected = new ReliefMap([.. data], width, height);

            mapActual.Erode();
            ErodeGt(mapExpected);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal(mapExpected[x, y], mapActual[x, y]);
                }
            }
        }
    }

    [Fact]
    public void DilateGt_MatchesActualDilate()
    {
        var random = new Random(0);

        var numIterations = 100000;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            const int width = 10;
            const int height = 10;
            var data = new float[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = random.NextSingle() > 0.5f ? 1f : 0f;
            }

            var mapActual = new ReliefMap([.. data], width, height);
            var mapExpected = new ReliefMap([.. data], width, height);

            mapActual.Dilate();
            DilateGt(mapExpected);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal(mapExpected[x, y], mapActual[x, y]);
                }
            }
        }
    }

    private static void ErodeGt(ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var result = new float[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var index = y * width + x;

                if (map[x, y] == 0f)
                {
                    result[index] = 0f;
                    continue;
                }

                var allNeighborsSet = true;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;

                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            allNeighborsSet = false;
                            break;
                        }

                        if (map[nx, ny] == 0f)
                        {
                            allNeighborsSet = false;
                            break;
                        }
                    }
                    if (!allNeighborsSet)
                        break;
                }

                result[index] = allNeighborsSet ? 1f : 0f;
            }
        }

        Array.Copy(result, map.Data, result.Length);
    }

    private static void DilateGt(ReliefMap map)
    {
        var width = map.Width;
        var height = map.Height;
        var result = new float[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var index = y * width + x;

                if (map[x, y] == 1f)
                {
                    result[index] = 1f;
                    continue;
                }

                var hasSetNeighbor = false;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;

                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && map[nx, ny] == 1f)
                        {
                            hasSetNeighbor = true;
                            break;
                        }
                    }
                    if (hasSetNeighbor)
                        break;
                }

                result[index] = hasSetNeighbor ? 1f : 0f;
            }
        }

        Array.Copy(result, map.Data, result.Length);
    }
}
