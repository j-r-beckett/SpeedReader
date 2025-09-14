using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TestUtils;

/// <summary>
/// Utilities for creating test images with oriented rectangles and gradients
/// for validating text orientation and cropping behavior.
/// </summary>
public static class OrientedRectangleTestUtils
{
    /// <summary>
    /// Creates a test image with a blue background (0, 0, 255) containing an oriented rectangle
    /// with gradient patterns that help validate cropping orientation and direction.
    ///
    /// The oriented rectangle is filled with:
    /// - Y-gradient: (0,0,0) to (255,0,0) running in the direction of increasing Y from point p
    /// - X-gradient: (0,0,0) to (0,255,0) running in the direction of increasing X from point p
    ///
    /// This allows us to verify that:
    /// 1. The cropped image maintains proper orientation (red should increase downward, green rightward)
    /// 2. Text direction is preserved (not reversed or flipped)
    /// </summary>
    /// <param name="imageWidth">Width of the test image</param>
    /// <param name="imageHeight">Height of the test image</param>
    /// <param name="p">First corner point of the oriented rectangle</param>
    /// <param name="q">Second corner point of the oriented rectangle (defines one edge)</param>
    /// <param name="width">Width of the oriented rectangle (perpendicular to p-q edge)</param>
    /// <returns>Test image with blue background and gradient-filled oriented rectangle</returns>
    public static Image<Rgb24> CreateGradientOrientedRectangle(
        int imageWidth,
        int imageHeight,
        PointF p,
        PointF q,
        float width)
    {
        var image = new Image<Rgb24>(imageWidth, imageHeight);

        // Fill background with blue (0, 0, 255)
        image.Mutate(ctx => ctx.BackgroundColor(Color.Blue));

        // Ensure the background is actually filled
        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                image[x, y] = new Rgb24(0, 0, 255); // Blue background
            }
        }

        // Calculate the oriented rectangle vertices
        var vertices = CalculateOrientedRectangleVertices(p, q, width);

        // Create polygon from vertices
        var polygon = new Polygon(vertices);

        // Fill the oriented rectangle with the gradient pattern
        FillOrientedRectangleWithGradients(image, vertices, p);

        return image;
    }

    /// <summary>
    /// Calculates the four vertices of an oriented rectangle given two points and a width.
    /// </summary>
    /// <param name="p">First point (origin for gradients)</param>
    /// <param name="q">Second point (defines direction of one edge)</param>
    /// <param name="width">Width of the rectangle perpendicular to the p-q edge</param>
    /// <returns>Four vertices in order: p, q, q+perpendicular, p+perpendicular</returns>
    public static PointF[] CalculateOrientedRectangleVertices(PointF p, PointF q, float width)
    {
        // Vector from p to q
        var pqX = q.X - p.X;
        var pqY = q.Y - p.Y;
        var pqLength = MathF.Sqrt(pqX * pqX + pqY * pqY);

        // Normalized perpendicular vector (rotate 90 degrees counterclockwise)
        var perpX = -pqY / pqLength * width;
        var perpY = pqX / pqLength * width;

        return new PointF[]
        {
            p,                                          // Corner 0: origin
            q,                                          // Corner 1: along main edge
            new PointF(q.X + perpX, q.Y + perpY),     // Corner 2: opposite corner
            new PointF(p.X + perpX, p.Y + perpY)      // Corner 3: complete rectangle
        };
    }

    /// <summary>
    /// Fills the oriented rectangle with gradient patterns.
    /// Red gradient increases in the Y-direction from point p.
    /// Green gradient increases in the X-direction from point p.
    /// </summary>
    private static void FillOrientedRectangleWithGradients(Image<Rgb24> image, PointF[] vertices, PointF origin)
    {
        // Calculate the bounding box of the oriented rectangle
        var minX = (int)Math.Floor(vertices.Min(v => v.X));
        var maxX = (int)Math.Ceiling(vertices.Max(v => v.X));
        var minY = (int)Math.Floor(vertices.Min(v => v.Y));
        var maxY = (int)Math.Ceiling(vertices.Max(v => v.Y));

        // Create polygon for point-in-polygon testing
        var polygon = new Polygon(vertices);

        // Fill each pixel in the bounding box if it's inside the oriented rectangle
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                // Skip pixels outside image bounds
                if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
                    continue;

                var point = new PointF(x, y);

                // Check if point is inside the oriented rectangle
                if (IsPointInPolygon(point, vertices))
                {
                    // Transform point to local rectangle coordinate system
                    var localCoords = TransformToLocalCoordinates(point, vertices, origin);

                    // Calculate gradients in local coordinates (0-1 range)
                    var redValue = (byte)Math.Max(0, Math.Min(255, localCoords.Y * 255));
                    var greenValue = (byte)Math.Max(0, Math.Min(255, localCoords.X * 255));

                    // Set pixel color: Red increases with local Y, Green increases with local X
                    image[x, y] = new Rgb24(redValue, greenValue, 0);
                }
            }
        }

        // Add a small white square at the origin corner for reference
        var originSize = 6;
        var originRect = new Rectangle(
            (int)origin.X - originSize / 2,
            (int)origin.Y - originSize / 2,
            originSize,
            originSize
        );

        image.Mutate(ctx => ctx.Fill(Color.White, originRect));
    }

    /// <summary>
    /// Transform a point from global coordinates to local rectangle coordinates (0-1 range).
    /// Local X runs from origin to the adjacent corner along the primary edge.
    /// Local Y runs from origin to the adjacent corner along the perpendicular edge.
    /// </summary>
    private static PointF TransformToLocalCoordinates(PointF globalPoint, PointF[] vertices, PointF origin)
    {
        // vertices[0] = origin (p)
        // vertices[1] = q (defines primary edge direction)
        // vertices[3] = p + perpendicular (defines perpendicular edge direction)

        var primaryEdge = new PointF(vertices[1].X - vertices[0].X, vertices[1].Y - vertices[0].Y);
        var perpEdge = new PointF(vertices[3].X - vertices[0].X, vertices[3].Y - vertices[0].Y);

        // Vector from origin to the global point
        var pointVector = new PointF(globalPoint.X - origin.X, globalPoint.Y - origin.Y);

        // Calculate lengths for normalization
        var primaryLength = MathF.Sqrt(primaryEdge.X * primaryEdge.X + primaryEdge.Y * primaryEdge.Y);
        var perpLength = MathF.Sqrt(perpEdge.X * perpEdge.X + perpEdge.Y * perpEdge.Y);

        // Project point vector onto the two edges to get local coordinates
        var localX = (pointVector.X * primaryEdge.X + pointVector.Y * primaryEdge.Y) / (primaryLength * primaryLength);
        var localY = (pointVector.X * perpEdge.X + pointVector.Y * perpEdge.Y) / (perpLength * perpLength);

        return new PointF(localX, localY);
    }

    /// <summary>
    /// Simple point-in-polygon test using ray casting algorithm.
    /// </summary>
    private static bool IsPointInPolygon(PointF point, PointF[] polygon)
    {
        int intersectionCount = 0;
        int n = polygon.Length;

        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];

            if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                intersectionCount++;
            }
        }

        return (intersectionCount % 2) == 1;
    }

    /// <summary>
    /// Creates a simple test case with a rectangular text region at a given angle.
    /// Useful for testing basic oriented rectangle detection and cropping.
    /// </summary>
    /// <param name="imageWidth">Width of the test image</param>
    /// <param name="imageHeight">Height of the test image</param>
    /// <param name="centerX">X coordinate of rectangle center</param>
    /// <param name="centerY">Y coordinate of rectangle center</param>
    /// <param name="rectWidth">Width of the oriented rectangle</param>
    /// <param name="rectHeight">Height of the oriented rectangle</param>
    /// <param name="angleDegrees">Rotation angle in degrees</param>
    /// <returns>Test image with oriented rectangle</returns>
    public static Image<Rgb24> CreateRotatedRectangleTest(
        int imageWidth,
        int imageHeight,
        float centerX,
        float centerY,
        float rectWidth,
        float rectHeight,
        float angleDegrees)
    {
        var angleRadians = angleDegrees * MathF.PI / 180f;
        var cos = MathF.Cos(angleRadians);
        var sin = MathF.Sin(angleRadians);

        // Calculate the four corners of the rotated rectangle
        var halfWidth = rectWidth / 2;
        var halfHeight = rectHeight / 2;

        var corners = new PointF[]
        {
            new PointF(
                centerX + (-halfWidth * cos - -halfHeight * sin),
                centerY + (-halfWidth * sin + -halfHeight * cos)
            ),
            new PointF(
                centerX + (halfWidth * cos - -halfHeight * sin),
                centerY + (halfWidth * sin + -halfHeight * cos)
            ),
            new PointF(
                centerX + (halfWidth * cos - halfHeight * sin),
                centerY + (halfWidth * sin + halfHeight * cos)
            ),
            new PointF(
                centerX + (-halfWidth * cos - halfHeight * sin),
                centerY + (-halfWidth * sin + halfHeight * cos)
            )
        };

        return CreateGradientOrientedRectangle(imageWidth, imageHeight, corners[0], corners[1], rectHeight);
    }
}
