using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Core;
using Ocr.Blocks;
using Resources;
using TestUtils;

namespace Ocr.Test;

public class BackpressureTests : IDisposable
{
    private readonly Meter _meter;
    private readonly ModelProvider _modelProvider;

    public BackpressureTests()
    {
        _meter = new Meter("BackpressureTests");
        _modelProvider = new ModelProvider();
    }

    [Fact]
    public async Task InferenceBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.DbNet18);
        var inferenceBlock = new InferenceBlock(
            session,
            elementShape: [3, 64, 64],
            _meter,
            "test",
            cacheFirstInference: false
        );

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<float[]>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of 64x64 black images
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await inputSource.SendAsync(new float[3 * 64 * 64]);
            }
            inputSource.Complete();
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            inferenceBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(100)
        );

        inferenceBlock.Target.Complete();
        await inferenceBlock.Target.Completion;
    }

    public void Dispose()
    {
        _meter?.Dispose();
        _modelProvider?.Dispose();
    }
}
