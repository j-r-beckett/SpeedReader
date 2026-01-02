// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Win32.SafeHandles;

namespace SpeedReader.Native.Onnx.Internal;

internal sealed class SafeEnvironmentHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeEnvironmentHandle() : base(ownsHandle: true) { }

    protected override bool ReleaseHandle()
    {
        SpeedReaderOrt.speedreader_ort_destroy_env(handle);
        return true;
    }
}

internal sealed class SafeSessionHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeSessionHandle() : base(ownsHandle: true) { }

    protected override bool ReleaseHandle()
    {
        SpeedReaderOrt.speedreader_ort_destroy_session(handle);
        return true;
    }
}
