// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Blocks.DBNet;

public class DBNetPostprocessingBlock
{
    private readonly int _width;
    private readonly int _height;

    public IPropagatorBlock<(float[], Image<Rgb24>, VizData?), (List<TextBoundary>, Image<Rgb24>, VizData?)> Target
    {
        get;
    }

    public DBNetPostprocessingBlock(DbNetConfiguration config)
    {
        _width = config.Width;
        _height = config.Height;

        Target = new TransformBlock<(float[] RawResult, Image<Rgb24> OriginalImage, VizData? VizData), (List<TextBoundary>, Image<Rgb24>, VizData?)>(input =>
        {
            input.VizData?.ProbabilityMap = CreateProbabilityMap(input.RawResult, input.OriginalImage.Width, input.OriginalImage.Height);

            var textBoundaries = PostProcess(input.RawResult, input.OriginalImage.Width, input.OriginalImage.Height);

            return (textBoundaries, input.OriginalImage, input.VizData);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
            MaxDegreeOfParallelism = 1
        });
    }

    private List<TextBoundary> PostProcess(float[] processedImage, int originalWidth, int originalHeight)
    {
        Thresholding.BinarizeInPlace(processedImage, 0.2f);
        var probabilityMapSpan = processedImage.AsSpan().AsSpan2D(_height, _width);
        var boundaries = BoundaryTracing.FindBoundaries(probabilityMapSpan);
        List<TextBoundary> textBoundaries = [];

        foreach (var boundary in boundaries)
        {
            // Simplify
            var simplifiedPolygon = PolygonSimplification.DouglasPeucker(boundary);

            // Dilate
            var dilatedPolygon = Dilation.DilatePolygon(simplifiedPolygon.ToList());

            // Convert back to original coordinate system
            double scale = Math.Max((double)originalWidth / probabilityMapSpan.Width, (double)originalHeight / probabilityMapSpan.Height);
            Scale(dilatedPolygon, scale);

            // Clamp coordinates to image bounds
            ClampToImageBounds(dilatedPolygon, originalWidth, originalHeight);

            if (dilatedPolygon.Count >= 4)
            {
                textBoundaries.Add(TextBoundary.Create(dilatedPolygon));
            }
        }

        return textBoundaries;
    }

    private static void Scale(List<(int X, int Y)> polygon, double scale)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int originalX = (int)Math.Round(polygon[i].X * scale);
            int originalY = (int)Math.Round(polygon[i].Y * scale);
            polygon[i] = (originalX, originalY);
        }
    }

    private static void ClampToImageBounds(List<(int X, int Y)> polygon, int imageWidth, int imageHeight)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int clampedX = Math.Max(0, Math.Min(imageWidth - 1, polygon[i].X));
            int clampedY = Math.Max(0, Math.Min(imageHeight - 1, polygon[i].Y));
            polygon[i] = (clampedX, clampedY);
        }
    }

    private Image<L8> CreateProbabilityMap(float[] rawResult, int originalWidth, int originalHeight)
    {
        var probabilityMap = rawResult.AsSpan().AsSpan2D(_height, _width);

        // Calculate the fitted dimensions (what the image was resized to before padding)
        double scale = Math.Min((double)_width / originalWidth, (double)_height / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);

        // Create a grayscale image from the probability map (only the fitted portion, not padding)
        var probImage = new Image<L8>(fittedWidth, fittedHeight);

        for (int y = 0; y < fittedHeight; y++)
        {
            for (int x = 0; x < fittedWidth; x++)
            {
                var probability = probabilityMap[y, x];
                // Convert probability [0,1] to grayscale [0,255]
                probImage[x, y] = new L8((byte)(probability * 255));
            }
        }

        // Resize back to original image size
        var resizedImage = probImage.Clone(ctx =>
            ctx.Resize(originalWidth, originalHeight, KnownResamplers.Bicubic));

        probImage.Dispose();
        return resizedImage;
    }
}
