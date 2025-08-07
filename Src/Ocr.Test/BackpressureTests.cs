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
            cacheFirstInference: false
        );
        _blockUnderTest = inferenceBlock.Target;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<float[]>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await inputSource.SendAsync(new float[3 * 64 * 64]);
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            inferenceBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );

        inputSource.Complete();
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

        // Create input source
        var inputSource = new BufferBlock<int>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100000; i++)
            {
                await inputSource.SendAsync(i);
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            encapsulated,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(100)
        );

        inputSource.Complete();
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

        // Create input source
        var inputSource = new BufferBlock<int>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100000; i++)
            {
                await inputSource.SendAsync(i);
            }
            inputSource.Complete();
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            encapsulated,
            inputSource,
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
            CacheFirstInference = false
        };
        var modelRunnerBlock = new DBNetModelRunnerBlock(session, config, _meter);
        _blockUnderTest = modelRunnerBlock.Target;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<(float[], Image<Rgb24>, VizBuilder)>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of processed data
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var image = new Image<Rgb24>(640, 640, Color.White);
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                var floatData = new float[3 * 640 * 640];  // CHW format
                await inputSource.SendAsync((floatData, image, vizBuilder));
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            modelRunnerBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );

        inputSource.Complete();
    }


    [Fact]
    public async Task SVTRBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration
        {
            Svtr = new SvtrConfiguration(),
            CacheFirstInference = false
        };
        var svtrBlock = new SVTRBlock(session, config, _meter);
        _blockUnderTest = svtrBlock.Target;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder)>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of text boundaries with images
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 10000; i++)
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
                await inputSource.SendAsync((boundaries, image, vizBuilder));
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            svtrBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );

        inputSource.Complete();
    }

    [Fact]
    public async Task SVTRModelRunnerBlock_Backpressure()
    {
        // Arrange
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration
        {
            Svtr = new SvtrConfiguration(),
            CacheFirstInference = false
        };
        var svtrModelRunnerBlock = new SVTRModelRunnerBlock(session, config, _meter);
        _blockUnderTest = svtrModelRunnerBlock.Target;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder)>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of preprocessed SVTR data
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
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
                await inputSource.SendAsync((floatData, boundary, image, vizBuilder));
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            svtrModelRunnerBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );

        inputSource.Complete();
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
            CacheFirstInference = false
        };
        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, config, _meter);
        _blockUnderTest = ocrBlock;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<(Image<Rgb24>, VizBuilder)>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of images
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var image = new Image<Rgb24>(640, 640, Color.White);
                image.Mutate(ctx => ctx.DrawText("test", Fonts.GetFont(fontSize: 24f), Color.Black, new PointF(20, 20)));
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                await inputSource.SendAsync((image, vizBuilder));
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            ocrBlock,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(5000)
        );

        inputSource.Complete();
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
            CacheFirstInference = false
        };
        var dbNetBlock = new DBNetBlock(session, config, _meter);
        _blockUnderTest = dbNetBlock.Target;

        // Create input source that continuously sends data
        var inputSource = new BufferBlock<(Image<Rgb24>, VizBuilder)>(new DataflowBlockOptions
        {
            BoundedCapacity = DataflowBlockOptions.Unbounded
        });

        // Feed continuous stream of 640x640 white images with void visualization
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var image = new Image<Rgb24>(640, 640, Color.Black);
                image.Mutate(ctx => ctx.DrawText("hello", Fonts.GetFont(fontSize: 24f), Color.Black, new PointF(20, 20)));
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                await inputSource.SendAsync((image, vizBuilder));
            }
        });

        // Act & Assert
        var tester = new Backpressure();
        await tester.TestBackpressure(
            dbNetBlock.Target,
            inputSource,
            initialDelay: TimeSpan.FromMilliseconds(1000)
        );

        inputSource.Complete();
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
