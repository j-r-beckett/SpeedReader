using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TextDetection;

public class TextDetectorOutput
{
    public required float[,] ProbabilityMap { get; init; }

    public Image<Rgb24> RenderAsGreyscale()
    {
        int height = ProbabilityMap.GetLength(0);
        int width = ProbabilityMap.GetLength(1);

        var image = new Image<Rgb24>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float probability = ProbabilityMap[y, x];
                byte greyValue = (byte)(probability * 255f);
                image[x, y] = new Rgb24(greyValue, greyValue, greyValue);
            }
        }

        return image;
    }
}
