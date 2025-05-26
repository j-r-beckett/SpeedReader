using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Engine.Test;

public class CapturingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _logEntries = new();

    public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _logEntries.Add(new LogEntry(logLevel, message, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Clear() => _logEntries.Clear();

    public bool HasLoggedMessageContaining(string text, LogLevel? logLevel = null) =>
        _logEntries.Any(entry =>
            entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase) &&
            (logLevel == null || entry.LogLevel == logLevel));

    public LogEntry? GetLastLogEntry() => _logEntries.LastOrDefault();

    public IEnumerable<LogEntry> GetLogEntriesContaining(string text) =>
        _logEntries.Where(entry => entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase));
}

public record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
