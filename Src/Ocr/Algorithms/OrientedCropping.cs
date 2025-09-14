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
    /// <param name="orientedRectangle">4 corner points defining the oriented rectangle</param>
    /// <returns>Cropped and straightened image</returns>
    public static Image<Rgb24> CropOrientedRectangle(Image<Rgb24> image, List<(int X, int Y)> orientedRectangle)
    {
        if (orientedRectangle == null || orientedRectangle.Count != 4)
            throw new ArgumentException("Oriented rectangle must have exactly 4 points", nameof(orientedRectangle));

        return ExtractOrientedRectangle(image, orientedRectangle);
    }

    private static Image<Rgb24> ExtractOrientedRectangle(Image<Rgb24> sourceImage, List<(int X, int Y)> rectangle)
    {
        // Rectangle corners in counter-clockwise order
        var p0 = rectangle[0]; // Bottom-left
        var p1 = rectangle[1]; // Bottom-right
        var p2 = rectangle[2]; // Top-right
        var p3 = rectangle[3]; // Top-left

        // Calculate rectangle dimensions using proper sides
        double width = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
        double height = Math.Sqrt(Math.Pow(p3.X - p0.X, 2) + Math.Pow(p3.Y - p0.Y, 2));

        int targetWidth = Math.Max(1, (int)Math.Round(width));
        int targetHeight = Math.Max(1, (int)Math.Round(height));

        var result = new Image<Rgb24>(targetWidth, targetHeight);

        // Calculate the rectangle's local coordinate system
        // Unit vector along width (p0 -> p1)
        double ux = (p1.X - p0.X) / width;
        double uy = (p1.Y - p0.Y) / width;

        // Unit vector along height (p0 -> p3)
        double vx = (p3.X - p0.X) / height;
        double vy = (p3.Y - p0.Y) / height;

        // Fill the result image by mapping each target pixel to source coordinates
        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < targetHeight; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < targetWidth; x++)
                {
                    // Map target rectangle coordinates to source image coordinates
                    double srcX = p0.X + x * ux + y * vx;
                    double srcY = p0.Y + x * uy + y * vy;

                    // Sample the source image at the mapped coordinates
                    var color = SampleBilinear(sourceImage, srcX, srcY);
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
}
