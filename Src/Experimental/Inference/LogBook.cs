// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Experimental.Inference;

// *Important*
// - Thread-safe for concurrent LogStart/LogEnd calls.
// - GetSummary and Prune methods must NOT be called concurrently with each other.
// - After calling Prune(T), all future GetSummary calls must have start > T.
// - Call LogEnd exactly once for each token minted by LogStart. try/finally pattern is strongly
//   recommended.
public class LogBook
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();  // Monotonic clock

    private readonly ConcurrentDictionary<Token, TimeSpan> _startTimes = new();
    private readonly ConcurrentDictionary<Token, TimeSpan> _endTimes = new();

    public interface IToken;

    public interface ISummary
    {
        public double AvgDuration { get; }  // Seconds
        public double AvgThroughput { get; }  // Jobs per second
        public double AvgParallelism { get; }
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
        if (token is not Token t)
            throw new Exception($"Third-party implementations of {nameof(IToken)} are not allowed");
        _endTimes.TryAdd(t, _clock.Elapsed);
    }

    public ISummary GetSummary(TimeSpan start, TimeSpan end)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        // Snapshot end times before start times to avoid end times without corresponding start times. Note that start
        // times without end times are possible, but acceptable.
        var snapshot = TakeSnapshot();

        var pairs = Pairs(snapshot, start, end);
        if (pairs.Count == 0)
            return Summary.Zero;  // No jobs completed in the specified interval, we can't estimate anything

        var activeTime = ActiveTime();
        if (activeTime == 0)
            return Summary.Zero;

        // All pairs are in [start, end] bounds
        var clampedStart = pairs.Select(p => p.Start).Min();
        var clampedEnd = pairs.Select(p => p.End).Max();

        return new Summary
        {
            AvgDuration = GetAvgDuration(),
            AvgThroughput = GetAvgThroughput(),
            AvgParallelism = GetAvgParallelism()
        };

        double GetAvgDuration()
        {
            var totalDuration = pairs.Aggregate(TimeSpan.Zero, (sum, pair) => sum + (pair.End - pair.Start));
            return (totalDuration / pairs.Count).TotalSeconds;
        }

        double GetAvgThroughput() => clampedEnd == clampedStart ? 0 : pairs.Count / activeTime;

        double GetAvgParallelism()
        {
            var tokens = pairs.Select(p => p.Token).ToHashSet();
            return LogBook.GetAvgParallelism(snapshot, tokens, clampedStart, clampedEnd);
        }

        // Returns the total amount of time during which at least one job was active
        double ActiveTime()
        {
            if (pairs.Count == 0)
                return 0;

            // Create events for sweep line algorithm
            const int JOB_START = 0;
            const int JOB_END = 1;

            List<(TimeSpan Time, int EventType)> events = [];
            foreach (var (s, e, _) in pairs)
            {
                events.Add((s, JOB_START));
                events.Add((e, JOB_END));
            }

            events.Sort();

            var activeTime = 0.0;
            var currentActiveJobs = 0;
            for (int i = 0; i < events.Count - 1; i++)
            {
                var (time, eventType) = events[i];
                var (nextTime, _) = events[i + 1];

                // Add 1 if start event, subtract 1 if end event
                if (eventType == JOB_START)
                    currentActiveJobs++;
                else if (eventType == JOB_END)
                    currentActiveJobs--;

                // If there is at least one active job, add to active time
                if (currentActiveJobs > 0)
                {
                    activeTime += (nextTime - time).TotalSeconds;
                }
            }

            return activeTime;
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
        // before entering the interval) is safe because Prune only removes complete (start, end) pairs,
        // and each complete pair contributes net zero to parallelism: the start event increments the
        // count and the end event decrements it by the same amount.

        var snapshot = TakeSnapshot();

        foreach (var pair in Pairs(snapshot, TimeSpan.Zero, before))
        {
            if (pair.End == before)
                continue;  // Exclusive bound on before, Pairs is <= so we need this check
            _startTimes.Remove(pair.Token, out _);
            _endTimes.Remove(pair.Token, out _);
        }
    }

    private static double GetAvgParallelism(
        Snapshot snapshot,
        HashSet<Token> completedJobs,
        TimeSpan start,
        TimeSpan end)
    {
        if (start == end)
            return 0;

        const int START_EVENT = 0;
        const int END_EVENT = 1;
        const int INTERVAL_EVENT = 2;

        List<(TimeSpan Time, int EventType, Token? Token)> events = [
            (start, INTERVAL_EVENT, null),
            (end, INTERVAL_EVENT, null)
        ];

        foreach (var (token, time) in snapshot.EndTimes)
        {
            if (time <= end)
                events.Add((time, END_EVENT, token));
        }

        foreach (var (token, time) in snapshot.StartTimes)
        {
            if (time <= end)
                events.Add((time, START_EVENT, token));
        }

        events.Sort();

        // Sweep line algorithm to compute weighted average parallelism
        var currentParallelism = 0;
        var weightedParallelismSum = 0.0;
        var currentActiveCompletedJobs = 0;
        var activeTime = 0.0;
        var timeWeightsSum = 0.0;

        // Count up occurs in two phases:
        // Phase 1: increment and decrement currentParallelism, do not add to weighted sum
        // Phase 2: keep updating currentParallelism, add to weighted sum
        for (int i = 0; i < events.Count - 1; i++)
        {
            var (time, eventType, token) = events[i];
            var (nextTime, _, _) = events[i + 1];

            // Update active completed jobs count
            if (token is not null && completedJobs.Contains(token))
            {
                // Add 1 if start event, subtract 1 if end event. Will not be an interval event (interval events have null tokens)
                if (eventType == START_EVENT)
                    currentActiveCompletedJobs++;
                else if (eventType == END_EVENT)
                    currentActiveCompletedJobs--;
            }

            // Now update parallelism count

            // Add 1 if start event, subtract 1 if end event, do nothing if interval event
            if (eventType == START_EVENT)
                currentParallelism++;
            else if (eventType == END_EVENT)
                currentParallelism--;

            // Only add to weighted sum if [time, nextTime] is takes place while a job that is known to be eventually
            // completed is running
            if (currentActiveCompletedJobs > 0)
            {
                var timeWeight = (nextTime - time).TotalSeconds;
                timeWeightsSum += timeWeight;
                weightedParallelismSum += currentParallelism * timeWeight;
            }
        }

        return weightedParallelismSum / timeWeightsSum;
    }

    private static List<(TimeSpan Start, TimeSpan End, Token Token)> Pairs(Snapshot snapshot, TimeSpan start, TimeSpan end)
    {
        var pairs = new List<(TimeSpan, TimeSpan, Token)>();

        foreach (var (token, s) in snapshot.StartTimes)
        {
            // Add to pairs if start time is in [start, end] and there is a corresponding end time also in [start, end]
            if (s >= start && s <= end && snapshot.EndTimes.TryGetValue(token, out var e) && e <= end)
                pairs.Add((s, e, token));
        }

        return pairs;
    }

    private Snapshot TakeSnapshot() => new(_startTimes, _endTimes);

    private class Token : IToken, IEquatable<Token>, IComparable<Token>
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

    private class Summary : ISummary
    {
        public double AvgDuration { get; init; }
        public double AvgThroughput { get; init; }
        public double AvgParallelism { get; init; }

        public static Summary Zero = new();
    }

    private record Snapshot
    {
        public readonly Dictionary<Token, TimeSpan> StartTimes;
        public readonly Dictionary<Token, TimeSpan> EndTimes;

        public Snapshot(ConcurrentDictionary<Token, TimeSpan> startTimes, ConcurrentDictionary<Token, TimeSpan> endTimes)
        {
            // Snapshot end times before start times to avoid end times without corresponding start times. Note that start
            // times without end times are possible, but acceptable.
            EndTimes = new Dictionary<Token, TimeSpan>(endTimes);
            StartTimes = new Dictionary<Token, TimeSpan>(startTimes);
        }
    }
}
