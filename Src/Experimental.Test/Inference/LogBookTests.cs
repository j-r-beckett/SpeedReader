// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;

namespace Experimental.Test.Inference;

public class LogBookTests
{
    private readonly double _minExecutionTime = 0.01;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LogBook_LinearJobs_ConstantThreads_Dense(int threads)
    {
        var logBook = new LogBook();
        for (var i = 0; i < 20; i++)
        {
            List<LogBook.IToken> tokens = [];
            for (int t = 0; t < threads; t++)
                tokens.Add(logBook.LogStart());
            Thread.Sleep(TimeSpan.FromSeconds(_minExecutionTime));
            for (int t = 0; t < threads; t++)
                logBook.LogEnd(tokens[t]);
        }
        var stats = logBook.GetSummary(TimeSpan.Zero, TimeSpan.MaxValue);

        Assert.Equal(_minExecutionTime, stats.AvgDuration, 2);
        AssertWithin(threads, stats.AvgParallelism, 0.05);
        AssertWithin(threads / _minExecutionTime * 0.95, stats.AvgThroughput, 0.05);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LogBook_LinearJobs_ConstantThreads_Sparse(int threads)
    {
        var logBook = new LogBook();
        for (var i = 0; i < 20; i++)
        {
            if (i % 3 == 0)
            {
                List<LogBook.IToken> tokens = [];
                for (int t = 0; t < threads; t++)
                    tokens.Add(logBook.LogStart());
                Thread.Sleep(TimeSpan.FromSeconds(_minExecutionTime));
                for (int t = 0; t < threads; t++)
                    logBook.LogEnd(tokens[t]);
            }
            else
            {
                Thread.Sleep(TimeSpan.FromSeconds(_minExecutionTime));
            }
        }
        var stats = logBook.GetSummary(TimeSpan.Zero, TimeSpan.MaxValue);

        // Should be the same as the dense case
        Assert.Equal(_minExecutionTime, stats.AvgDuration, 2);
        AssertWithin(threads, stats.AvgParallelism, 0.05);
        AssertWithin(threads / _minExecutionTime * 0.95, stats.AvgThroughput, 0.05);
    }

    [Fact]
    public void LogBook_NoJobs()
    {
        var logBook = new LogBook();

        var stats = logBook.GetSummary(TimeSpan.Zero, TimeSpan.MaxValue);

        Assert.Equal(0, stats.AvgDuration);
        Assert.Equal(0, stats.AvgParallelism);
        Assert.Equal(0, stats.AvgThroughput);
    }

    [Fact]
    public void LogBook_NoCompletedJobs()
    {
        var logBook = new LogBook();

        logBook.LogStart();
        Thread.Sleep(TimeSpan.FromSeconds(_minExecutionTime) * 5);

        var stats = logBook.GetSummary(TimeSpan.Zero, TimeSpan.MaxValue);

        Assert.Equal(0, stats.AvgDuration);
        Assert.Equal(0, stats.AvgParallelism);
        Assert.Equal(0, stats.AvgThroughput);
    }

    // Verifies that actual is within (100 * maxVariation)% of expected
    private static void AssertWithin(double expected, double actual, double maxVariation)
    {
        var tolerance = maxVariation * expected;
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected: {expected}, Actual: {actual}");
    }
}
