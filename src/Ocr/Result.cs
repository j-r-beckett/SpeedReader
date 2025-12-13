// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr;

public class Result<T>
{
    private readonly T? _value;
    private readonly bool _hasValue;
    private readonly Exception? _exception;

    public Result(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public Result(Exception? exception)
    {
        _exception = exception;
        _hasValue = false;
    }

    public bool HasValue() => _hasValue;

    public Exception Exception() => _exception ?? throw new InvalidOperationException("Result has a value, not an exception");

    public T Value() => _hasValue ? _value! : throw _exception!;
}
