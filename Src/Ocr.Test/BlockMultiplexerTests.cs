using System.Threading.Tasks.Dataflow;
using Ocr.Blocks;

namespace Ocr.Test;

public class BlockMultiplexerTests
{
    [Fact]
    public async Task ProcessAsync_WithSimpleTransform_ReturnsCorrectResults()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var task1 = bridge.ProcessSingle(42, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessSingle(100, CancellationToken.None, CancellationToken.None);

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
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var task1 = bridge.ProcessSingle(100, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessSingle(50, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessSingle(10, CancellationToken.None, CancellationToken.None);

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

        await using var bridge = new BlockMultiplexer<int, string>(transform);

        // Start multiple operations concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(i => bridge.ProcessSingle(i, CancellationToken.None, CancellationToken.None))
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
        var bridge = new BlockMultiplexer<int, string>(transform);

        // Submit operations before disposal
        var task1 = bridge.ProcessSingle(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessSingle(2, CancellationToken.None, CancellationToken.None);

        // Get the inner tasks before disposal
        var innerTask1 = await task1;
        var innerTask2 = await task2;

        // Now dispose
        await bridge.DisposeAsync();

        // The operations that were already accepted should complete
        var result1 = await innerTask1;
        var result2 = await innerTask2;

        Assert.Equal("1", result1);
        Assert.Equal("2", result2);
    }

    [Fact]
    public async Task ProcessAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        var bridge = new BlockMultiplexer<int, string>(transform);

        await bridge.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await (await bridge.ProcessSingle(1, CancellationToken.None, CancellationToken.None)));
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

        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var task1 = bridge.ProcessSingle(1, CancellationToken.None, CancellationToken.None);
        await Task.Delay(50);
        var task2 = bridge.ProcessSingle(42, CancellationToken.None, CancellationToken.None);
        await Task.Delay(50);
        var task3 = bridge.ProcessSingle(3, CancellationToken.None, CancellationToken.None);

        var result1 = await (await task1);
        Assert.Equal("1", result1);

        var ex2 = await Assert.ThrowsAsync<MultiplexerException>(async () => await (await task2));
        Assert.IsType<AggregateException>(ex2.InnerException);
        var innerEx = ex2.InnerException as AggregateException;
        Assert.Contains(innerEx!.InnerExceptions, e => e is InvalidOperationException && e.Message == "Cannot process 42");

        // Pipeline should be shut down after throw an exception for item 2
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await (await task3));
    }

