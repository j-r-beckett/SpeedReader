using System.Diagnostics.Metrics;
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
}