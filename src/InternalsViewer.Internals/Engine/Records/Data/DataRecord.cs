using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

namespace InternalsViewer.Internals.Engine.Records.Data;

public sealed class DataRecord : FixedVarRecord
{
    public SparseVector? SparseVector { get; set; }

    [DataStructureItem(ItemType.StatusBitsB)]
    public string StatusBitsBDescription => string.Empty;

    [DataStructureItem(ItemType.ForwardingStub)]
    public RowIdentifier? ForwardingStub { get; set; }

    public RowIdentifier? RowIdentifier { get; set; }
}
