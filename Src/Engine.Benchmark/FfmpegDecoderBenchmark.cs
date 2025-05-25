using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Engine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine.Benchmark;

public class FfmpegDecoderBenchmark
{
    private const int Width = 1440;
    private const int Height = 1080;
    private const int FrameRate = 30;

    private readonly (int frames, string name)[] _testCases = {
        (25, "Small"),
        (100, "Medium"), 
        (500, "Large")
    };

    public async Task RunAsync()
    {
        Console.WriteLine("FFmpeg Decoder Benchmark");
        Console.WriteLine("========================");
        Console.WriteLine($"Video: {Width}x{Height}, {FrameRate}fps, All Black Frames");
        Console.WriteLine();

        // Warm up run
        Console.WriteLine("Warming up...");
        await RunWarmup();
        Console.WriteLine();

        // Table header
        Console.WriteLine("Test Case | Frames | Create (s) | Decode (ms) | FPS   ");
        Console.WriteLine("----------|--------|------------|-------------|-------");

        // Run benchmarks
        foreach (var (frames, name) in _testCases)
        {
            await RunBenchmark(name, frames);
        }
    }

    private async Task RunWarmup()
    {
        var videoStream = await CreateTestVideo(50);
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, default);

        // Extract a few frames
        var frameCount = 0;
        while (await sourceBlock.OutputAvailableAsync() && frameCount < 10)
        {
            var frame = await sourceBlock.ReceiveAsync();
            frame.Dispose();
            frameCount++;
        }

        videoStream.Dispose();
    }

    private async Task RunBenchmark(string testName, int frameCount)
    {
        // Time video creation
        var createStopwatch = Stopwatch.StartNew();
        var videoStream = await CreateTestVideo(frameCount);
        createStopwatch.Stop();
        var createTime = createStopwatch.Elapsed.TotalSeconds;

        // Time decode process
        videoStream.Position = 0;
        var decodeStopwatch = Stopwatch.StartNew();
        
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, default);

        var extractedFrames = 0;
        while (await sourceBlock.OutputAvailableAsync())
        {
            var frame = await sourceBlock.ReceiveAsync();
            frame.Dispose();
            extractedFrames++;
        }

        decodeStopwatch.Stop();
        var decodeTimeMs = decodeStopwatch.Elapsed.TotalMilliseconds;
        var fps = extractedFrames / decodeStopwatch.Elapsed.TotalSeconds;

        videoStream.Dispose();

        // Output results
        Console.WriteLine($"{testName,-9} | {frameCount,6} | {createTime,10:F2} | {decodeTimeMs,11:F0} | {fps,5:F1}");
    }

    private async Task<Stream> CreateTestVideo(int frameCount)
    {
        // TODO: Optimize performance - 500 frames takes 12.1s generation + 27.2s compression
        var frames = GenerateBlackFrames(frameCount).ToList();
        var result = await FrameWriter.ToCompressedVideo(Width, Height, FrameRate, 
            frames.ToAsyncEnumerable(), default);
        
        // Dispose frames to free memory
        foreach (var frame in frames)
            frame.Dispose();
            
        return result;
    }

    private IEnumerable<Image<Rgb24>> GenerateBlackFrames(int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            yield return new Image<Rgb24>(Width, Height); // Default constructor creates black image
        }
    }
}