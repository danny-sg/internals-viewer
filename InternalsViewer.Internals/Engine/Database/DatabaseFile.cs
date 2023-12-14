
namespace InternalsViewer.Internals.Engine.Database;

public class DatabaseFile(short fileId)
{
    public long TotalPages { get; set; }

    public long UsedPages { get; set; }

    public float TotalMb => TotalPages * 512 / 1024F;

    public float UsedMb => UsedPages * 512 / 1024F;

    public short FileId { get; set; } = fileId;

    public string FileGroup { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PhysicalName { get; set; } = string.Empty;

    public string FileName => PhysicalName[(PhysicalName.LastIndexOf('\\') + 1)..];

    public int Size { get; set; }
}