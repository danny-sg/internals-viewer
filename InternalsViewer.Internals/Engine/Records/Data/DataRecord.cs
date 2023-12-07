﻿using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records.Data;

public class DataRecord : Record
{
    public DataRecord(Page page, ushort slotOffset, Structure structure)
        : base(page, slotOffset, structure)
    {
        DataRecordLoader.Load(this);
    }

    public SparseVector SparseVector { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append(
            $"DataRecord | Page: {Page.Header.PageAddress} | Slot Offset: {SlotOffset} | Allocation Unit: {Page.Header.AllocationUnit}\n");

        sb.Append("-----------------------------------------------------------------------------------------\n");
        sb.Append($"Status Bits A:                {GetStatusBitsDescription(this)}\n");
        sb.Append($"Column count offset:          {ColumnCountOffset}\n");
        sb.Append($"Number of columns:            {ColumnCount}\n");
        sb.Append(
            $"Null bitmap:                  {(HasNullBitmap ? GetNullBitmapString(NullBitmap) : "(No null bitmap)")}\n");
        sb.Append($"Variable length column count: {VariableLengthColumnCount}\n");
        sb.Append(
            $"Column offset array:          {(HasVariableLengthColumns ? GetArrayString(ColOffsetArray) : "(no variable length columns)")}\n");

        foreach (var field in Fields)
        {
            sb.AppendLine(field.ToString());
        }
        return sb.ToString();
    }

    [Mark(MarkType.StatusBitsB)]
    public string StatusBitsBDescription => "";

    [Mark(MarkType.ForwardingRecord)]
    public RowIdentifier ForwardingRecord { get; set; }
}