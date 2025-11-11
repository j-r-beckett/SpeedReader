// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Test;

public class AspectResizeTests
{
    public class AspectResizeTestCase
    {
        public required string Name { get; init; }
        public required Size SourceSize { get; init; }
        public required Rectangle RedRect { get; init; }
        public required Size TargetSize { get; init; }
    }

    public class AspectResizeTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Width-constrained upscaling",
                    SourceSize = new Size(200, 100),
                    RedRect = new Rectangle(40, 20, 120, 60),
                    TargetSize = new Size(100, 200)
                }
            ];

            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Height-constrained upscaling",
                    SourceSize = new Size(100, 200),
                    RedRect = new Rectangle(20, 40, 60, 120),
                    TargetSize = new Size(200, 100)
                }
            ];

            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Width-constrained downscaling",
                    SourceSize = new Size(400, 200),
                    RedRect = new Rectangle(80, 40, 240, 120),
                    TargetSize = new Size(100, 200)
                }
            ];

            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Height-constrained downscaling",
                    SourceSize = new Size(200, 400),
                    RedRect = new Rectangle(40, 80, 120, 240),
                    TargetSize = new Size(200, 100)
                }
            ];

            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Equal upscaling",
                    SourceSize = new Size(100, 100),
                    RedRect = new Rectangle(20, 20, 60, 60),
                    TargetSize = new Size(200, 200)
                }
            ];

            yield return
            [
                new AspectResizeTestCase
                {
                    Name = "Equal downscaling",
                    SourceSize = new Size(200, 200),
                    RedRect = new Rectangle(40, 40, 120, 120),
                    TargetSize = new Size(100, 100)
                }
            ];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Image<Rgb24> CreateTestImage(int width, int height, int redX, int redY, int redWidth, int redHeight)
    {
        var image = new Image<Rgb24>(width, height, new Rgb24(255, 255, 255));

        for (int y = redY; y < redY + redHeight; y++)
        {
            for (int x = redX; x < redX + redWidth; x++)
            {
                image[x, y] = new Rgb24(255, 0, 0);
            }
        }

        return image;
    }

    [Theory]
    [ClassData(typeof(AspectResizeTestData))]
    public void HardAspectResize_PreservesAspectRatio(AspectResizeTestCase testCase)
    {
        // Arrange
        var src = CreateTestImage(
            testCase.SourceSize.Width, testCase.SourceSize.Height,
            testCase.RedRect.X, testCase.RedRect.Y,
            testCase.RedRect.Width, testCase.RedRect.Height);

        // Act
        using var result = src.HardAspectResize(testCase.TargetSize);

        // Assert
        var scaleX = (double)testCase.TargetSize.Width / testCase.SourceSize.Width;
        var scaleY = (double)testCase.TargetSize.Height / testCase.SourceSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        var expectedRedWidth = (int)Math.Round(testCase.RedRect.Width * scale);
        var expectedRedHeight = (int)Math.Round(testCase.RedRect.Height * scale);

        var (actualRedWidth, actualRedHeight) = MeasureRectangle(result,
            p => p.R > 250 && p.G < 5 && p.B < 5);  // Is red

        Assert.InRange(actualRedWidth, expectedRedWidth - 2, expectedRedWidth + 2);
        Assert.InRange(actualRedHeight, expectedRedHeight - 2, expectedRedHeight + 2);

        var expectedCompositeWidth = (int)Math.Round(testCase.SourceSize.Width * scale);
        var expectedCompositeHeight = (int)Math.Round(testCase.SourceSize.Height * scale);

        var (actualCompositeWidth, actualCompositeHeight) = MeasureRectangle(result,
            p => !(p.R < 5 && p.G < 5 && p.B < 5));  // Is not black

        Assert.InRange(actualCompositeWidth, expectedCompositeWidth - 2, expectedCompositeWidth + 2);
        Assert.InRange(actualCompositeHeight, expectedCompositeHeight - 2, expectedCompositeHeight + 2);

        Assert.Equal(testCase.TargetSize.Width, result.Width);
        Assert.Equal(testCase.TargetSize.Height, result.Height);

        var topLeftPixel = result[0, 0];
        var isWhite = topLeftPixel.R > 250 && topLeftPixel.G > 250 && topLeftPixel.B > 250;
        var isRed = topLeftPixel.R > 250 && topLeftPixel.G < 5 && topLeftPixel.B < 5;
        Assert.True(isWhite || isRed, $"Pixel at (0,0) should be white or red (top-left positioning), but was R={topLeftPixel.R} G={topLeftPixel.G} B={topLeftPixel.B}");
    }

    [Fact]
    public void HardAspectResize_Identity_ExactEquality()
    {
        // Arrange
        var src = CreateTestImage(100, 100, 20, 20, 60, 60);
        var targetSize = new Size(100, 100);

        // Act
        using var result = src.HardAspectResize(targetSize);

        // Assert
        var (actualRedWidth, actualRedHeight) = MeasureRectangle(result,
            p => p.R > 250 && p.G < 5 && p.B < 5);

        Assert.Equal(60, actualRedWidth);
        Assert.Equal(60, actualRedHeight);

        var (actualCompositeWidth, actualCompositeHeight) = MeasureRectangle(result,
            p => !(p.R < 5 && p.G < 5 && p.B < 5));

        Assert.Equal(100, actualCompositeWidth);
        Assert.Equal(100, actualCompositeHeight);

        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void HardAspectResize_PaddingIsBlack()
    {
        // Arrange
        var src = CreateTestImage(100, 50, 20, 10, 60, 30);
        var targetSize = new Size(100, 100);

        // Act
        using var result = src.HardAspectResize(targetSize);

        // Assert
        for (int x = 0; x < 100; x++)
        {
            Assert.Equal(new Rgb24(0, 0, 0), result[x, 75]);
            Assert.Equal(new Rgb24(0, 0, 0), result[x, 99]);
        }
    }

    [Fact]
    public void HardAspectResize_1x1Source()
    {
        // Arrange
        var src = new Image<Rgb24>(1, 1, new Rgb24(255, 0, 0));
        var targetSize = new Size(10, 10);

        // Act
        using var result = src.HardAspectResize(targetSize);

        // Assert
        Assert.Equal(10, result.Width);
        Assert.Equal(10, result.Height);
    }

    [Fact]
    public void HardAspectResize_1x1Target()
    {
        // Arrange
        var src = CreateTestImage(100, 100, 20, 20, 60, 60);
        var targetSize = new Size(1, 1);

        // Act
        using var result = src.HardAspectResize(targetSize);

        // Assert
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    private static (int Width, int Height) MeasureRectangle(Image<Rgb24> image, Func<Rgb24, bool> predicate)
    {
        var rowStripLengths = new List<int>();

        for (int y = 0; y < image.Height; y++)
        {
            int stripLength = 0;
            int stripCount = 0;

            for (int x = 0; x < image.Width; x++)
            {
                if (predicate(image[x, y]))
                {
                    stripLength++;
                }
                else if (stripLength > 0)
                {
                    if (stripLength > 5)
                    {
                        stripCount++;
                        rowStripLengths.Add(stripLength);
                    }
                    stripLength = 0;
                }
            }

            if (stripLength > 5)
            {
                stripCount++;
                rowStripLengths.Add(stripLength);
            }

            if (stripCount > 1)
            {
                Assert.Fail($"Row {y} has {stripCount} strips > 5 pixels, expected 0 or 1");
            }
        }

        var significantRowStrips = rowStripLengths.Where(len => len > 5).ToList();
        if (significantRowStrips.Count == 0)
        {
            return (0, 0);
        }

        var minRowStrip = significantRowStrips.Min();
        var maxRowStrip = significantRowStrips.Max();
        Assert.True(maxRowStrip - minRowStrip <= 4,
            $"Row strip lengths vary too much: min={minRowStrip}, max={maxRowStrip}. Not a rectangle.");

        var avgWidth = (int)Math.Round(significantRowStrips.Average());

        var colStripLengths = new List<int>();

        for (int x = 0; x < image.Width; x++)
        {
            int stripLength = 0;
            int stripCount = 0;

            for (int y = 0; y < image.Height; y++)
            {
                if (predicate(image[x, y]))
                {
                    stripLength++;
                }
                else if (stripLength > 0)
                {
                    if (stripLength > 5)
                    {
                        stripCount++;
                        colStripLengths.Add(stripLength);
                    }
                    stripLength = 0;
                }
            }

            if (stripLength > 5)
            {
                stripCount++;
                colStripLengths.Add(stripLength);
            }

            if (stripCount > 1)
            {
                Assert.Fail($"Column {x} has {stripCount} strips > 5 pixels, expected 0 or 1");
            }
        }

        var significantColStrips = colStripLengths.Where(len => len > 5).ToList();
        var minColStrip = significantColStrips.Min();
        var maxColStrip = significantColStrips.Max();
        Assert.True(maxColStrip - minColStrip <= 4,
            $"Column strip lengths vary too much: min={minColStrip}, max={maxColStrip}. Not a rectangle.");

        var avgHeight = (int)Math.Round(significantColStrips.Average());

        return (avgWidth, avgHeight);
    }
}
