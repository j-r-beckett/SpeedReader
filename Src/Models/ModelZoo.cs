using System.Reflection;
using Microsoft.ML.OnnxRuntime;

namespace Models;

public class ModelZoo
{
    private const string ModelDir = "models";
    private const string ModelOnnxFileName = "end2end.onnx";

    public static InferenceSession GetInferenceSession(Model model) => GetInferenceSession(model, new SessionOptions());

    public static InferenceSession GetInferenceSession(Model model, SessionOptions options)
        => new(GetModelPath(model), options);

    private static string GetModelPath(Model model)
    {
        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string modelName = model switch
        {
            Model.DbNet18 => "dbnet_resnet18_fpnc_1200e_icdar2015",
            Model.Robust31 => "robustscanner_resnet31_5e_st-sub_mj-sub_sa_real",
            _ => throw new ArgumentException($"Unknown model {model}")
        };
        // Example: my/assembly/location/models/dbnet_resnet18_fpnc_1200e_icdar2015/end2end.onnx
        return Path.Combine(assemblyDirectory, ModelDir, modelName, ModelOnnxFileName);
    }
}

public enum Model
{
    DbNet18,
    Robust31
}
