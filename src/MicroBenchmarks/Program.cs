// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using BenchmarkDotNet.Running;
using MicroBenchmarks;

BenchmarkRunner.Run<DryPipelineBenchmark>();
BenchmarkRunner.Run<StartupBenchmark>();
