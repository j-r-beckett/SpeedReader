using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TextDetection;

public readonly struct DbNetImage
{
    private readonly ReadOnlyMemory<float> _normalizedData;
    public ReadOnlySpan<float> Data => _normalizedData.Span;
    public int Width { get; }
    public int Height { get; }

    private DbNetImage(ReadOnlyMemory<float> data, int width, int height)
    {
        _normalizedData = data;
        Width = width;
        Height = height;
    }

    public static DbNetImage Create(Image<Rgb24> image)
    {
        return new DbNetImage(new Memory<float>(), 0, 0);
    }
}
