// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Native;
using Resources.CharDict;
using Resources.Weights;

namespace Ocr.InferenceEngine;


public class NativeOnnxInferenceKernel : IInferenceKernel, IDisposable
{
    private static readonly Lazy<SafeEnvironmentHandle> _sharedEnvironment = new(CreateEnvironment);
    private readonly SafeSessionHandle _session;
    private readonly Model _model;
    private readonly int _vocabSize;

    public static NativeOnnxInferenceKernel Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<OnnxInferenceKernelOptions>(key);
        var modelWeights = serviceProvider.GetRequiredKeyedService<EmbeddedWeights>(key);
        var charDict = serviceProvider.GetService<EmbeddedCharDict>();
        return new NativeOnnxInferenceKernel(options, modelWeights, charDict);
    }

    protected NativeOnnxInferenceKernel(
        OnnxInferenceKernelOptions inferenceOptions,
        EmbeddedWeights weights,
        EmbeddedCharDict? charDict = null)
    {
        _model = inferenceOptions.Model;

        if (_model == Model.Svtr && charDict is null)
            throw new ArgumentException("EmbeddedCharDict is required for SVTR model", nameof(charDict));

        _vocabSize = charDict?.Count ?? 0;

        // Convert options to native format
        var nativeOptions = new SpeedReaderOrt.SessionOptions
        {
            IntraOpNumThreads = inferenceOptions.NumIntraOpThreads,
            InterOpNumThreads = inferenceOptions.NumInterOpThreads,
            EnableProfiling = inferenceOptions.EnableProfiling ? 1 : 0
        };

        // Create session (triggers lazy environment initialization on first use)
        _session = CreateSession(_sharedEnvironment.Value, weights.Bytes, nativeOptions);
    }

    public virtual (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        try
        {
            return ExecuteInternal(data, shape);
        }
        catch (Exception ex)
        {
            throw new OnnxInferenceException("An exception was thrown during native ONNX inference", ex);
        }
    }

    private unsafe (float[], int[]) ExecuteInternal(float[] inputData, int[] inputShape)
    {
        // Calculate expected output shape and size
        var expectedOutputShape = CalculateOutputShape(inputShape);
        var expectedOutputCount = 1;
        foreach (var dim in expectedOutputShape)
            expectedOutputCount *= dim;

        // Allocate output data array
        var outputData = new float[expectedOutputCount];

        // Stackalloc fixed-size buffers
        var outputShapeBuffer = stackalloc long[SpeedReaderOrt.MaxShapeDims];
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];
        nuint outputNdim = 0;

        // Convert input shape to long array
        Span<long> inputShapeLong = stackalloc long[inputShape.Length];
        for (int i = 0; i < inputShape.Length; i++)
            inputShapeLong[i] = inputShape[i];

        fixed (float* inputDataPtr = inputData)
        fixed (long* inputShapePtr = inputShapeLong)
        fixed (float* outputDataPtr = outputData)
        {
            var status = SpeedReaderOrt.speedreader_ort_run(
                _session,
                inputDataPtr,
                inputShapePtr,
                (nuint)inputShape.Length,
                outputDataPtr,
                (nuint)expectedOutputCount,
                outputShapeBuffer,
                &outputNdim,
                errorBuffer);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
                throw new InvalidOperationException($"Native ONNX inference failed: {errorMessage}");
            }
        }

        // Convert output shape to int array
        var outputShape = new int[outputNdim];
        for (int i = 0; i < (int)outputNdim; i++)
            outputShape[i] = (int)outputShapeBuffer[i];

        return (outputData, outputShape);
    }

    private int[] CalculateOutputShape(int[] inputShape) =>
        (_model, inputShape) switch
        {
            // DBNet: [n, 3, h, w] -> [n, h, w]
            (Model.DbNet, [var n, _, var h, var w]) => [n, h, w],
            // SVTR: [n, 3, 48, w] -> [n, w/8, vocab_size]
            (Model.Svtr, [var n, _, _, var w]) => [n, w / 8, _vocabSize],
            _ => throw new ArgumentException($"Unexpected input shape for model {_model}: [{string.Join(", ", inputShape)}]")
        };

    private static unsafe SafeEnvironmentHandle CreateEnvironment()
    {
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];

        var status = SpeedReaderOrt.speedreader_ort_create_env(out var env, errorBuffer);

        if (status != SpeedReaderOrt.Status.Ok)
        {
            var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
            throw new InvalidOperationException($"Failed to create ONNX Runtime environment: {errorMessage}");
        }

        return env;
    }

    private static unsafe SafeSessionHandle CreateSession(
        SafeEnvironmentHandle env,
        byte[] modelData,
        SpeedReaderOrt.SessionOptions options)
    {
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];

        fixed (byte* modelDataPtr = modelData)
        {
            var status = SpeedReaderOrt.speedreader_ort_create_session(
                env,
                modelDataPtr,
                (nuint)modelData.Length,
                ref options,
                out var session,
                errorBuffer);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
                throw new InvalidOperationException($"Failed to create ONNX Runtime session: {errorMessage}");
            }

            return session;
        }
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class OnnxInferenceException : Exception
{
    public OnnxInferenceException(string message, Exception innerException) : base(message, innerException) { }
}
