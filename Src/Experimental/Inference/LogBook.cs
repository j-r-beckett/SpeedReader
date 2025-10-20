// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Experimental.Inference;

// Thread-safe for concurrent LogStart/LogEnd calls.
// GetAvg* and Prune methods must NOT be called concurrently with each other.
public class LogBook
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();  // Monotonic clock

    private readonly ConcurrentDictionary<Token, TimeSpan> _startTimes = new();
    private readonly ConcurrentDictionary<Token, TimeSpan> _endTimes = new();

    public interface IToken;

    private class Token : IEquatable<Token>, IToken
    {
        private static long _globalIdCounter = -1;
        private long Id { get; } = Interlocked.Increment(ref _globalIdCounter);

        public override bool Equals(object? obj) => obj is Token other && other.Id == Id;

        public override int GetHashCode() => HashCode.Combine(Id);

        public bool Equals(Token? other) => other?.Id == Id;

        public static bool operator ==(Token? left, Token? right) =>
            left is null && right is null || left is not null && right is not null && left.Equals(right);

        public static bool operator !=(Token? left, Token? right) => !(left == right);
    }

    public IToken LogStart()
    {
        var token = new Token();
        _startTimes.TryAdd(token, _clock.Elapsed);
        return token;
    }

    // Must be called exactly once for each token minted by LogStart
    public void LogEnd(IToken token)
    {
        if (token is not Token t) throw new Exception($"Third-party implementations of {nameof(IToken)} are not allowed");
        _endTimes.TryAdd(t, _clock.Elapsed);
    }

    // Average duration of jobs
    public TimeSpan GetAvgDuration(TimeSpan start, TimeSpan end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        var pairs = Pairs(start, end);
        return pairs.Count == 0
            ? TimeSpan.Zero
            : pairs.Aggregate(TimeSpan.Zero, (sum, pair) => sum + (pair.End - pair.Start)) / pairs.Count;
    }

    // Average number of concurrent jobs
    public double GetAvgParallelism(TimeSpan start, TimeSpan end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        if (start == end) return 0;

        const int START_EVENT = 0;
        const int END_EVENT = 1;
        const int INTERVAL_EVENT = 2;

        var events = new List<(TimeSpan Time, int EventType)>();
        events.Add((TimeSpan.Zero, INTERVAL_EVENT));  // We need to count up from zero so we can accurately track currentParallelism
        events.Add((end, INTERVAL_EVENT));

        foreach (var (_, e) in _endTimes)
        {
            if (e <= end) events.Add((e, END_EVENT));
        }

        foreach (var (_, s) in _startTimes)
        {
            if (s <= end) events.Add((s, START_EVENT));
        }

        events.Sort();

        // Sweep line algorithm to compute weighted average parallelism
        var currentParallelism = 0;
        var weightedParallelismSum = 0.0;

        // Count up occurs in two phases:
        // Phase 1: incremement and decrement currentParallelism, but do not add to weighted sum (overlapWeight <= 0)
        // Phase 2: increment/decrement currentParallelism and add to weighted sum (overlapWeight > 0)
        for (int i = 0; i < events.Count - 1; i++)
        {
            var (time, eventType) = events[i];
            var (nextTime, _) = events[i + 1];

            // Add 1 if start event, subtract 1 if end event, do nothing if interval event
            if (eventType == START_EVENT) currentParallelism++;
            else if (eventType == END_EVENT) currentParallelism--;

            // Use Max(time, start) to properly handle when a job starts before [start, end] and is still executing during [start, end]
            var overlapWeight = (nextTime - Max(time, start)).TotalSeconds;
            if (overlapWeight > 0)
            {
                weightedParallelismSum += currentParallelism * overlapWeight;
            }
        }

        return weightedParallelismSum / (end - start).TotalSeconds;
    }

    // Jobs per second
    public double GetAvgThroughput(TimeSpan start, TimeSpan end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        var pairs = Pairs(start, end);
        if (pairs.Count == 0) return 0;
        var clampedStart = Max(start, pairs.Min(p => p.Start));
        var clampedEnd = Min(end, pairs.Max(p => p.End));
        var avgDuration = GetAvgDuration(clampedStart, clampedEnd);
        if (avgDuration == TimeSpan.Zero) return 0;
        return GetAvgParallelism(clampedStart, clampedEnd) / avgDuration.TotalSeconds;
    }

    // Caller must ensure no further LogStart/LogEnd calls start before 'before'
    public void Prune(TimeSpan before)
    {
        // A (start, end) pair has net zero effect on the parallelism tracker in GetAvgParallelism.
        // That ensures that Prune does not affect Phase 1, and the constraints on the caller for before ensures that
        // Phase 2 is also unaffected.
        foreach (var pair in Pairs(TimeSpan.Zero, before))
        {
            _startTimes.Remove(pair.Token, out _);
            _endTimes.Remove(pair.Token, out _);
        }
    }

    private List<(TimeSpan Start, TimeSpan End, Token Token)> Pairs(TimeSpan start, TimeSpan end)
    {
        var pairs = new List<(TimeSpan, TimeSpan, Token)>();

        foreach (var (token, s) in _startTimes)
        {
            if (s >= start && s <= end && _endTimes.TryGetValue(token, out var e) && e <= end) pairs.Add((s, e, token));
        }

        return pairs;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
