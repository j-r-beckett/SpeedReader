using CommunityToolkit.HighPerformance;

namespace Ocr.Algorithms;

public static class MorphologicalOps
{
    // 3x3 structuring element (8-connectivity)
    private static readonly (int dx, int dy)[] StructuringElement = {
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    };

    /// <summary>
    /// Performs morphological erosion on the probability map in place.
    /// Erosion shrinks foreground regions and removes thin protrusions.
    /// </summary>
    /// <param name="image">2D probability map to erode</param>
    /// <param name="threshold">Threshold for considering a pixel as foreground (default 0.5)</param>
    public static void ErodeInPlace(Span2D<float> image, float threshold = 0.5f)
    {
        var temp = new float[image.Height * image.Width];
        var tempSpan = temp.AsSpan().AsSpan2D(image.Height, image.Width);
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // Erosion: pixel is foreground only if ALL neighbors are foreground
                bool allNeighborsForeground = image[y, x] >= threshold;
                
                if (allNeighborsForeground)
                {
                    foreach (var (dx, dy) in StructuringElement)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= image.Width || ny < 0 || ny >= image.Height || 
                            image[ny, nx] < threshold)
                        {
                            allNeighborsForeground = false;
                            break;
                        }
                    }
                }
                
                tempSpan[y, x] = allNeighborsForeground ? 1.0f : 0.0f;
            }
        }
        
        // Copy result back to original
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[y, x] = tempSpan[y, x];
            }
        }
    }

    /// <summary>
    /// Performs morphological dilation on the probability map in place.
    /// Dilation expands foreground regions and fills small gaps.
    /// </summary>
    /// <param name="image">2D probability map to dilate</param>
    /// <param name="threshold">Threshold for considering a pixel as foreground (default 0.5)</param>
    public static void DilateInPlace(Span2D<float> image, float threshold = 0.5f)
    {
        var temp = new float[image.Height * image.Width];
        var tempSpan = temp.AsSpan().AsSpan2D(image.Height, image.Width);
        
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // Dilation: pixel is foreground if ANY neighbor is foreground
                bool anyNeighborForeground = image[y, x] >= threshold;
                
                if (!anyNeighborForeground)
                {
                    foreach (var (dx, dy) in StructuringElement)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < image.Width && ny >= 0 && ny < image.Height && 
                            image[ny, nx] >= threshold)
                        {
                            anyNeighborForeground = true;
                            break;
                        }
                    }
                }
                
                tempSpan[y, x] = anyNeighborForeground ? 1.0f : 0.0f;
            }
        }
        
        // Copy result back to original
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[y, x] = tempSpan[y, x];
            }
        }
    }

    /// <summary>
    /// Performs morphological opening (erosion followed by dilation) on the probability map in place.
    /// Opening removes small noise and thin protrusions while preserving the main shape.
    /// </summary>
    /// <param name="image">2D probability map to process</param>
    /// <param name="threshold">Threshold for considering a pixel as foreground (default 0.5)</param>
    public static void OpeningInPlace(Span2D<float> image, float threshold = 0.5f)
    {
        ErodeInPlace(image, threshold);
        DilateInPlace(image, threshold);
    }
}