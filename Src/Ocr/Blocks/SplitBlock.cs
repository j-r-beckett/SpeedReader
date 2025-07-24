using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class SplitBlock<TIn, TOutLeft, TOutRight>
{
    public readonly ITargetBlock<TIn> Target;
    public readonly ISourceBlock<TOutLeft> LeftSource;
    public readonly ISourceBlock<TOutRight> RightSource;

    public SplitBlock(Func<TIn, (TOutLeft, TOutRight)> splitter)
    {
        var leftSource = new BufferBlock<TOutLeft>(new GroupingDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount
        });
        var rightSource = new BufferBlock<TOutRight>(new GroupingDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount
        });

        Target = new ActionBlock<TIn>(async item =>
        {
            (TOutLeft left, TOutRight right) = splitter(item);
            await Task.WhenAll(leftSource.SendAsync(left), rightSource.SendAsync(right));
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount
        });

        Target.Completion.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                ((IDataflowBlock)leftSource).Fault(t.Exception);
                ((IDataflowBlock)rightSource).Fault(t.Exception);
            }
            else
            {
                leftSource.Complete();
                rightSource.Complete();
            }
        });

        LeftSource = leftSource;
        RightSource = rightSource;
    }
}
