using System.IO;
using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for SQL Server data files
/// </summary>
/// <param name="path"></param>
public sealed class DataFilePageReader(string path) : PageReader, IPageReader, IMultiFilePageReader
{
    private const short PrimaryFileId = 1;

    private string PrimaryFilePath { get; } = path;

    private Dictionary<short, string> FilePaths { get; } = new() { [PrimaryFileId] = path };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads a page from a SQL Server data file
    /// </summary>
    /// <remarks>
    /// SQL Server data files (MDF/LDF) are stored in 8 KB (8192 bytes) pages, so a page is located in the file at location (Page Id
    /// * 8192)
    ///
    /// The file has to be detached/not attached to SQL Server to be read as it will be locked by the SQL Server
    /// process.
    /// </remarks>
    public async Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var data = new byte[PageData.Size];

        await ReadInto(name, pageAddress, data, cancellationToken);

        return data;
    }

    public async Task ReadInto(string name,
                               PageAddress pageAddress,
                               byte[] buffer,
                               CancellationToken cancellationToken)
    {
        if (!FilePaths.TryGetValue(pageAddress.FileId, out var filePath))
        {
            throw new InvalidOperationException(
                $"No data file registered for File Id {pageAddress.FileId}. Secondary data files are resolved " +
                $"from the file metadata when the database is loaded.");
        }

        var offset = (long)pageAddress.PageId * PageData.Size;

        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (offset < 0 || offset + PageData.Size > file.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageAddress),
                $"Page {pageAddress} (offset {offset}) is outside file '{filePath}' (length {file.Length}).");
        }

        file.Seek(offset, SeekOrigin.Begin);

        await file.ReadExactlyAsync(buffer, 0, PageData.Size, cancellationToken);
    }

    public async Task RegisterFiles(IReadOnlyList<DatabaseFile> files, CancellationToken cancellationToken)
    {
        var missingFiles = new List<DatabaseFile>();

        foreach (var file in files.Where(f => f.FileType == FileType.Rows && f.FileId != PrimaryFileId))
        {
            var filePath = await ResolveFilePath(file, cancellationToken);

            if (filePath == null)
            {
                missingFiles.Add(file);

                continue;
            }

            FilePaths[file.FileId] = filePath;
        }

        if (missingFiles.Count > 0)
        {
            var fileList = string.Join(", ", missingFiles.Select(f => $"{f.Name} ({f.PhysicalName})"));

            throw new MissingDataFileException(
                $"Unable to locate data file(s): {fileList}. Files are located via the path recorded in the " +
                $"database file metadata, falling back to the directory containing '{PrimaryFilePath}'.",
                missingFiles);
        }
    }

    private async Task<string?> ResolveFilePath(DatabaseFile file, CancellationToken cancellationToken)
    {
        var primaryDirectory = Path.GetDirectoryName(PrimaryFilePath) ?? string.Empty;

        var candidates = new[] { file.PhysicalName, Path.Combine(primaryDirectory, file.FileName) };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            if (await IsMatchingDataFile(candidate, file.FileId, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<bool> IsMatchingDataFile(string filePath,
                                                       short fileId,
                                                       CancellationToken cancellationToken)
    {
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (file.Length < PageData.Size)
        {
            return false;
        }

        var data = new byte[PageData.Size];

        await file.ReadExactlyAsync(data, 0, PageData.Size, cancellationToken);

        var header = PageHeaderParser.Parse(data, false);

        return header.PageType == PageType.FileHeader && header.PageAddress == new PageAddress(fileId, 0);
    }
}
