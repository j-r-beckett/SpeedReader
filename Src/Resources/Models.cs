namespace Resources;

public static class Models
{
    public static byte[] GetModelBytes(Model model)
    {
        string resourceName = GetResourceName(model);
        return Resource.GetBytes(resourceName);
    }

    private static string GetResourceName(Model model)
    {
        string modelName = model switch
        {
            Model.DbNet18 => "dbnet_resnet18_fpnc_1200e_icdar2015",
            Model.SVTRv2 => "svtrv2_base_ctc",
            _ => throw new ArgumentException($"Unknown model {model}")
        };

        return $"models.{modelName}.end2end.onnx";
    }
}

public enum Model
{
    DbNet18,
    SVTRv2
}