using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Ocr;
using Ocr.Blocks;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Video;

namespace Core;

public class CliVideoOcrBlock
{
    public ISourceBlock<OcrResult> ResultsBlock { get; init; }

    public CliVideoOcrBlock(string videoFilePath, int sampleRate)
    {
        var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        var ocrBlock = new OcrBlock(dbnetSession, svtrSession, new OcrConfiguration(), new Meter("SpeedReader.Ocr"));

        using var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);
        var videoOcrBlock = new VideoOcrBlock(ocrBlock, fileStream, sampleRate);

        var splitBlock =
            new SplitBlock<(Image<Rgb24> Img, OcrResult Result), (Image<Rgb24>, OcrResult), OcrResult>(item =>
            {
                return ((item.Img, item.Result), item.Result);
            });

        videoOcrBlock.Source.LinkTo(splitBlock.Target);

        ResultsBlock = splitBlock.RightSource;
    }
}
