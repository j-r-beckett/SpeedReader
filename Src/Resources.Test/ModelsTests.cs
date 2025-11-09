// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

// NOTE: These tests are commented out pending migration to the new InferenceEngine stack.
// The Models class uses the old Resources.Model enum which conflicts with Ocr.InferenceEngine.Kernels.Model.
// TODO: Refit these tests to work with the new stack.

/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;

namespace Resources.Test;

public class ModelsTests
{
    [Theory]
    [MemberData(nameof(GetAllModels))]
    public void CanGetModelBytes(Model model)
    {
        var modelBytes = Models.GetModelBytes(model);
        Assert.NotNull(modelBytes);
        Assert.True(modelBytes.Length > 0);

        // Verify it's a valid ONNX model by constructing an InferenceSession
        using var session = new InferenceSession(modelBytes);
        Assert.NotNull(session);
    }

    public static IEnumerable<object[]> GetAllModels() => Enum.GetValues<Model>().Select(model => new object[] { model });

    [Fact]
    public void NonExistentModelFile_ThrowsOnnxRuntimeException()
    {
        // Verify that if the model file does not exist, constructing an InferenceSession throws an exception
        var action = () => new InferenceSession("/path/that/does/not/exist/model.onnx");
        Assert.Throws<OnnxRuntimeException>(action);
    }

    [Fact]
    public void InvalidModelFile_ThrowsException()
    {
        // Verify that if the model file is not a valid ONNX file, constructing an InferenceSession throws an exception
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "This is not a valid ONNX model file");

        try
        {
            var action = () => new InferenceSession(tempFile);
            Assert.Throws<OnnxRuntimeException>(action);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
*/
