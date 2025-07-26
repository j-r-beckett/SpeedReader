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

        var task1 = bridge.ProcessAsync(42, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(100, CancellationToken.None, CancellationToken.None);

        var output1 = await (await task1);
        var output2 = await (await task2);

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

        var task1 = bridge.ProcessAsync(100, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(50, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessAsync(10, CancellationToken.None, CancellationToken.None);

        var innerTask1 = await task1;
        var innerTask2 = await task2;
        var innerTask3 = await task3;
        var results = await Task.WhenAll(innerTask1, innerTask2, innerTask3);

        Assert.Equal(["100", "50", "10"], results);
    }

    [Fact]
    public async Task ProcessAsync_HandlesMultipleConcurrentOperations()
    {
        var transform = new TransformBlock<int, string>(x => Task.FromResult(x.ToString()));

        await using var bridge = new DataflowBridge<int, string>(transform);

        // Start multiple operations concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(i => bridge.ProcessAsync(i, CancellationToken.None, CancellationToken.None))
            .ToList();

        // Wait for all to complete
        var innerTasks = new Task<string>[tasks.Count];
        for (int i = 0; i < tasks.Count; i++)
        {
            innerTasks[i] = await tasks[i];
        }
        var results = await Task.WhenAll(innerTasks);

        // Verify results maintain order
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i.ToString()), results);
    }

    [Fact]
    public async Task DisposeAsync_CompletesAllPendingOperations()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        var bridge = new DataflowBridge<int, string>(transform);

        var task1 = bridge.ProcessAsync(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(2, CancellationToken.None, CancellationToken.None);

        await bridge.DisposeAsync();

        var result1 = await (await task1);
        var result2 = await (await task2);

        Assert.Equal("1", result1);
        Assert.Equal("2", result2);
    }

    [Fact]
    public async Task ProcessAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        var bridge = new DataflowBridge<int, string>(transform);

        await bridge.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await (await bridge.ProcessAsync(1, CancellationToken.None, CancellationToken.None)));
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

        var task1 = bridge.ProcessAsync(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(42, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessAsync(3, CancellationToken.None, CancellationToken.None);

        var result1 = await (await task1);
        Assert.Equal("1", result1);

        var ex2 = await Assert.ThrowsAsync<DataflowBridgeException>(async () => await (await task2));
        Assert.IsType<AggregateException>(ex2.InnerException);
        var innerEx = ex2.InnerException as AggregateException;
        Assert.Contains(innerEx!.InnerExceptions, e => e is InvalidOperationException && e.Message == "Cannot process 42");

        await Assert.ThrowsAsync<DataflowBridgeException>(async () => await (await task3));
    }

    [Fact]
    public async Task ProcessAsync_WithCancelledBridgeToken_ThrowsOperationCanceledException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new DataflowBridge<int, string>(transform);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await (await bridge.ProcessAsync(42, cts.Token, CancellationToken.None)));
    }

    [Fact]
    public async Task ProcessAsync_CancellingTokenAfterAcceptance_DoesNotCancelProcessing()
    {
        var processingStarted = new TaskCompletionSource();
        var allowProcessingToComplete = new TaskCompletionSource();

        var transform = new TransformBlock<int, string>(async x =>
        {
            processingStarted.SetResult();
            await allowProcessingToComplete.Task;
            return x.ToString();
        });

        await using var bridge = new DataflowBridge<int, string>(transform);

        using var cts = new CancellationTokenSource();
        var processTask = bridge.ProcessAsync(42, cts.Token, CancellationToken.None);

        await processingStarted.Task;
        cts.Cancel();
        allowProcessingToComplete.SetResult();

        var result = await (await processTask);
        Assert.Equal("42", result);
    }

    [Fact]
    public async Task ProcessAsync_WhenTransformerFaultsWithPendingOperations_PropagatesFaultToOrphanedCompletions()
    {
        var faultAfterFirstItem = new TaskCompletionSource();
        var itemCount = 0;

        var transform = new TransformBlock<int, string>(async x =>
        {
            if (Interlocked.Increment(ref itemCount) == 2)
            {
                await faultAfterFirstItem.Task;
                throw new InvalidOperationException("Transformer fault");
            }
            return x.ToString();
        });

        await using var bridge = new DataflowBridge<int, string>(transform);

        var task1 = bridge.ProcessAsync(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(2, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessAsync(3, CancellationToken.None, CancellationToken.None);

        var result1 = await (await task1);
        Assert.Equal("1", result1);

        faultAfterFirstItem.SetResult();

        await Assert.ThrowsAsync<DataflowBridgeException>(async () => await (await task2));
        await Assert.ThrowsAsync<DataflowBridgeException>(async () => await (await task3));
    }

    [Fact]
    public async Task ProcessAsync_WhenTransformerCancelledWithPendingOperations_PropagatesCancellationToOrphanedCompletions()
    {
        using var transformerCts = new CancellationTokenSource();
        var cancelAfterFirstItem = new TaskCompletionSource();
        var itemCount = 0;

        var transform = new TransformBlock<int, string>(async x =>
        {
            if (Interlocked.Increment(ref itemCount) == 2)
            {
                await cancelAfterFirstItem.Task;
                transformerCts.Token.ThrowIfCancellationRequested();
            }
            return x.ToString();
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = transformerCts.Token
        });

        await using var bridge = new DataflowBridge<int, string>(transform);

        var task1 = bridge.ProcessAsync(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessAsync(2, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessAsync(3, CancellationToken.None, CancellationToken.None);

        var result1 = await (await task1);
        Assert.Equal("1", result1);

        transformerCts.Cancel();
        cancelAfterFirstItem.SetResult();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await (await task2));
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await (await task3));
    }

    [Fact]
    public async Task ProcessAsync_CancellingDuringBackpressure_CancelsBlockedSendAsync()
    {
        var releaseTransformer = new TaskCompletionSource();

        var transform = new TransformBlock<int, string>(async x =>
        {
            await releaseTransformer.Task;
            return x.ToString();
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1
        });

        await using var bridge = new DataflowBridge<int, string>(transform);

        var processorCount = Environment.ProcessorCount;
        var successfulTasks = new List<Task<Task<string>>>();

        // Fill the pipeline: ProcessorCount + 1 items (1 in transformer, ProcessorCount in origin)
        for (int i = 0; i < processorCount + 1; i++)
        {
            successfulTasks.Add(bridge.ProcessAsync(i, CancellationToken.None, CancellationToken.None));
        }

        await Task.Delay(100); // Let the pipeline fill up

        using var cts = new CancellationTokenSource();
        var cancelledTask = bridge.ProcessAsync(998, cts.Token, CancellationToken.None);
        var blockedTask = bridge.ProcessAsync(999, CancellationToken.None, CancellationToken.None);

        await Task.Delay(100);
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await (await cancelledTask));

        releaseTransformer.SetResult();

        // Verify all ProcessorCount + 1 items complete successfully
        var innerSuccessfulTasks = new Task<string>[successfulTasks.Count];
        for (int i = 0; i < successfulTasks.Count; i++)
        {
            innerSuccessfulTasks[i] = await successfulTasks[i];
        }
        var results = await Task.WhenAll(innerSuccessfulTasks);
        Assert.Equal(processorCount + 1, results.Length);
        for (int i = 0; i < processorCount + 1; i++)
        {
            Assert.Equal(i.ToString(), results[i]);
        }

        // The non-cancelled blocked task should complete successfully once pipeline is unblocked
        var blockedResult = await (await blockedTask);
        Assert.Equal("999", blockedResult);
    }

    [Fact]
    public async Task ProcessAsync_WithCancelledTransformerToken_ThrowsOperationCanceledException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new DataflowBridge<int, string>(transform);

        using var transformerCts = new CancellationTokenSource();
        transformerCts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await (await bridge.ProcessAsync(42, CancellationToken.None, transformerCts.Token)));
    }
}
