// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Ocr.Inference;
using Ocr.Telemetry;

namespace Ocr.Controls;

public class Sensor
{
    internal readonly ConcurrentDictionary<Token, long> StartTimes = new();
    internal readonly ConcurrentDictionary<Token, long> EndTimes = new();
    internal readonly Dictionary<string, string>? Tags;
    internal int Parallelism;

    public Sensor(Dictionary<string, string>? tags = null) => Tags = tags;

    public class SummaryStatistics
    {
        public double AvgDuration { get; init; }  // Average job duration in seconds. Counts only jobs that started and ended in time interval.
                                                  // AvgDuration > 0 => Throughput > 0, BoxedThroughput > 0, AvgParallelism > 0
        public double Throughput { get; init; }  // Jobs per second; (number of jobs ended in the time interval) / (time interval)
        public double BoxedThroughput { get; init; }  // Jobs per second; jobs that started and ended in the time interval only
        public double AvgParallelism { get; init; }  // Avg number of jobs concurrently executing in the time interval
        public List<(long Start, long End)> Enclosed { get; init; } = [];
    }

    public interface IJob : IDisposable;

    private class Job : IJob
    {
        private readonly Sensor _parent;
        private readonly Token _token;
        private readonly long _startTimestamp;
        private bool _disposed;

        public Job(Sensor parent)
        {
            _token = new Token();
            _startTimestamp = Stopwatch.GetTimestamp();
            parent.StartTimes.TryAdd(_token, _startTimestamp);
            _parent = parent;
            MetricRecorder.RecordMetric("speedreader.inference.parallelism", Interlocked.Increment(ref _parent.Parallelism), _parent.Tags);
        }

