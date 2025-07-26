using System.Threading.Tasks.Dataflow;
using Ocr.Algorithms;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public class SVTRPostprocessingBlock
{
    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)> Target { get; }

    public SVTRPostprocessingBlock()
    {
        Target = new TransformBlock<(float[] RawResult, TextBoundary TextBoundary, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)>(input =>
        {
            var (recognizedText, confidence) = PostProcess(input.RawResult);

            // Add individual recognition result using thread-safe method
            input.VizBuilder.AddRecognitionResult(recognizedText, input.TextBoundary);

            return (recognizedText, confidence, input.TextBoundary, input.OriginalImage, input.VizBuilder);
        });
    }

    private static (string text, double confidence) PostProcess(float[] modelOutput)
    {
        return CTC.DecodeSingleSequence(modelOutput, CharacterDictionary.Count);
    }
}
