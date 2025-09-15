// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Algorithms;

/// <summary>
/// Utilities for cropping images using various bounding shapes.
/// </summary>
public static class ImageCropping
{
    /// <summary>
    /// Crops an image using an axis-aligned rectangle.
    /// </summary>
    /// <param name="image">Source image to crop</param>
    /// <param name="rectangle">Axis-aligned rectangle defining the crop area</param>
    /// <returns>Cropped image</returns>
    public static Image<Rgb24> CropAxisAligned(Image<Rgb24> image, Rectangle rectangle)
    {
        return image.Clone(x => x.Crop(rectangle));
    }

    /// <summary>
    /// Crops an image using an oriented rectangle defined by four corner points.
    /// The cropping preserves text orientation by ensuring proper mapping from
    /// the oriented rectangle to the output image.
    ///
    /// The algorithm automatically detects the orientation by finding the longer
    /// pair of parallel edges (which become the width/text direction) and the
    /// shorter pair (which become the height). Vertex order is irrelevant.
    /// </summary>
    /// <param name="image">Source image to crop</param>
    /// <param name="orientedRectangle">Four corner points defining the oriented rectangle in floating-point precision</param>
    /// <returns>Cropped and rectified image where text appears upright and left-to-right</returns>
    /// <exception cref="ArgumentException">Thrown when orientedRectangle doesn't have exactly 4 points</exception>
    public static Image<Rgb24> CropOriented(Image<Rgb24> image, List<(double X, double Y)> orientedRectangle)
    {
        if (orientedRectangle == null || orientedRectangle.Count != 4)
            throw new ArgumentException("Oriented rectangle must have exactly 4 points", nameof(orientedRectangle));

        // Detect orientation and establish proper corner correspondence
        var corners = DetectOrientationAndOrderCorners(orientedRectangle);

        // Calculate target dimensions based on the detected orientation
        var width = CalculateDistance(corners.TopLeft, corners.TopRight);
        var height = CalculateDistance(corners.TopLeft, corners.BottomLeft);

        // Check if this is already axis-aligned (no geometric transformation needed)
        const double alignmentTolerance = 1e-3;
        bool isAxisAligned =
            Math.Abs(corners.TopLeft.Y - corners.TopRight.Y) < alignmentTolerance &&
            Math.Abs(corners.BottomLeft.Y - corners.BottomRight.Y) < alignmentTolerance &&
            Math.Abs(corners.TopLeft.X - corners.BottomLeft.X) < alignmentTolerance &&
            Math.Abs(corners.TopRight.X - corners.BottomRight.X) < alignmentTolerance;

        if (isAxisAligned)
        {
            // Use simple axis-aligned cropping for better performance and accuracy
            var boundingRect = new Rectangle(
                (int)Math.Round(corners.TopLeft.X),
                (int)Math.Round(corners.TopLeft.Y),
                (int)Math.Round(width),
                (int)Math.Round(height)
            );
            return CropAxisAligned(image, boundingRect);
        }

        // Apply custom geometric transformation for oriented rectangles
        return CropOrientedWithGeometry(image, corners, (int)Math.Round(width), (int)Math.Round(height));
    }

