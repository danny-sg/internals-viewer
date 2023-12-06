using System.Text;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers;

/// <summary>
/// BLOB internal field
/// </summary>
public class BlobField : Field
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlobField"/> class.
    /// </summary>
    public BlobField(byte[] data, int offset)
    {
        Data = data;
        PointerType = (BlobFieldType)data[0];
        Offset = offset;

        Mark("PointerType", offset, sizeof(byte));

        LoadLinks();
    }

    /// <summary>
    /// Gets or sets the timestamp used by DBCC CHECKTABLE
    /// </summary>
    /// <value>The timestamp.</value>
    [Mark(MarkType.Timestamp)]
    public int Timestamp { get; set; }

    public List<BlobChildLink> Links { get; set; }

    [Mark(MarkType.Rid)]
    public BlobChildLink[] LinksArray => Links.ToArray();

    public byte[] Data { get; set; }

    [Mark(MarkType.PointerType)]
    public BlobFieldType PointerType { get; set; }

    public int Offset { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine(Timestamp.ToString());

        foreach (var b in Links)
        {
            sb.AppendLine(b.ToString());
        }

        return sb.ToString();
    }

    protected virtual void LoadLinks()
    {
    }
}