// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr.SmartMetrics;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class CpuEngine : IInferenceEngine
{
    private struct ThreadState
    {
        public (float[] Data, int[] Shape) Input;
        public ManualResetEventSlim OnInputReady;
        public TaskCompletionSource<float[]> Output;
    }

    private readonly IInferenceKernel _inferenceKernel;
    private readonly ThreadState[] _threads = new ThreadState[Environment.ProcessorCount];
    private readonly ConcurrentQueue<int> _availableThreads = new();
    private readonly ConcurrentQueue<(float[] Input, TaskCompletionSource<float[]> Output)> _jobs = new();

    public static CpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<CpuEngineConfig>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        var meterFactory = serviceProvider.GetService<IMeterFactory>();
        return new CpuEngine(config, kernel, meterFactory);
    }

    private CpuEngine(CpuEngineConfig config, IInferenceKernel inferenceKernel, IMeterFactory? meterFactory)
    {
        _inferenceKernel = inferenceKernel;
    }

    private void Thread()
    {
        var index = 0;

        while (true)
        {
            var state = _threads[index];
            state.OnInputReady.Wait();
            var (data, shape) = state.Input;
            _inferenceKernel.Execute(data, shape);
            state.Output.SetResult(data);
            state.OnInputReady.Reset();
            _availableThreads.Enqueue(index);
        }
    }

    public int CurrentMaxCapacity() => 0;

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        return Task.FromResult(_inferenceKernel.Execute(inputData, inputShape));
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
    }
}
