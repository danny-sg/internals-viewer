using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for SQL Server data files
/// </summary>
public sealed class DataFilePageReader(string path) : PageReader, IPageReader, IMultiFilePageReader
{
    private const short PrimaryFileId = 1;

    private const FileOptions OpenOptions = FileOptions.None;

    private string PrimaryFilePath { get; } = path;

    private ConcurrentDictionary<short, OpenDataFile> OpenFiles { get; } = new();

    public ValueTask DisposeAsync()
    {
        foreach (var file in OpenFiles.Values)
        {
            file.Handle.Dispose();
        }

        OpenFiles.Clear();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Reads a page from a SQL Server data file
    /// </summary>
    /// <remarks>
    /// SQL Server data files (MDF/NDF) are stored in 8 KB (8192 bytes) pages, so a page is located in the file at location (Page Id
    /// * 8192)
    ///
    /// The file has to be detached/not attached to SQL Server to be read as it will be locked by the SQL Server process.
    /// </remarks>
    public Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var data = new byte[PageData.Size];

        ReadInto(name, pageAddress, data, cancellationToken);

        return Task.FromResult(data);
    }

    public Task ReadInto(string name,
                         PageAddress pageAddress,
                         byte[] buffer,
                         CancellationToken cancellationToken)
    {
        var file = GetFile(pageAddress.FileId);

        var offset = (long)pageAddress.PageId * PageData.Size;

        if (offset < 0 || offset + PageData.Size > file.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageAddress),
                $"Page {pageAddress} (offset {offset}) is outside file '{file.Path}' (length {file.Length}).");
        }

        ReadExactly(file, offset, buffer.AsSpan(0, PageData.Size));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers the database files with the reader + creates file handles
    /// </summary>
    /// <remarks>
    /// Databases can have multiple data files, and the page address contains a file id to indicate which file the page is in.
    ///
    /// This method registers the files with the reader so that it can resolve the file id to a file path.
    ///
    /// A list of DatabaseFile is provided from the primary file metadata, which contains the file id, file name and physical path of the
    /// file. The reader resolves the file names from this information, checking files that sit alongside the .mdf file.
    ///
    /// Handles are created for each file so pages can be read from the relevant file id and high volume loads can be performed without
    /// having to open/close files for each page read. Handles are returned and disposed when the reader is disposed.
    /// </remarks>
    public Task RegisterFiles(IReadOnlyList<DatabaseFile> files, CancellationToken cancellationToken)
    {
        var missingFiles = new List<DatabaseFile>();

        foreach (var file in files.Where(f => f.FileType == FileType.Rows && f.FileId != PrimaryFileId))
        {
            var openFile = ResolveFile(file);

            if (openFile == null)
            {
                missingFiles.Add(file);

                continue;
            }

            if (!OpenFiles.TryAdd(file.FileId, openFile))
            {
                openFile.Handle.Dispose();
            }
        }

        if (missingFiles.Count > 0)
        {
            var fileList = string.Join(", ", missingFiles.Select(f => $"{f.Name} ({f.PhysicalName})"));

            throw new MissingDataFileException(
                $"Unable to locate data file(s): {fileList}. Files are located via the path recorded in the " +
                $"database file metadata, falling back to the directory containing '{PrimaryFilePath}'.",
                missingFiles);
        }

        return Task.CompletedTask;
    }

    private OpenDataFile GetFile(short fileId)
    {
        if (OpenFiles.TryGetValue(fileId, out var file))
        {
            return file;
        }

        if (fileId != PrimaryFileId)
        {
            throw new InvalidOperationException(
                $"No data file registered for File Id {fileId}. Secondary data files are resolved " +
                $"from the file metadata when the database is loaded.");
        }

        var primary = Open(PrimaryFilePath);

        if (!OpenFiles.TryAdd(PrimaryFileId, primary))
        {
            primary.Handle.Dispose();

            return OpenFiles[PrimaryFileId];
        }

        return primary;
    }

    private static OpenDataFile Open(string filePath)
    {
        var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, OpenOptions);

        return new OpenDataFile(filePath, handle, RandomAccess.GetLength(handle));
    }

    private OpenDataFile? ResolveFile(DatabaseFile file)
    {
        var primaryDirectory = Path.GetDirectoryName(PrimaryFilePath) ?? string.Empty;

        var candidates = new[] { file.PhysicalName, Path.Combine(primaryDirectory, file.FileName) };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            var openFile = Open(candidate);

            if (IsMatchingDataFile(openFile, file.FileId))
            {
                return openFile;
            }

            openFile.Handle.Dispose();
        }

        return null;
    }

    private static bool IsMatchingDataFile(OpenDataFile file, short fileId)
    {
        if (file.Length < PageData.Size)
        {
            return false;
        }

        var data = new byte[PageData.Size];

        ReadExactly(file, 0, data);

        var header = PageHeaderParser.Parse(data, false);

        return header.PageType == PageType.FileHeader && header.PageAddress == new PageAddress(fileId, 0);
    }

    private static void ReadExactly(OpenDataFile file, long offset, Span<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = RandomAccess.Read(file.Handle, buffer[totalRead..], offset + totalRead);

            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"Unexpected end of file '{file.Path}' reading at offset {offset + totalRead}.");
            }

            totalRead += read;
        }
    }

    private sealed record OpenDataFile(string Path, SafeFileHandle Handle, long Length);
}
