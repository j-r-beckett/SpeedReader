using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Engine;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine.Benchmark;

public record BenchmarkResult(string TestName, int FrameCount, double CreateTimeSeconds, double DecodeTimeMs, double Fps);
public record FpsTimePoint(double ElapsedSeconds, double Fps);

public class FfmpegDecoderBenchmark
{
    private const int Width = 1440;
    private const int Height = 1080;
    private const int FrameRate = 30;
    private const int FrameCount = 300;
    
    private readonly List<BenchmarkResult> _results = new();
    private readonly List<FpsTimePoint> _fpsOverTime = new();
    private readonly IUrlPublisher<FfmpegDecoderBenchmark> _urlPublisher;

    public FfmpegDecoderBenchmark(IUrlPublisher<FfmpegDecoderBenchmark> urlPublisher)
    {
        _urlPublisher = urlPublisher;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("FFmpeg Decoder Benchmark");
        Console.WriteLine("========================");
        Console.WriteLine($"Video: {Width}x{Height}, {FrameRate}fps, All Black Frames");
        Console.WriteLine();

        // Run benchmark
        await RunBenchmark();
        
        // Generate and publish chart
        Console.WriteLine();
        Console.WriteLine("Generating performance chart...");
        await PublishPerformanceChart();
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

    private async Task RunBenchmark()
    {
        // Clear previous FPS data
        _fpsOverTime.Clear();
        
        // Time video creation
        var createStopwatch = Stopwatch.StartNew();
        var videoStream = await CreateTestVideo(FrameCount);
        createStopwatch.Stop();
        var createTime = createStopwatch.Elapsed.TotalSeconds;

        // Time decode process
        videoStream.Position = 0;
        var decodeStopwatch = Stopwatch.StartNew();
        
        // Add initial data point at time 0
        _fpsOverTime.Add(new FpsTimePoint(0.0, 0.0));
        
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, default);

        var extractedFrames = 0;
        var frameTimestamps = new List<DateTime>();
        
        while (await sourceBlock.OutputAvailableAsync())
        {
            var frame = await sourceBlock.ReceiveAsync();
            var timestamp = DateTime.UtcNow;
            frameTimestamps.Add(timestamp);
            
            frame.Dispose();
            extractedFrames++;
            
            // Calculate rolling FPS immediately for every frame
            CalculateAndStoreRollingFps(frameTimestamps, decodeStopwatch.Elapsed);
        }

        decodeStopwatch.Stop();
        var decodeTimeMs = decodeStopwatch.Elapsed.TotalMilliseconds;

        // Calculate average FPS after 500ms warmup (this is our main FPS metric)
        var warmupFpsPoints = _fpsOverTime.Where(p => p.ElapsedSeconds >= 0.5).ToList();
        var fps = warmupFpsPoints.Any() ? warmupFpsPoints.Average(p => p.Fps) : extractedFrames / decodeStopwatch.Elapsed.TotalSeconds;

        videoStream.Dispose();

        // Store result for chart generation
        _results.Add(new BenchmarkResult("Benchmark", FrameCount, createTime, decodeTimeMs, fps));
        
        // Output results
        Console.WriteLine($"Frames: {FrameCount} | Create: {createTime:F2}s | Decode: {decodeTimeMs:F0}ms | FPS: {fps:F1}");
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
    
    private void CalculateAndStoreRollingFps(List<DateTime> frameTimestamps, TimeSpan elapsedTime)
    {
        // For the first frame, we can't calculate FPS yet
        if (frameTimestamps.Count < 2) return;
        
        const double windowMs = 250.0;
        var now = frameTimestamps.Last();
        var windowStart = now.AddMilliseconds(-windowMs);
        
        // Get frames within the 250ms window
        var framesInWindow = frameTimestamps.Where(t => t >= windowStart).ToList();
        
        if (framesInWindow.Count >= 2)
        {
            var windowDuration = (framesInWindow.Last() - framesInWindow.First()).TotalSeconds;
            if (windowDuration > 0)
            {
                var rollingFps = (framesInWindow.Count - 1) / windowDuration;
                var elapsedSeconds = elapsedTime.TotalSeconds;
                
                _fpsOverTime.Add(new FpsTimePoint(elapsedSeconds, rollingFps));
            }
        }
        else if (frameTimestamps.Count >= 2)
        {
            // If window is smaller than 250ms, use all available frames
            var totalDuration = (frameTimestamps.Last() - frameTimestamps.First()).TotalSeconds;
            if (totalDuration > 0)
            {
                var rollingFps = (frameTimestamps.Count - 1) / totalDuration;
                var elapsedSeconds = elapsedTime.TotalSeconds;
                
                _fpsOverTime.Add(new FpsTimePoint(elapsedSeconds, rollingFps));
            }
        }
    }

    private async Task PublishPerformanceChart()
    {
        var chartData = new ChartData(
            Labels: _fpsOverTime.Select(p => p.ElapsedSeconds.ToString("F2")).ToArray(),
            Datasets: new[]
            {
                new ChartDataset(
                    Label: "FPS Over Time (250ms rolling avg)",
                    Data: _fpsOverTime.Select(p => p.Fps).ToArray(),
                    BackgroundColor: "#36A2EB")
            });
            
        await _urlPublisher.PublishChartAsync(
            "FFmpeg Decoder FPS Over Time", 
            chartData, 
            "Real-time FPS progression chart");
    }
}