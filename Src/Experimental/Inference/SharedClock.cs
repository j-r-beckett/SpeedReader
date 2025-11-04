// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;

namespace Experimental.Inference;

public static class SharedClock
{
    private static readonly Stopwatch _clock;
    private static readonly DateTime _start;

    static SharedClock() {
        _clock = Stopwatch.StartNew();
        _start = DateTime.UtcNow;
    }

    public static TimeSpan Now => _clock.Elapsed;

    public static DateTime ToUtc(this TimeSpan timeSpan) => _start.Add(timeSpan);
}
