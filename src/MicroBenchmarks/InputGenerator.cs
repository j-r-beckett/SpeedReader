// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpeedReader.Resources.Font;

namespace SpeedReader.MicroBenchmarks;

public enum Density
{
    Low,
    High
}

public static class InputGenerator
{
    private static readonly Font _font = EmbeddedFont.Default.Get(fontSize: 28f);

    public static Image<Rgb24> GenerateInput(int width, int height, Density density = Density.High)
    {
        var image = new Image<Rgb24>(width, height, Color.White);
        const string text = "lorem ipsum ";

        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        var textWidth = Math.Ceiling(textRect.Width);
        var textHeight = Math.Ceiling(textRect.Height);
        var lineSpacing = textHeight * 1.2;

        var repetitionsPerLine = (int)Math.Ceiling(width / textWidth) + 1;
        var numberOfLines = (int)Math.Ceiling(height / lineSpacing) + 1;

        for (var lineIndex = 0; lineIndex < numberOfLines; lineIndex++)
        {
            if (density == Density.Low && lineIndex % 2 == 1)
                continue;

            var reps = density == Density.Low ? 1 : repetitionsPerLine;
            var lineText = string.Concat(Enumerable.Repeat(text, reps));
            var y = (float)(lineIndex * lineSpacing);
            image.Mutate(ctx => ctx.DrawText(lineText, _font, Color.Black, new PointF(0, y)));
        }

        return image;
    }
}
