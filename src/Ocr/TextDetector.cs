// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.DependencyInjection;
using Ocr.Algorithms;
using Ocr.Geometry;
using Ocr.InferenceEngine;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr;

public class TextDetector
{
    private readonly IInferenceEngine _inferenceEngine;
    private readonly int _tileHeight;
    private readonly int _tileWidth;

    public int InferenceEngineCapacity() => _inferenceEngine.CurrentMaxCapacity();

    public static TextDetector Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredService<DetectionOptions>();
        var engine = serviceProvider.GetRequiredKeyedService<IInferenceEngine>(key);
        return new TextDetector(engine, options);
    }

    public TextDetector(IInferenceEngine inferenceEngine, DetectionOptions options)
    {
        _inferenceEngine = inferenceEngine;
        _tileWidth = options.TileWidth;
        _tileHeight = options.TileHeight;
    }

    private const double OverlapMultiplier = 0.05;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public List<(float[] Data, int[] Shape)> Preprocess(Image<Rgb24> image, Tiling tiling, VizBuilder vizBuilder)
    {
        vizBuilder.AddBaseImage(image);

        // The last tile is the bottom-right tile. The bottom-right corner of the bottom-right tile is the bottom-right
        // corner of the image formed out of all tiles
        var tileRects = tiling.Tiles;
        var bottomRightTile = tileRects[^1];
        var tiledWidth = bottomRightTile.Right;
        var tiledHeight = bottomRightTile.Bottom;

        // Resize, preserves aspect ratio, pads with black, positions at top-left
        using var resized = image.Clone(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(tiledWidth, tiledHeight),
                Mode = ResizeMode.Pad,
                Position = AnchorPositionMode.TopLeft
            }));

        int[] tensorShape = [3, _tileHeight, _tileWidth];  // CHW

        // Thanks to the resize, we can now cut the image perfectly into tiles
        return tileRects.Select(t => (ToTensor(t), inferenceShape: tensorShape)).ToList();

        float[] ToTensor(Rectangle tile)
        {
            // Crop tile rect out of the image, convert it to a tensor (float array), apply ImageNet normalization
            Span<float> means = [123.675f, 116.28f, 103.53f];
            Span<float> stds = [58.395f, 57.12f, 57.375f];
            return resized.ToNormalizedChwTensor(tile, means, stds);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public List<BoundingBox> Postprocess((float[] Data, int[] Shape)[] inferenceOutputs, Tiling tiling, Image<Rgb24> originalImage, VizBuilder vizBuilder)
    {
        var tileRects = tiling.Tiles;
        var tiledWidth = tileRects[^1].Right;
        var tiledHeight = tileRects[^1].Bottom;
        var compositeModelOutput = new float[tiledWidth * tiledHeight];

        Debug.Assert(tileRects.Count == inferenceOutputs.Length);
        foreach (var (tileRect, (modelOutput, shape)) in tileRects.Zip(inferenceOutputs))
        {
            Debug.Assert(shape.Length == 2);
            Debug.Assert(shape[0] == _tileHeight);
            Debug.Assert(shape[1] == _tileWidth);

            for (int row = 0; row < _tileHeight; row++)
            {
                var imageRow = tileRect.Top + row;

                for (int col = 0; col < _tileWidth; col++)
                {
                    var imageCol = tileRect.Left + col;

                    // Take the max value in overlapping regions
                    compositeModelOutput[imageRow * tiledWidth + imageCol] =
                        Math.Max(compositeModelOutput[imageRow * tiledWidth + imageCol], modelOutput[row * _tileWidth + col]);
                }
            }
        }

        var probabilityMapSpan = compositeModelOutput.AsSpan().AsSpan2D(tiledHeight, tiledWidth);
        vizBuilder.CreateAndAddProbabilityMap(probabilityMapSpan, originalImage.Width, originalImage.Height);

        var boundingBoxes = PostprocessComposite(compositeModelOutput, tiledHeight, tiledWidth);

        vizBuilder.AddBoundingBoxes(boundingBoxes);

        return boundingBoxes;

        List<BoundingBox> PostprocessComposite(float[] modelOutput, int height, int width)
        {
            var boundaries = new ReliefMap(modelOutput, width, height).TraceAllBoundaries();

            // Calculate the scale used in HardAspectResize
            var scaleX = (double)width / originalImage.Width;
            var scaleY = (double)height / originalImage.Height;
            var scale = Math.Min(scaleX, scaleY);

            return boundaries
                .Select(BoundaryToBBox)
                .OfType<BoundingBox>()  // Filter out nulls
                .ToList();

            BoundingBox? BoundaryToBBox(Polygon boundary)
            {
                var polygon = boundary
                    .Simplify(4)  // Remove redundant points; see https://en.wikipedia.org/wiki/Visvalingam%E2%80%93Whyatt_algorithm
                    .Scale(1 / scale)  // Undo scaling; convert from tiled to original coordinates
                    .Dilate(1.5)  // Undo contraction baked into DBNet during training; 1.5 is a model-specific constant
                    ?.Clamp(originalImage.Height - 1, originalImage.Width - 1);  // Make sure we don't go out of bounds

                var convexHull = polygon?.ToConvexHull();
                var rotatedRectangle = convexHull?.ToRotatedRectangle();
                var axisAlignedRectangle = rotatedRectangle?.ToAxisAlignedRectangle();

                if (axisAlignedRectangle == null)
                    return null;

                // If axisAlignedRectangle is non-null then polygon and rotatedRectangle must also be non-null
                return new BoundingBox
                {
                    Polygon = polygon!,
                    RotatedRectangle = rotatedRectangle!,
                    AxisAlignedRectangle = axisAlignedRectangle
                };
            }
        }
    }

    // Override for testing only
    public virtual async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder)
    {
        var tiling = Tile(image);
        var modelInput = Preprocess(image, tiling, vizBuilder);
        var modelOutput = await RunInference(modelInput);
        return Postprocess(modelOutput, tiling, image, vizBuilder);
    }

    public async Task<(float[], int[])[]> RunInference(List<(float[], int[])> tiles)
    {
        List<Task<(float[], int[])>> inferenceTasks = [];
        foreach (var (data, shape) in tiles)
        {
            inferenceTasks.Add(await _inferenceEngine.Run(data, shape));
        }
        return await Task.WhenAll(inferenceTasks);
    }

    public Tiling Tile(Image<Rgb24> image)
    {
        var horizontalOverlap = (int)Math.Round(OverlapMultiplier * _tileWidth);
        var verticalOverlap = (int)Math.Round(OverlapMultiplier * _tileHeight);

        var numTilesHorizontal = (int)Math.Ceiling(((double)image.Width - horizontalOverlap) / (_tileWidth - horizontalOverlap));
        var numTilesVertical = (int)Math.Ceiling(((double)image.Height - verticalOverlap) / (_tileHeight - verticalOverlap));
        var numTiles = numTilesHorizontal * numTilesVertical;

        var tiles = new List<Rectangle>(numTiles);

        for (int tileRow = 0; tileRow < numTilesVertical; tileRow++)
        {
            for (int tileCol = 0; tileCol < numTilesHorizontal; tileCol++)
            {
                var tileX = tileCol * (_tileWidth - horizontalOverlap);
                var tileY = tileRow * (_tileHeight - verticalOverlap);
                var tileRect = new Rectangle(tileX, tileY, _tileWidth, _tileHeight);
                tiles.Add(tileRect);
            }
        }

        return new Tiling(tiles, numTilesHorizontal, numTilesVertical);
    }

    public record Tiling(List<Rectangle> Tiles, int NumTilesHorizontal, int NumTilesVertical);
}
