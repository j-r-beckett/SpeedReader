using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test;

public class OrientedCroppingTests
{
    [Fact]
    public void CropOrientedRectangle_DimensionConsistency_MatchesCalculatedDimensions()
    {
        // Arrange: Create a test image
        var sourceImage = new Image<Rgb24>(100, 100);

        // Define an oriented rectangle (a 6x4 rectangle rotated)
        var orientedRectangle = new List<(int X, int Y)>
        {
            (10, 10),  // Bottom-left
            (16, 13),  // Bottom-right (width = √((16-10)² + (13-10)²) = √(36+9) = √45 ≈ 6.71)
            (12, 19),  // Top-right
            (6, 16)    // Top-left (height = √((6-10)² + (16-10)²) = √(16+36) = √52 ≈ 7.21)
        };

        // Act
        var croppedImage = OrientedCropping.CropOrientedRectangle(sourceImage, orientedRectangle);

        // Assert: Verify dimensions match the calculated rectangle dimensions
        // Calculate expected dimensions the same way the implementation does
        var p0 = orientedRectangle[0];
        var p1 = orientedRectangle[1];
        var p3 = orientedRectangle[3];

        double expectedWidth = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
        double expectedHeight = Math.Sqrt(Math.Pow(p3.X - p0.X, 2) + Math.Pow(p3.Y - p0.Y, 2));

        int expectedTargetWidth = Math.Max(1, (int)Math.Round(expectedWidth));
        int expectedTargetHeight = Math.Max(1, (int)Math.Round(expectedHeight));

        Console.WriteLine($"Expected dimensions: {expectedTargetWidth}x{expectedTargetHeight}");
        Console.WriteLine($"Actual dimensions: {croppedImage.Width}x{croppedImage.Height}");
        Console.WriteLine($"Calculated width: {expectedWidth:F2}, height: {expectedHeight:F2}");

        Assert.Equal(expectedTargetWidth, croppedImage.Width);
        Assert.Equal(expectedTargetHeight, croppedImage.Height);

        croppedImage.Dispose();
        sourceImage.Dispose();
    }

