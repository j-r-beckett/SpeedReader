// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using SpeedReader.MicroBenchmarks.Cli;

var rootCommand = Commands.CreateRootCommand();
return await rootCommand.InvokeAsync(args);
