using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class DataflowBridge<TIn, TOut> : IAsyncDisposable
{
    private readonly TransformBlock<(TIn Input, TaskCompletionSource<TOut> Tcs), TIn> _origin;
    private readonly ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>> _terminus;

    // Transformer block MUST maintain an ordered 1-1 correspondence between inputs and outputs
    public DataflowBridge(IPropagatorBlock<TIn, TOut> transformer)
    {
        var completionSources = new BufferBlock<TaskCompletionSource<TOut>>();

        _origin = new TransformBlock<(TIn Input, TaskCompletionSource<TOut> Tcs), TIn>(item =>
        {
            if (!completionSources.Post(item.Tcs))
            {
                // secondary is unbounded, if this happens something has gone wrong
                throw new InvalidOperationException($"{nameof(completionSources)} declined input");
            }

            return item.Input;
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount
        });

        _origin.LinkTo(transformer, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        var joiner = new JoinBlock<TOut, TaskCompletionSource<TOut>>(new GroupingDataflowBlockOptions
        {
            Greedy = false
        });

        transformer.LinkTo(joiner.Target1);
        completionSources.LinkTo(joiner.Target2);

        _terminus = new ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>>(pair => pair.Item2.SetResult(pair.Item1));

        joiner.LinkTo(_terminus, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });

        transformer.Completion.ContinueWith(primaryCompletionTask =>
        {
            if (primaryCompletionTask.IsFaulted)
            {
                var orphanedCompletionHandler = new ActionBlock<TaskCompletionSource<TOut>>(orphanedCompletion =>
                    orphanedCompletion.SetException(new DataflowBridgeException("Dataflow mesh completed unexpectedly", primaryCompletionTask.Exception)));
                completionSources.LinkTo(orphanedCompletionHandler, new DataflowLinkOptions { PropagateCompletion = true });
            }

            completionSources.Complete();
            joiner.Complete();
        });
    }

    public bool ProcessAsync(TIn input, out Task<TOut> result)
    {
        ObjectDisposedException.ThrowIf(_origin.Completion.IsCompleted, this);

        var completionSource = new TaskCompletionSource<TOut>();
        if (_origin.Post((input, completionSource)))
        {
            result = completionSource.Task;
            return true;
        }

        result = default!;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _origin.Complete();
        await _terminus.Completion;
        GC.SuppressFinalize(this);
    }
}

public class DataflowBridgeException(string message, Exception inner) : Exception(message, inner);
