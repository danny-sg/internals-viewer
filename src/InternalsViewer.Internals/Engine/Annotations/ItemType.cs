namespace InternalsViewer.Internals.Engine.Annotations;

public enum ItemType
{
    //<!--Header-->

    //<models:MarkStyle x:Key = "HeaderPageAddressMarkerStyle" ForeColour = "#D50000" BackColour = "#EDE7F6" Name = "Page Address" />
    //< models:MarkStyle x:Key = "PageTypeMarkerStyle" ForeColour = "#C51162" BackColour = "#E3F2FD" Name = "Page Type" />
    //< models:MarkStyle x:Key = "NextPageMarkerStyle" ForeColour = "#AA00FF" BackColour = "#EDE7F6" Name = "Next Page" />
    //< models:MarkStyle x:Key = "PreviousPageMarkerStyle" ForeColour = "#6200EA" BackColour = "#EDE7F6" Name = "Previous Page" />
    //< models:MarkStyle x:Key = "InternalObjectIdMarkerStyle" ForeColour = "#304FFE" BackColour = "#BBDEFB" Name = "Internal Object Id" />
    //< models:MarkStyle x:Key = "InternalIndexIdMarkerStyle" ForeColour = "#2962FF" BackColour = "#BBDEFB" Name = "Internal Index Id" />
    //< models:MarkStyle x:Key = "IndexLevelMarkerStyle" ForeColour = "#0277BD" BackColour = "#E3F2FD" Name = "Index Level" />
    //< models:MarkStyle x:Key = "SlotCountMarkerStyle" ForeColour = "#004D40" BackColour = "#E3F2FD" Name = "Slot Count" />
    //< models:MarkStyle x:Key = "FixedLengthSizeMarkerStyle" ForeColour = "#00C853" BackColour = "#E3F2FD" Name = "Fixed Length Size" />
    //< models:MarkStyle x:Key = "FreeCountMarkerStyle" ForeColour = "#1B5E20" BackColour = "#E3F2FD" Name = "Free Count" />
    //< models:MarkStyle x:Key = "FreeDataOffsetMarkerStyle" ForeColour = "#F9A825" BackColour = "#E3F2FD" Name = "Free Data Offset" />
    //< models:MarkStyle x:Key = "ReservedCountMarkerStyle" ForeColour = "#827717" BackColour = "#E3F2FD" Name = "Reserved Count" />
    //< models:MarkStyle x:Key = "TransactionReservedMarkerStyle" ForeColour = "#FF6D00" BackColour = "#E3F2FD" Name = "Transaction Reserved" />
    //< models:MarkStyle x:Key = "TornBitsMarkerStyle" ForeColour = "#DD2C00" BackColour = "#E3F2FD" Name = "Torn Bits" />
    //< models:MarkStyle x:Key = "FlagBitsMarkerStyle" ForeColour = "#212121" BackColour = "#E3F2FD" Name = "Flag Bits" />
    //< models:MarkStyle x:Key = "LsnMarkerStyle" ForeColour = "#263238" BackColour = "#E3F2FD" Name = "LSN" />
    //< models:MarkStyle x:Key = "HeaderVersionMarkerStyle" ForeColour = "#455A64" BackColour = "#E3F2FD" Name = "Header Version" />
    //< models:MarkStyle x:Key = "GhostRecordCountMarkerStyle" ForeColour = "#546E7A" BackColour = "#E3F2FD" Name = "Ghost Record Count" />
    //< models:MarkStyle x:Key = "TypeFlagBitsMarkerStyle" ForeColour = "#546E7A" BackColour = "#E3F2FD" Name = "Type Flag Bits" />
    //< models:MarkStyle x:Key = "InternalTransactionIdMarkerStyle" ForeColour = "#6D4C41" BackColour = "#E3F2FD" Name = "Internal Transaction Id" />

    //< !--FixedVar Format-->

    //<models:MarkStyle x:Key = "StatusBitsAMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#193960" Name = "Status Bits A" />
    //< models:MarkStyle x:Key = "StatusBitsBMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#2C5C5B" Name = "Status Bits B" />
    //< models:MarkStyle x:Key = "ColumnCountOffsetMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#266AAE" Name = "Column Count Offset" />
    //< models:MarkStyle x:Key = "ColumnCountMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#518183" Name = "Column Count" />
    //< models:MarkStyle x:Key = "NullBitmapMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#8AB7BD" Name = "Null Bitmap" />
    //< models:MarkStyle x:Key = "VariableLengthColumnCountMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#606264" Name = "Variable Length Column Count" />
    //< models:MarkStyle x:Key = "VariableLengthColumnOffsetArrayMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#2D563A" Name = "Variable Length Column Offset Array" />
    //< models:MarkStyle x:Key = "FixedLengthValueMarkerStyle" ForeColour = "#00" BackColour = "#D6DAD4" Name = "Fixed Length Value" />
    //< models:MarkStyle x:Key = "VariableLengthValueMarkerStyle" ForeColour = "#00" BackColour = "#C2D0CB" Name = "Variable Length Value" />
    //< models:MarkStyle x:Key = "ForwardingStubMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#d85240" Name = "Forwarding Stub" />

    //< !--CD Format-->

    //<models:MarkStyle x:Key = "RecordHeaderMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#7ea597" Name = "Record Header" />
    //< models:MarkStyle x:Key = "ColumnDescriptorMarkerStyle" ForeColour = "#00" BackColour = "#B6F2D0" Name = "Column Descriptor" />
    //< models:MarkStyle x:Key = "ShortDataClusterArrayMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#345D7F" Name = "Short Data Cluster Array" />
    //< models:MarkStyle x:Key = "ShortFieldValueMarkerStyle" ForeColour = "#00" BackColour = "#BBD9E8" Name = "Short Field Value" />
    //< models:MarkStyle x:Key = "LongDataHeaderMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#ab5384" Name = "Long Data Header" />
    //< models:MarkStyle x:Key = "LongDataOffsetCountMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#DEBED0" Name = "Long Data Offset Count" />
    //< models:MarkStyle x:Key = "LongDataOffsetArrayMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#B28D8A" Name = "Long Data Offset Array" />
    //< models:MarkStyle x:Key = "LongDataClusterArrayMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#735a6d" Name = "Long Data Cluster Array" />
    //< models:MarkStyle x:Key = "LongFieldValueMarkerStyle" ForeColour = "#00" BackColour = "#e2bbe8" Name = "Long Field Value" />

    //< !--Index-- >


    //< models:MarkStyle x:Key = "UniquifierIndexMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#313240" Name = "Uniquifier" />
    //< models:MarkStyle x:Key = "RidIndexMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#313240" Name = "RID" />
    //< models:MarkStyle x:Key = "DownPagePointerIndexMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#313240" Name = "Down Page Pointer" />

    //< !--Compression Info-->

    //<models:MarkStyle x:Key = "HeaderMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#06411a" Name = "Header" />
    //< models:MarkStyle x:Key = "PageModificationCountMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#8EBC49" Name = "Page Modification Count" />
    //< models:MarkStyle x:Key = "LengthMarkerStyle" ForeColour = "#FFFFFF" BackColour = "#26994C" Name = "Length" />
    //< models:MarkStyle x:Key = "AnchorRecordMarkerStyle" ForeColour = "#00" BackColour = "#ECECEC" Name = "Anchor Record" />

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