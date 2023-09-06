using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnmanagedArrayGenerator;

namespace UnmanagedArray.ConsoleApp;

public static class Program
{
    public static void Main()
    {
        var str = new TestStruct();
        var span = str.AsSpan();
        var size = Unsafe.SizeOf<TestStruct>();
        Console.WriteLine($"Size of TestStruct: {size}");
        Console.WriteLine($"TestStruct has {span.Length} elements");
    }
}

[UnmanagedArray(typeof(Vector<int>), 10)]
public readonly partial struct TestStruct
{
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Vector<T> where T : unmanaged
{
    public readonly T x;
    public readonly T y;
    public readonly T z;
}