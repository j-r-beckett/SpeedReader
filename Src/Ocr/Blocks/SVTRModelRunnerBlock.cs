using System.Numerics.Tensors;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Ocr.Visualization;

namespace Ocr.Blocks;

public class SVTRModelRunnerBlock
{
    private readonly int _width;
    private readonly int _height;
    private readonly InferenceSession _session;

    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)> Target { get; }

    public SVTRModelRunnerBlock(InferenceSession session, SvtrConfiguration config)
    {
        _session = session;
        _width = config.Width;
        _height = config.Height;

        Target = new TransformBlock<(float[] ProcessedRegion, TextBoundary TextBoundary, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)>(input =>
        {
            // Model input should be [1, 3, height, width] - single rectangle, 3 channels, configured dimensions
            var inputTensor = Tensor.Create(input.ProcessedRegion, [1, 3, _height, _width]);

            var outputBuffer = ModelRunner.Run(_session, inputTensor);

            float[] outputData = outputBuffer.AsSpan().ToArray();
            outputBuffer.Dispose();

            return (outputData, input.TextBoundary, input.OriginalImage, input.VizBuilder);
        });
    }
}