// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

// LEGACY: Original managed ONNX Runtime implementation.
// Replaced by NativeOnnxInferenceKernel using statically-linked native library.
// This code is kept for reference but should not be used in production.

#if false

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.InferenceEngine;

public class OnnxInferenceKernel : IInferenceKernel
{
    private readonly InferenceSession _inferenceSession;

    public static OnnxInferenceKernel Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<OnnxInferenceKernelOptions>(key);
        var modelLoader = serviceProvider.GetRequiredService<ModelLoader>();
        return new OnnxInferenceKernel(options, modelLoader);
    }

    protected OnnxInferenceKernel(OnnxInferenceKernelOptions inferenceOptions, ModelLoader modelLoader)
    {
        // By default:
        // - execution mode is ORT_SEQUENTIAL
        // - memory arena is enabled
        // - all graph optimizations are enabled
        var options = new SessionOptions
        {
            IntraOpNumThreads = inferenceOptions.NumIntraOpThreads,
            InterOpNumThreads = inferenceOptions.NumInterOpThreads,
            EnableProfiling = inferenceOptions.EnableProfiling
        };

        var weights = modelLoader.LoadModel(inferenceOptions.Model, inferenceOptions.Quantization);
        _inferenceSession = new InferenceSession(weights, options);
    }

    public virtual (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        try
        {
            return ExecuteInternal();
        }
        catch (Exception ex)
        {
            throw new OnnxInferenceException("An exception was thrown during Onnx inference", ex);
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

#endif

public class OnnxInferenceException : Exception
{
    public OnnxInferenceException(string message, Exception innerException) : base(message, innerException) { }
}
