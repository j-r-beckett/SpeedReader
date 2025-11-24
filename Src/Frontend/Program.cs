// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Diagnostics;
using Frontend.Cli;

namespace Frontend;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("SPEEDREADER_DEBUG_WAIT")?.ToLower() == "true")
        {
            Console.WriteLine("Waiting for debugger to attach");
            while (!Debugger.IsAttached)
            {
                await Task.Delay(25);
            }
            Console.WriteLine("Debugger attached");
        }

        var rootCommand = await Commands.CreateRootCommand(args);

        return await rootCommand.InvokeAsync(args);
    }
}
