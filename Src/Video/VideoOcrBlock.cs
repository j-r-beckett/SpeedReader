using System.IO;
using System.Threading.Tasks.Dataflow;
using Ocr.Blocks;

namespace Video;

public class VideoOcrBlock
{
    public ITargetBlock<(Stream videoData, int sampleRate)> Target { get; init; }
    public ISourceBlock<>

    public VideoOcrBlock(OcrBlock ocrBlock)
    {

    }
}
