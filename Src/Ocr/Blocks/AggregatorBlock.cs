using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Ocr.Blocks;

public class AggregatorBlock<T>
{
    private readonly TransformBlock<int, T[]> _transformer;
    private readonly ActionBlock<T> _inBuffer;

    private readonly Channel<T> _channel;

    public ITargetBlock<int> BatchSizeTarget => _transformer;
    public ITargetBlock<T> InputTarget => _inBuffer;
    public ISourceBlock<T[]> OutputTarget => _transformer;

    public AggregatorBlock()
    {
        _channel = Channel.CreateUnbounded<T>();

        _inBuffer = new ActionBlock<T>(async item => await _channel.Writer.WriteAsync(item));

        List<T> resultBuilder = [];

        _transformer = new TransformBlock<int, T[]>(async batchSize =>
        {
            for (int i = 0; i < batchSize && (!_channel.Reader.Completion.IsCompleted || _channel.Reader.Count > 0); i++)
            {
                resultBuilder.Add(await _channel.Reader.ReadAsync());
            }

            T[] result = resultBuilder.ToArray();
            resultBuilder.Clear();
            return result;
        });

        _inBuffer.Completion.ContinueWith(t =>
        {
            _channel.Writer.Complete();

            if (t.IsFaulted)
            {
                ((IDataflowBlock)_transformer).Fault(t.Exception);
            }
            else
            {
                _transformer.Complete();
            }
        });
    }
}
