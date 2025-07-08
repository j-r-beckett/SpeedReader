using System.Numerics.Tensors;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

public static class SVTRv2
{
    private const int TargetHeight = 48;
    private const int MinWidth = 12;
    private const int MaxWidth = 320;



    public static float[] PreProcessSingle(Image<Rgb24> image, List<Rectangle> rectangles)
    {
        // Use fixed dimensions for all text regions
        const int fixedHeight = 48;
        const int fixedWidth = 320;  // Maximum width
        
        int totalRectangles = rectangles.Count;
        
        // Create buffer to match the batch preprocessing approach
        var buffer = new Buffer<float>([totalRectangles, fixedHeight, fixedWidth, 3]);
        
        // Process each rectangle
        for (int i = 0; i < rectangles.Count; i++)
        {
            var rect = rectangles[i];
            int targetWidth = CalculateTargetWidth(rect);
            
            var dest = buffer.AsSpan().Slice(i * fixedHeight * fixedWidth * 3, fixedHeight * fixedWidth * 3);
            Resampling.CropResizeInto(image, rect, dest, fixedWidth, fixedHeight, targetWidth);
        }
        
        // Convert to NCHW format (just like batch preprocessing)
        TensorOps.NhwcToNchw(buffer);
        
        // Apply SVTRv2 normalization: [0,255] → [-1,1]
        var tensor = buffer.AsTensor();
        Tensor.Divide(tensor, 127.5f, tensor);
        Tensor.Subtract(tensor, 1.0f, tensor);
        
        // Return as float array
        var result = buffer.AsSpan().ToArray();
        buffer.Dispose();
        return result;
    }

    public static string[] PostProcessSingle(float[] modelOutput, int numRectangles)
    {
        // Model output dimensions from SVTRv2
        int sequenceLength = (int)modelOutput.Length / numRectangles / 6625;  // Assuming 6625 vocab size
        int numClasses = 6625;
        
        var results = new List<string>();
        
        for (int i = 0; i < numRectangles; i++)
        {
            // Each rectangle's output data: [sequence_length, num_classes]
            var regionSpan = modelOutput.AsSpan().Slice(i * sequenceLength * numClasses, sequenceLength * numClasses);
            string text = CTC.DecodeSingleSequence(regionSpan, sequenceLength, numClasses);
            results.Add(text);
        }
        
        return results.ToArray();
    }

    internal static int CalculateTargetWidth(Rectangle rect)
    {
        // Calculate target width maintaining aspect ratio of the cropped region
        double aspectRatio = (double)rect.Width / rect.Height;
        int targetWidth = (int)Math.Round(aspectRatio * TargetHeight);

        // Clamp to reasonable bounds
        return Math.Max(MinWidth, Math.Min(MaxWidth, targetWidth));
    }


}
