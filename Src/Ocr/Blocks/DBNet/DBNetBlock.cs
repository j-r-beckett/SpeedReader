using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.DBNet;

public class DBNetBlock
{
    public IPropagatorBlock<(Image<Rgb24>, VizBuilder), (List<TextBoundary>, Image<Rgb24>, VizBuilder)> Target { get; }

    public DBNetBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var preprocessingBlock = new DBNetPreprocessingBlock(config.DbNet);
        var modelRunnerBlock = new DBNetModelRunnerBlock(session, config, meter);
        var postprocessingBlock = new DBNetPostprocessingBlock(config.DbNet);

        preprocessingBlock.Target.LinkTo(modelRunnerBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.Target.LinkTo(postprocessingBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(preprocessingBlock.Target, postprocessingBlock.Target);
    }
}
