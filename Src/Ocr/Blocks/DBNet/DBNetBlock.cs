using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.Blocks.DBNet;

public class DBNetBlock
{
    public IPropagatorBlock<OcrContext, (List<TextBoundary>, OcrContext)> Target { get; }

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
