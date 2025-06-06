using System.Buffers;
using System.Numerics.Tensors;

namespace OCR;

// Provides views into backing memory
public class Buffer<T> : IDisposable where T : unmanaged
{
    private static readonly ArrayPool<T> s_pool = ArrayPool<T>.Create();

    public nint[] Shape
    {
        get;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Buffer<T>));

            if (_size != TotalSize(value))
            {
                throw new ArgumentException($"Shape {value} does not match size {_size}");
            }

            field = value;
        }
    }

    private readonly T[] _backingArray;
    private readonly int _size;
    private bool _disposed;

    public Buffer(int size, nint[] shape)
    {
        if (size <= 0)
        {
            throw new ArgumentException("Size must be positive");
        }

        if (size != TotalSize(shape))
        {
            throw new ArgumentException($"Shape {shape} does not match size {size}");
        }

        _backingArray = s_pool.Rent(size);  // may return an array larger than size!
        _size = size;
        Shape = shape;
    }

    public Tensor<T> AsTensor() => AsTensorInternal(0, Shape);

    public Tensor<T> AsTensor(nint[] shape) => AsTensorInternal(0, shape);

    public Tensor<T> AsTensor(int start, nint[] shape) => AsTensorInternal(start, shape);

    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Buffer<T>));

        return _backingArray.AsSpan(0, _size);
    }

    private Tensor<T> AsTensorInternal(int start, nint[] shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Buffer<T>));

        if (start < 0)
        {
            throw new ArgumentException($"Start {start} is less than zero");
        }

        if (start + TotalSize(shape) > _size)
        {
            throw new ArgumentException($"Start {start} + shape size {TotalSize(shape)} exceeds buffer size {_size}");
        }

        return Tensor.Create(_backingArray, start, shape, default);
    }

    private static int TotalSize(nint[] shape) => shape.Aggregate(1, (a, b) => a * (int)b);

    public void Dispose()
    {
        if (!_disposed)
        {
            s_pool.Return(_backingArray);
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
