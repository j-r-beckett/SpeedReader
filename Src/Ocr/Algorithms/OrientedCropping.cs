using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Algorithms;

public static class OrientedCropping
{
    /// <summary>
    /// Crops an oriented rectangle from an image and returns it as an upright rectangle.
    /// The oriented rectangle is defined by 4 corner points in counter-clockwise order.
    /// </summary>
    /// <param name="image">Source image to crop from</param>
    /// <param name="rectangle">4 corner points defining the oriented rectangle</param>
    /// <returns>Cropped and straightened image</returns>
    public static Image<Rgb24> CropOrientedRectangle(Image<Rgb24> image, List<(int X, int Y)> rectangle)
    {
        if (rectangle.Count != 4)
            throw new ArgumentException("Oriented rectangle must have exactly 4 points", nameof(rectangle));

        // Input from MinAreaRectangle is in counter-clockwise order:
        // [0] = bottom-left, [1] = bottom-right, [2] = top-right, [3] = top-left
        var bottomLeft = rectangle[0];
        var bottomRight = rectangle[1];
        // var topRight = rectangle[2]; // Not needed for simple parallelogram mapping
        var topLeft = rectangle[3];

        // Calculate dimensions of the oriented rectangle
        double width = Distance(bottomLeft, bottomRight);
        double height = Distance(bottomLeft, topLeft);

        int targetWidth = Math.Max(1, (int)Math.Round(width));
        int targetHeight = Math.Max(1, (int)Math.Round(height));

        var result = new Image<Rgb24>(targetWidth, targetHeight);

        // Parallelogram vectors from bottom-left corner
        // Width vector: bottom-left → bottom-right
        double widthVecX = bottomRight.X - bottomLeft.X;
        double widthVecY = bottomRight.Y - bottomLeft.Y;

        // Height vector: bottom-left → top-left
        double heightVecX = topLeft.X - bottomLeft.X;
        double heightVecY = topLeft.Y - bottomLeft.Y;

        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < targetHeight; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < targetWidth; x++)
                {
                    // Normalized coordinates (0,0) to (1,1) in output rectangle
                    double s = (double)x / (targetWidth - 1);   // Along width
                    double t = (double)y / (targetHeight - 1);  // Along height

                    // BUT: output (0,0) should map to top-left, not bottom-left
                    // So we need to flip t: output y=0 → source t=1, output y=max → source t=0
                    t = 1.0 - t;

                    // Map to source coordinates using parallelogram equation: P = A + s*u + t*v
                    double srcX = bottomLeft.X + s * widthVecX + t * heightVecX;
                    double srcY = bottomLeft.Y + s * widthVecY + t * heightVecY;

                    // Sample the source image
                    var color = SampleBilinear(image, srcX, srcY);
                    row[x] = color;
                }
            }
        });

        return result;
    }

    private static Rgb24 SampleBilinear(Image<Rgb24> image, double x, double y)
    {
        // Clamp coordinates to image bounds
        x = Math.Clamp(x, 0, image.Width - 1);
        y = Math.Clamp(y, 0, image.Height - 1);

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, image.Width - 1);
        int y1 = Math.Min(y0 + 1, image.Height - 1);

        double fx = x - x0;
        double fy = y - y0;

        // Get the four surrounding pixels
        var p00 = image[x0, y0];
        var p10 = image[x1, y0];
        var p01 = image[x0, y1];
        var p11 = image[x1, y1];

        // Bilinear interpolation
        double r = (1 - fx) * (1 - fy) * p00.R + fx * (1 - fy) * p10.R + (1 - fx) * fy * p01.R + fx * fy * p11.R;
        double g = (1 - fx) * (1 - fy) * p00.G + fx * (1 - fy) * p10.G + (1 - fx) * fy * p01.G + fx * fy * p11.G;
        double b = (1 - fx) * (1 - fy) * p00.B + fx * (1 - fy) * p10.B + (1 - fx) * fy * p01.B + fx * fy * p11.B;

        return new Rgb24((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b));
    }


    private static double Distance((int X, int Y) p1, (int X, int Y) p2)
    {
        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    }
}
