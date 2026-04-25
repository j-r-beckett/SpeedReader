// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SpeedReader.Library;

public static unsafe class Exports
{
    private const int Ok = 0;
    private const int Error = 1;
    private const int Timeout = 2;

    private const int ErrorBufSize = 256;

    private static long _nextInstanceId = -1;
    private static readonly ConcurrentDictionary<long, Instance> Instances = new();

    private static long _nextHandleId = -1;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_create")]
    public static int Create(long* instance, byte* error)
    {
        try
        {
            var id = Interlocked.Increment(ref _nextInstanceId);
            var inst = new Instance();
            Instances[id] = inst;
            *instance = id;
            return Ok;
        }
        catch (Exception ex)
        {
            WriteError(error, ex.Message);
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_destroy")]
    public static void Destroy(long instance)
    {
        try
        {
            if (Instances.TryRemove(instance, out var inst))
                inst.Dispose();
        }
        catch
        {
            // Best-effort. Nothing to report to.
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_submit")]
    public static int Submit(long instance, byte* imageData, nuint imageLen, long* handle, byte* error)
    {
        try
        {
            var inst = GetInstance(instance);
            var imageSpan = new ReadOnlySpan<byte>(imageData, (int)imageLen);
            var image = Image.Load<Rgb24>(imageSpan);
            var resultTask = inst.Pipeline.ReadOne(image).GetAwaiter().GetResult();

            var id = Interlocked.Increment(ref _nextHandleId);
            // TODO: store resultTask in handle table for await
            *handle = id;
            return Ok;
        }
        catch (Exception ex)
        {
            WriteError(error, ex.Message);
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_await")]
    public static int Await(long instance, long handle, int timeoutMs, byte** resultJson, nuint* resultLen, byte* error)
    {
        try
        {
            return Ok;
        }
        catch (Exception ex)
        {
            WriteError(error, ex.Message);
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_cancel")]
    public static int Cancel(long instance, long handle)
    {
        try
        {
            return Ok;
        }
        catch
        {
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_free_result")]
    public static void FreeResult(byte* resultJson)
    {
        try
        {
            if (resultJson != null)
                NativeMemory.Free(resultJson);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static Instance GetInstance(long instance) =>
        Instances.TryGetValue(instance, out var inst)
            ? inst
            : throw new ArgumentException($"Invalid instance handle: {instance}");

    private static void WriteError(byte* error, string message)
    {
        if (error == null)
            return;

        var maxBytes = ErrorBufSize - 1;
        var written = Encoding.UTF8.GetBytes(message.AsSpan(), new Span<byte>(error, maxBytes));
        error[written] = 0;
    }
}
