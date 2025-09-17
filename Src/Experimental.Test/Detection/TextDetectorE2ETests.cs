// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Experimental.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Ocr;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.Detection;

public class TextDetectorE2ETests : IDisposable
{
    private readonly ModelProvider _modelProvider;
    private readonly Font _font;
    private readonly TestLogger _logger;

    public TextDetectorE2ETests(ITestOutputHelper outputHelper)
    {
        _modelProvider = new ModelProvider();
        _font = Fonts.GetFont(fontSize: 24f);
        _logger = new TestLogger(outputHelper);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults()
    {
        var dbnetRunner = new CpuModelRunner(_modelProvider.GetSession(Model.DbNet18), 1);

        var detector = new TextDetector(dbnetRunner);

        using var image = new Image<Rgb24>(1200, 800, Color.White);
        DrawText(image, "hello", 100, 100);

        var results = await detector.Detect(image);

        var svg = new VizBuilder()
            .AddBaseImage(image)
            .AddAxisAlignedBBoxes(results.Select(r => r.AARectangle).ToList())
            .AddOrientedBBoxes(results.Select(r => r.ORectangle).ToList(), true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");
    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _font, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    public void Dispose()
    {
        _modelProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
