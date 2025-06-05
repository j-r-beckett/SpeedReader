using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public static class ModelRunner
{
    public static Tensor<float> Run(InferenceSession session, Tensor<float> input)
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

        // Convert OrtValue to Tensor<float>
        var outputSpan = firstOutput.GetTensorDataAsSpan<float>();
        var outputShape = firstOutput.GetTensorTypeAndShape().Shape;
        var outputData = outputSpan.ToArray();
        ReadOnlySpan<nint> tensorShape = outputShape.Select(x => (nint)x).ToArray();

        return Tensor.Create(outputData, tensorShape);
    }
}