using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

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