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

        // Calculate the axis-aligned bounding box of the oriented rectangle
        var minX = Math.Min(Math.Min(corners.TopLeft.X, corners.TopRight.X),
                           Math.Min(corners.BottomLeft.X, corners.BottomRight.X));
        var maxX = Math.Max(Math.Max(corners.TopLeft.X, corners.TopRight.X),
                           Math.Max(corners.BottomLeft.X, corners.BottomRight.X));
        var minY = Math.Min(Math.Min(corners.TopLeft.Y, corners.TopRight.Y),
                           Math.Min(corners.BottomLeft.Y, corners.BottomRight.Y));
        var maxY = Math.Max(Math.Max(corners.TopLeft.Y, corners.TopRight.Y),
                           Math.Max(corners.BottomLeft.Y, corners.BottomRight.Y));

        var boundingRect = new Rectangle(
            (int)Math.Floor(minX),
            (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX - minX),
            (int)Math.Ceiling(maxY - minY)
        );

        return CropAxisAligned(image, boundingRect);
    }

    /// <summary>
    /// Detects the orientation of an oriented rectangle and orders the corners properly.
    /// Finds the longer parallel edges (text direction) and shorter parallel edges (text height).
    /// </summary>
    private static (
        (double X, double Y) TopLeft,
        (double X, double Y) TopRight,
        (double X, double Y) BottomRight,
        (double X, double Y) BottomLeft
    ) DetectOrientationAndOrderCorners(List<(double X, double Y)> vertices)
    {
        // Pick the first vertex as reference point
        var referenceVertex = vertices[0];
        var otherVertices = vertices.Skip(1).ToList();

        // Calculate vectors from reference vertex to all other vertices
        var vectors = otherVertices
            .Select(v => new
            {
                Vertex = v,
                Distance = CalculateDistance(referenceVertex, v),
                Vector = (X: v.X - referenceVertex.X, Y: v.Y - referenceVertex.Y)
            })
            .OrderBy(v => v.Distance)
            .ToList();

        // The longest vector is the diagonal, the other two are the adjacent sides
        var diagonal = vectors[2]; // Longest distance
        var side1 = vectors[0];    // Shorter side
        var side2 = vectors[1];    // Longer side

        // The vertex opposite to reference is at the end of the diagonal
        var oppositeVertex = diagonal.Vertex;

        // Determine which side is longer (text direction) vs shorter (text height)
        var longerSide = side1.Distance > side2.Distance ? side1 : side2;
        var shorterSide = side1.Distance > side2.Distance ? side2 : side1;

        // Now we have the rectangle: reference -> longerSide -> opposite -> shorterSide -> reference
        // Map this to our standard orientation where text flows left-to-right
        var topLeft = referenceVertex;
        var topRight = longerSide.Vertex;
        var bottomRight = oppositeVertex;
        var bottomLeft = shorterSide.Vertex;

        return (topLeft, topRight, bottomRight, bottomLeft);
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
