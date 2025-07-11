using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class DataflowBridge<TIn, TOut> : IAsyncDisposable
{
    private readonly TransformBlock<(TIn Input, TaskCompletionSource<TOut> Tcs), TIn> _in;
    private readonly ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>> _out;

    // Block MUST maintain an ordered 1-1 correspondence between inputs and outputs
    public DataflowBridge(IPropagatorBlock<TIn, TOut> block)
    {
        var completions = new BufferBlock<TaskCompletionSource<TOut>>();

        _in = new TransformBlock<(TIn Input, TaskCompletionSource<TOut> Tcs), TIn>(data =>
        {
            if (!completions.Post(data.Tcs))
            {
                // completions is unbounded, if this happens something has gone wrong
                throw new InvalidOperationException($"{nameof(completions)} declined input");
            }

            return data.Input;
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount
        });

        _in.LinkTo(block, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        var join = new JoinBlock<TOut, TaskCompletionSource<TOut>>(new GroupingDataflowBlockOptions
        {
            Greedy = false
        });

        block.LinkTo(join.Target1);
        completions.LinkTo(join.Target2);

        block.Completion.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var cleanupBlock = new ActionBlock<TaskCompletionSource<TOut>>(tcs => tcs.SetException(new DataflowBridgeException("Dataflow mesh completed unexpectedly", t.Exception)));
                completions.LinkTo(cleanupBlock, new DataflowLinkOptions { PropagateCompletion = true });
            }

            completions.Complete();
            join.Complete();
        });

        _out = new ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>>(data => data.Item2.SetResult(data.Item1));

        join.LinkTo(_out, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });
    }

    public bool ProcessAsync(TIn input, out Task<TOut> result)
    {
        ObjectDisposedException.ThrowIf(_in.Completion.IsCompleted, this);

        var tcs = new TaskCompletionSource<TOut>();
        if (_in.Post((input, tcs)))
        {
            result = tcs.Task;
            return true;
        }

        result = default!;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _in.Complete();
        await _out.Completion;
        GC.SuppressFinalize(this);
    }
}

public class DataflowBridgeException(string message, Exception inner) : Exception;
