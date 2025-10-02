// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Experimental.Algorithms;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Experimental;

public class TextDetector
{
    private readonly ModelRunner _modelRunner;

    public TextDetector(ModelRunner modelRunner) => _modelRunner = modelRunner;

    // We pass tiles into the model, so (TileHeight, TileWidth) == (ModelHeight, ModelWidth)
    private const int TileHeight = 640;
    private const int TileWidth = 640;

    private const double OverlapMultiplier = 0.05;

    public List<(float[] Data, int[] Shape)> Preprocess(Image<Rgb24> image, VizBuilder vizBuilder)
    {
        vizBuilder.AddBaseImage(image);

        var tileRects = Tile(image);
        var cloneConfig = new Configuration { PreferContiguousImageBuffers = true };
        var tiledWidth = tileRects[^1].Right;
        var tiledHeight = tileRects[^1].Bottom;
        using var resized = image.HardAspectResize(new Size(tiledWidth, tiledHeight));
        var tiles = tileRects.Select(r => resized.Clone(cloneConfig, ctx => ctx.Crop(r))).ToList();

        int[] inferenceShape = [3, TileHeight, TileWidth];
        var inferenceInputs = tiles.Select(t => (PreprocessTile(t), inferenceShape)).ToList();
        foreach (var tile in tiles) tile.Dispose();
        return inferenceInputs;

        float[] PreprocessTile(Image<Rgb24> tile)
        {
            var (height, width) = (tile.Height, tile.Width);
            var tensor = tile.ToTensor([height, width, 3]);
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
    }

    public List<BoundingBox> Postprocess((float[] Data, int[] Shape)[] inferenceOutputs, Image<Rgb24> originalImage, VizBuilder vizBuilder)
    {
        var tileRects = Tile(originalImage);
        var tiledWidth = tileRects[^1].Right;
        var tiledHeight = tileRects[^1].Bottom;
        var compositeModelOutput = new float[tiledWidth * tiledHeight];

        Debug.Assert(tileRects.Count == inferenceOutputs.Length);
        foreach (var (tileRect, (modelOutput, shape)) in tileRects.Zip(inferenceOutputs))
        {
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
                    .Scale(1 / scale)  // Undo HardAspectResize scaling; convert from tiled to original coordinates
                    .Simplify()  // Remove redundant points
                    .Dilate(1.5)  // Undo contraction baked into DBNet during training; 1.5 is a model-specific constant
                    .Clamp(originalImage.Height - 1, originalImage.Width - 1);  // Make sure we don't go out of bounds

                if (polygon.Points.Count <= 4)
                    return null;  // Not enough points to define a polygon

                return BoundingBox.Create(polygon);  // Bounding box construction creates rotated rectangle and axis-aligned rectangle
            }
        }
    }

    // Override for testing only
    public virtual async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder)
    {
        var modelInput = Preprocess(image, vizBuilder);
        var modelOutput = await RunInference(modelInput);
        var result = Postprocess(modelOutput, image, vizBuilder);
        return result;
    }

    public async Task<(float[], int[])[]> RunInference(List<(float[], int[])> tiles) =>
        await Task.WhenAll(tiles.Select(t => _modelRunner.Run(t.Item1, t.Item2)));

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
}