    [Fact]
    public void CropOrientedRectangle_SimpleAxisAligned_DebugCoordinateMapping()
    {
        // Arrange: Create a 10x10 test image with distinct corner colors
        var sourceImage = new Image<Rgb24>(10, 10);

        // Fill with black, then set specific corner colors
        sourceImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 10; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < 10; x++)
                {
                    row[x] = new Rgb24(0, 0, 0); // Black background
                }
            }
        });

        // Set corner colors in source image
        sourceImage[2, 3] = new Rgb24(255, 0, 0);    // Red at (2, 3)
        sourceImage[6, 3] = new Rgb24(0, 255, 0);    // Green at (6, 3)
        sourceImage[6, 7] = new Rgb24(0, 0, 255);    // Blue at (6, 7)
        sourceImage[2, 7] = new Rgb24(255, 255, 0);  // Yellow at (2, 7)

        // Define axis-aligned rectangle using those exact coordinates
        // According to the implementation comments: bottom-left, bottom-right, top-right, top-left
        var axisAlignedRectangle = new List<(int X, int Y)>
        {
            (2, 3),  // Bottom-left - Red
            (6, 3),  // Bottom-right - Green
            (6, 7),  // Top-right - Blue
            (2, 7)   // Top-left - Yellow
        };

        // Act
        var croppedImage = OrientedCropping.CropOrientedRectangle(sourceImage, axisAlignedRectangle);

        // Assert and Debug
        Console.WriteLine($"Source corner colors:");
        Console.WriteLine($"  (2,3): R={sourceImage[2, 3].R}, G={sourceImage[2, 3].G}, B={sourceImage[2, 3].B} (Red)");
        Console.WriteLine($"  (6,3): R={sourceImage[6, 3].R}, G={sourceImage[6, 3].G}, B={sourceImage[6, 3].B} (Green)");
        Console.WriteLine($"  (6,7): R={sourceImage[6, 7].R}, G={sourceImage[6, 7].G}, B={sourceImage[6, 7].B} (Blue)");
        Console.WriteLine($"  (2,7): R={sourceImage[2, 7].R}, G={sourceImage[2, 7].G}, B={sourceImage[2, 7].B} (Yellow)");

        Console.WriteLine($"\nCropped image dimensions: {croppedImage.Width}x{croppedImage.Height}");
        Console.WriteLine($"Cropped corner colors:");
        Console.WriteLine($"  (0,{croppedImage.Height - 1}): R={croppedImage[0, croppedImage.Height - 1].R}, G={croppedImage[0, croppedImage.Height - 1].G}, B={croppedImage[0, croppedImage.Height - 1].B} (bottom-left)");
        Console.WriteLine($"  ({croppedImage.Width - 1},{croppedImage.Height - 1}): R={croppedImage[croppedImage.Width - 1, croppedImage.Height - 1].R}, G={croppedImage[croppedImage.Width - 1, croppedImage.Height - 1].G}, B={croppedImage[croppedImage.Width - 1, croppedImage.Height - 1].B} (bottom-right)");
        Console.WriteLine($"  ({croppedImage.Width - 1},0): R={croppedImage[croppedImage.Width - 1, 0].R}, G={croppedImage[croppedImage.Width - 1, 0].G}, B={croppedImage[croppedImage.Width - 1, 0].B} (top-right)");
        Console.WriteLine($"  (0,0): R={croppedImage[0, 0].R}, G={croppedImage[0, 0].G}, B={croppedImage[0, 0].B} (top-left)");

        // Expected: width=4, height=4 for a 4x4 rectangle
        Assert.Equal(4, croppedImage.Width);
        Assert.Equal(4, croppedImage.Height);

        croppedImage.Dispose();
        sourceImage.Dispose();
    }

    [Fact]
    public void CropOrientedRectangle_CornerSampling_MapsCorrectly()
    {
        // Arrange: Create a test image with distinct corner colors
        var sourceImage = new Image<Rgb24>(100, 100);

        // Fill with black, then put distinct colors at specific locations
        sourceImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 100; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < 100; x++)
                {
                    row[x] = new Rgb24(0, 0, 0); // Default black
                }
            }
        });

        // Place distinct colors at the corners of our oriented rectangle
        var corners = new List<(int X, int Y, Rgb24 Color)>
        {
            (20, 30, new Rgb24(255, 0, 0)),   // Red at bottom-left
            (35, 20, new Rgb24(0, 255, 0)),   // Green at bottom-right
            (45, 35, new Rgb24(0, 0, 255)),   // Blue at top-right
            (30, 45, new Rgb24(255, 255, 0))  // Yellow at top-left
        };

        foreach (var (x, y, color) in corners)
        {
            // Paint a 3x3 area to make sampling more reliable
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = Math.Clamp(x + dx, 0, 99);
                    int py = Math.Clamp(y + dy, 0, 99);
                    sourceImage[px, py] = color;
                }
            }
        }

        // Define oriented rectangle using the same corners
        var orientedRectangle = new List<(int X, int Y)>
        {
            (20, 30),  // Bottom-left (Red)
            (35, 20),  // Bottom-right (Green)
            (45, 35),  // Top-right (Blue)
            (30, 45)   // Top-left (Yellow)
        };

        // Act
        var croppedImage = OrientedCropping.CropOrientedRectangle(sourceImage, orientedRectangle);

        // Assert: Check that the corners of the cropped image have the expected colors
        Console.WriteLine($"Cropped image dimensions: {croppedImage.Width}x{croppedImage.Height}");

        // Bottom-left of cropped image (0, height-1) should be red
        var bottomLeft = croppedImage[0, croppedImage.Height - 1];
        Console.WriteLine($"Bottom-left: R={bottomLeft.R}, G={bottomLeft.G}, B={bottomLeft.B}");
        Assert.True(bottomLeft.R > 200 && bottomLeft.G < 50 && bottomLeft.B < 50, "Bottom-left should be red");

        // Bottom-right of cropped image (width-1, height-1) should be green
        var bottomRight = croppedImage[croppedImage.Width - 1, croppedImage.Height - 1];
        Console.WriteLine($"Bottom-right: R={bottomRight.R}, G={bottomRight.G}, B={bottomRight.B}");
        Assert.True(bottomRight.R < 50 && bottomRight.G > 200 && bottomRight.B < 50, "Bottom-right should be green");

        // Top-right of cropped image (width-1, 0) should be blue
        var topRight = croppedImage[croppedImage.Width - 1, 0];
        Console.WriteLine($"Top-right: R={topRight.R}, G={topRight.G}, B={topRight.B}");
        Assert.True(topRight.R < 50 && topRight.G < 50 && topRight.B > 200, "Top-right should be blue");

        // Top-left of cropped image (0, 0) should be yellow
        var topLeft = croppedImage[0, 0];
        Console.WriteLine($"Top-left: R={topLeft.R}, G={topLeft.G}, B={topLeft.B}");
        Assert.True(topLeft.R > 200 && topLeft.G > 200 && topLeft.B < 50, "Top-left should be yellow");

        croppedImage.Dispose();
        sourceImage.Dispose();
    }


    [Fact]
    public void DebugCoordinateMapping_CheckSpecificPixels()
    {
        // Create a 10x10 source image with a clear pattern
        var sourceImage = new Image<Rgb24>(10, 10);

        // Fill with a clear pattern: each pixel gets a unique color based on its coordinates
        sourceImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 10; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < 10; x++)
                {
                    // Encode coordinates in RGB: R = x*25, G = y*25, B = 100
                    row[x] = new Rgb24((byte)(x * 25), (byte)(y * 25), 100);
                }
            }
        });

        // Define a simple axis-aligned rectangle
        var rectangle = new List<(int X, int Y)>
        {
            (2, 3),  // Bottom-left
            (5, 3),  // Bottom-right (width=3)
            (5, 6),  // Top-right
            (2, 6)   // Top-left (height=3)
        };

        // Act
        var croppedImage = OrientedCropping.CropOrientedRectangle(sourceImage, rectangle);

        // Assert: Check specific pixel mappings
        Console.WriteLine($"Cropped image dimensions: {croppedImage.Width}x{croppedImage.Height}");
        Console.WriteLine("Source pixel coordinate encoding (R=x*25, G=y*25, B=100):");

        for (int y = 0; y < croppedImage.Height; y++)
        {
            for (int x = 0; x < croppedImage.Width; x++)
            {
                var pixel = croppedImage[x, y];
                int sourceX = pixel.R / 25;
                int sourceY = pixel.G / 25;

                Console.WriteLine($"  Output({x},{y}) -> Source({sourceX},{sourceY}) [R={pixel.R},G={pixel.G},B={pixel.B}]");
            }
        }

        // Expected mappings for axis-aligned rectangle:
        // Output (0,0) should map to source (2,6) (top-left of rectangle)
        // Output (2,2) should map to source (4,4) (somewhere in middle)

        sourceImage.Dispose();
        croppedImage.Dispose();
    }
}
