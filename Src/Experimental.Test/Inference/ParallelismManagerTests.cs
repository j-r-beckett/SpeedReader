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
        var tcs = new TaskCompletionSource();
        var parallelism = 0;

        var func = async () =>
        {
            Interlocked.Increment(ref parallelism);
            await tcs.Task;
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int>(func, maxParallelism);

        // Act
        var tasks = Enumerable.Range(0, 4).Select(_ => manager.Call()).ToList();
        await Task.Delay(50);
        var observedParallelism = parallelism;
        tcs.SetResult();
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(maxParallelism, observedParallelism);
    }

    [Fact(Timeout = 500)]
    public async Task Increment_IncreasesParallelism()
    {
        // Arrange
        const int initialParallelism = 1;
        var tcs = new TaskCompletionSource();
        var parallelism = 0;

        var func = async () =>
        {
            Interlocked.Increment(ref parallelism);
            await tcs.Task;
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int>(func, initialParallelism);

        // Act
        var tasks = Enumerable.Range(0, 3).Select(_ => manager.Call()).ToList();
        await Task.Delay(50);
        Assert.Equal(1, parallelism);

        manager.IncrementParallelism();
        await Task.Delay(50);
        var observedParallelism = parallelism;

        tcs.SetResult();
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(2, observedParallelism);
    }

    [Fact(Timeout = 500)]
    public async Task Decrement_DecreasesParallelism()
    {
        // Arrange
        const int initialParallelism = 2;
        var taskCompletionSources = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource()).ToList();
        var parallelism = 0;

        var func = async () =>
        {
            var currentIndex = Interlocked.Increment(ref parallelism) - 1;
            await taskCompletionSources[currentIndex].Task;
            Interlocked.Decrement(ref parallelism);
            return 0;
        };

        var manager = new ParallelismManager<int>(func, initialParallelism);

        // Act
        var tasks = Enumerable.Range(0, 4).Select(_ => manager.Call()).ToList();
        await Task.Delay(50);
        Assert.Equal(2, parallelism);

        // Start decrement in background
        var decrementTask = Task.Run(() => manager.DecrementParallelism());
        await Task.Delay(50);

        // Release the first task while decrement is ongoing
        taskCompletionSources[0].SetResult();
        await decrementTask;
        await Task.Delay(50);
        var observedParallelism = parallelism;

        // Clean up remaining tasks
        foreach (var tcs in taskCompletionSources.Skip(1))
        {
            tcs.SetResult();
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, observedParallelism);
    }
}
