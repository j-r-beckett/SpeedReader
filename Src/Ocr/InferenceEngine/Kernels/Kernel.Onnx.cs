// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.InferenceEngine.Kernels;

public record OnnxInferenceKernelOptions : InferenceKernelOptions
{
    public OnnxInferenceKernelOptions(Model model, Quantization quantization, int initialParallelism, int numIntraOpThreads,
        int numInterOpThreads = 1, bool enableProfiling = false)
        : base(model, quantization)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialParallelism, 1, nameof(initialParallelism));
        ArgumentOutOfRangeException.ThrowIfLessThan(numIntraOpThreads, 1, nameof(numIntraOpThreads));
        ArgumentOutOfRangeException.ThrowIfLessThan(numInterOpThreads, 1, nameof(numInterOpThreads));

        NumIntraOpThreads = numIntraOpThreads;
        NumInterOpThreads = numInterOpThreads;
        EnableProfiling = enableProfiling;
    }

    public int NumIntraOpThreads { get; }
    public int NumInterOpThreads { get; }
    public bool EnableProfiling { get; }
}

public class OnnxInferenceKernel : IInferenceKernel
{
    private readonly InferenceSession _inferenceSession;

    /// <summary>
    /// Factory method for creating OnnxInferenceKernel from DI container with keyed services.
    /// </summary>
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
            throw new OnnxInferenceException("An exception was thrown during ONNX inference", ex);
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

public class OnnxInferenceException : InferenceKernelException
{
    public OnnxInferenceException(string message, Exception innerException) : base(message, innerException) { }
}
