using System.Runtime.CompilerServices;
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
    }
}

[UnmanagedArray(typeof(int), 10)]
public readonly partial record struct TestStruct;