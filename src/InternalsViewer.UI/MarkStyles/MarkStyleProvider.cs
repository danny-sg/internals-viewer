using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.UI.MarkStyles;

public class MarkStyleProvider
{
    public Dictionary<DataStructureItemType, MarkStyle> Styles = new()
    {
        { DataStructureItemType.Rid, new MarkStyle(Color.DarkMagenta, Color.Thistle, "Row Identifier")},
        { DataStructureItemType.Uniqueifier, new MarkStyle(Color.SteelBlue, Color.AliceBlue, "Uniqueifier")},

        { DataStructureItemType.BlobChildOffset, new MarkStyle(Color.Blue, Color.Thistle, "Offset")},
        { DataStructureItemType.BlobChildLength, new MarkStyle(Color.Red, Color.Thistle, "Length")},
        { DataStructureItemType.StatusBitsA, new MarkStyle(Color.Red, Color.Gainsboro, "Status Bits A")},
        { DataStructureItemType.StatusBitsB, new MarkStyle(Color.Maroon, Color.Gainsboro, "Status Bits B")},
        { DataStructureItemType.BlobId, new MarkStyle(Color.Navy, Color.AliceBlue, "ID")},
        { DataStructureItemType.BlobLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { DataStructureItemType.BlobType, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Type")},
        { DataStructureItemType.MaxLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "MaxLinks")},
        { DataStructureItemType.CurrentLinks, new MarkStyle(Color.Firebrick, Color.Gainsboro, "Current Links")},
        { DataStructureItemType.Level, new MarkStyle(Color.SlateGray, Color.Gainsboro, "Level")},
        { DataStructureItemType.BlobSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { DataStructureItemType.BlobData, new MarkStyle(Color.Gray, Color.PaleGoldenrod,  "Data")},
        { DataStructureItemType.CompressedValue, new MarkStyle(Color.Black, Color.PaleGreen, Color.LightGreen ,"Data")},
        { DataStructureItemType.ForwardingStub, new MarkStyle(Color.DarkBlue, Color.Gainsboro,  "Forwarding Stub")},
        { DataStructureItemType.DownPagePointer, new MarkStyle(Color.White, Color.Navy,  "Down Page Pointer")},
        { DataStructureItemType.ColumnOffsetArray, new MarkStyle(Color.Blue, Color.AliceBlue,  "Column Offset Array")},
        { DataStructureItemType.ColumnCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro,  "Column Count")},
        { DataStructureItemType.ColumnCountOffset, new MarkStyle(Color.Blue, Color.Gainsboro,  "Column Count Offset")},
        { DataStructureItemType.SlotOffset, new MarkStyle(Color.Black, Color.White,  "Slot Offset")},
        { DataStructureItemType.VariableLengthColumnCount, new MarkStyle(Color.Black, Color.AliceBlue,  "Variable Length Column Count")},
        { DataStructureItemType.NullBitmap, new MarkStyle(Color.Purple, Color.Gainsboro,  "Null Bitmap")},
        { DataStructureItemType.Value, new MarkStyle(Color.Gray, Color.LemonChiffon, Color.PaleGoldenrod, string.Empty)},
        { DataStructureItemType.SparseColumns, new MarkStyle(Color.Black, Color.Olive, "Sparse Columns")},
        { DataStructureItemType.SparseColumnOffsets, new MarkStyle(Color.Black, Color.DarkKhaki, "Sparse Column Offsets")},
        { DataStructureItemType.SparseColumnCount, new MarkStyle(Color.Black, Color.SeaGreen, "Sparse Column Count")},
        { DataStructureItemType.ComplexHeader, new MarkStyle(Color.Green, Color.Gainsboro, "Complex Header")},
        { DataStructureItemType.PageModCount, new MarkStyle(Color.DarkGreen, Color.Gainsboro, "Page Mod Count")},
        { DataStructureItemType.CiSize, new MarkStyle(Color.Purple, Color.Gainsboro, "Size")},
        { DataStructureItemType.CiLength, new MarkStyle(Color.Blue, Color.Gainsboro, "Length")},
        { DataStructureItemType.Timestamp, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Timestamp")},
        { DataStructureItemType.PointerType, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Type")},
        { DataStructureItemType.EntryCount, new MarkStyle(Color.AliceBlue, Color.Gainsboro, "Entry Count")},
        { DataStructureItemType.OverflowLength, new MarkStyle(Color.Red, Color.PeachPuff, "Length")},
        { DataStructureItemType.Unused, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "Unused")},
        { DataStructureItemType.UpdateSeq, new MarkStyle(Color.DarkGreen, Color.PeachPuff, "UpdateSeq")},
        { DataStructureItemType.CdArrayItem, new MarkStyle(Color.White, Color.Orange,string.Empty)},
        { DataStructureItemType.SlotCount, new MarkStyle(Color.DarkGreen, Color.PeachPuff,"Slot Count")},
    };

    public MarkStyle GetMarkStyle(DataStructureItemType dataStructureItemType)
    {
        return Styles[dataStructureItemType];
    }
}