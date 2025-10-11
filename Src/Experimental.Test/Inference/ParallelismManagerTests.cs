// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;

namespace Experimental.Test.Inference;

public class ParallelismManagerTests
{
    [Fact(Timeout = 500)]
    public async Task DoesNotExceedMaxDegreeOfParallelism()
    {
        // Arrange
        const int maxParallelism = 2;
        bool proceed = false;
        var parallelism = 0;

        Func<int, int> func = _ =>
        {
            Interlocked.Increment(ref parallelism);
            while (!proceed)
                Thread.Sleep(10);
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int, int>(func, maxParallelism);

        // Act
        var tasks = Enumerable.Range(0, 4).Select(_ => manager.Call(0)).ToList();
        await Task.Delay(50);
        var observedParallelism = parallelism;

        proceed = true;
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(maxParallelism, observedParallelism);
    }

    [Fact(Timeout = 500)]
    public async Task Increment_IncreasesParallelism()
    {
        // Arrange
        const int initialParallelism = 1;
        var proceed = false;
        var parallelism = 0;

        Func<int, int> func = _ =>
        {
            Interlocked.Increment(ref parallelism);
            while (!proceed)
                Thread.Sleep(10);
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int, int>(func, initialParallelism);

        // Act
        var tasks = Enumerable.Range(0, 3).Select(_ => manager.Call(0)).ToList();
        await Task.Delay(50);
        Assert.Equal(1, parallelism);

        manager.IncrementParallelism();
        await Task.Delay(50);
        var observedParallelism = parallelism;

        proceed = true;
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(2, observedParallelism);
    }

    [Fact(Timeout = 500)]
    public async Task Decrement_DecreasesParallelism()
    {
        // Arrange
        const int initialParallelism = 2;
        var proceedFlags = new bool[4];
        var parallelism = 0;

        Func<int, int> func = _ =>
        {
            var currentIndex = Interlocked.Increment(ref parallelism) - 1;
            while (!proceedFlags[currentIndex])
                Thread.Sleep(10);
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int, int>(func, initialParallelism);

        // Act
        var tasks = Enumerable.Range(0, 4).Select(_ => manager.Call(0)).ToList();
        await Task.Delay(50);
        Assert.Equal(2, parallelism);

        // Start decrement in background
        var decrementTask = Task.Run(() => manager.DecrementParallelism());
        await Task.Delay(50);

        // Release the first task while decrement is ongoing
        proceedFlags[0] = true;
        await decrementTask;
        await Task.Delay(50);
        var observedParallelism = parallelism;

        // Clean up remaining tasks
        for (int i = 1; i < proceedFlags.Length; i++)
        {
            proceedFlags[i] = true;
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, observedParallelism);
    }
}
