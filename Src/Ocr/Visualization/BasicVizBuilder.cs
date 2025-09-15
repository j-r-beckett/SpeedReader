// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Visualization;

public class BasicVizBuilder : VizBuilder
{
    private List<Rectangle> _mergedRectangles = [];
    private List<string> _mergedTexts = [];

    public BasicVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage) { }

    public override void AddMergedResults(List<Rectangle> mergedRectangles, List<string> mergedTexts)
    {
        _mergedRectangles = mergedRectangles;
        _mergedTexts = mergedTexts;
    }

    public override Image<Rgb24> Render()
    {
        return RenderBasicViz();
    }

    protected Image<Rgb24> RenderBasicViz()
    {
        var result = _sourceImage.Clone();

        if (_mergedRectangles.Count == 0)
        {
            return result;
        }

        var font = Resources.Fonts.GetFont(fontSize: 14f, fontStyle: FontStyle.Bold);

        result.Mutate(ctx =>
        {
            for (int i = 0; i < _mergedRectangles.Count; i++)
            {
                var rectangle = _mergedRectangles[i];

                // Draw bounding box
                var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                ctx.Draw(Pens.Solid(Color.Red, 2), boundingRect);

                // Draw recognized text to the right of the bounding box
                if (i < _mergedTexts.Count)
                {
                    var textPosition = new PointF(rectangle.X + rectangle.Width + 5, rectangle.Y);
                    var text = _mergedTexts[i].Trim();

                    var whiteBrush = Brushes.Solid(Color.White);
                    var blueBrush = Brushes.Solid(Color.Blue);

                    // Draw white outline for improved legibility
                    var outlineWidth = 1;
                    for (int dx = -outlineWidth; dx <= outlineWidth; dx++)
                    {
                        for (int dy = -outlineWidth; dy <= outlineWidth; dy++)
                        {
                            if (dx != 0 || dy != 0)
                            {
                                ctx.DrawText(text, font, whiteBrush,
                                    new PointF(textPosition.X + dx, textPosition.Y + dy));
                            }
                        }
                    }

                    // Draw blue text on top
                    ctx.DrawText(text, font, blueBrush, textPosition);
                }
            }
        });

        return result;
    }
}
