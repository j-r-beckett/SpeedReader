using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class BlockMultiplexer<TIn, TOut> : IAsyncDisposable
{
    private readonly TransformBlock<(TIn Input, ITargetBlock<TOut> Tcs), TIn> _origin;
    private readonly ActionBlock<Tuple<TOut, ITargetBlock<TOut>>> _terminus;
    private bool _disposed;

    // Transformer block MUST maintain an ordered 1-1 correspondence between inputs and outputs.
    // For best results, the transformer should be capable of emitting backpressure.
    public BlockMultiplexer(IPropagatorBlock<TIn, TOut> transformer)
    {
        var targets = new BufferBlock<ITargetBlock<TOut>>();

        _origin = new TransformBlock<(TIn Input, ITargetBlock<TOut> Tcs), TIn>(item =>
        {
            if (!targets.Post(item.Tcs))
            {
                // secondary is unbounded, if this happens something has gone wrong
                throw new InvalidOperationException($"{nameof(targets)} declined input");
            }

            return item.Input;
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        _origin.LinkTo(transformer);

        var joiner = new JoinBlock<TOut, ITargetBlock<TOut>>(new GroupingDataflowBlockOptions
        {
            Greedy = false
        });

        transformer.LinkTo(joiner.Target1);
        targets.LinkTo(joiner.Target2);

        _terminus = new ActionBlock<Tuple<TOut, ITargetBlock<TOut>>>(async pair => await pair.Item2.SendAsync(pair.Item1));

        joiner.LinkTo(_terminus, new DataflowLinkOptions { PropagateCompletion = true });

        transformer.Completion.ContinueWith(primaryCompletionTask =>
        {
            if (!primaryCompletionTask.IsCompletedSuccessfully)
            {
                Action<ITargetBlock<TOut>> handler;

                handler = orphanedTarget =>
                {
                    orphanedTarget.Fault(
                        new MultiplexerException("Dataflow mesh faulted",
                            primaryCompletionTask.Exception!));
                };

                var orphanedCompletionHandler = new ActionBlock<ITargetBlock<TOut>>(handler);
                targets.LinkTo(orphanedCompletionHandler, new DataflowLinkOptions { PropagateCompletion = true });
            }

            _origin.Complete();
        });

        _origin.Completion.ContinueWith(async _ =>
        {
            targets.Complete();
            await targets.Completion;
            joiner.Complete();
        });
    }

    public async Task<Task<TOut>> ProcessSingle(TIn input, CancellationToken multiplexerCancellationToken, CancellationToken transformerCancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var completionSource = new TaskCompletionSource<TOut>();
        var target = new ActionBlock<TOut>(result => completionSource.TrySetResult(result));
        _ = target.Completion.ContinueWith(sts =>
        {
            if (sts.IsFaulted)
            {
                completionSource.SetException(sts.Exception.GetBaseException());
            }
            else if (sts.IsCanceled)
            {
                completionSource.SetCanceled();
            }
        });
        // No need to complete target, it will just be garbage collected

        // This is just for the convenience of the caller; once the input is in the pipeline we are unable to truly
        // cancel it.
        transformerCancellationToken.Register(() => completionSource.TrySetCanceled(transformerCancellationToken));

        if (!await _origin.SendAsync((input, target), multiplexerCancellationToken))
        {
            throw new InvalidOperationException($"{nameof(_origin)} declined input");
        }

        return completionSource.Task;
    }

    public async Task<Task<TOut[]>> ProcessMultipleAsync(IAsyncEnumerable<TIn> inputs,
        CancellationToken multiplexerCancellationToken, CancellationToken transformerCancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tasks = new List<Task<TOut>>();
        await foreach (var input in inputs.WithCancellation(multiplexerCancellationToken))
        {
            tasks.Add(await ProcessSingle(input, multiplexerCancellationToken, transformerCancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    public (ITargetBlock<TIn>, ISourceBlock<TOut>) GetAccessorBlocks()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var target = new TransformBlock<TIn, Task<TOut>>(input =>
            ProcessSingle(input, CancellationToken.None, CancellationToken.None), new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        var source = new TransformBlock<Task<TOut>, TOut>(async result => await result, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        target.LinkTo(source, new DataflowLinkOptions { PropagateCompletion = true });

        return (target, source);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _origin.Complete();
        await _terminus.Completion;
        GC.SuppressFinalize(this);
    }
}

public class MultiplexerException : Exception
{
    public MultiplexerException(string message) : base(message) { }
    public MultiplexerException(string message, Exception innerException) : base(message, innerException) { }
}
