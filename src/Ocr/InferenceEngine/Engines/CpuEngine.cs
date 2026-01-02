// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace SpeedReader.Ocr.InferenceEngine.Engines;

public class CpuEngine : IInferenceEngine
{
    private struct Job
    {
        public (float[] Data, int[] Shape) Input;
        public TaskCompletionSource<(float[], int[])> Output;
    }

    private readonly BlockingCollection<InferenceThread> _threads = new();
    private readonly BlockingCollection<Job> _jobs = new();
    private readonly CancellationTokenSource _activatorCts = new();
    private readonly Task _activatorTask;

    public static CpuEngine Factory(IServiceProvider serviceProvider, object? key)
    {
        var config = serviceProvider.GetRequiredKeyedService<CpuEngineConfig>(key);
        var kernel = serviceProvider.GetRequiredKeyedService<IInferenceKernel>(key);
        var meterFactory = serviceProvider.GetService<IMeterFactory>();
        return new CpuEngine(config, kernel, meterFactory);
    }

    private CpuEngine(CpuEngineConfig config, IInferenceKernel inferenceKernel, IMeterFactory? meterFactory)
    {
        var numThreads = 4;
        for (var i = 0; i < numThreads; i++)
        {
            var thread = new InferenceThread(inferenceKernel, thread => _threads.Add(thread));
            _threads.Add(thread);
        }
        _activatorTask = Task.Run(ThreadActivator);
    }

    private void ThreadActivator()
    {
        while (true)
        {
            _activatorCts.Token.ThrowIfCancellationRequested();

            var job = _jobs.Take(_activatorCts.Token);
            var thread = _threads.Take(_activatorCts.Token);
            thread.Input = job.Input;
            thread.Output = job.Output;
            thread.Activate.Set();
        }
    }

    public int CurrentMaxCapacity() => _threads.Count;

    public async Task<Task<(float[] OutputData, int[] OutputShape)>> Run(float[] inputData, int[] inputShape)
    {
        // return Task.FromResult(_inferenceKernel.Execute(inputData, inputShape));
        var job = new Job { Input = (inputData, inputShape), Output = new TaskCompletionSource<(float[], int[])>() };
        _jobs.Add(job);
        return job.Output.Task;
    }

    public async ValueTask DisposeAsync()
    {
        // TODO: shut down threads
        _activatorCts.Cancel();
        try
        {
            await _activatorTask;
        }
        catch (OperationCanceledException) { }

        foreach (var thread in _threads)
            thread.Cts.Cancel();

        GC.SuppressFinalize(this);
    }

    private class InferenceThread
    {
        public (float[] Data, int[] Shape)? Input { get; set; }
        public TaskCompletionSource<(float[], int[])>? Output { get; set; }
        public ManualResetEventSlim Activate { get; } = new(false);

        public CancellationTokenSource Cts { get; } = new();

        private readonly Action<InferenceThread> _requeue;
        private readonly IInferenceKernel _inferenceKernel;
        private readonly Thread _thread;

        public InferenceThread(IInferenceKernel inferenceKernel, Action<InferenceThread> requeue)
        {
            _requeue = requeue;
            _inferenceKernel = inferenceKernel;
            _thread = new Thread(Run) { IsBackground = true };
            _thread.Start();
        }

        private void Run()
        {
            try
            {
                while (true)
                {
                    Cts.Token.ThrowIfCancellationRequested();
                    Activate.Wait(Cts.Token);
                    if (Input is null)
                        throw new InvalidOperationException("Must set Input before activating thread");
                    if (Output is null)
                        throw new InvalidOperationException("Must set Output before activating thread");
                    var (data, shape) = Input.Value;
                    var batchedInputShape = new[] { 1 }.Concat(shape).ToArray();
                    var (resultData, batchedResultShape) = _inferenceKernel.Execute(data, batchedInputShape);
                    var resultShape = batchedResultShape[1..];
                    Output.SetResult((resultData, resultShape));
                    Activate.Reset();
                    _requeue(this);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
