using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;

namespace Models.Test;

public class ModelZooTests
{
    [Theory]
    [MemberData(nameof(GetAllModels))]
    public void CanRetrieveSession(Model model)
    {
        // Constructing an InferenceSession will throw an exception if the model file doesn't exist or isn't in ONNX format
        using var session = ModelZoo.GetInferenceSession(model);
        Assert.NotNull(session);
    }

    public static IEnumerable<object[]> GetAllModels()
    {
        return Enum.GetValues<Model>().Select(model => new object[] { model });
    }

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
