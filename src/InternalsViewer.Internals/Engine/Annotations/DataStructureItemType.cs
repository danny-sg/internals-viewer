﻿namespace InternalsViewer.Internals.Engine.Annotations;

public enum DataStructureItemType
{
    ColumnOffsetArray,
    StatusBitsA,
    StatusBitsB,
    ColumnCount,
    ColumnCountOffset,
    VariableLengthColumnCount,
    NullBitmap,
    ForwardingRecord,
    DownPagePointer,
    Rid,
    SparseColumns,
    SparseColumnOffsets,
    SparseColumnCount,
    ComplexHeader,
    BlobData,
    BlobId,
    BlobLength,
    BlobType,
    MaxLinks,
    CurrentLinks,
    Level,
    BlobSize,
    CompressedValue,
    SlotOffset,
    Value,
    BlobChildOffset,
    BlobChildLength,
    PageModCount,
    CiSize,
    CiLength,
    Timestamp,
    PointerType,
    EntryCount,
    OverflowLength,
    Unused,
    UpdateSeq,
    CdArrayItem,
    SlotCount
}