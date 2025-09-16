// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Video;

public class StreamingPipeSource : PipeSource
{
    private readonly Stream _sourceStream;

    public StreamingPipeSource(Stream sourceStream) => _sourceStream = sourceStream;

    public override async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default) => await _sourceStream.CopyToAsync(destination, cancellationToken);
}
