using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.UI.MarkStyles;

public class MarkStyleProvider
{
    private Dictionary<MarkType, MarkStyle> Styles = new()
    {
        { MarkType.Rid, new MarkStyle(Color.DarkMagenta, Color.Thistle, "Row Identifier")},
        { MarkType.BlobChildOffset, new MarkStyle(Color.Blue, Color.Thistle, "Offset")},
        { MarkType.BlobChildLength, new MarkStyle(Color.Red, Color.Thistle, "Length")},
        { MarkType.StatusBitsA, new MarkStyle(Color.Red, Color.Gainsboro, "Status Bits A")},
        { MarkType.StatusBitsB, new MarkStyle(Color.Maroon, Color.Gainsboro, "Status Bits B")},
        { MarkType.BlobId, new MarkStyle(Color.Navy, Color.AliceBlue, "ID")},
        { MarkType.BlobLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { MarkType.BlobType, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Type")},
        { MarkType.MaxLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "MaxLinks")},
        { MarkType.CurrentLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "Current Links")},
        { MarkType.Level, new MarkStyle(Color.SlateGray, Color.Gainsboro, "Level")},
        { MarkType.BlobSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { MarkType.BlobData, new MarkStyle(Color.Gray, Color.PaleGoldenrod,  "Data")},
        { MarkType.CompressedValue, new MarkStyle(Color.Black, Color.PaleGreen, Color.LightGreen ,"Data")},
        { MarkType.ForwardingRecord, new MarkStyle(Color.DarkBlue, Color.Gainsboro,  "Forwarding Record")},
        { MarkType.DownPagePointer, new MarkStyle(Color.Navy, Color.Gainsboro,  "Down Page Pointer")},
        { MarkType.ColumnOffsetArray, new MarkStyle(Color.Blue, Color.AliceBlue,  "Column Offset Array")},
        { MarkType.ColumnCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro,  "Column Count")},
        { MarkType.ColumnCountOffset, new MarkStyle(Color.Blue, Color.Gainsboro,  "Column Count Offset")},
        { MarkType.SlotOffset, new MarkStyle(Color.Black, Color.White,  "Slot Offset")},
        { MarkType.VariableLengthColumnCount, new MarkStyle(Color.Black, Color.AliceBlue,  "Variable Length Column Count")},
        { MarkType.NullBitmap, new MarkStyle(Color.Purple, Color.Gainsboro,  "Null Bitmap")},
        { MarkType.Value, new MarkStyle(Color.Gray, Color.LemonChiffon, Color.PaleGoldenrod, string.Empty)},
        { MarkType.SparseColumns, new MarkStyle(Color.Black, Color.Olive, "Sparse Columns")},
        { MarkType.SparseColumnOffsets, new MarkStyle(Color.Black, Color.DarkKhaki, "Sparse Column Offsets")},
        { MarkType.SparseColumnCount, new MarkStyle(Color.Black, Color.SeaGreen, "Sparse Column Count")},
        { MarkType.ComplexHeader, new MarkStyle(Color.Green, Color.Gainsboro, "Complex Header")},
        { MarkType.PageModCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Page Mod Count")},
        { MarkType.CiSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { MarkType.CiLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { MarkType.Timestamp, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Timestamp")},
        { MarkType.PointerType, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Type")},
        { MarkType.EntryCount, new MarkStyle(Color.AliceBlue, Color.Gainsboro, "Entry Count")},
        { MarkType.OverflowLength, new MarkStyle(Color.Red, Color.PeachPuff, "Length")},
        { MarkType.Unused, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Unused")},
        { MarkType.UpdateSeq, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "UpdateSeq")},
        { MarkType.CdArrayItem, new MarkStyle(Color.White, Color.Orange,string.Empty)},
        { MarkType.SlotCount, new MarkStyle(Color.DarkGreen, Color.PeachPuff,"Slot Count")},
    };

    public MarkStyle GetMarkStyle(MarkType markType)
    {
        return Styles[markType];
    }
}