    [Fact]
    public async Task ProcessAsync_WithCancelledBridgeToken_ThrowsOperationCanceledException()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await (await bridge.ProcessSingle(42, cts.Token, CancellationToken.None)));
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

        await using var bridge = new BlockMultiplexer<int, string>(transform);

        using var cts = new CancellationTokenSource();
        var processTask = bridge.ProcessSingle(42, cts.Token, CancellationToken.None);

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

        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var task1 = bridge.ProcessSingle(1, CancellationToken.None, CancellationToken.None);
        var task2 = bridge.ProcessSingle(2, CancellationToken.None, CancellationToken.None);
        var task3 = bridge.ProcessSingle(3, CancellationToken.None, CancellationToken.None);

        var result1 = await (await task1);
        Assert.Equal("1", result1);

        faultAfterFirstItem.SetResult();

        await Assert.ThrowsAsync<MultiplexerException>(async () => await (await task2));
        await Assert.ThrowsAsync<MultiplexerException>(async () => await (await task3));
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

        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var processorCount = Environment.ProcessorCount;
        var successfulTasks = new List<Task<Task<string>>>();

        // Fill the pipeline: ProcessorCount + 1 items (1 in transformer, ProcessorCount in origin)
        for (int i = 0; i < processorCount + 1; i++)
        {
            successfulTasks.Add(bridge.ProcessSingle(i, CancellationToken.None, CancellationToken.None));
        }

        await Task.Delay(100); // Let the pipeline fill up

        using var cts = new CancellationTokenSource();
        var cancelledTask = bridge.ProcessSingle(998, cts.Token, CancellationToken.None);
        var blockedTask = bridge.ProcessSingle(999, CancellationToken.None, CancellationToken.None);

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
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        using var transformerCts = new CancellationTokenSource();
        transformerCts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await (await bridge.ProcessSingle(42, CancellationToken.None, transformerCts.Token)));
    }

    [Fact]
    public async Task ProcessMultiple_WithSimpleTransform_ReturnsCorrectResults()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var inputs = new List<int> { 1, 2, 3, 4, 5 };
        var task = bridge.ProcessMultiple(inputs, CancellationToken.None, CancellationToken.None);
        var results = await (await task);

        Assert.Equal(["1", "2", "3", "4", "5"], results);
    }

    [Fact]
    public async Task ProcessMultiple_MaintainsOrderWithDelayedProcessing()
    {
        var transform = new TransformBlock<int, string>(async x =>
        {
            // Longer delay for smaller numbers to test order preservation
            await Task.Delay(100 - x * 10);
            return x.ToString();
        });
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var inputs = new List<int> { 1, 2, 3, 4, 5 };
        var task = bridge.ProcessMultiple(inputs, CancellationToken.None, CancellationToken.None);
        var results = await (await task);

        Assert.Equal(["1", "2", "3", "4", "5"], results);
    }

    [Fact]
    public async Task GetAccessorBlocks_BasicFunctionality_SendsInputsAndReceivesOutputs()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();
        var results = new List<string>();
        var consumer = new ActionBlock<string>(result => results.Add(result));
        source.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

        await target.SendAsync(42);
        await target.SendAsync(100);
        target.Complete();

        await consumer.Completion;

        Assert.Equal(["42", "100"], results);
    }

    [Fact]
    public async Task GetAccessorBlocks_MultipleConcurrentInputs_HandlesAllInputsCorrectly()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();
        var results = new List<string>();
        var consumer = new ActionBlock<string>(result => results.Add(result));
        source.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

        // Send multiple inputs concurrently
        var sendTasks = Enumerable.Range(1, 5)
            .Select(i => target.SendAsync(i))
            .ToArray();

        await Task.WhenAll(sendTasks);
        target.Complete();
        await consumer.Completion;

        Assert.Equal(5, results.Count);
        Assert.Contains("1", results);
        Assert.Contains("2", results);
        Assert.Contains("3", results);
        Assert.Contains("4", results);
        Assert.Contains("5", results);
    }

    [Fact]
    public async Task GetAccessorBlocks_OrderPreservation_MaintainsInputOrder()
    {
        var transform = new TransformBlock<int, string>(async x =>
        {
            // Longer delay for smaller numbers to test order preservation
            await Task.Delay(50 - x * 5);
            return x.ToString();
        });
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();
        var results = new List<string>();
        var consumer = new ActionBlock<string>(result => results.Add(result));
        source.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

        await target.SendAsync(1);
        await target.SendAsync(2);
        await target.SendAsync(3);
        target.Complete();

        await consumer.Completion;

        Assert.Equal(["1", "2", "3"], results);
    }

    [Fact]
    public async Task GetAccessorBlocks_TargetCompletion_PropagatesCompletionToSource()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();
        var consumer = new ActionBlock<string>(_ => { });
        source.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

        await target.SendAsync(42);
        target.Complete();

        await consumer.Completion;

        Assert.True(target.Completion.IsCompletedSuccessfully);
        Assert.True(source.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task GetAccessorBlocks_TargetFault_PropagatesFaultToSource()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();

        var testException = new InvalidOperationException("Test fault");
        target.Fault(testException);

        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => target.Completion);
        Assert.Equal(ex1.Message, testException.Message);
        var ex2 = await Assert.ThrowsAsync<AggregateException>(() => source.Completion);
        Assert.Equal(ex2.GetBaseException().Message, testException.Message);
    }

    [Fact]
    public async Task GetAccessorBlocks_BackpressureHandling_HandlesBoundedCapacity()
    {
        var transform = new TransformBlock<int, string>(x => x.ToString());
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();

        // Create a propagator block from the target/source pair for testing
        var propagator = DataflowBlock.Encapsulate(target, source);

        var tester = new TestUtils.Backpressure();
        await tester.TestBackpressure(
            propagator,
            () => Random.Shared.Next(),
            initialDelay: TimeSpan.FromMilliseconds(500)
        );
    }

    [Fact]
    public async Task GetAccessorBlocks_TransformerException_PropagatesExceptionThroughSource()
    {
        var transform = new TransformBlock<int, string>(x =>
        {
            if (x == 42)
                throw new InvalidOperationException("Transformer fault");
            return x.ToString();
        });
        await using var bridge = new BlockMultiplexer<int, string>(transform);

        var (target, source) = bridge.GetAccessorBlocks();
        var results = new List<string>();
        var consumer = new ActionBlock<string>(result => results.Add(result));
        source.LinkTo(consumer, new DataflowLinkOptions { PropagateCompletion = true });

        await target.SendAsync(1);
        await target.SendAsync(42); // This will cause transformer to fault
        target.Complete();

        // The first item should be processed
        await Task.Delay(100);
        Assert.Single(results);
        Assert.Equal("1", results[0]);

        // Source should eventually fault due to transformer exception
        var ex = await Assert.ThrowsAnyAsync<MultiplexerException>(() => source.Completion);
        // Assert.True(ex);
    }
}
