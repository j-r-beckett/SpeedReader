// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace SpeedReader.Library;

public static unsafe class Exports
{
    private const int Ok = 0;
    private const int Error = 1;
    private const int Timeout = 2;

    private const int ErrorBufSize = 256;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_create")]
    public static int Create(nint* instance, byte* error) => Ok;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_destroy")]
    public static void Destroy(nint instance)
    {
    }

    [UnmanagedCallersOnly(EntryPoint = "speedreader_submit")]
    public static int Submit(nint instance, byte* imageData, nuint imageLen, long* handle, byte* error) => Ok;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_await")]
    public static int Await(nint instance, long handle, int timeoutMs, byte** resultJson, nuint* resultLen, byte* error) => Ok;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_cancel")]
    public static int Cancel(nint instance, long handle) => Ok;

    [UnmanagedCallersOnly(EntryPoint = "speedreader_free_result")]
    public static void FreeResult(byte* resultJson)
    {
    }
}
