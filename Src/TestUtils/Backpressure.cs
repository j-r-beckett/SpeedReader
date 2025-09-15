// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Tasks.Dataflow;
using Xunit;

namespace TestUtils;

public class Backpressure
{
    public async Task TestBackpressure<TIn, TOut>(IPropagatorBlock<TIn, TOut> sut, Func<TIn> inputCreator,
        TimeSpan initialDelay)
    {
        int inputCount = 0;

        var inputProducer = new BufferBlock<TIn>(new DataflowBlockOptions { BoundedCapacity = 1 });

        var cts = new CancellationTokenSource();
        var inputProducerTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await inputProducer.SendAsync(inputCreator(), cts.Token);
                    inputCount++;
                    cts.Token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                // do nothing
            }
        }, cts.Token);

        _ = sut.Completion.ContinueWith(async _ =>
        {
            cts.Cancel();
            try
            {
                await inputProducerTask;
            }
            catch (OperationCanceledException)
            {
                // do nothing
            }
            inputProducer.Complete();
            await inputProducer.Completion;
        });

        inputProducer.LinkTo(sut);

        var outputConsumer = new BufferBlock<TOut>(new DataflowBlockOptions { BoundedCapacity = 1 });

        sut.LinkTo(outputConsumer);

        await Task.Delay(initialDelay);

        Assert.True(inputCount > 1, $"{nameof(sut)} did not consume any input");

        int count = inputCount;

        await Task.Delay(initialDelay);

        Assert.True(count == inputCount, $"{nameof(sut)} continued to consume input after backpressure should have been engaged, {inputCount} items consumed");

        var outputBucket = new BufferBlock<TOut>(new DataflowBlockOptions { BoundedCapacity = 100 });

        outputConsumer.LinkTo(outputBucket);

        await Task.Delay(initialDelay);

        Assert.True(count < inputCount, $"{nameof(sut)} did not consume any input after backpressure was released");
    }
}
