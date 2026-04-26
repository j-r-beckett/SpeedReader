// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Native.Threading;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.Weights;

namespace SpeedReader.BenchLib;

public static class Exports
{
    private static NativeOnnxInferenceKernel? _kernel;
    private static ServiceProvider? _serviceProvider;
    private static float[]? _inputData;
    private static int[]? _batchedShape;

    [UnmanagedCallersOnly(EntryPoint = "benchlib_init")]
    public static void Init()
    {
        try
        {
            var model = Model.DbNet;
            var kernelOptions = new OnnxInferenceKernelOptions(model, Quantization.Int8, numIntraOpThreads: 1);

            var services = new ServiceCollection();
            services.AddKeyedSingleton(model, kernelOptions);
            services.AddKeyedSingleton(model, EmbeddedWeights.Dbnet_Int8);
            _serviceProvider = services.BuildServiceProvider();

            _kernel = NativeOnnxInferenceKernel.Factory(_serviceProvider, model);

            _inputData = new float[3 * 640 * 640];
            _batchedShape = [1, 3, 640, 640];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"benchlib_init failed: {ex}");
            Environment.FailFast($"benchlib_init failed: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "benchlib_rundbnet")]
    public static void RunDbNet(int coreId)
    {
        Affinitizer.PinToCore(coreId);
        _kernel!.Execute(_inputData!, _batchedShape!);
    }

    [UnmanagedCallersOnly(EntryPoint = "benchlib_destroy")]
    public static void Destroy()
    {
        _kernel?.Dispose();
        _kernel = null;
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        _inputData = null;
        _batchedShape = null;
    }
}
