// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Globalization;
using CliWrap;

namespace Benchmarks;

public class PerfMemoryBandwidth : IDisposable
{
    private CancellationTokenSource? _forcefulCts;
    private CancellationTokenSource? _gracefulCts;
    private Task? _perfTask;
    private readonly string _outputFile;
    private bool _isRunning;

    public PerfMemoryBandwidth() => _outputFile = Path.GetTempFileName();

    public void Start()
    {
        if (_isRunning)
            throw new InvalidOperationException("Perf is already running");

        _forcefulCts = new CancellationTokenSource();
        _gracefulCts = new CancellationTokenSource();

        _perfTask = Cli.Wrap("perf")
            .WithArguments($"stat -a -M tma_info_system_dram_bw_use -x, -o {_outputFile}")
            .ExecuteAsync(_forcefulCts.Token, _gracefulCts.Token);

        _isRunning = true;

        // Give perf a moment to start up
        Thread.Sleep(100);
    }

    public double Stop()
    {
        if (!_isRunning || _forcefulCts == null || _gracefulCts == null || _perfTask == null)
            throw new InvalidOperationException("Perf is not running");

        // Send graceful cancellation (SIGINT) first
        _gracefulCts.Cancel();

        // Forceful cancellation as fallback after 3 seconds
        _forcefulCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            _perfTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Expected - perf was canceled
        }

        _isRunning = false;

        // Give perf a moment to flush output to file
        Thread.Sleep(200);

        // Parse the output file
        return ParseOutput();
    }

    private double ParseOutput()
    {
        var lines = File.ReadAllLines(_outputFile);

        foreach (var line in lines)
        {
            if (line.Contains("tma_info_system_dram_bw_use"))
            {
                // CSV format: count,,event_name,time,percentage,bandwidth_value,metric_name
                var parts = line.Split(',');
                if (parts.Length >= 7 && double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var bandwidth))
                {
                    return bandwidth;
                }
            }
        }

        throw new InvalidOperationException($"Failed to parse perf output. File contents:\n{string.Join('\n', lines)}");
    }

    public void Dispose()
    {
        if (_isRunning && _gracefulCts != null)
        {
            _gracefulCts.Cancel();
        }

        _forcefulCts?.Dispose();
        _gracefulCts?.Dispose();

        if (File.Exists(_outputFile))
        {
            File.Delete(_outputFile);
        }
    }
}
