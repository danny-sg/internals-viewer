using System.Text;
using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

/// <summary>
/// BLOB internal field
/// </summary>
public class BlobField : Field
{
    public BlobField()
    {
        IsMarkEnabled = true;
    }

    /// <summary>
    /// Timestamp used by DBCC CHECKTABLE
    /// </summary>
    [DataStructureItem(ItemType.Timestamp, "Timestamp")]
    public uint Timestamp { get; set; }

    public List<BlobChildLink> Links { get; set; } = new();

    [DataStructureItem(ItemType.Rid, "RID")]
    public BlobChildLink[] LinksArray => Links.ToArray();

    [DataStructureItem(ItemType.PointerType, "Pointer Type")]
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
}