// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using BenchmarkDotNet.EventProcessors;
using BenchmarkDotNet.Running;

namespace Benchmarks;

public class SimpleTimeCounter : EventProcessor
{
    private DateTime _startTime;
    private Timer? _timer;
    private readonly string _benchmarkName;

    public SimpleTimeCounter(string benchmarkName) => _benchmarkName = benchmarkName;

    public override void OnStartBuildStage(IReadOnlyList<BuildPartition> partitions)
    {
        _startTime = DateTime.Now;

        // Update every 100ms
        _timer = new Timer(_ => UpdateTime(), null, 0, 100);
    }

    public override void OnEndRunStage()
    {
        _timer?.Dispose();

        var elapsed = DateTime.Now - _startTime;
        Console.Out.Write("\r\x1b[K");  // Clear line
        Console.Out.WriteLine($"Completed {_benchmarkName} ({elapsed.TotalSeconds:F1}s)");
    }

    private void UpdateTime()
    {
        var elapsed = DateTime.Now - _startTime;
        Console.Out.Write("\r\x1b[K");  // Clear line
        Console.Out.Write($"\rRunning {_benchmarkName}... ({elapsed.TotalSeconds:F1}s)");
        Console.Out.Flush();
    }
}
