using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;

namespace OCR;

public static class ModelRunner
{
    public static Buffer<float> Run(InferenceSession session, Tensor<float> input)
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
        using var ortOutputs = session.Run(runOptions, inputs, session.OutputNames);
        var firstOutput = ortOutputs.First();

        // Convert OrtValue to Buffer<float>
        var outputSpan = firstOutput.GetTensorDataAsSpan<float>();
        var outputShape = firstOutput.GetTensorTypeAndShape().Shape;
        var buffer = new Buffer<float>(outputSpan.Length, outputShape.Select(x => (nint)x).ToArray());
        outputSpan.CopyTo(buffer.AsSpan());

        return buffer;
    }
}
