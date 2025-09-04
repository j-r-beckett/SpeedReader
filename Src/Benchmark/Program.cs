using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Core;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

var durationOption = new Option<int>(
    name: "--duration",
    description: "Duration to run the benchmark in seconds",
    getDefaultValue: () => 60
);

var rootCommand = new RootCommand("OCR Pipeline Benchmark")
{
    durationOption
};

rootCommand.SetHandler(async (int duration) =>
{
    await RunBenchmark(duration);
}, durationOption);

return await rootCommand.InvokeAsync(args);

static async Task RunBenchmark(int durationSeconds)
{
    Console.WriteLine($"Starting OCR pipeline benchmark for {durationSeconds} seconds...");

    using var modelProvider = new ModelProvider();
    var dbnetSession = modelProvider.GetSession(Model.DbNet18);
    var svtrSession = modelProvider.GetSession(Model.SVTRv2);
    using var meter = new Meter("OcrBenchmark");

    var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration()
    {
        CacheFirstInference = false
    }, meter);
    await using var bridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);

    var font = Fonts.GetFont(fontSize: 24f);
    var random = new Random();
    var wordBank = new[]
    {
        "hello", "world", "quick", "brown", "fox", "jumps", "over", "lazy", "dog",
        "computer", "science", "machine", "learning", "artificial", "intelligence",
        "software", "development", "programming", "algorithm", "benchmark", "performance",
        "optimization", "efficiency", "throughput", "processing", "pipeline"
    };

    var completedCount = 0;
    var throughputHistory = new List<double>();
    var stopwatch = Stopwatch.StartNew();
    var lastSecondMark = 0;
    var lastSecondCount = 0;

    var cts = new CancellationTokenSource();

    // Producer task - generates inputs as fast as possible
    var producerTask = Task.Run(async () =>
    {
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var image = GenerateTestImage(font, random, wordBank);
                var vizBuilder = VizBuilder.Create(VizMode.None, image);

                var processTask = await bridge.ProcessAsync((image, vizBuilder), cts.Token, cts.Token);

                // Fire and forget - let backpressure handle flow control
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await processTask;
                        Interlocked.Increment(ref completedCount);
                        image.Dispose();
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing image: {ex.Message}");
                    }
                }, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
    }, cts.Token);

    // Monitor throughput every second
    var monitorTask = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested && stopwatch.Elapsed.TotalSeconds < durationSeconds)
        {
            await Task.Delay(1000, cts.Token);

            var currentSecond = (int)stopwatch.Elapsed.TotalSeconds;
            var currentCount = completedCount;

            if (currentSecond > lastSecondMark)
            {
                var throughputThisSecond = currentCount - lastSecondCount;
                throughputHistory.Add(throughputThisSecond);

                Console.WriteLine($"[{currentSecond:D3}s] Throughput: {throughputThisSecond:D3} images/sec | Total: {currentCount:D6} images");

                lastSecondMark = currentSecond;
                lastSecondCount = currentCount;
            }
        }
    }, cts.Token);

    // Wait for the specified duration
    await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

    // Cancel all operations
    cts.Cancel();

    // Wait a bit for remaining operations to complete
    try
    {
        await Task.WhenAll(producerTask, monitorTask).WaitAsync(TimeSpan.FromSeconds(5));
    }
    catch (TimeoutException)
    {
        Console.WriteLine("Timeout waiting for tasks to complete");
    }

    stopwatch.Stop();

    // Final results
    Console.WriteLine("\n=== Benchmark Results ===");
    Console.WriteLine($"Total Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    Console.WriteLine($"Total Images Processed: {completedCount}");
    Console.WriteLine($"Average Throughput: {completedCount / stopwatch.Elapsed.TotalSeconds:F2} images/sec");

    if (throughputHistory.Any())
    {
        Console.WriteLine($"Peak Throughput: {throughputHistory.Max()} images/sec");
        Console.WriteLine($"Min Throughput: {throughputHistory.Min()} images/sec");
        Console.WriteLine($"Median Throughput: {throughputHistory.OrderBy(x => x).Skip(throughputHistory.Count / 2).First()} images/sec");
        Console.WriteLine("\nThroughput History (images/sec):");
        for (int i = 0; i < throughputHistory.Count; i++)
        {
            Console.WriteLine($"  Second {i + 1:D3}: {throughputHistory[i]} images/sec");
        }
    }
}

static Image<Rgb24> GenerateTestImage(Font font, Random random, string[] wordBank)
{
    var image = new Image<Rgb24>(1080, 920, Color.White);
    var wordCount = random.Next(10, 21); // 10-20 words

    for (int i = 0; i < wordCount; i++)
    {
        var word = wordBank[random.Next(wordBank.Length)];
        var x = random.Next(50, image.Width - 200);  // Leave margin for text
        var y = random.Next(50, image.Height - 50);

        image.Mutate(ctx => ctx.DrawText(word, font, Color.Black, new PointF(x, y)));
    }

    return image;
}
