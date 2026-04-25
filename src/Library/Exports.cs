// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr;

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
    private static readonly ConcurrentDictionary<long, Task<OcrPipelineResult>> Handles = new();

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
            Handles[id] = resultTask;
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
            if (!Handles.TryGetValue(handle, out var task))
                throw new ArgumentException($"Invalid handle: {handle}");

            var completed = task.Wait(timeoutMs < 0 ? System.Threading.Timeout.Infinite : timeoutMs);
            if (!completed)
                return Timeout;

            Handles.TryRemove(handle, out _);

            var pipelineResult = task.GetAwaiter().GetResult();
            try
            {
                var jsonResult = new OcrJsonResult(
                    Filename: null,
                    Results: pipelineResult.Results.Select(r => new OcrTextResult(
                        BoundingBox: r.BBox,
                        Text: r.Text,
                        Confidence: r.Confidence
                    )).ToList()
                );

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonResult, LibraryJsonContext.Default.OcrJsonResult);

                var buf = (byte*)NativeMemory.Alloc((nuint)(jsonBytes.Length + 1));
                jsonBytes.AsSpan().CopyTo(new Span<byte>(buf, jsonBytes.Length));
                buf[jsonBytes.Length] = 0;

                *resultJson = buf;
                *resultLen = (nuint)jsonBytes.Length;
                return Ok;
            }
            finally
            {
                pipelineResult.Image.Dispose();
            }
        }
        catch (Exception ex)
        {
            Handles.TryRemove(handle, out _);
            WriteError(error, ex.Message);
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_cancel")]
    public static int Cancel(long instance, long handle)
    {
        try
        {
            Handles.TryRemove(handle, out _);
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
            // Best-effort
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