        // Records a job end only the first time it's called, subsequent calls are ignored
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, true, false))
                return;
            var endTimestamp = Stopwatch.GetTimestamp();
            _parent.EndTimes.TryAdd(_token, endTimestamp);
            MetricRecorder.RecordMetric("speedreader.inference.counter", 1, _parent.Tags);
            MetricRecorder.RecordMetric("speedreader.inference.parallelism", Interlocked.Decrement(ref _parent.Parallelism), _parent.Tags);
            MetricRecorder.RecordMetric("speedreader.inference.duration", Stopwatch.GetElapsedTime(_startTimestamp, endTimestamp).TotalMilliseconds, _parent.Tags);
        }
    }

    public IJob RecordJob() => new Job(this);

    public SummaryStatistics GetSummaryStatistics(long startTimestamp, long endTimestamp)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endTimestamp, startTimestamp);

        if (startTimestamp == endTimestamp)
            return new SummaryStatistics { AvgDuration = 0, Throughput = 0, BoxedThroughput = 0, AvgParallelism = 0 };

        var snapshot = TakeSnapshot();
        var enclosedJobs = GetEnclosedJobs(snapshot, startTimestamp, endTimestamp);

        // Avg job duration
        var durations = enclosedJobs.Select(p => Stopwatch.GetElapsedTime(p.Start, p.End)).ToList();
        if (durations.Count > 0)
            Console.WriteLine($"Durations: {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
        var totalDuration = enclosedJobs.Aggregate(0.0, (sum, pair) => sum + Stopwatch.GetElapsedTime(pair.Start, pair.End).TotalSeconds);
        var avgDuration = enclosedJobs.Count == 0 ? 0 : totalDuration / enclosedJobs.Count;

        // Throughput
        var intervalDuration = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalSeconds;
        var numJobEnds = snapshot.EndTimes.Values.Count(t => t >= startTimestamp && t <= endTimestamp);
        var throughput = numJobEnds / intervalDuration;

        // Boxed throughput
        var boxedThroughput = enclosedJobs.Count / intervalDuration;

        return new SummaryStatistics
        {
            AvgDuration = avgDuration,
            Throughput = throughput,
            BoxedThroughput = boxedThroughput,
            AvgParallelism = GetAvgParallelism(),
            Enclosed = enclosedJobs.Select(p => (p.Start, p.End)).ToList()
        };

        double GetAvgParallelism()
        {
            const int START_EVENT = 0;
            const int END_EVENT = 1;
            const int INTERVAL_EVENT = 2;

            List<(long Timestamp, int EventType)> events =
            [
                (0, INTERVAL_EVENT),  // Need to start at time 0 so that when we reach start we have the correct parallelism count
                (startTimestamp, INTERVAL_EVENT),
                (endTimestamp, INTERVAL_EVENT)
            ];

            foreach (var (_, timestamp) in snapshot.EndTimes)
            {
                if (timestamp <= endTimestamp)
                    events.Add((timestamp, END_EVENT));
            }

            foreach (var (_, timestamp) in snapshot.StartTimes)
            {
                if (timestamp <= endTimestamp)
                    events.Add((timestamp, START_EVENT));
            }

            events.Sort();

            // Sweep line algorithm to compute weighted average parallelism
            var currentParallelism = 0;
            var weightedParallelismSum = 0.0;

            // Count up occurs in two phases:
            // Phase 1: increment and decrement currentParallelism only
            // Phase 2: keep updating currentParallelism, also update weightedParallelismSum
            // Transition from phase 1 to phase 2 occurs when we encounter the start interval event
            // Phase 2 lasts from start to end
            for (var i = 0; i < events.Count - 1; i++)
            {
                var (timestamp, eventType) = events[i];
                var (nextTimestamp, _) = events[i + 1];

                // Increment if start event, decrement if end event, do nothing if interval event
                currentParallelism += eventType switch
                {
                    START_EVENT => 1,
                    END_EVENT => -1,
                    _ => 0
                };

                if (timestamp >= startTimestamp)
                {
                    weightedParallelismSum += currentParallelism * Stopwatch.GetElapsedTime(timestamp, nextTimestamp).TotalSeconds;
                }
            }

            return weightedParallelismSum / intervalDuration;
        }
    }

    // Remove completed jobs in [0, before)
    public void Prune(long beforeTimestamp)
    {
        // Removes completed jobs before a given timestamp to prevent unbounded memory growth.
        // Prune safety relies on a caller invariant: after Prune(before), GetSummary(start, end) will
        // never be called with start <= before. Under this invariant, Prune cannot affect GetSummary
        // results. Duration and throughput only examine pairs where both start and end are >= start,
        // so pruned pairs are already excluded. Parallelism Phase 2 (weighted sum accumulation when
        // time >= start) is safe for the same reason. Parallelism Phase 1 (bootstrapping the count
        // before entering the interval) is safe because Prune only removes complete (start, end) pairs.
        // Each completed pair contributes net zero to parallelism because the start event increments the
        // count and the end event decrements it

        var snapshot = TakeSnapshot();

        foreach (var pair in GetEnclosedJobs(snapshot, 0, beforeTimestamp, inclusiveUpperBound: false))
        {
            // Remove ends first to avoid end times without corresponding start times
            EndTimes.Remove(pair.Token, out _);
            StartTimes.Remove(pair.Token, out _);
        }
    }

    private Snapshot TakeSnapshot() => new(StartTimes, EndTimes);

    // Get all jobs that started and ended in [start, end]
    private static List<(long Start, long End, Token Token)> GetEnclosedJobs(Snapshot snapshot, long startTimestamp, long endTimestamp, bool inclusiveUpperBound = true)
    {
        var pairs = new List<(long, long, Token)>();

        foreach (var (token, s) in snapshot.StartTimes)
        {
            // Add to pairs if start time is in [start, end] and there is a corresponding end time also in [start, end] or [start, end) if inclusiveUpperBound is false
            if (s >= startTimestamp && s <= endTimestamp && snapshot.EndTimes.TryGetValue(token, out var e) && (e < endTimestamp || e == endTimestamp && inclusiveUpperBound))
                pairs.Add((s, e, token));
        }

        return pairs;
    }

    private record Snapshot
    {
        public readonly Dictionary<Token, long> StartTimes;
        public readonly Dictionary<Token, long> EndTimes;

        public Snapshot(ConcurrentDictionary<Token, long> startTimes, ConcurrentDictionary<Token, long> endTimes)
        {
            // Snapshot end times before start times to avoid end times without corresponding start times. Note that start
            // times without end times are possible but acceptable.
            EndTimes = new Dictionary<Token, long>(endTimes);
            StartTimes = new Dictionary<Token, long>(startTimes);
        }
    }

    internal class Token : IEquatable<Token>, IComparable<Token>
    {
        private static long _globalIdCounter = -1;
        private long Id { get; } = Interlocked.Increment(ref _globalIdCounter);

        public override bool Equals(object? obj) => obj is Token other && other.Id == Id;

        public override int GetHashCode() => HashCode.Combine(Id);

        public bool Equals(Token? other) => other?.Id == Id;

        public static bool operator ==(Token? left, Token? right) =>
            left is null && right is null || left is not null && right is not null && left.Equals(right);

        public static bool operator !=(Token? left, Token? right) => !(left == right);

        public int CompareTo(Token? other) => other is null ? 1 : Id.CompareTo(other.Id);
    }
}