    /// <summary>
    /// Detects the orientation of an oriented rectangle and orders the corners properly.
    /// Uses analytic geometry to determine the correct text reading orientation.
    /// </summary>
    internal static (
        (double X, double Y) TopLeft,
        (double X, double Y) TopRight,
        (double X, double Y) BottomRight,
        (double X, double Y) BottomLeft
    ) DetectOrientationAndOrderCorners(List<(double X, double Y)> vertices)
    {
        // Step 1: Identify rectangle structure using edge lengths
        var distances = new List<(int i, int j, double distance)>();
        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = i + 1; j < vertices.Count; j++)
            {
                var distance = CalculateDistance(vertices[i], vertices[j]);
                distances.Add((i, j, distance));
            }
        }

        // Sort by distance - 4 shortest are edges, 2 longest are diagonals
        distances.Sort((a, b) => a.distance.CompareTo(b.distance));
        var edges = distances.Take(4).ToList();

        // Group edges by length to find parallel pairs
        var edgeGroups = edges.GroupBy(e => Math.Round(e.distance, 1)).OrderBy(g => g.Key).ToList();
        var shortEdges = edgeGroups[0].ToList();
        var longEdges = edgeGroups[1].ToList();

        // Step 2: Determine text direction (along longer edges) and text height (along shorter edges)
        var textDirectionEdges = longEdges.Count >= 2 ? longEdges : shortEdges;
        var textHeightEdges = longEdges.Count >= 2 ? shortEdges : longEdges;

        // Step 3: Pick any text direction edge and establish the text direction vector
        var primaryTextEdge = textDirectionEdges[0];
        var v1 = vertices[primaryTextEdge.i];
        var v2 = vertices[primaryTextEdge.j];

        // Calculate the text direction vector (from v1 to v2)
        var textVector = (X: v2.X - v1.X, Y: v2.Y - v1.Y);

        // Step 4: Determine the correct direction for left-to-right reading
        // Choose the direction that has a more positive X component (rightward)
        var (textStart, textEnd) = textVector.X >= 0 ? (v1, v2) : (v2, v1);
        var canonicalTextVector = (X: textEnd.X - textStart.X, Y: textEnd.Y - textStart.Y);

        // Step 5: Find the text height vector (perpendicular to text direction)
        // Look for the vertex connected to textStart by a text height edge
        var heightVertex = textHeightEdges
            .Where(e => e.i == vertices.IndexOf(textStart) || e.j == vertices.IndexOf(textStart))
            .Select(e => e.i == vertices.IndexOf(textStart) ? vertices[e.j] : vertices[e.i])
            .First();

        var heightVector = (X: heightVertex.X - textStart.X, Y: heightVertex.Y - textStart.Y);

        // Step 6: Ensure text height points downward (positive Y direction)
        var (topLeft, bottomLeft) = heightVector.Y >= 0 ? (textStart, heightVertex) : (heightVertex, textStart);

        // Step 7: Calculate remaining corners using vector arithmetic
        var topRight = (X: topLeft.X + canonicalTextVector.X, Y: topLeft.Y + canonicalTextVector.Y);
        var bottomRight = (X: bottomLeft.X + canonicalTextVector.X, Y: bottomLeft.Y + canonicalTextVector.Y);

        // Ensure we're using actual vertices, not calculated points (handle floating point precision)
        topRight = FindClosestVertex(vertices, topRight);
        bottomRight = FindClosestVertex(vertices, bottomRight);

        return (topLeft, topRight, bottomRight, bottomLeft);
    }

    /// <summary>
    /// Finds the vertex in the list that is closest to the target point.
    /// </summary>
    private static (double X, double Y) FindClosestVertex(
        List<(double X, double Y)> vertices,
        (double X, double Y) target)
    {
        return vertices
            .OrderBy(v => CalculateDistance(v, target))
            .First();
    }


    /// <summary>
    /// Crops an oriented rectangle using custom geometric transformation.
    /// Maps each pixel in the output rectangle back to the source oriented quadrilateral.
    /// </summary>
    private static Image<Rgb24> CropOrientedWithGeometry(
        Image<Rgb24> sourceImage,
        (
            (double X, double Y) TopLeft,
            (double X, double Y) TopRight,
            (double X, double Y) BottomRight,
            (double X, double Y) BottomLeft
        ) corners,
        int outputWidth,
        int outputHeight)
    {
        var outputImage = new Image<Rgb24>(outputWidth, outputHeight);

        // Define the local coordinate system using the rectangle edges as basis vectors
        var uVector = (X: corners.TopRight.X - corners.TopLeft.X, Y: corners.TopRight.Y - corners.TopLeft.Y);
        var vVector = (X: corners.BottomLeft.X - corners.TopLeft.X, Y: corners.BottomLeft.Y - corners.TopLeft.Y);

        // For each pixel in the output image
        for (int j = 0; j < outputHeight; j++)
        {
            for (int i = 0; i < outputWidth; i++)
            {
                // Convert output pixel to normalized coordinates [0,1]
                double u = outputWidth > 1 ? (double)i / (outputWidth - 1) : 0;
                double v = outputHeight > 1 ? (double)j / (outputHeight - 1) : 0;

                // Map to source image coordinates using bilinear combination
                var sourceX = corners.TopLeft.X + u * uVector.X + v * vVector.X;
                var sourceY = corners.TopLeft.Y + u * uVector.Y + v * vVector.Y;

                // Sample the source image with bilinear interpolation
                var color = BilinearSample(sourceImage, sourceX, sourceY);
                outputImage[i, j] = color;
            }
        }

        return outputImage;
    }

    /// <summary>
    /// Samples a source image at the given coordinates using bilinear interpolation.
    /// </summary>
    private static Rgb24 BilinearSample(Image<Rgb24> sourceImage, double x, double y)
    {
        // Handle out-of-bounds coordinates
        if (x < 0 || y < 0 || x >= sourceImage.Width || y >= sourceImage.Height)
        {
            return new Rgb24(0, 0, 0); // Return black for out-of-bounds
        }

        // Get the integer coordinates and fractional parts
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, sourceImage.Width - 1);
        int y1 = Math.Min(y0 + 1, sourceImage.Height - 1);

        double fx = x - x0;
        double fy = y - y0;

        // Sample the four surrounding pixels
        var p00 = sourceImage[x0, y0];
        var p10 = sourceImage[x1, y0];
        var p01 = sourceImage[x0, y1];
        var p11 = sourceImage[x1, y1];

        // Perform bilinear interpolation for each color channel
        byte r = (byte)Math.Round(
            (1 - fx) * (1 - fy) * p00.R +
            fx * (1 - fy) * p10.R +
            (1 - fx) * fy * p01.R +
            fx * fy * p11.R);

        byte g = (byte)Math.Round(
            (1 - fx) * (1 - fy) * p00.G +
            fx * (1 - fy) * p10.G +
            (1 - fx) * fy * p01.G +
            fx * fy * p11.G);

        byte b = (byte)Math.Round(
            (1 - fx) * (1 - fy) * p00.B +
            fx * (1 - fy) * p10.B +
            (1 - fx) * fy * p01.B +
            fx * fy * p11.B);

        return new Rgb24(r, g, b);
    }

    /// <summary>
    /// Calculates the Euclidean distance between two points.
    /// </summary>
    private static double CalculateDistance((double X, double Y) p1, (double X, double Y) p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
