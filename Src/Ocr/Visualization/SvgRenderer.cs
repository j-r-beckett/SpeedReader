// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Fluid;
using Ocr.Algorithms;
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
        public double X
        {
            get; set;
        }
        public double Y
        {
            get; set;
        }
        public double Width
        {
            get; set;
        }
        public double Height
        {
            get; set;
        }
    }

    public class OrientedBoundingBox
    {
        public string Points { get; set; } = string.Empty;
    }

    public class Polygon
    {
        public string Points { get; set; } = string.Empty;
    }

    public class TextItem
    {
        public string Text { get; set; } = string.Empty;
        public double CenterX
        {
            get; set;
        }
        public double CenterY
        {
            get; set;
        }
        public double FontSize
        {
            get; set;
        }
        public double RotationAngle
        {
            get; set;
        }
        public double Confidence
        {
            get; set;
        }
    }

    public class LegendItem
    {
        public string Color { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ElementClass { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool DefaultVisible { get; set; } = true;
    }

    public class TemplateData
    {
        public int Width
        {
            get; set;
        }
        public int Height
        {
            get; set;
        }
        public string BaseImageDataUri { get; set; } = string.Empty;
        public string? ProbabilityMapDataUri
        {
            get; set;
        }
        public BoundingBox[] BoundingBoxes { get; set; } = [];
        public OrientedBoundingBox[] OrientedBoundingBoxes { get; set; } = [];
        public Polygon[] Polygons { get; set; } = [];
        public TextItem[] TextItems { get; set; } = [];
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

        // Prepare oriented bounding boxes
        var orientedBoundingBoxes = ocrResult.Words.Select(word =>
        {
            var obb = word.BoundingBox.ORectangle;
            var points = string.Join(" ", obb.Select(v => $"{v.X * sourceImage.Width},{v.Y * sourceImage.Height}"));
            return new OrientedBoundingBox { Points = points };
        }).ToArray();

        // Prepare polygons
        var polygons = ocrResult.Words.Select(word =>
        {
            var polygon = word.BoundingBox.Polygon;
            var points = string.Join(" ", polygon.Select(p => $"{p.X * sourceImage.Width},{p.Y * sourceImage.Height}"));
            return new Polygon { Points = points };
        }).ToArray();

        // Prepare text items with orientation
        var textItems = ocrResult.Words.Select(word =>
        {
            // Convert ORectangle to the format expected by DetectOrientationAndOrderCorners
            var orientedRectangle = word.BoundingBox.ORectangle
                .Select(p => (p.X, p.Y))
                .ToList();

            // Get properly ordered corners
            var corners = ImageCropping.DetectOrientationAndOrderCorners(orientedRectangle);

            // Calculate center
            var centerX = (corners.TopLeft.X + corners.TopRight.X + corners.BottomRight.X + corners.BottomLeft.X) / 4.0 * sourceImage.Width;
            var centerY = (corners.TopLeft.Y + corners.TopRight.Y + corners.BottomRight.Y + corners.BottomLeft.Y) / 4.0 * sourceImage.Height;

            // Calculate rotation angle (text direction from TopLeft to TopRight)
            var textVector = (X: corners.TopRight.X - corners.TopLeft.X, Y: corners.TopRight.Y - corners.TopLeft.Y);
            var rotationAngle = Math.Atan2(textVector.Y, textVector.X) * 180.0 / Math.PI;

            // Calculate font size based on text height (70% of the distance from TopLeft to BottomLeft)
            var textHeight = Math.Sqrt(
                Math.Pow((corners.BottomLeft.X - corners.TopLeft.X) * sourceImage.Width, 2) +
                Math.Pow((corners.BottomLeft.Y - corners.TopLeft.Y) * sourceImage.Height, 2));
            var fontSize = textHeight * 0.95;

            return new TextItem
            {
                Text = word.Text,
                CenterX = centerX,
                CenterY = centerY,
                FontSize = fontSize,
                RotationAngle = rotationAngle,
                Confidence = word.Confidence
            };
        }).ToArray();

        // Create legend items
        var legendItems = new List<LegendItem>
        {
            new()
            {
                Color = "red",
                Description = "Axis-aligned bounding boxes",
                ElementClass = "bounding-boxes",
                IsVisible = true,
                DefaultVisible = false
            },
            new()
            {
                Color = "blue",
                Description = "Oriented bounding boxes",
                ElementClass = "oriented-bounding-boxes",
                IsVisible = true,
                DefaultVisible = true
            },
            new()
            {
                Color = "green",
                Description = "Polygons",
                ElementClass = "polygons",
                IsVisible = true,
                DefaultVisible = false
            },
            new()
            {
                Color = "white",
                Description = "Recognized Text",
                ElementClass = "text-overlay",
                IsVisible = true,
                DefaultVisible = false
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
                IsVisible = true,
                DefaultVisible = false
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
            OrientedBoundingBoxes = orientedBoundingBoxes,
            Polygons = polygons,
            TextItems = textItems,
            LegendItems = legendItems.ToArray()
        };

        // Create template options and register our types
        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<TemplateData>();
        options.MemberAccessStrategy.Register<BoundingBox>();
        options.MemberAccessStrategy.Register<OrientedBoundingBox>();
        options.MemberAccessStrategy.Register<Polygon>();
        options.MemberAccessStrategy.Register<TextItem>();
        options.MemberAccessStrategy.Register<LegendItem>();

        // Create template context
        var context = new TemplateContext(templateData, options);

        // Render SVG
        return new Svg(template.Render(context));
    }

    private static IFluidTemplate LoadTemplate()
    {
        var templateContent = Resource.GetString("templates.svg-visualization.liquid");
        return !_parser.TryParse(templateContent, out var template, out var error)
            ? throw new InvalidOperationException($"Failed to parse SVG template: {error}")
            : template;
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
