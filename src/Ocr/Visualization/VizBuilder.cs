// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpeedReader.Ocr.Geometry;
using SpeedReader.Resources.Viz;

namespace SpeedReader.Ocr.Visualization;

public class VizBuilder
{
    public class TextItem
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }

        [JsonPropertyName("centerX")]
        public required double CenterX { get; init; }

        [JsonPropertyName("centerY")]
        public required double CenterY { get; init; }

        [JsonPropertyName("fontSize")]
        public required double FontSize { get; init; }

        [JsonPropertyName("rotationAngle")]
        public required double RotationAngle { get; init; }

        [JsonPropertyName("confidence")]
        public required double Confidence { get; init; }
    }

    public class TemplateData
    {
        [JsonPropertyName("width")]
        public required int Width { get; init; }

        [JsonPropertyName("height")]
        public required int Height { get; init; }

        [JsonPropertyName("baseImageDataUri")]
        public required string BaseImageDataUri { get; init; }

        [JsonPropertyName("probabilityMapDataUri")]
        public string? ProbabilityMapDataUri { get; init; }

        [JsonPropertyName("axisAlignedBoundingBoxes")]
        public required List<Polygon> AxisAlignedBoundingBoxes { get; init; }

        [JsonPropertyName("orientedBoundingBoxes")]
        public required List<Polygon> OrientedBoundingBoxes { get; init; }

        [JsonPropertyName("expectedAxisAlignedBoundingBoxes")]
        public required List<Polygon> ExpectedAxisAlignedBoundingBoxes { get; init; }

        [JsonPropertyName("expectedOrientedBoundingBoxes")]
        public required List<Polygon> ExpectedOrientedBoundingBoxes { get; init; }

        [JsonPropertyName("polygons")]
        public required List<Polygon> Polygons { get; init; }

        [JsonPropertyName("textItems")]
        public required List<TextItem> TextItems { get; init; }

        [JsonPropertyName("defaultVisible")]
        public required Dictionary<string, bool> DefaultVisible { get; init; }
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
    private bool _displayTextItemsByDefault;

    private static readonly Lazy<string> _template = new(LoadTemplate);

    public VizBuilder AddBaseImage(Image image)
    {
        if (_baseImage != null)
        {
            throw new MultipleAddException($"{nameof(AddBaseImage)} cannot be called twice");
        }

        _baseImage = image;

        return this;
    }


    // Supports being called from multiple threads. If any call sets displayByDefault=true, it stays true.
    public VizBuilder AddTextItems(List<(string Text, double Confidence, List<(double X, double Y)> ORectangle)> textItems, bool displayByDefault = false)
    {
        _textItemsData ??= [];
        foreach (var item in textItems)
            _textItemsData.Add(item);
        _displayTextItemsByDefault |= displayByDefault;

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

        _axisAlignedBBoxes = axisAlignedBBoxes.Select(rect => rect.Corners()).ToList();
        _displayAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddOrientedBBoxes(List<RotatedRectangle> orientedBBoxes, bool displayByDefault = false)
    {
        if (_orientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddOrientedBBoxes)} cannot be called twice");
        }

        _orientedBBoxes = orientedBBoxes.Select(rect => rect.Corners()).ToList();
        _displayOrientedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedAxisAlignedBBoxes(List<AxisAlignedRectangle> expectedAxisAlignedBBoxes, bool displayByDefault = false)
    {
        if (_expectedAxisAlignedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedAxisAlignedBBoxes)} cannot be called twice");
        }

        _expectedAxisAlignedBBoxes = expectedAxisAlignedBBoxes.Select(rect => rect.Corners()).ToList();
        _displayExpectedAxisAlignedBBoxesByDefault = displayByDefault;

        return this;
    }

    public VizBuilder AddExpectedOrientedBBoxes(List<RotatedRectangle> expectedOrientedBBoxes, bool displayByDefault = false)
    {
        if (_expectedOrientedBBoxes != null)
        {
            throw new MultipleAddException($"{nameof(AddExpectedOrientedBBoxes)} cannot be called twice");
        }

        _expectedOrientedBBoxes = expectedOrientedBBoxes.Select(rect => rect.Corners()).ToList();
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

        var defaultVisible = new Dictionary<string, bool>
        {
            ["bounding-boxes"] = _displayAxisAlignedBBoxesByDefault,
            ["oriented-bounding-boxes"] = _displayOrientedBBoxesByDefault,
            ["expected-bounding-boxes"] = _displayExpectedAxisAlignedBBoxesByDefault,
            ["expected-oriented-bounding-boxes"] = _displayExpectedOrientedBBoxesByDefault,
            ["polygons"] = _displayPolygonBBoxesByDefault,
            ["text-overlay"] = _displayTextItemsByDefault,
            ["dbnet-overlay"] = _displayProbabilityMapByDefault
        };

        string? probabilityMapDataUri = null;
        if (_probabilityMap != null)
        {
            probabilityMapDataUri = ConvertProbabilityMapToDataUri(_probabilityMap);
        }

        List<TextItem> textItems = [];
        if (_textItemsData != null)
        {
            textItems = _textItemsData.Select(item =>
            {
                // The corners are already ordered: topLeft, topRight, bottomRight, bottomLeft
                var topLeft = item.ORectangle[0];
                var topRight = item.ORectangle[1];
                var bottomRight = item.ORectangle[2];
                var bottomLeft = item.ORectangle[3];

                // Calculate center
                var centerX = (topLeft.X + topRight.X + bottomRight.X + bottomLeft.X) / 4.0;
                var centerY = (topLeft.Y + topRight.Y + bottomRight.Y + bottomLeft.Y) / 4.0;

                // Calculate rotation angle
                var textVector = (X: topRight.X - topLeft.X, Y: topRight.Y - topLeft.Y);
                var rotationAngle = Math.Atan2(textVector.Y, textVector.X) * 180.0 / Math.PI;

                // Calculate font size to use based on text height
                var textHeight = Math.Sqrt(
                    Math.Pow(bottomLeft.X - topLeft.X, 2) +
                    Math.Pow(bottomLeft.Y - topLeft.Y, 2));
                var fontSize = textHeight;

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
            DefaultVisible = defaultVisible
        };

        var json = JsonSerializer.Serialize(templateData, VizJsonContext.Default.TemplateData);
        var svg = template.Replace("{{VIZ_DATA}}", json);

        return new Svg(svg);
    }

    private static string ConvertImageToDataUri(Image image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var base64 = Convert.ToBase64String(stream.ToArray());
        return $"data:image/png;base64,{base64}";
    }

    private static string LoadTemplate() => new EmbeddedViz().Template;

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

[JsonSerializable(typeof(VizBuilder.TemplateData))]
[JsonSerializable(typeof(VizBuilder.TextItem))]
[JsonSerializable(typeof(Polygon))]
[JsonSerializable(typeof(SpeedReader.Ocr.Geometry.PointF))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
internal partial class VizJsonContext : JsonSerializerContext
{
}
