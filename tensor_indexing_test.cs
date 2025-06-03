using System;
using System.Numerics.Tensors;

// Test to explore TensorSpan indexing capabilities
class TensorIndexingTest
{
    static void Main()
    {
        // Create a simple 2D tensor [2, 3]
        var data = new float[] { 1, 2, 3, 4, 5, 6 };
        ReadOnlySpan<nint> shape = [2, 3];
        var tensor = Tensor.Create(data, shape);
        var tensorSpan = tensor.AsTensorSpan();
        
        Console.WriteLine("Tensor shape: " + string.Join(", ", tensor.Lengths.ToArray()));
        
        // Test what indexing methods are available
        try
        {
            // Try single index access
            ReadOnlySpan<nint> indices = [0, 1];
            var value = tensorSpan[indices];
            Console.WriteLine($"Element at [0,1]: {value}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Single element access failed: {e.Message}");
        }
        
        // Check available properties and methods
        Console.WriteLine($"Rank: {tensorSpan.Rank}");
        Console.WriteLine($"FlattenedLength: {tensorSpan.FlattenedLength}");
        Console.WriteLine($"Lengths: [{string.Join(", ", tensorSpan.Lengths.ToArray())}]");
    }
}