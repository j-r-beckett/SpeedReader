// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;

namespace Experimental.Inference;

public static class SharedClock
{
    private static readonly Stopwatch _clock = Stopwatch.StartNew();

    public static TimeSpan Now => _clock.Elapsed;
}
