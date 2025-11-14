// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Ocr.InferenceEngine.Native;

namespace Ocr.InferenceEngine;


public class NativeOnnxInferenceKernel : IInferenceKernel, IDisposable
{
    private static readonly Lazy<SafeEnvironmentHandle> _sharedEnvironment = new(CreateEnvironment);
    private readonly SafeSessionHandle _session;

    private const int ErrorBufferSize = 512;
    private const int MaxOutputShapeDimensions = 10;
    private const int InitialOutputCapacity = 500_000;  // ~2MB for float arrays

    public static NativeOnnxInferenceKernel Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<OnnxInferenceKernelOptions>(key);
        var modelLoader = serviceProvider.GetRequiredService<ModelLoader>();
        return new NativeOnnxInferenceKernel(options, modelLoader);
    }

    protected NativeOnnxInferenceKernel(OnnxInferenceKernelOptions inferenceOptions, ModelLoader modelLoader)
    {
        var weights = modelLoader.LoadModel(inferenceOptions.Model, inferenceOptions.Quantization);

        // Convert options to native format
        var nativeOptions = new SpeedReaderOrt.SessionOptions
        {
            IntraOpNumThreads = inferenceOptions.NumIntraOpThreads,
            InterOpNumThreads = inferenceOptions.NumInterOpThreads,
            EnableProfiling = inferenceOptions.EnableProfiling ? 1 : 0
        };

        // Create session (triggers lazy environment initialization on first use)
        _session = CreateSession(_sharedEnvironment.Value, weights, nativeOptions);
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

    private (float[], int[]) ExecuteInternal(float[] inputData, int[] inputShape)
    {
        Debug.Assert(inputShape.Length > 2);  // 1 batch dimension, at least one other dimension

        // Pin input data and shape arrays for native access
        var inputDataHandle = GCHandle.Alloc(inputData, GCHandleType.Pinned);
        var inputShapeArray = inputShape.Select(d => (long)d).ToArray();
        var inputShapeHandle = GCHandle.Alloc(inputShapeArray, GCHandleType.Pinned);

        try
        {
            // Allocate output buffers (will resize if needed)
            var outputDataArray = new float[InitialOutputCapacity];
            var outputShapeArray = new long[MaxOutputShapeDimensions];
            var errorBuffer = new byte[ErrorBufferSize];

            var outputDataHandle = GCHandle.Alloc(outputDataArray, GCHandleType.Pinned);
            var outputShapeHandle = GCHandle.Alloc(outputShapeArray, GCHandleType.Pinned);
            var errorHandle = GCHandle.Alloc(errorBuffer, GCHandleType.Pinned);

            try
            {
                var outputDataBuf = new SpeedReaderOrt.FloatBuf
                {
                    Buffer = outputDataHandle.AddrOfPinnedObject(),
                    Capacity = (nuint)outputDataArray.Length,
                    Length = 0
                };

                var outputShapeBuf = new SpeedReaderOrt.Int64Buf
                {
                    Buffer = outputShapeHandle.AddrOfPinnedObject(),
                    Capacity = (nuint)outputShapeArray.Length,
                    Length = 0
                };

                var errorBuf = new SpeedReaderOrt.StringBuf
                {
                    Buffer = errorHandle.AddrOfPinnedObject(),
                    Capacity = (nuint)errorBuffer.Length,
                    Length = 0
                };

                // Execute inference
                var status = SpeedReaderOrt.speedreader_ort_run(
                    _session,
                    inputDataHandle.AddrOfPinnedObject(),
                    inputShapeHandle.AddrOfPinnedObject(),
                    (nuint)inputShapeArray.Length,
                    ref outputDataBuf,
                    ref outputShapeBuf,
                    ref errorBuf);

                if (status == SpeedReaderOrt.Status.Truncated)
                {
                    // Buffer was too small, retry with correct size
                    outputDataHandle.Free();
                    outputShapeHandle.Free();

                    outputDataArray = new float[outputDataBuf.Length];
                    outputShapeArray = new long[outputShapeBuf.Length];

                    outputDataHandle = GCHandle.Alloc(outputDataArray, GCHandleType.Pinned);
                    outputShapeHandle = GCHandle.Alloc(outputShapeArray, GCHandleType.Pinned);

                    outputDataBuf.Buffer = outputDataHandle.AddrOfPinnedObject();
                    outputDataBuf.Capacity = (nuint)outputDataArray.Length;
                    outputDataBuf.Length = 0;

                    outputShapeBuf.Buffer = outputShapeHandle.AddrOfPinnedObject();
                    outputShapeBuf.Capacity = (nuint)outputShapeArray.Length;
                    outputShapeBuf.Length = 0;

                    // Retry
                    status = SpeedReaderOrt.speedreader_ort_run(
                        _session,
                        inputDataHandle.AddrOfPinnedObject(),
                        inputShapeHandle.AddrOfPinnedObject(),
                        (nuint)inputShapeArray.Length,
                        ref outputDataBuf,
                        ref outputShapeBuf,
                        ref errorBuf);
                }

                if (status != SpeedReaderOrt.Status.Ok)
                {
                    var errorMessage = System.Text.Encoding.UTF8.GetString(errorBuffer, 0, (int)errorBuf.Length);
                    throw new InvalidOperationException($"Native ONNX inference failed: {errorMessage}");
                }

                // Copy results to correctly sized arrays
                var outputData = new float[outputDataBuf.Length];
                Array.Copy(outputDataArray, outputData, (int)outputDataBuf.Length);

                var outputShape = new int[outputShapeBuf.Length];
                for (int i = 0; i < outputShape.Length; i++)
                {
                    outputShape[i] = (int)outputShapeArray[i];
                }

                return (outputData, outputShape);
            }
            finally
            {
                if (outputDataHandle.IsAllocated)
                    outputDataHandle.Free();
                if (outputShapeHandle.IsAllocated)
                    outputShapeHandle.Free();
                if (errorHandle.IsAllocated)
                    errorHandle.Free();
            }
        }
        finally
        {
            inputDataHandle.Free();
            inputShapeHandle.Free();
        }
    }

    private static SafeEnvironmentHandle CreateEnvironment()
    {
        var errorBuffer = new byte[ErrorBufferSize];
        var errorHandle = GCHandle.Alloc(errorBuffer, GCHandleType.Pinned);

        try
        {
            var errorBuf = new SpeedReaderOrt.StringBuf
            {
                Buffer = errorHandle.AddrOfPinnedObject(),
                Capacity = (nuint)errorBuffer.Length,
                Length = 0
            };

            var status = SpeedReaderOrt.speedreader_ort_create_env(out var env, ref errorBuf);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = System.Text.Encoding.UTF8.GetString(errorBuffer, 0, (int)errorBuf.Length);
                throw new InvalidOperationException($"Failed to create ONNX Runtime environment: {errorMessage}");
            }

            return env;
        }
        finally
        {
            errorHandle.Free();
        }
    }

    private static SafeSessionHandle CreateSession(
        SafeEnvironmentHandle env,
        byte[] modelData,
        SpeedReaderOrt.SessionOptions options)
    {
        var errorBuffer = new byte[ErrorBufferSize];
        var modelDataHandle = GCHandle.Alloc(modelData, GCHandleType.Pinned);
        var errorHandle = GCHandle.Alloc(errorBuffer, GCHandleType.Pinned);

        try
        {
            var errorBuf = new SpeedReaderOrt.StringBuf
            {
                Buffer = errorHandle.AddrOfPinnedObject(),
                Capacity = (nuint)errorBuffer.Length,
                Length = 0
            };

            var status = SpeedReaderOrt.speedreader_ort_create_session(
                env,
                modelDataHandle.AddrOfPinnedObject(),
                (nuint)modelData.Length,
                ref options,
                out var session,
                ref errorBuf);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = System.Text.Encoding.UTF8.GetString(errorBuffer, 0, (int)errorBuf.Length);
                throw new InvalidOperationException($"Failed to create ONNX Runtime session: {errorMessage}");
            }

            return session;
        }
        finally
        {
            modelDataHandle.Free();
            errorHandle.Free();
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class OnnxInferenceException : Exception
{
    public OnnxInferenceException(string message, Exception innerException) : base(message, innerException) { }
}
