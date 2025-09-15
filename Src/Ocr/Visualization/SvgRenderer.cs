// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Fluid;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public static class SvgRenderer
{
    private static readonly FluidParser _parser = new();
    private static readonly Lazy<IFluidTemplate> _template = new(LoadTemplate);

    public class BoundingBox
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class LegendItem
    {
        public string Color { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ElementClass { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
    }

    public class TemplateData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string BaseImageDataUri { get; set; } = string.Empty;
        public string? ProbabilityMapDataUri { get; set; }
        public BoundingBox[] BoundingBoxes { get; set; } = [];
        public LegendItem[] LegendItems { get; set; } = [];
    }

    public static Svg Render(Image<Rgb24> sourceImage, OcrResult ocrResult, VizData? vizData)
    {
        var template = _template.Value;

        // Convert source image to base64 data URI
        string baseImageDataUri = ConvertImageToDataUri(sourceImage);

        // Convert probability map to base64 data URI if available
        string? probabilityMapDataUri = null;
        if (vizData?.ProbabilityMap != null)
        {
            probabilityMapDataUri = ConvertProbabilityMapToDataUri(vizData.ProbabilityMap);
        }

        // Prepare bounding boxes for template
        var boundingBoxes = ocrResult.Words.Select(word => new BoundingBox
        {
            X = word.BoundingBox.AARectangle.X * sourceImage.Width,
            Y = word.BoundingBox.AARectangle.Y * sourceImage.Height,
            Width = word.BoundingBox.AARectangle.Width * sourceImage.Width,
            Height = word.BoundingBox.AARectangle.Height * sourceImage.Height
        }).ToArray();

        // Create legend items
        var legendItems = new List<LegendItem>
        {
            new()
            {
                Color = "red",
                Description = "Axis-aligned bounding boxes",
                ElementClass = "bounding-boxes",
                IsVisible = true
            }
        };

        // Add DBNet output legend item if probability map is available
        if (probabilityMapDataUri != null)
        {
            legendItems.Add(new LegendItem
            {
                Color = "yellow",
                Description = "Raw text detection output",
                ElementClass = "dbnet-overlay",
                IsVisible = true
            });
        }

        // Create template data
        var templateData = new TemplateData
        {
            Width = sourceImage.Width,
            Height = sourceImage.Height,
            BaseImageDataUri = baseImageDataUri,
            ProbabilityMapDataUri = probabilityMapDataUri,
            BoundingBoxes = boundingBoxes,
            LegendItems = legendItems.ToArray()
        };

        // Create template options and register our types
        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<TemplateData>();
        options.MemberAccessStrategy.Register<BoundingBox>();
        options.MemberAccessStrategy.Register<LegendItem>();

        // Create template context
        var context = new TemplateContext(templateData, options);

        // Render SVG
        return new Svg(template.Render(context));
    }

    private static IFluidTemplate LoadTemplate()
    {
        var templateContent = Resource.GetString("templates.svg-visualization.liquid");
        if (!_parser.TryParse(templateContent, out var template, out var error))
        {
            throw new InvalidOperationException($"Failed to parse SVG template: {error}");
        }
        return template;
    }

    private static string ConvertProbabilityMapToDataUri(Image<L8> probabilityMap)
    {
        // Convert grayscale probability map to RGBA with transparency
        using var overlayImage = new Image<Rgba32>(probabilityMap.Width, probabilityMap.Height);

        for (int y = 0; y < probabilityMap.Height; y++)
        {
            for (int x = 0; x < probabilityMap.Width; x++)
            {
                var intensity = probabilityMap[x, y].PackedValue;
                // Create semi-transparent yellow overlay (higher probability = more opaque)
                var alpha = (byte)(intensity / 2); // Max 50% opacity
                // Yellow = full red + full green, no blue
                overlayImage[x, y] = new Rgba32(255, 255, 0, alpha);
            }
        }

        return ConvertImageToDataUri(overlayImage);
    }

    private static string ConvertImageToDataUri(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        string base64 = Convert.ToBase64String(stream.ToArray());
        return $"data:image/png;base64,{base64}";
    }
}
