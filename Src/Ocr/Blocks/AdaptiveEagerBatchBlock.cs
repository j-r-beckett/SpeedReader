using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class AdaptiveEagerBatchBlock<T>
{
    public IPropagatorBlock<T, T[]> Target;
    private int _softMax;
    private int _hardMax;

    public AdaptiveEagerBatchBlock(int initialSoftMax, int initialHardMax)
    {
        _softMax = initialSoftMax;
        _hardMax = initialHardMax;

        var outBlock = new BufferBlock<T[]>();

        List<T> batchBuilder = [];

        var inBlock = new ActionBlock<T>(async input =>
        {
            batchBuilder.Add(input);
            if (batchBuilder.Count >= _softMax)
            {
                await outBlock.SendAsync(batchBuilder.ToArray());
                batchBuilder.Clear();
            }
        });

        inBlock.Completion.ContinueWith(async t =>
        {
            if (batchBuilder.Count > 0)
            {
                await outBlock.SendAsync(batchBuilder.ToArray());
            }

            if (t.IsFaulted)
            {
                ((IDataflowBlock)outBlock).Fault(t.Exception);
            }
            else
            {
                outBlock.Complete();
            }
        });

        Target = DataflowBlock.Encapsulate(inBlock, outBlock);
    }

    public void ReportPerformance(int batchSize, TimeSpan duration)
    {
        // TODO: Update _softMax, making sure to keep it below _hardMax
    }

    public void SetHardMax(int limit)
    {
        _hardMax = limit;
        _softMax = Math.Min(_softMax, limit);
    }
}
