using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PadTest;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing ResizeMode.Pad behavior:");

        // Create a simple test image programmatically
        var image = new Image<Rgb24>(100, 200); // 100x200 source (tall)

        // Fill with a color so we can see it
        image.Mutate(x => x.Fill(Color.Red));

        Console.WriteLine($"Source image: {image.Width}x{image.Height}");

        // Test 1: Target 300x300 (square, should add horizontal padding)
        using var result1 = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(300, 300),
            Mode = ResizeMode.Pad
        }));
        Console.WriteLine($"Target 300x300 -> Actual: {result1.Width}x{result1.Height}");

        // Test 2: Target 400x200 (wide, should add vertical padding)
        using var result2 = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(400, 200),
            Mode = ResizeMode.Pad
        }));
        Console.WriteLine($"Target 400x200 -> Actual: {result2.Width}x{result2.Height}");

        // Test 3: Target 50x100 (smaller but same aspect ratio)
        using var result3 = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(50, 100),
            Mode = ResizeMode.Pad
        }));
        Console.WriteLine($"Target 50x100 -> Actual: {result3.Width}x{result3.Height}");

        // Test 4: Target 150x75 (different aspect ratio)
        using var result4 = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(150, 75),
            Mode = ResizeMode.Pad
        }));
        Console.WriteLine($"Target 150x75 -> Actual: {result4.Width}x{result4.Height}");

        image.Dispose();
    }
}