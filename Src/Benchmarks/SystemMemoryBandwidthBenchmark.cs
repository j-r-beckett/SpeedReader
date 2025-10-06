// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;

namespace Benchmarks;

public class SystemMemoryBandwidthBenchmark
{
    private readonly int _numCores;
    private readonly int _testPeriodSeconds;

    public SystemMemoryBandwidthBenchmark(int numCores, int testPeriodSeconds)
    {
        _numCores = numCores;
        _testPeriodSeconds = testPeriodSeconds;
    }

    public double RunBenchmark()
    {
        const int arraySize = 256 * 1024 * 1024 / sizeof(long); // 256 MB per array
        var warmupPeriod = TimeSpan.FromSeconds(1);
        var testPeriod = TimeSpan.FromSeconds(_testPeriodSeconds);

        var stopwatch = Stopwatch.StartNew();
        using var perfBandwidth = new PerfMemoryBandwidth();
        bool perfStarted = false;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _numCores };

        Parallel.For(0, _numCores, parallelOptions, coreId =>
        {
            var data = new long[arraySize];

            // Fill with data
            for (int i = 0; i < arraySize; i++)
                data[i] = i;

            while (stopwatch.Elapsed < testPeriod + warmupPeriod)
            {
                if (!perfStarted && stopwatch.Elapsed > warmupPeriod)
                {
                    lock (perfBandwidth)
                    {
                        if (!perfStarted)
                        {
                            perfBandwidth.Start();
                            perfStarted = true;
                        }
                    }
                }

                // Sequential reads - sum array values
                long sum = 0;
                for (int i = 0; i < arraySize; i++)
                {
                    sum += data[i];
                }

                // Prevent optimization from removing the loop
                if (sum == long.MaxValue)
                    data[0] = sum;
            }
        });

        var bandwidth = perfStarted ? perfBandwidth.Stop() : 0.0;
        return bandwidth;
    }
}