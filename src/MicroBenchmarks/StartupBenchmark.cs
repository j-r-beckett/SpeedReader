// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp;

namespace SpeedReader.MicroBenchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[MemoryDiagnoser]
public class StartupBenchmark
{
    private string _imagePath = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var image = InputGenerator.GenerateInput(640, 480, Density.Low);
        _imagePath = "/tmp/startup_benchmark.png";
        image.SaveAsPng(_imagePath);
        image.Dispose();
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task<int> ColdStart() => await SpeedReader.Frontend.Program.Main([_imagePath]);
}
