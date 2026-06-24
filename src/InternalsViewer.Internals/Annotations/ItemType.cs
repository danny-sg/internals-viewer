namespace InternalsViewer.Internals.Annotations;

public enum ItemType
{
    // Allocations
    PageAddress,

    // Page Header
    AllocationUnitId,
    HeaderPageAddress,
    PageType,
    NextPage,
    PreviousPage,
    InternalObjectId,
    InternalIndexId,
    IndexLevel,
    SlotCount,
    FixedLengthSize,
    FreeCount,
    FreeDataOffset,
    ReservedCount,
    TransactionReserved,
    TornBits,
    FlagBits,
    Lsn,
    HeaderVersion,
    GhostRecordCount,
    TypeFlagBits,
    InternalTransactionId,

    // FixedVar Format
    StatusBitsA,
    StatusBitsB,
    ColumnCountOffset,
    ColumnCount,
    NullBitmap,
    VariableLengthColumnCount,
    VariableLengthColumnOffsetArray,
    FixedLengthValue,
    VariableLengthValue,
    ForwardingStub,

    // CD Format
    RecordHeader,
    ColumnDescriptor,
    ColumnDescriptors,
    ShortDataClusterArray,
    ShortFieldValue,
    LongDataHeader,
    LongDataOffsetCount,
    LongDataOffsetArray,
    LongDataClusterArray,
    LongFieldValue,

    // Index
    UniquifierIndex,
    Rid,
    DownPagePointer,

    // Compression Info
    Header,
    PageModificationCount,
    Length,
    Size,
    AnchorRecord,
    CompressionDictionary,

    // Dictionary
    DictionaryColumnOffsets,
    DictionaryEntryCount,
    DictionaryValue,
    DictionaryEntries,
    DictionarySymbol,

    // Blob
    BlobId,
    BlobData,
    BlobLength,
    BlobType,
    BlobSize,
    BlobChildLength,
    BlobChildOffset,
    Level,
    Timestamp,
    PointerType,

    CurrentLinks,
    MaxLinks,

    // Sparse Header
    ComplexHeader,
    SparseColumnCount,
    SparseColumnOffsets,
    SparseColumns,

    // Overflow
    OverflowLevel,
    OverflowLength,
    Unused,
    UpdateSeq
}