// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace SpeedReader.TestUtils;

public class TestLogger : ILogger
{
    private readonly ITestOutputHelper _outputHelper;

    public TestLogger(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        _outputHelper.WriteLine(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
}

public class TestLogger<T> : TestLogger, ILogger<T>
{
    public TestLogger(ITestOutputHelper outputHelper) : base(outputHelper) { }
}
