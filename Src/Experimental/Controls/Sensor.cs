// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Experimental.Inference;

namespace Experimental.Controls;

public class Sensor
{
    private readonly ConcurrentDictionary<Token, TimeSpan> _startTimes = new();
    private readonly ConcurrentDictionary<Token, TimeSpan> _endTimes = new();

    public static TimeSpan Now => SharedClock.Now;

    public class SummaryStatistics
    {
        public double AvgDuration { get; init; }  // Average job duration in seconds. Counts only jobs that started and ended in time interval.
                                                  // AvgDuration > 0 => Throughput > 0, BoxedThroughput > 0, AvgParallelism > 0
        public double Throughput { get; init; }  // Jobs per second; (number of jobs ended in the time interval) / (time interval)
        public double BoxedThroughput { get; init; }  // Jobs per second; jobs that started and ended in the time interval only
        public double AvgParallelism { get; init; }  // Avg number of jobs concurrently executing in the time interval
        public List<(TimeSpan Start, TimeSpan End)> Enclosed { get; init; } = [];
    }

    public interface IJob : IDisposable;

    private class Job : IJob
    {
        private readonly ConcurrentDictionary<Token, TimeSpan> _endTimes;
        private readonly Token _token;
        private bool _disposed;

        public Job(ConcurrentDictionary<Token, TimeSpan> startTimes, ConcurrentDictionary<Token, TimeSpan> endTimes)
        {
            _token = new Token();
            startTimes.TryAdd(_token, Now);
            _endTimes = endTimes;
        }

        // Records a job end only the first time it's called, subsequent calls are ignored
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, true, false))
                return;
            _endTimes.TryAdd(_token, Now);
        }
    }

    public IJob RecordJob() => new Job(_startTimes, _endTimes);

    public SummaryStatistics GetSummaryStatistics(TimeSpan start, TimeSpan end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        if (start == end)
            return new SummaryStatistics { AvgDuration = 0, Throughput = 0, BoxedThroughput = 0, AvgParallelism = 0 };

        var snapshot = TakeSnapshot();
        var enclosedJobs = GetEnclosedJobs(snapshot, start, end);

        // Avg job duration
        var durations = enclosedJobs.Select(p => p.End - p.Start).ToList();
        if (durations.Count > 0)
            Console.WriteLine($"Durations: {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
        var totalDuration = enclosedJobs.Aggregate(TimeSpan.Zero, (sum, pair) => sum + (pair.End - pair.Start));
        var avgDuration = enclosedJobs.Count == 0 ? 0 : totalDuration.TotalSeconds / enclosedJobs.Count;

        // Throughput
        var numJobEnds = snapshot.EndTimes.Values.Count(t => t >= start && t <= end);
        var throughput = numJobEnds / (end - start).TotalSeconds;

        // Boxed throughput
        var boxedThroughput = enclosedJobs.Count / (end - start).TotalSeconds;

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

            List<(TimeSpan Time, int EventType)> events =
            [
                (TimeSpan.Zero, INTERVAL_EVENT),  // Need to start at time 0 so that when we reach start we have the correct parallelism count
                (start, INTERVAL_EVENT),
                (end, INTERVAL_EVENT)
            ];

            foreach (var (_, time) in snapshot.EndTimes)
            {
                if (time <= end)
                    events.Add((time, END_EVENT));
            }

            foreach (var (_, time) in snapshot.StartTimes)
            {
                if (time <= end)
                    events.Add((time, START_EVENT));
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
                var (time, eventType) = events[i];
                var (nextTime, _) = events[i + 1];

                // Increment if start event, decrement if end event, do nothing if interval event
                currentParallelism += eventType switch
                {
                    START_EVENT => 1,
                    END_EVENT => -1,
                    _ => 0
                };

                if (time >= start)
                {
                    weightedParallelismSum += currentParallelism * (nextTime - time).TotalSeconds;
                }
            }

            return weightedParallelismSum / (end - start).TotalSeconds;
        }
    }

    // Remove completed jobs in [0, before)
    public void Prune(TimeSpan before)
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

        foreach (var pair in GetEnclosedJobs(snapshot, TimeSpan.Zero, before, inclusiveUpperBound: false))
        {
            // Remove ends first to avoid end times without corresponding start times
            _endTimes.Remove(pair.Token, out _);
            _startTimes.Remove(pair.Token, out _);
        }
    }

    private Snapshot TakeSnapshot() => new(_startTimes, _endTimes);

    // Get all jobs that started and ended in [start, end]
    private static List<(TimeSpan Start, TimeSpan End, Token Token)> GetEnclosedJobs(Snapshot snapshot, TimeSpan start, TimeSpan end, bool inclusiveUpperBound = true)
    {
        var pairs = new List<(TimeSpan, TimeSpan, Token)>();

        foreach (var (token, s) in snapshot.StartTimes)
        {
            // Add to pairs if start time is in [start, end] and there is a corresponding end time also in [start, end] or [start, end) if inclusiveUpperBound is false
            if (s >= start && s <= end && snapshot.EndTimes.TryGetValue(token, out var e) && (e < end || e == end && inclusiveUpperBound))
                pairs.Add((s, e, token));
        }

        return pairs;
    }

    private record Snapshot
    {
        public readonly Dictionary<Token, TimeSpan> StartTimes;
        public readonly Dictionary<Token, TimeSpan> EndTimes;

        public Snapshot(ConcurrentDictionary<Token, TimeSpan> startTimes, ConcurrentDictionary<Token, TimeSpan> endTimes)
        {
            // Snapshot end times before start times to avoid end times without corresponding start times. Note that start
            // times without end times are possible but acceptable.
            EndTimes = new Dictionary<Token, TimeSpan>(endTimes);
            StartTimes = new Dictionary<Token, TimeSpan>(startTimes);
        }
    }

    private class Token : IEquatable<Token>, IComparable<Token>
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
