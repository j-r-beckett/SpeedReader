using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Models;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Test;

public class DataflowTests
{
    [Fact]
    public async Task OcrBlock_Complete_AwaitsCompletionWithTimeout()
    {
        // Arrange
        using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);
        using var meter = new Meter("DataflowTests");

        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);

        // Act
        ocrBlock.Complete();

        // Assert
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await ocrBlock.Completion.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("OcrBlock completion timed out after 1 second");
        }

        Assert.True(ocrBlock.Completion.IsCompleted);
    }

    [Fact]
    public async Task OcrBlock_BackpressureStopsInputAcceptance()
    {
        // Arrange
        using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);
        using var meter = new Meter("DataflowTests");

        var config = new OcrConfiguration { CacheFirstInference = true };
        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, config, meter);

        // Warm up the pipeline by sending a single input through and waiting for completion
        var warmupImage = CreateTestImage("WARMUP");
        var warmupVizBuilder = VizBuilder.Create(VizMode.None, warmupImage);
        var warmupInput = (warmupImage, warmupVizBuilder);

        var warmupBridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);
        await warmupBridge.ProcessAsync(warmupInput, default, default);

        int inputsSent = 0;
        var backpressureDetected = false;
        Task<bool>? blockedSendAsyncTask = null;

        // Phase 1: Send inputs until SendAsync blocks (no output consumption)
        const int maxInputs = 1000;
        for (int i = 0; i < maxInputs; i++)
        {
            // Clone the test image for each pipeline input since pipeline takes ownership
            var testImage = CreateTestImage("TEST");
            var vizBuilder = VizBuilder.Create(VizMode.None, testImage);
            var testInput = (testImage, vizBuilder);

            // Start SendAsync but don't await it
            var sendAsyncTask = ocrBlock.SendAsync(testInput);
            var delayTask = Task.Delay(500); // 300ms timeout like video tests

            var completedTask = await Task.WhenAny(sendAsyncTask, delayTask);

            if (completedTask == delayTask)
            {
                // SendAsync didn't complete in 300ms = backpressure detected!
                backpressureDetected = true;
                blockedSendAsyncTask = sendAsyncTask;

                // Verify sustained backpressure with second SendAsync
                var secondTestImage = CreateTestImage("TEST");
                var secondVizBuilder = VizBuilder.Create(VizMode.None, secondTestImage);
                var secondTestInput = (secondTestImage, secondVizBuilder);
                var secondSendAsyncTask = ocrBlock.SendAsync(secondTestInput);
                var secondDelayTask = Task.Delay(200);

                var secondCompletedTask = await Task.WhenAny(secondSendAsyncTask, secondDelayTask);

                if (secondCompletedTask == secondDelayTask)
                {
                    // Replace first with second blocked task for later completion
                    blockedSendAsyncTask = secondSendAsyncTask;
                }
                else
                {
                    await secondSendAsyncTask;
                }

                break;
            }

            // SendAsync completed quickly, continue
            await sendAsyncTask;
            inputsSent++;
        }

        // Verify backpressure was detected
        Assert.True(backpressureDetected, "Expected backpressure to be detected, but SendAsync never blocked");
        Assert.True(inputsSent < maxInputs, $"Expected backpressure before {maxInputs} inputs, but sent {inputsSent}");

        // Phase 2: Start consuming outputs to release backpressure
        var outputConsumptionTask = ConsumeOcrOutputsAsync(ocrBlock);

        // Phase 3: Complete the blocked SendAsync and verify flow resumes
        if (blockedSendAsyncTask != null)
        {
            await blockedSendAsyncTask; // Should complete quickly now
            inputsSent++;
        }

        // Send a few more inputs to verify flow resumed
        for (int i = 0; i < 5; i++)
        {
            var finalTestImage = CreateTestImage("TEST");
            var finalVizBuilder = VizBuilder.Create(VizMode.None, finalTestImage);
            var finalTestInput = (finalTestImage, finalVizBuilder);
            await ocrBlock.SendAsync(finalTestInput);
            inputsSent++;
        }

        ocrBlock.Complete();
        await outputConsumptionTask;
    }

    private Image<Rgb24> CreateTestImage(string text)
    {
        var image = new Image<Rgb24>(200, 50, Color.White);

        // Get font for drawing text
        if (!SystemFonts.TryGet("Arial", out var fontFamily))
        {
            fontFamily = SystemFonts.Families.First();
        }
        var font = fontFamily.CreateFont(24);

        image.Mutate(ctx =>
        {
            ctx.DrawText(text, font, Color.Black, new PointF(10, 10));
        });

        return image;
    }

    private async Task ConsumeOcrOutputsAsync(IPropagatorBlock<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)> ocrBlock)
    {
        var outputConsumer = new ActionBlock<(Image<Rgb24>, OcrResult, VizBuilder)>(output =>
        {
            // Just consume the outputs, dispose the image
            output.Item1.Dispose();
        });

        ocrBlock.LinkTo(outputConsumer, new DataflowLinkOptions { PropagateCompletion = true });

        await outputConsumer.Completion;
    }
}
