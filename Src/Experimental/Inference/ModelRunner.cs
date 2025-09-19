// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;

namespace Experimental.Inference;

public abstract class ModelRunner : IAsyncDisposable
{
    private readonly InferenceSession _inferenceSession;

    protected ModelRunner(InferenceSession inferenceSession) => _inferenceSession = inferenceSession;

    public abstract Task<(float[] Data, int[] Shape)> Run(float[] batch, int[] shape);

    protected virtual (float[] Data, int[] Shape) RunInferenceInternal(float[] batch, int[] shape)
    {
        Debug.Assert(shape.Length > 2);  // 1 batch size dimension, at least one other dimension

        var longShape = shape.Select(d => (long)d).ToArray();  // convert int[] -> long[]

        // try/finally to prevent leaking unmanaged ONNX tensors
        OrtValue? inputTensor = null;
        IDisposableReadOnlyCollection<OrtValue>? outputTensors = null;
        float[] output;
        int[] outputShape;
        try
        {
            inputTensor = OrtValue.CreateTensorValueFromMemory(batch, longShape);  // Instantiate ONNX tensor
            var inputs = new Dictionary<string, OrtValue> { { "input", inputTensor } };
            using var runOptions = new RunOptions();
            outputTensors = _inferenceSession.Run(runOptions, inputs, _inferenceSession.OutputNames); // Run inference
            var outputTensor = outputTensors[0]; // One input, so one output
            outputShape = outputTensor.GetTensorTypeAndShape().Shape.Select(l => (int)l).ToArray(); // Convert output shape from long[] -> int[]
            var outputSize = outputShape.Aggregate(1, (prod, next) => prod * next); // Multiply all shape dimensions together to get total element count
            output = new float[outputSize]; // Instantiate an output buffer
            outputTensor.GetTensorDataAsSpan<float>().CopyTo(output); // Copy to output buffer
        }
        finally
        {
            inputTensor?.Dispose();
            outputTensors?.Dispose();
        }

        return (output, outputShape);
    }

    protected abstract ValueTask SubclassDisposeAsync();

    public async ValueTask DisposeAsync()
    {
        await SubclassDisposeAsync();

        GC.SuppressFinalize(this);
    }

}
