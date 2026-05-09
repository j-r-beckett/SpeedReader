// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Native.Threading;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.CharDict;
using SpeedReader.Resources.Weights;

namespace SpeedReader.BenchLib;

public static class Exports
{
    // Model IDs (matches the Python side)
    private const int ModelDbNet = 0;
    private const int ModelSvtr = 1;

    private static ServiceProvider? _serviceProvider;

    private static NativeOnnxInferenceKernel? _dbnetKernel;
    private static float[]? _dbnetInput;
    private static int[]? _dbnetShape;

    private static NativeOnnxInferenceKernel? _svtrKernel;
    private static float[]? _svtrInput;
    private static int[]? _svtrShape;

    [UnmanagedCallersOnly(EntryPoint = "benchlib_init")]
    public static void Init()
    {
        try
        {
            var services = new ServiceCollection();

            var dbnetOptions = new OnnxInferenceKernelOptions(Model.DbNet, Quantization.Int8, numIntraOpThreads: 1);
            services.AddKeyedSingleton(Model.DbNet, dbnetOptions);
            services.AddKeyedSingleton(Model.DbNet, EmbeddedWeights.Dbnet_Int8);

            var svtrOptions = new OnnxInferenceKernelOptions(Model.Svtr, Quantization.Fp32, numIntraOpThreads: 1);
            services.AddKeyedSingleton(Model.Svtr, svtrOptions);
            services.AddKeyedSingleton(Model.Svtr, EmbeddedWeights.Svtr_Fp32);
            services.AddSingleton<EmbeddedCharDict>();

            _serviceProvider = services.BuildServiceProvider();

            _dbnetKernel = NativeOnnxInferenceKernel.Factory(_serviceProvider, Model.DbNet);
            _dbnetInput = new float[3 * 640 * 640];
            _dbnetShape = [1, 3, 640, 640];

            _svtrKernel = NativeOnnxInferenceKernel.Factory(_serviceProvider, Model.Svtr);
            _svtrInput = new float[3 * 48 * 160];
            _svtrShape = [1, 3, 48, 160];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"benchlib_init failed: {ex}");
            Environment.FailFast($"benchlib_init failed: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "benchlib_run")]
    public static void Run(int modelId, int coreId)
    {
        Affinitizer.PinToCore(coreId);
        switch (modelId)
        {
            case ModelDbNet:
                _dbnetKernel!.Execute(_dbnetInput!, _dbnetShape!);
                break;
            case ModelSvtr:
                _svtrKernel!.Execute(_svtrInput!, _svtrShape!);
                break;
            default:
                Environment.FailFast($"benchlib_run: unknown model id {modelId}");
                break;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "benchlib_destroy")]
    public static void Destroy()
    {
        _dbnetKernel?.Dispose();
        _dbnetKernel = null;
        _dbnetInput = null;
        _dbnetShape = null;

        _svtrKernel?.Dispose();
        _svtrKernel = null;
        _svtrInput = null;
        _svtrShape = null;

        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}
