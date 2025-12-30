// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.ExceptionServices;

namespace SpeedReader.Ocr;

public class Result<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;
    private readonly ExceptionDispatchInfo? _exceptionDispatchInfo;

    public Result(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public Result(Exception exception)
    {
        _exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
        _hasValue = false;
    }

    public bool HasValue() => _hasValue;

    public Exception Exception() => _exceptionDispatchInfo?.SourceException ?? throw new InvalidOperationException("Result has a value, not an exception");

    public T Value()
    {
        if (_hasValue)
            return _value!;

        _exceptionDispatchInfo!.Throw();
        return default!; // Unreachable, but compiler needs it
    }
}
