// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Visualization;

public static class VizRenderer
{
    public static Image<Rgb24> Render(Image<Rgb24> sourceImage, OcrResult ocrResult, VizData? vizData)
    {
        var result = sourceImage.Clone();

        result.Mutate(ctx =>
        {
            // Draw probability map as semi-transparent overlay (if available)
            if (vizData?.ProbabilityMap != null)
            {
                // Convert grayscale probability map to RGBA with transparency
                using var overlayImage = new Image<Rgba32>(vizData.ProbabilityMap.Width, vizData.ProbabilityMap.Height);

                for (int y = 0; y < vizData.ProbabilityMap.Height; y++)
                {
                    for (int x = 0; x < vizData.ProbabilityMap.Width; x++)
                    {
                        var intensity = vizData.ProbabilityMap[x, y].PackedValue;
                        // Create semi-transparent yellow overlay (higher probability = more opaque)
                        var alpha = (byte)(intensity / 2); // Max 50% opacity
                        // Yellow = full red + full green, no blue
                        overlayImage[x, y] = new Rgba32(255, 255, 0, alpha);
                    }
                }

                // Draw the probability map overlay
                ctx.DrawImage(overlayImage, 1.0f);
            }

            // Draw word axis-aligned rectangles
            foreach (var word in ocrResult.Words)
            {
                // Convert normalized coordinates back to pixel coordinates
                var rect = new RectangleF(
                    (float)(word.BoundingBox.AARectangle.X * sourceImage.Width),
                    (float)(word.BoundingBox.AARectangle.Y * sourceImage.Height),
                    (float)(word.BoundingBox.AARectangle.Width * sourceImage.Width),
                    (float)(word.BoundingBox.AARectangle.Height * sourceImage.Height)
                );

                // Draw bounding box
                ctx.Draw(Pens.Solid(Color.Red, 2), rect);
            }
        });

        return result;
    }
}
