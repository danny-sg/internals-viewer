namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

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