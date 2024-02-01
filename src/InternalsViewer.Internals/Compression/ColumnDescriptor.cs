using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Compression;

public enum ColumnDescriptorFlag : byte
{
    Null = 0,
    ZeroByteShort = 1,
    OneByteShort = 2,
    TwoByteShort = 3,
    ThreeByteShort = 4,
    FourByteShort = 5,
    FiveByteShort = 6,
    SixByteShort = 7,
    SevenByteShort = 8,
    EightByteShort = 9,
    Long = 10,
    BitTrue = 11,
    PageSymbol = 12
}

public class ColumnDescriptor(byte value) : DataStructure
{
    public ColumnDescriptorFlag Value { get; } = (ColumnDescriptorFlag)value;

    public int Size => Value switch
    {
        ColumnDescriptorFlag.Null => 0,
        ColumnDescriptorFlag.ZeroByteShort => 0,
        ColumnDescriptorFlag.OneByteShort => 1,
        ColumnDescriptorFlag.TwoByteShort => 2,
        ColumnDescriptorFlag.ThreeByteShort => 3,
        ColumnDescriptorFlag.FourByteShort => 4,
        ColumnDescriptorFlag.FiveByteShort => 5,
        ColumnDescriptorFlag.SixByteShort => 6,
        ColumnDescriptorFlag.SevenByteShort => 7,
        ColumnDescriptorFlag.EightByteShort => 8,
        ColumnDescriptorFlag.Long => 0,
        ColumnDescriptorFlag.BitTrue => 0,
        ColumnDescriptorFlag.PageSymbol => 1,
        _ => 0
    };

    public override string ToString()
    {
        return Value.ToString().SplitCamelCase();
    }
}