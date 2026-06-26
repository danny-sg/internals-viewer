using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// SQL Server Database File
/// </summary>
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-files-and-filegroups"/>
public record DatabaseFile(short FileId)
{
    public short FileId { get; set; } = FileId;

    public short FileGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public FileType FileType { get; set; }

    public string PhysicalName { get; set; } = string.Empty;

    public long TotalPages { get; set; }

    public long UsedPages { get; set; }

    public double TotalMb => TotalPages * PageData.Size / 1024d / 1024d;

    public double UsedMb => UsedPages * PageData.Size / 1024d / 1024d;

    public string FileName => PhysicalName[(PhysicalName.LastIndexOf('\\') + 1)..];

    public int Size { get; set; }
}