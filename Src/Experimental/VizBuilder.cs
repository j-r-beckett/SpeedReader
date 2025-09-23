// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using Experimental.Geometry;
using Fluid;
using Ocr.Algorithms;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Point = Experimental.Geometry.Point;

namespace Experimental;

public class VizBuilder
{
    public class TextItem
    {
        public required string Text;
        public required double CenterX;
        public required double CenterY;
        public required double FontSize;
        public required double RotationAngle;
        public required double Confidence;
    }

    public class TemplateData
    {
        public required int Width;
        public required int Height;
        public required string BaseImageDataUri;
        public string? ProbabilityMapDataUri;
        public required List<Polygon> AxisAlignedBoundingBoxes;
        public required List<Polygon> OrientedBoundingBoxes;
        public required List<Polygon> ExpectedAxisAlignedBoundingBoxes;
        public required List<Polygon> ExpectedOrientedBoundingBoxes;
        public required List<Polygon> Polygons = [];
        public required List<TextItem> TextItems = [];
        public required SvgRenderer.LegendItem[] Legend;
    }

    public class MultipleAddException : Exception
    {
        public MultipleAddException(string message) : base(message) { }
    }

    private Image? _baseImage;

    private Image<L8>? _probabilityMap;
    private bool _displayProbabilityMapByDefault;


    private List<Polygon>? _axisAlignedBBoxes;
    private bool _displayAxisAlignedBBoxesByDefault;

    private List<Polygon>? _orientedBBoxes;
    private bool _displayOrientedBBoxesByDefault;

    private List<Polygon>? _expectedAxisAlignedBBoxes;
    private bool _displayExpectedAxisAlignedBBoxesByDefault;

    private List<Polygon>? _expectedOrientedBBoxes;
    private bool _displayExpectedOrientedBBoxesByDefault;

    private List<Polygon>? _polygonBBoxes;
    private bool _displayPolygonBBoxesByDefault;

    private ConcurrentBag<(string Text, double Confidence, List<(double X, double Y)> ORectangle)>? _textItemsData;

    private static readonly FluidParser _parser = new();
    private static readonly Lazy<IFluidTemplate> _template = new(LoadTemplate);

    public VizBuilder AddBaseImage(Image image)
    {
        if (_baseImage != null)
        {
            throw new MultipleAddException($"{nameof(AddBaseImage)} cannot be called twice");
        }

        _baseImage = image;

        return this;
    }


    // Supports being called from multiple threads
    public VizBuilder AddTextItems(List<(string Text, double Confidence, List<(double X, double Y)> ORectangle)> textItems)
    {
        // Allow adding text items multiple times

        _textItemsData ??= [];
        foreach (var item in textItems)
            _textItemsData.Add(item);

        return this;
    }

    public VizBuilder AddBoundingBoxes(List<BoundingBox> boundingBoxes) => AddAxisAlignedBBoxes(boundingBoxes.Select(bb => bb.AxisAlignedRectangle).ToList())
            .AddOrientedBBoxes(boundingBoxes.Select(bb => bb.RotatedRectangle).ToList(), true)
            .AddPolygonBBoxes(boundingBoxes.Select(bb => bb.Polygon).ToList());

