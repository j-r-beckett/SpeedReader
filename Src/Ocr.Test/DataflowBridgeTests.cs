using System.Threading.Tasks.Dataflow;
using Ocr.Blocks;

namespace Ocr.Test;

public class DataflowBridgeTests
{
    [Fact]
    public async Task ProcessAsync_WithSimpleTransform_ReturnsCorrectResults()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new DataflowBridge<int, string>(transform);

        var result1 = bridge.ProcessAsync(42, out var task1);
        var result2 = bridge.ProcessAsync(100, out var task2);

        Assert.True(result1);
        Assert.True(result2);

        var output1 = await task1;
        var output2 = await task2;

        Assert.Equal("42", output1);
        Assert.Equal("100", output2);
    }

    [Fact]
    public async Task ProcessAsync_MaintainsOrderWithDelayedProcessing()
    {
        var transform = new TransformBlock<int, string>(async x =>
        {
            await Task.Delay(x);
            return x.ToString();
        });
        await using var bridge = new DataflowBridge<int, string>(transform);

        bridge.ProcessAsync(100, out var task1);
        bridge.ProcessAsync(50, out var task2);
        bridge.ProcessAsync(10, out var task3);

        var results = await Task.WhenAll(task1, task2, task3);

        Assert.Equal(["100", "50", "10"], results);
    }

    [Fact]
    public async Task ProcessAsync_WhenAtCapacity_ReturnsFalse()
    {
        var blockingSemaphore = new SemaphoreSlim(0);
        var transform = new TransformBlock<int, string>(async x =>
        {
            await blockingSemaphore.WaitAsync();
            return x.ToString();
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        await using var bridge = new DataflowBridge<int, string>(transform);

        var results = new List<bool>();
        var tasks = new List<Task<string>>();

        for (int i = 0; i < Environment.ProcessorCount + 5; i++)
        {
            var posted = bridge.ProcessAsync(i, out var task);
            results.Add(posted);
            if (posted) tasks.Add(task);
        }

        Assert.True(results.Count(r => r) > 0);
        Assert.Contains(false, results);

        blockingSemaphore.Release(results.Count(r => r));
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task DisposeAsync_CompletesAllPendingOperations()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        var bridge = new DataflowBridge<int, string>(transform);

        bridge.ProcessAsync(1, out var task1);
        bridge.ProcessAsync(2, out var task2);

        await bridge.DisposeAsync();

        var result1 = await task1;
        var result2 = await task2;

        Assert.Equal("1", result1);
        Assert.Equal("2", result2);
    }

    [Fact]
    public async Task ProcessAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        var bridge = new DataflowBridge<int, string>(transform);

        await bridge.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => bridge.ProcessAsync(1, out _));
    }

    [Fact]
    public async Task ProcessAsync_WhenBlockFaults_PropagatesException()
    {
        var transform = new TransformBlock<int, string>(x =>
        {
            if (x == 42)
                throw new InvalidOperationException("Cannot process 42");
            return x.ToString();
        });

        await using var bridge = new DataflowBridge<int, string>(transform);

        bridge.ProcessAsync(1, out var task1);
        bridge.ProcessAsync(42, out var task2);
        bridge.ProcessAsync(3, out var task3);

        var result1 = await task1;
        Assert.Equal("1", result1);

        var ex2 = await Assert.ThrowsAsync<DataflowBridgeException>(async () => await task2);
        Assert.IsType<AggregateException>(ex2.InnerException);
        var innerEx = ex2.InnerException as AggregateException;
        Assert.Contains(innerEx!.InnerExceptions, e => e is InvalidOperationException && e.Message == "Cannot process 42");

        await Assert.ThrowsAsync<DataflowBridgeException>(async () => await task3);
    }
}