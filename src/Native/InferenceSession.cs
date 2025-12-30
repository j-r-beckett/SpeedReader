// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using SpeedReader.Native.Internal;

namespace SpeedReader.Native;

public sealed class InferenceSession : IDisposable
{
    private readonly SafeSessionHandle _session;
    private bool _disposed;

    public InferenceSession(byte[] modelData, SessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modelData);
        _session = CreateSession(modelData, options ?? new SessionOptions());
    }

    public void Run(OrtValue input, OrtValue output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        UnsafeRunInternal(input, output);
    }

    private unsafe void UnsafeRunInternal(OrtValue input, OrtValue output)
    {
        var outputShapeBuffer = stackalloc long[SpeedReaderOrt.MaxShapeDims];
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];
        nuint outputNdim = 0;

        Span<long> inputShapeLong = stackalloc long[input.Shape.Length];
        for (int i = 0; i < input.Shape.Length; i++)
            inputShapeLong[i] = input.Shape[i];

        using var inputHandle = input.Data.Pin();
        using var outputHandle = output.Data.Pin();

        fixed (long* inputShapePtr = inputShapeLong)
        {
            var status = SpeedReaderOrt.speedreader_ort_run(
                _session,
                (float*)inputHandle.Pointer,
                inputShapePtr,
                (nuint)input.Shape.Length,
                (float*)outputHandle.Pointer,
                (nuint)output.Data.Length,
                outputShapeBuffer,
                &outputNdim,
                errorBuffer);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
                throw new OrtException($"Inference failed: {errorMessage}");
            }
        }

        ThrowIfShapeMismatch(output.Shape, outputShapeBuffer, (int)outputNdim);
    }

    private static unsafe void ThrowIfShapeMismatch(int[] expectedShape, long* actualShape, int actualNdim)
    {
        if (expectedShape.Length == actualNdim)
        {
            for (int i = 0; i < actualNdim; i++)
            {
                if (expectedShape[i] != actualShape[i])
                    goto THROW;
            }

            return;
        }

    THROW:
        Span<long> actual = stackalloc long[actualNdim];
        for (int i = 0; i < actualNdim; i++)
            actual[i] = actualShape[i];

        throw new OrtException(
            $"Output shape mismatch: expected [{string.Join(", ", expectedShape)}], got [{string.Join(", ", actual.ToArray())}]");
    }

    private static unsafe SafeSessionHandle CreateSession(byte[] modelData, SessionOptions options)
    {
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];
        var nativeOptions = options.ToNative();

        fixed (byte* modelDataPtr = modelData)
        {
            var status = SpeedReaderOrt.speedreader_ort_create_session(
                OrtEnvironment.Instance,
                modelDataPtr,
                (nuint)modelData.Length,
                ref nativeOptions,
                out var session,
                errorBuffer);

            if (status != SpeedReaderOrt.Status.Ok)
            {
                var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
                throw new OrtException($"Failed to create session: {errorMessage}");
            }

            return session;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _session.Dispose();
        _disposed = true;
    }
}
