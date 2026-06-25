using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records.Data;

public sealed class SparseVector : DataStructure
{
    public const int ColCountOffset = 2;
    public const int ColumnsOffset = 4;

    internal SparseVector(byte[] sparseRecord, TableStructure structure, DataRecord parentRecord, short recordOffset)
    {
        Data = sparseRecord;
        Structure = structure;
        ParentRecord = parentRecord;
        RecordOffset = recordOffset;
    }

    public ushort[] Columns { get; set; } = [];

    public byte[] Data { get; set; }

    [DataStructureItem(ItemType.SparseColumns)]
    public string ColumnsDescription => RecordHelpers.GetArrayString(Columns);

    [DataStructureItem(ItemType.SparseColumnOffsets)]
    public string OffsetsDescription => RecordHelpers.GetArrayString(Offset);

    public ushort[] Offset { get; set; } = Array.Empty<ushort>();

    [DataStructureItem(ItemType.SparseColumnCount)]
    public short ColCount { get; set; }

    public short RecordOffset { get; set; }

    public short ComplexHeader { get; set; }

    [DataStructureItem(ItemType.ComplexHeader)]
    public string ComplexHeaderDescription => GetComplexHeaderDescription(ComplexHeader);

    internal DataRecord ParentRecord { get; set; }

    internal TableStructure Structure { get; set; }

    private static string GetComplexHeaderDescription(short complexVector)
    {
        switch (complexVector)
        {
            case 5:
                return "In row sparse vector";
            default:
                return "Unknown";
        }
    }
}