    public VizBuilder AddAxisAlignedBBoxes(List<AxisAlignedRectangle> axisAlignedBBoxes, bool displayByDefault = false)
    {
        if (_axisAlignedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddAxisAlignedBBoxes)} cannot be called twice");
        }

        _axisAlignedBBoxes = axisAlignedBBoxes.Select(rect => rect.ToPolygon()).ToList();
        _displayAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddOrientedBBoxes(List<RotatedRectangle> orientedBBoxes, bool displayByDefault = false)
    {
        if (_orientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddOrientedBBoxes)} cannot be called twice");
        }

        _orientedBBoxes = orientedBBoxes.Select(rect => rect.ToPolygon()).ToList();
        _displayOrientedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedAxisAlignedBBoxes(List<AxisAlignedRectangle> expectedAxisAlignedBBoxes, bool displayByDefault = false)
    {
        if (_expectedAxisAlignedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedAxisAlignedBBoxes)} cannot be called twice");
        }

        _expectedAxisAlignedBBoxes = expectedAxisAlignedBBoxes.Select(rect => rect.ToPolygon()).ToList();
        _displayExpectedAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedOrientedBBoxes(List<RotatedRectangle> expectedOrientedBBoxes, bool displayByDefault = false)
    {
        if (_expectedOrientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedOrientedBBoxes)} cannot be called twice");
        }

        _expectedOrientedBBoxes = expectedOrientedBBoxes.Select(rect => rect.ToPolygon()).ToList();
        _displayExpectedOrientedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddPolygonBBoxes(List<Polygon> polygonBBoxes, bool displayByDefault = false)
    {
        if (_polygonBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddPolygonBBoxes)} cannot be called twice");
        }

        _polygonBBoxes = polygonBBoxes;
        _displayPolygonBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddProbabilityMap(Image<L8> probabilityMap, bool displayByDefault = false)
    {
        if (_probabilityMap != null)
        {
            throw new MultipleAddException($"{nameof(AddProbabilityMap)} cannot be called twice");
        }

        _probabilityMap = probabilityMap;
        _displayProbabilityMapByDefault = displayByDefault;

        return this;
    }

    public VizBuilder CreateAndAddProbabilityMap(Span2D<float> probabilityMapSpan, int originalWidth, int originalHeight, bool displayByDefault = false)
    {
        if (_probabilityMap != null)
        {
            throw new MultipleAddException($"Probability map already exists");
        }

        int modelHeight = probabilityMapSpan.Height;
        int modelWidth = probabilityMapSpan.Width;

        // Calculate the fitted dimensions (what the image was resized to before padding)
        double scale = Math.Min((double)modelWidth / originalWidth, (double)modelHeight / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);

        // Create a grayscale image from the probability map (only the fitted portion, not padding)
        var probImage = new Image<L8>(fittedWidth, fittedHeight);

        for (int y = 0; y < fittedHeight; y++)
        {
            for (int x = 0; x < fittedWidth; x++)
            {
                var probability = probabilityMapSpan[y, x];
                // Convert probability [0,1] to grayscale [0,255]
                probImage[x, y] = new L8((byte)(probability * 255));
            }
        }

        // Resize back to original image size
        probImage.Mutate(ctx =>
            ctx.Resize(originalWidth, originalHeight, KnownResamplers.Bicubic));

        _probabilityMap = probImage;
        _displayProbabilityMapByDefault = displayByDefault;

        return this;
    }

    public Svg RenderSvg()
    {
        ArgumentNullException.ThrowIfNull(_baseImage);  // base image is required

        var template = _template.Value;
        var baseImageDataUri = ConvertImageToDataUri(_baseImage);

        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<TemplateData>();
        options.MemberAccessStrategy.Register<Point>();
        options.MemberAccessStrategy.Register<Polygon>();
        options.MemberAccessStrategy.Register<TextItem>();

        // Currently unused
        // ----------------
        options.MemberAccessStrategy.Register<SvgRenderer.TemplateData>();
        options.MemberAccessStrategy.Register<SvgRenderer.OrientedBoundingBox>();
        options.MemberAccessStrategy.Register<SvgRenderer.Polygon>();
        options.MemberAccessStrategy.Register<SvgRenderer.TextItem>();
        options.MemberAccessStrategy.Register<SvgRenderer.LegendItem>();
        // ----------------

        List<SvgRenderer.LegendItem> legend = [];

        if (_axisAlignedBBoxes != null)
        {
            legend.Add(new()
            {
                Color = "red",
                Description = "Axis-aligned bounding boxes",
                ElementClass = "bounding-boxes",
                DefaultVisible = _displayAxisAlignedBBoxesByDefault
            });
        }

        if (_orientedBBoxes != null)
        {
            legend.Add(new()
            {
                Color = "blue",
                Description = "Oriented bounding boxes",
                ElementClass = "oriented-bounding-boxes",
                DefaultVisible = _displayOrientedBBoxesByDefault
            });
        }

        if (_expectedAxisAlignedBBoxes != null)
        {
            legend.Add(new()
            {
                Color = "black",
                Description = "Expected axis-aligned bounding boxes",
                ElementClass = "expected-bounding-boxes",
                DefaultVisible = _displayExpectedAxisAlignedBBoxesByDefault
            });
        }

        if (_expectedOrientedBBoxes != null)
        {
            legend.Add(new()
            {
                Color = "orange",
                Description = "Expected oriented bounding boxes",
                ElementClass = "expected-oriented-bounding-boxes",
                DefaultVisible = _displayExpectedOrientedBBoxesByDefault
            });
        }

        if (_polygonBBoxes != null)
        {
            legend.Add(new()
            {
                Color = "green",
                Description = "Polygon bounding boxes",
                ElementClass = "polygons",
                DefaultVisible = _displayPolygonBBoxesByDefault
            });
        }

        if (_textItemsData != null)
        {
            legend.Add(new()
            {
                Color = "white",
                Description = "Recognized Text",
                ElementClass = "text-overlay",
                DefaultVisible = false
            });
        }

        string? probabilityMapDataUri = null;
        if (_probabilityMap != null)
        {
            probabilityMapDataUri = ConvertProbabilityMapToDataUri(_probabilityMap);
            legend.Add(new()
            {
                Color = "yellow",
                Description = "Raw text detection output",
                ElementClass = "dbnet-overlay",
                DefaultVisible = _displayProbabilityMapByDefault
            });
        }

        List<TextItem> textItems = [];
        if (_textItemsData != null)
        {
            textItems = _textItemsData.Select(item =>
            {
                var corners = ImageCropping.DetectOrientationAndOrderCorners(item.ORectangle);

                // Calculate center
                var centerX = (corners.TopLeft.X + corners.TopRight.X + corners.BottomRight.X + corners.BottomLeft.X) / 4.0;
                var centerY = (corners.TopLeft.Y + corners.TopRight.Y + corners.BottomRight.Y + corners.BottomLeft.Y) / 4.0;

                // Calculate rotation angle
                var textVector = (X: corners.TopRight.X - corners.TopLeft.X, Y: corners.TopRight.Y - corners.TopLeft.Y);
                var rotationAngle = Math.Atan2(textVector.Y, textVector.X) * 180.0 / Math.PI;

                // Calculate font size to use based on text height
                var textHeight = Math.Sqrt(
                    Math.Pow(corners.BottomLeft.X - corners.TopLeft.X, 2) +
                    Math.Pow(corners.BottomLeft.Y - corners.TopLeft.Y, 2));
                var fontSize = textHeight * 0.70;

                return new TextItem
                {
                    Text = item.Text,
                    CenterX = centerX,
                    CenterY = centerY,
                    FontSize = fontSize,
                    RotationAngle = rotationAngle,
                    Confidence = item.Confidence
                };
            }).ToList();
        }

        var templateData = new TemplateData
        {
            Width = _baseImage.Width,
            Height = _baseImage.Height,
            BaseImageDataUri = baseImageDataUri,
            ProbabilityMapDataUri = probabilityMapDataUri,
            AxisAlignedBoundingBoxes = _axisAlignedBBoxes ?? [],
            OrientedBoundingBoxes = _orientedBBoxes ?? [],
            ExpectedAxisAlignedBoundingBoxes = _expectedAxisAlignedBBoxes ?? [],
            ExpectedOrientedBoundingBoxes = _expectedOrientedBBoxes ?? [],
            Polygons = _polygonBBoxes ?? [],
            TextItems = textItems,
            Legend = legend.ToArray()
        };

        var context = new TemplateContext(templateData, options);

        return new Svg(template.Render(context));
    }

    private static string ConvertImageToDataUri(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var base64 = Convert.ToBase64String(stream.ToArray());
        return $"data:image/png;base64,{base64}";
    }

    private static IFluidTemplate LoadTemplate()
    {
        var templateContent = Resource.GetString("templates.svg-visualization-2.liquid");
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
                var alpha = (byte)(intensity / 2); // Max 50% opacity
                overlayImage[x, y] = new Rgba32(255, 255, 0, alpha);  // Yellow
            }
        }

        return ConvertImageToDataUri(overlayImage);
    }
}
