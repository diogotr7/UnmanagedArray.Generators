namespace UnmanagedArray.Generators;

public class StructInfo
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string Namespace { get; set; }
    public string ParentStruct { get; set; }
    public string ChildStruct { get; set; }
    public int Count { get; set; }
    public bool IsRecordStruct { get; set; }
    public bool IsReadOnly { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}