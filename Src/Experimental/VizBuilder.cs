// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using CommunityToolkit.HighPerformance;
using Fluid;
using Ocr.Algorithms;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Experimental;

public class VizBuilder
{
    public class Point
    {
        public required double X;
        public required double Y;
    }

    public class Polygon
    {
        public required List<Point> Points;
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
        public required List<SvgRenderer.TextItem> TextItems = [];
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

    public VizBuilder AddAxisAlignedBBoxes(List<Rectangle> axisAlignedBBoxes, bool displayByDefault = false)
    {
        if (_axisAlignedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddAxisAlignedBBoxes)} cannot be called twice");
        }

        var polygons = axisAlignedBBoxes.Select(bbox =>
        {
            var topLeft = new Point { X = bbox.X, Y = bbox.Y };
            var topRight = new Point { X = bbox.X + bbox.Width, Y = bbox.Y };
            var bottomRight = new Point { X = bbox.X + bbox.Width, Y = bbox.Y + bbox.Height };
            var bottomLeft = new Point { X = bbox.X, Y = bbox.Y + bbox.Height };
            return new Polygon { Points = [topLeft, topRight, bottomRight, bottomLeft] };
        });

        _axisAlignedBBoxes = polygons.ToList();
        _displayAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddOrientedBBoxes(List<List<(double X, double Y)>> orientedBBoxes, bool displayByDefault = false)
    {
        if (_orientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddOrientedBBoxes)} cannot be called twice");
        }

        var polygons = orientedBBoxes
            .Select(bbox => bbox.Select(p => new Point { X = p.X, Y = p.Y }))
            .Select(points => new Polygon { Points = points.ToList() })
            .ToList();

        _orientedBBoxes = polygons;
        _displayOrientedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedAxisAlignedBBoxes(List<Rectangle> expectedAxisAlignedBBoxes, bool displayByDefault = false)
    {
        if (_expectedAxisAlignedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedAxisAlignedBBoxes)} cannot be called twice");
        }

        var polygons = expectedAxisAlignedBBoxes.Select(bbox =>
        {
            var topLeft = new Point { X = bbox.X, Y = bbox.Y };
            var topRight = new Point { X = bbox.X + bbox.Width, Y = bbox.Y };
            var bottomRight = new Point { X = bbox.X + bbox.Width, Y = bbox.Y + bbox.Height };
            var bottomLeft = new Point { X = bbox.X, Y = bbox.Y + bbox.Height };
            return new Polygon { Points = [topLeft, topRight, bottomRight, bottomLeft] };
        });

        _expectedAxisAlignedBBoxes = polygons.ToList();
        _displayExpectedAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedOrientedBBoxes(List<List<(double X, double Y)>> expectedOrientedBBoxes, bool displayByDefault = false)
    {
        if (_expectedOrientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedOrientedBBoxes)} cannot be called twice");
        }

        var polygons = expectedOrientedBBoxes
            .Select(bbox => bbox.Select(p => new Point { X = p.X, Y = p.Y }))
            .Select(points => new Polygon { Points = points.ToList() })
            .ToList();

        _expectedOrientedBBoxes = polygons;
        _displayExpectedOrientedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddPolygonBBoxes(List<List<(int X, int Y)>> polygonBBoxes, bool displayByDefault = false)
    {
        if (_polygonBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddPolygonBBoxes)} cannot be called twice");
        }

        var polygons = polygonBBoxes
            .Select(bbox => bbox.Select(p => new Point { X = p.X, Y = p.Y }))
            .Select(points => new Polygon { Points = points.ToList() })
            .ToList();

        _polygonBBoxes = polygons;
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
            TextItems = [],
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
