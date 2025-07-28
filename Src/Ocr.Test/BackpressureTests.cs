using System.Threading.Tasks.Dataflow;

namespace Ocr.Test;

public class BackpressureTests
{


    private async Task TestBackpressure<TIn, TOut>(IPropagatorBlock<TIn, TOut> sut, ISourceBlock<TIn> inputProducer,
        TimeSpan initialDelay)
    {
        int inputCount = 0;

        var inputCounter = new TransformBlock<TIn, TIn>(input =>
        {
            inputCount++;
            return input;
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1});

        inputProducer.LinkTo(inputCounter);

        inputCounter.LinkTo(sut);

        var outputConsumer = new BufferBlock<TOut>(new DataflowBlockOptions { BoundedCapacity = 1 });

        sut.LinkTo(outputConsumer);

        await Task.Delay(initialDelay);

        Assert.True(inputCount > 1, $"{nameof(sut)} did not consume any input");

        int count = inputCount;

        await Task.Delay(initialDelay / 4);

        Assert.True(count == inputCount, $"{nameof(sut)} continued to consume input after backpressure should have been engaged");

        var outputBucket = new BufferBlock<TOut>(new DataflowBlockOptions { BoundedCapacity = 100 });

        outputConsumer.LinkTo(outputBucket);

        await Task.Delay(initialDelay / 4);

        Assert.True(count < inputCount, $"{nameof(sut)} did not consume any input after backpressure was released");
    }
}
