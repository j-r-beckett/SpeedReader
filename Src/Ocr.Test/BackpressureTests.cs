using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Core;
using Ocr.Blocks;
using Ocr.Blocks.DBNet;
using Ocr.Blocks.SVTR;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;

namespace Ocr.Test;

public class BackpressureTests : IAsyncDisposable
{
    private readonly Meter _meter;
    private readonly ModelProvider _modelProvider;
    private IDataflowBlock? _blockUnderTest;

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
            cacheFirstInference: true
        );
        _blockUnderTest = inferenceBlock.Target;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            inferenceBlock.Target,
            () => new float[3 * 64 * 64],
            initialDelay: TimeSpan.FromMilliseconds(500)
        );
    }

    [Fact]
    public async Task SplitBlock_Backpressure()
    {
        // Arrange
        var splitBlock = new SplitBlock<int, int, int>(x => (x * 2, x * 3));

        // Create a propagator by encapsulating the split block's outputs back together
        var joinBlock = new JoinBlock<int, int>(new GroupingDataflowBlockOptions { Greedy = false, BoundedCapacity = 1 });
        splitBlock.LeftSource.LinkTo(joinBlock.Target1);
        splitBlock.RightSource.LinkTo(joinBlock.Target2);

        var encapsulated = DataflowBlock.Encapsulate(splitBlock.Target, joinBlock);
        _blockUnderTest = encapsulated;

        // Act & Assert
        var counter = 0;
        var tester = new Backpressure();
        await tester.TestBackpressure(
            encapsulated,
            () => counter++,
            initialDelay: TimeSpan.FromMilliseconds(100)
        );
    }


    [Fact]
    public async Task MergeBlock_Backpressure()
    {
        // Arrange
        var mergeBlock = new MergeBlock<int, int, (int, int)>((left, right) => (left, right));

        // Create a propagator from the merge block
        var splitBlock = new SplitBlock<int, int, int>(x => (x, x + 1));
        splitBlock.LeftSource.LinkTo(mergeBlock.LeftTarget);
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget);

        var encapsulated = DataflowBlock.Encapsulate(splitBlock.Target, mergeBlock.Source);
        _blockUnderTest = encapsulated;

        // Act & Assert
        var counter = 0;
        var tester = new Backpressure();
        await tester.TestBackpressure(
            encapsulated,
            () => counter++,
            initialDelay: TimeSpan.FromMilliseconds(100)
        );
    }

    [Fact]
    public async Task DBNetModelRunnerBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.DbNet18);
        var config = new OcrConfiguration
        {
            DbNet = new DbNetConfiguration
            {
                Width = 640,
                Height = 640
            },
            CacheFirstInference = true
        };
        var modelRunnerBlock = new DBNetModelRunnerBlock(session, config, _meter);
        _blockUnderTest = modelRunnerBlock.Target;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            modelRunnerBlock.Target,
            () =>
            {
                var image = new Image<Rgb24>(640, 640, Color.White);
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                var floatData = new float[3 * 640 * 640];  // CHW format
                return (floatData, image, vizBuilder);
            },
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );
    }


    [Fact]
    public async Task SVTRBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration
        {
            Svtr = new SvtrConfiguration(),
            CacheFirstInference = true
        };
        var svtrBlock = new SVTRBlock(session, config, _meter);
        _blockUnderTest = svtrBlock.Target;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            svtrBlock.Target,
            () =>
            {
                var image = new Image<Rgb24>(640, 480, Color.White);
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                var boundaries = new List<TextBoundary>
                {
                    TextBoundary.Create(new List<(int, int)>
                    {
                        (10, 10),
                        (100, 10),
                        (100, 50),
                        (10, 50)
                    }),
                    TextBoundary.Create(new List<(int, int)>
                    {
                        (110, 10),
                        (200, 10),
                        (200, 50),
                        (110, 50)
                    })
                };
                return (boundaries, image, vizBuilder);
            },
            initialDelay: TimeSpan.FromMilliseconds(500)
        );
    }

    [Fact]
    public async Task SVTRModelRunnerBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration
        {
            Svtr = new SvtrConfiguration(),
            CacheFirstInference = true
        };
        var svtrModelRunnerBlock = new SVTRModelRunnerBlock(session, config, _meter);
        _blockUnderTest = svtrModelRunnerBlock.Target;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            svtrModelRunnerBlock.Target,
            () =>
            {
                var image = new Image<Rgb24>(config.Svtr.Width, config.Svtr.Height, Color.White);
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                var floatData = new float[3 * config.Svtr.Height * config.Svtr.Width];  // CHW format for SVTR input
                var boundary = TextBoundary.Create(new List<(int, int)>
                {
                    (10, 10),
                    (90, 10),
                    (90, 40),
                    (10, 40)
                });
                return (floatData, boundary, image, vizBuilder);
            },
            initialDelay: TimeSpan.FromMilliseconds(500)
        );
    }

    [Fact]
    public async Task OcrBlock_Backpressure()
    {
        // Arrange
        var dbnetSession = _modelProvider.GetSession(Model.DbNet18);
        var svtrSession = _modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration
        {
            DbNet = new DbNetConfiguration(),
            Svtr = new SvtrConfiguration(),
            CacheFirstInference = true
        };
        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, config, _meter);
        _blockUnderTest = ocrBlock;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            ocrBlock,
            () =>
            {
                var image = new Image<Rgb24>(640, 640, Color.White);
                image.Mutate(ctx => ctx.DrawText("test", Fonts.GetFont(fontSize: 24f), Color.Black, new PointF(20, 20)));
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                return (image, vizBuilder);
            },
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );
    }

    [Fact]
    public async Task DBNetBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.DbNet18);
        var config = new OcrConfiguration
        {
            DbNet = new DbNetConfiguration
            {
                Width = 640,
                Height = 640
            },
            CacheFirstInference = true
        };
        var dbNetBlock = new DBNetBlock(session, config, _meter);
        _blockUnderTest = dbNetBlock.Target;

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            dbNetBlock.Target,
            () =>
            {
                var image = new Image<Rgb24>(640, 640, Color.Black);
                image.Mutate(ctx => ctx.DrawText("hello", Fonts.GetFont(fontSize: 24f), Color.Black, new PointF(20, 20)));
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                return (image, vizBuilder);
            },
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_blockUnderTest != null)
        {
            _blockUnderTest.Complete();
            await _blockUnderTest.Completion;
        }

        _meter?.Dispose();
        _modelProvider?.Dispose();
    }
}
