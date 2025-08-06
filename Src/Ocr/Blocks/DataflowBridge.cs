using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class DataflowBridge<TIn, TOut> : IAsyncDisposable
{
    private readonly TransformBlock<(TIn Input, TaskCompletionSource<TOut> Tcs), TIn> _origin;
    private readonly ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>> _terminus;
    private bool _disposed;

    // Transformer block MUST maintain an ordered 1-1 correspondence between inputs and outputs.
    // For best results, the transformer should be capable of emitting backpressure.
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
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        _origin.LinkTo(transformer);

        var joiner = new JoinBlock<TOut, TaskCompletionSource<TOut>>(new GroupingDataflowBlockOptions
        {
            Greedy = false
        });

        transformer.LinkTo(joiner.Target1);
        completionSources.LinkTo(joiner.Target2);

        _terminus = new ActionBlock<Tuple<TOut, TaskCompletionSource<TOut>>>(pair => pair.Item2.TrySetResult(pair.Item1));

        joiner.LinkTo(_terminus, new DataflowLinkOptions { PropagateCompletion = true });

        transformer.Completion.ContinueWith(primaryCompletionTask =>
        {
            if (!primaryCompletionTask.IsCompletedSuccessfully)
            {
                Action<TaskCompletionSource<TOut>> handler;

                if (primaryCompletionTask.IsFaulted)
                {
                    handler = orphanedCompletion =>
                        orphanedCompletion.TrySetException(
                            new DataflowBridgeException("Dataflow mesh faulted",
                                primaryCompletionTask.Exception));
                }
                else
                {
                    handler = orphanedCompletion => orphanedCompletion.TrySetCanceled();
                }

                var orphanedCompletionHandler = new ActionBlock<TaskCompletionSource<TOut>>(handler);
                completionSources.LinkTo(orphanedCompletionHandler, new DataflowLinkOptions { PropagateCompletion = true });
            }

            _origin.Complete();
        });

        _origin.Completion.ContinueWith(async _ =>
        {
            completionSources.Complete();
            await completionSources.Completion;
            joiner.Complete();
        });
    }

    public async Task<Task<TOut>> ProcessAsync(TIn input, CancellationToken bridgeCancellationToken, CancellationToken transformerCancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var completionSource = new TaskCompletionSource<TOut>();

        // This is just for the convenience of the caller; once the input is in the pipeline we are unable to truly
        // cancel it.
        transformerCancellationToken.Register(() => completionSource.TrySetCanceled(transformerCancellationToken));

        if (await _origin.SendAsync((input, completionSource), bridgeCancellationToken))
        {
            return completionSource.Task;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        throw new InvalidOperationException($"{nameof(_origin)} declined input");
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _origin.Complete();
        await _terminus.Completion;
        GC.SuppressFinalize(this);
    }
}

public class DataflowBridgeException(string message, Exception inner) : Exception(message, inner);
