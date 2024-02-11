using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing file information from the metadata collection
/// </summary>
public static class FileProvider
{
    public static List<DatabaseFile> GetFiles(InternalMetadata metadata)
    {
        return metadata.Files.Select(GetFile).Where(f => f.FileType == FileType.Rows).ToList();
    }

    private static DatabaseFile GetFile(InternalFile source)
    {
        var fileId = (short)(source.FileId & 0x7fff);

        var file = new DatabaseFile(fileId)
        {
            FileGroupId = (short)source.FileGroupId,
            Size = source.Size,
            FileType = (FileType)source.FileType,
            Name = source.LogicalName,
            PhysicalName = source.PhysicalName,
        };

        return file;
    }
}