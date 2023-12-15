using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Services.Records.Loaders;

namespace InternalsViewer.Internals.Engine.Records.Data;

public class DataRecord : Record
{
    public SparseVector SparseVector { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

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

    [DataStructureItem(DataStructureItemType.StatusBitsB)]
    public string StatusBitsBDescription => "";

    [DataStructureItem(DataStructureItemType.ForwardingRecord)]
    public RowIdentifier ForwardingRecord { get; set; }
}