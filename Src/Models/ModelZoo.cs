using System.Reflection;
using Microsoft.ML.OnnxRuntime;

namespace Models;

public class ModelZoo
{
    private static readonly Lock s_lock = new();

    public static InferenceSession GetInferenceSession(Model model) => GetInferenceSession(model, new SessionOptions
    {
        IntraOpNumThreads = Environment.ProcessorCount / 2
    });

    public static InferenceSession GetInferenceSession(Model model, SessionOptions options)
    {
        lock (s_lock)
        {
            string resourceName = GetResourceName(model);
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found");

            var modelBytes = new byte[stream.Length];
            stream.ReadExactly(modelBytes);

            return new InferenceSession(modelBytes, options);
        }
    }

    private static string GetResourceName(Model model)
    {
        string modelName = model switch
        {
            Model.DbNet18 => "dbnet_resnet18_fpnc_1200e_icdar2015",
            Model.SVTRv2 => "svtrv2_base_ctc",
            _ => throw new ArgumentException($"Unknown model {model}")
        };

        return $"Models.models.{modelName}.end2end.onnx";
    }
}

public enum Model
{
    DbNet18,
    SVTRv2
}
