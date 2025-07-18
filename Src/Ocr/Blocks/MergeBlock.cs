using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class MergeBlock<TInLeft, TInRight, TOut>
{
    public readonly ITargetBlock<TInLeft> LeftTarget;
    public readonly ITargetBlock<TInRight> RightTarget;
    public readonly ISourceBlock<TOut> Source;

    public MergeBlock(Func<TInLeft, TInRight, TOut> merger)
    {
        var joinBlock = new JoinBlock<TInLeft, TInRight>(new GroupingDataflowBlockOptions { Greedy = false });

        var source = new TransformBlock<Tuple<TInLeft, TInRight>, TOut>(input => merger(input.Item1, input.Item2));

        joinBlock.LinkTo(source, new DataflowLinkOptions { PropagateCompletion = true });

        LeftTarget = joinBlock.Target1;
        RightTarget = joinBlock.Target2;
        Source = source;
    }
}
