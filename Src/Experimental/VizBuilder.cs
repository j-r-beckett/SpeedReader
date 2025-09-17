// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Fluid;
using Ocr.Algorithms;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;

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
        public required List<Polygon> Polygons = [];
        public required List<SvgRenderer.TextItem> TextItems = [];
        public required SvgRenderer.LegendItem[] Legend;
    }

    public class MultipleAddException : Exception
    {
        public MultipleAddException(string message) : base(message) { }
    }

    private Image? _baseImage;

    private List<Polygon>? _axisAlignedBBoxes;
    private bool _displayAxisAlignedBBoxesByDefault;

    private List<Polygon>? _orientedBBoxes;
    private bool _displayOrientedBBoxesByDefault;

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

        var templateData = new TemplateData
        {
            Width = _baseImage.Width,
            Height = _baseImage.Height,
            BaseImageDataUri = baseImageDataUri,
            AxisAlignedBoundingBoxes = _axisAlignedBBoxes ?? [],
            OrientedBoundingBoxes = _orientedBBoxes ?? [],
            Polygons = [],
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
}
