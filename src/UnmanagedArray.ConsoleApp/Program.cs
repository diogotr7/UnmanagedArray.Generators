using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnmanagedArrayGenerator;

namespace UnmanagedArray.ConsoleApp;

public static class Program
{
    public static void Main()
    {
        var sizeOf = Unsafe.SizeOf<TestStruct>();
        var expectedSize = 10 * Unsafe.SizeOf<int>();
        if (sizeOf != expectedSize)
            throw new Exception($"Expected size of {expectedSize}, got {sizeOf}");
        
        var str = new TestStruct();
        var i = 0;
        foreach (ref var item in str.AsSpan())
        {
            item = i++;
        }
        
        Console.WriteLine(str);
    }
}

[UnmanagedArray(typeof(int), 10)]
public readonly partial record struct TestStruct;