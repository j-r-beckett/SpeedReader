using System.Numerics.Tensors;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public class TextDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<TextDetector> _logger;

    public TextDetector(InferenceSession session, ILogger<TextDetector> logger)
    {
        _session = session;
        _logger = logger;
    }

    public Tensor<float> RunTextDetection(Tensor<float> input)
    {
        float[] inputBuffer = new float[input.FlattenedLength];
        input.FlattenTo(inputBuffer);
        long[] shape = Array.ConvertAll(input.Lengths.ToArray(), x => (long)x);
        using var inputOrtValue = OrtValue.CreateTensorValueFromMemory(inputBuffer, shape);

        var inputs = new Dictionary<string, OrtValue>
        {
            { "input", inputOrtValue }
        };

        using var runOptions = new RunOptions();
        using var ortOutputs = _session.Run(runOptions, inputs, _session.OutputNames);
        var firstOutput = ortOutputs.First();

        // Convert OrtValue to Tensor<float>
        var outputSpan = firstOutput.GetTensorDataAsSpan<float>();
        var outputShape = firstOutput.GetTensorTypeAndShape().Shape;
        var outputData = outputSpan.ToArray();
        ReadOnlySpan<nint> tensorShape = outputShape.Select(x => (nint)x).ToArray();

        return Tensor.Create(outputData, tensorShape);
    }


    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
