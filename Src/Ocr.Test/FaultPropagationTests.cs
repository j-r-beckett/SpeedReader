// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Core;
using Ocr.Blocks;
using Resources;

namespace Ocr.Test;

public class FaultPropagationTests
{
    [Fact]
    public async Task OcrBlock_PropagatesFault()
    {
        var modelProvider = new ModelProvider();
        var dbnet = modelProvider.GetSession(Model.DbNet18);
        var svtrv2 = modelProvider.GetSession(Model.SVTRv2);
        var ocrBlock = new OcrBlock(dbnet, svtrv2, new OcrConfiguration(), new Meter("test_meter"));
        var fault = new InvalidOperationException("this is a test exception");
        ocrBlock.Block.Fault(fault);
        var exception = await Assert.ThrowsAsync<AggregateException>(async () => await ocrBlock.Block.Completion);
        Assert.Equal(fault.Message, exception.GetBaseException().Message);
    }

    [Fact]
    public async Task CliOcrBlock_PropagatesFault()
    {
        var cliOcrBlock = new CliOcrBlock(new CliOcrBlock.Config());
        var fault = new InvalidOperationException("this is a test exception");
        cliOcrBlock.Target.Fault(fault);
        var exception = await Assert.ThrowsAsync<AggregateException>(async () => await cliOcrBlock.Completion);
        Assert.Equal(fault.Message, exception.GetBaseException().Message);
    }
}
