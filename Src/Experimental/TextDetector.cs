// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices.Marshalling;
using CommunityToolkit.HighPerformance;
using Experimental.Algorithms;
using Experimental.Geometry;
using Experimental.Inference;
using Ocr;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Point = Experimental.Geometry.Point;

namespace Experimental;

public class TextDetector
{
    private readonly ModelRunner _modelRunner;

    public TextDetector(ModelRunner modelRunner) => _modelRunner = modelRunner;


    // We pass tiles into the model, so (TileHeight, TileWidth) == (ModelHeight, ModelWidth)
    private const int TileHeight = 640;
    private const int TileWidth = 640;

    private const double OverlapMultiplier = 0.05;

    // Override for testing only
    public virtual async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder)
    {
        vizBuilder.AddBaseImage(image);

        var tileRects = Tile(image);
        var cloneConfig = new Configuration { PreferContiguousImageBuffers = true };
        var tiledWidth = tileRects[^1].Right;
        var tiledHeight = tileRects[^1].Bottom;
        using var resized = image.HardAspectResize(new Size(tiledWidth, tiledHeight));
        var tiles = tileRects.Select(r => resized.Clone(cloneConfig, ctx => ctx.Crop(r))).ToList();

        var compositeModelOutput = new float[tiledWidth * tiledHeight];

        List<Task<(float[], int[])>> inferenceTasks = [];

        foreach (var tile in tiles)
        {
            var modelInput = Preprocess(tile, TileHeight, TileWidth);
            var inferenceTask = _modelRunner.Run(modelInput, [3, TileHeight, TileWidth]);
            inferenceTasks.Add(inferenceTask);
        }

        await Task.WhenAll(inferenceTasks);  // Gives the model runner the option of handling multiple tiles in a single batch

        Debug.Assert(tileRects.Count == inferenceTasks.Count && tileRects.Count == tiles.Count);
        foreach (var (tileRect, inferenceTask) in tileRects.Zip(inferenceTasks))
        {
            var (modelOutput, shape) = await inferenceTask;
            Debug.Assert(shape.Length == 2);
            Debug.Assert(shape[0] == TileHeight);
            Debug.Assert(shape[1] == TileWidth);

            for (int row = 0; row < TileHeight; row++)
            {
                var imageRow = tileRect.Top + row;

                for (int col = 0; col < TileWidth; col++)
                {
                    var imageCol = tileRect.Left + col;

                    // Take the max value in overlapping regions
                    compositeModelOutput[imageRow * tiledWidth + imageCol] =
                        Math.Max(compositeModelOutput[imageRow * tiledWidth + imageCol], modelOutput[row * TileWidth + col]);
                }
            }
        }

        var probabilityMapSpan = compositeModelOutput.AsSpan().AsSpan2D(tiledHeight, tiledWidth);
        vizBuilder.CreateAndAddProbabilityMap(probabilityMapSpan, image.Width, image.Height);

        var boundingBoxes = Postprocess(compositeModelOutput, image, tiledHeight, tiledWidth);

        vizBuilder.AddBoundingBoxes(boundingBoxes);

        foreach (var tile in tiles)
            tile.Dispose();

        return boundingBoxes;
    }

    private List<Rectangle> Tile(Image<Rgb24> image)
    {
        var horizontalOverlap = (int)Math.Round(OverlapMultiplier * TileWidth);
        var verticalOverlap = (int)Math.Round(OverlapMultiplier * TileHeight);

        var numTilesHorizontal = (int)Math.Ceiling(((double)image.Width - horizontalOverlap) / (TileWidth - horizontalOverlap));
        var numTilesVertical = (int)Math.Ceiling(((double)image.Height - verticalOverlap) / (TileHeight - verticalOverlap));
        var numTiles = numTilesHorizontal * numTilesVertical;

        var tiles = new List<Rectangle>(numTiles);

        for (int tileRow = 0; tileRow < numTilesVertical; tileRow++)
        {
            for (int tileCol = 0; tileCol < numTilesHorizontal; tileCol++)
            {
                var tileX = tileCol * (TileWidth - horizontalOverlap);
                var tileY = tileRow * (TileHeight - verticalOverlap);
                var tileRect = new Rectangle(tileX, tileY, TileWidth, TileHeight);
                tiles.Add(tileRect);
            }
        }

        return tiles;
    }

    private float[] Preprocess(Image<Rgb24> image, int height, int width)
    {
        // using var resized = image.SoftAspectResize(width, height);

        var tensor = image.ToTensor([height, width, 3]);
        tensor.NhwcToNchwInPlace([height, width, 3]);

        // Apply ImageNet normalization
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            var channelTensor = Tensor.Create(tensor, channel * height * width, [height, width], default);

            // Subtract mean and divide by std in place
            Tensor.Subtract(channelTensor, means[channel], channelTensor);
            Tensor.Divide(channelTensor, stds[channel], channelTensor);
        }

        return tensor;
    }

    private List<BoundingBox> Postprocess(float[] modelOutput, Image<Rgb24> originalImage, int height, int width)
    {
        modelOutput.BinarizeInPlace(0.2f);
        var tiledProbabilityMapSpan = modelOutput.AsSpan().AsSpan2D(height, width);

        // Create a temporary image from the probability map for undoing the resize
        using var tiledProbabilityImage = new Image<Rgb24>(tiledProbabilityMapSpan.Width, tiledProbabilityMapSpan.Height);

        // Convert probability map to image (for demonstration - in practice we'd want to work directly with spans)
        for (int y = 0; y < tiledProbabilityMapSpan.Height; y++)
        {
            for (int x = 0; x < tiledProbabilityMapSpan.Width; x++)
            {
                var probability = tiledProbabilityMapSpan[y, x];
                var gray = (byte)(probability * 255);
                tiledProbabilityImage[x, y] = new Rgb24(gray, gray, gray);
            }
        }

        // Undo the resize to get back to original coordinate space
        using var originalProbabilityImage = AspectResizeExtensions.UndoHardAspectResize(originalImage, tiledProbabilityImage);

        // Convert back to span for boundary tracing
        var originalProbabilitySpan = new float[originalProbabilityImage.Width * originalProbabilityImage.Height];
        for (int y = 0; y < originalProbabilityImage.Height; y++)
        {
            for (int x = 0; x < originalProbabilityImage.Width; x++)
            {
                originalProbabilitySpan[y * originalProbabilityImage.Width + x] = originalProbabilityImage[x, y].R / 255.0f;
            }
        }

        var probabilityMapSpan = originalProbabilitySpan.AsSpan().AsSpan2D(originalProbabilityImage.Height, originalProbabilityImage.Width);
        var boundaries = BoundaryTracing.FindBoundaries(probabilityMapSpan)
            .Select(b => b.Select(p => (Point)p).ToList())
            .Select(points => new Polygon { Points = points.ToImmutableList() });

        // No coordinate transformations needed - we're already in original image space!
        return boundaries
            .Select(BoundaryToBBox)
            .OfType<BoundingBox>()  // Filter out nulls
            .ToList();

        BoundingBox? BoundaryToBBox(Polygon boundary)
        {
            var polygon = boundary
                .Simplify()  // Remove redundant points
                .Dilate(1.5)  // Undo contraction baked into DBNet during training; 1.5 is a model-specific constant
                .Clamp(originalImage.Height - 1, originalImage.Width - 1);  // Make sure we don't go out of bounds

            if (polygon.Points.Count <= 4)
                return null;  // Not enough points to define a polygon

            return BoundingBox.Create(polygon);  // Bounding box construction creates rotated rectangle and axis-aligned rectangle
        }
    }
}
