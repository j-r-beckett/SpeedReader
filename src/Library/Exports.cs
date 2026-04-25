// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using System.Text;

namespace SpeedReader.Library;

public static unsafe class Exports
{
    private const int Ok = 0;
    private const int Error = 1;
    private const int Timeout = 2;

    private const int ErrorBufSize = 256;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_create")]
    public static int Create(nint* instance, byte* error)
    {
        try
        {
            var inst = new Instance();
            var gcHandle = GCHandle.Alloc(inst);
            *instance = GCHandle.ToIntPtr(gcHandle);
            return Ok;
        }
        catch (Exception ex)
        {
            WriteError(error, ex.Message);
            return Error;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_destroy")]
    public static void Destroy(nint instance)
    {
        try
        {
            var gcHandle = GCHandle.FromIntPtr(instance);
            gcHandle.Free();
        }
        catch
        {
            // Best-effort. Nothing to report to.
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_submit")]
    public static int Submit(nint instance, byte* imageData, nuint imageLen, long* handle, byte* error)
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

    [UnmanagedCallersOnly(EntryPoint = "speedreader_await")]
    public static int Await(nint instance, long handle, int timeoutMs, byte** resultJson, nuint* resultLen, byte* error)
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
    public static int Cancel(nint instance, long handle)
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

    private static Instance GetInstance(nint instance) => (Instance)GCHandle.FromIntPtr(instance).Target!;

    private static void WriteError(byte* error, string message)
    {
        if (error == null)
            return;

        var maxBytes = ErrorBufSize - 1;
        var written = Encoding.UTF8.GetBytes(message.AsSpan(), new Span<byte>(error, maxBytes));
        error[written] = 0;
    }
}
