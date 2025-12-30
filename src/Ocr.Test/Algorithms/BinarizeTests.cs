// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Ocr.Algorithms;

namespace SpeedReader.Ocr.Test.Algorithms;

public class BinarizeTests
{
    [Fact]
    public void Binarize_MatchesNaiveBinarize()
    {
        var random = new Random(0);
        var numIterations = 10000;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            int width = random.Next(1, 50);
            int height = random.Next(1, 50);
            var data = new float[width * height];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = random.NextSingle();
            }

            float threshold = random.NextSingle();

            var mapActual = new ReliefMap([.. data], width, height);
            var mapExpected = new ReliefMap([.. data], width, height);

            mapActual.Binarize(threshold);
            SimpleBinarize(mapExpected, threshold);

            for (int i = 0; i < data.Length; i++)
            {
                Assert.Equal(mapExpected.Data[i], mapActual.Data[i]);
            }
        }
    }

    private static void SimpleBinarize(ReliefMap map, float threshold)
    {
        for (int i = 0; i < map.Data.Length; i++)
        {
            map.Data[i] = map.Data[i] >= threshold ? 1.0f : 0.0f;
        }
    }
}
