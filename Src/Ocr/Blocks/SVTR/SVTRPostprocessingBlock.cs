using System.Threading.Tasks.Dataflow;
using Ocr.Algorithms;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.SVTR;

public class SVTRPostprocessingBlock
{
    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)> Target { get; }

    public SVTRPostprocessingBlock()
    {
        Target = new TransformBlock<(float[] RawResult, TextBoundary TextBoundary, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)>(input =>
        {
            var (recognizedText, confidence) = CTC.DecodeSingleSequence(input.RawResult, CharacterDictionary.Count);

            // Add individual recognition result using thread-safe method
            input.VizBuilder.AddRecognitionResult(recognizedText, input.TextBoundary);

            return (recognizedText, confidence, input.TextBoundary, input.OriginalImage, input.VizBuilder);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        });
    }
}
