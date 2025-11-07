// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.Kernels;

public class OnnxInferenceKernel : IInferenceKernel
{
    private readonly InferenceSession _inferenceSession;

    public OnnxInferenceKernel(InferenceSession inferenceSession) => _inferenceSession = inferenceSession;

    public (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        try
        {
            return ExecuteInternal();
        }
        catch (Exception ex)
        {
            throw new InferenceException("An exception was thrown during inference", ex);
        }

        (float[], int[]) ExecuteInternal()
        {
            Debug.Assert(shape.Length > 2);  // 1 batch size dimension, at least one other dimension

            var longShape = shape.Select(d => (long)d).ToArray();  // Convert int[] -> long[]

            // try/finally to avoid leaking unmanaged ONNX tensors
            OrtValue? inputTensor = null;
            IDisposableReadOnlyCollection<OrtValue>? outputTensors = null;
            float[] output;
            int[] outputShape;
            try
            {
                // Instantiate ONNX tensor
                inputTensor = OrtValue.CreateTensorValueFromMemory(data, longShape);
                var inputs = new Dictionary<string, OrtValue> { { "input", inputTensor } };

                // Run inference
                using var runOptions = new RunOptions();
                outputTensors = _inferenceSession.Run(runOptions, inputs, _inferenceSession.OutputNames);

                // One input tensor, so one output tensor
                var outputTensor = outputTensors[0];

                // Convert output shape from long[] -> int[]
                outputShape = outputTensor.GetTensorTypeAndShape().Shape.Select(l => (int)l).ToArray();

                // Multiply all shape dimensions together to get the total element count
                var outputSize = outputShape.Aggregate(1, (prod, next) => prod * next);

                // Instantiate the output buffer and copy results into it
                output = new float[outputSize];
                outputTensor.GetTensorDataAsSpan<float>().CopyTo(output);
            }
            finally
            {
                inputTensor?.Dispose();
                outputTensors?.Dispose();
            }

            return (output, outputShape);
        }
    }
}

public class InferenceException : Exception
{
    public InferenceException(string message, Exception innerException) : base(message, innerException) { }
}
