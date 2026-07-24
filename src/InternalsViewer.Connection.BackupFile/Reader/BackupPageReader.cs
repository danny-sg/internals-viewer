using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Index;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Connection.BackupFile.Reader;

/// <summary>
/// Page reader for SQL Server backup files
/// </summary>
internal sealed class BackupPageReader(string filename) : PageReader, IPageReader, IMultiFilePageReader
{
    private string Filename { get; } = filename;

    private SafeFileHandle? Handle { get; set; }

    private BackupPageLocator? Locator { get; set; }

    /// <summary>
    /// Initializes the backup page reader by loading the backup file and building the page index
    /// </summary>
    /// <remarks>
    /// The page index maps page address to file offset.
    ///
    /// The backup file is loaded and parsed to extract the descriptor blocks.
    ///
    /// The page index is then built using the descriptor blocks.
    /// </remarks>
    public async Task Initialize(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var loader = new BackupFileLoader(NullLogger<BackupFileLoader>.Instance, Filename);

            List<DescriptorBlock> blocks;

            try
            {
                blocks = loader.Load();
            }
            finally
            {
                loader.Reader.Dispose();
            }

            var handle = File.OpenHandle(Filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            try
            {
                Locator = BackupPageIndexer.Build(handle, blocks, cancellationToken);
            }
            catch
            {
                handle.Dispose();

                throw;
            }

            Handle = handle;
        }, cancellationToken);
    }

    public Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var data = new byte[PageData.Size];

        ReadBackupFileInto(pageAddress, data);

        return Task.FromResult(data);
    }

    public Task ReadInto(string name,
                         PageAddress pageAddress,
                         byte[] buffer,
                         CancellationToken cancellationToken)
    {
        ReadBackupFileInto(pageAddress, buffer);

        return Task.CompletedTask;
    }

    public Task RegisterFiles(IReadOnlyList<DatabaseFile> files, CancellationToken cancellationToken)
    {
        var locator = Locator
                      ?? throw new InvalidOperationException("Initialize must be called before files can be registered.");

        var missingFiles = files.Where(f => f.FileType == FileType.Rows && !locator.HasFile(f.FileId))
                                .ToList();

        if (missingFiles.Count > 0)
        {
            var fileList = string.Join(", ", missingFiles.Select(f => $"{f.Name} ({f.PhysicalName})"));

            throw new MissingDataFileException(
                $"The backup does not contain any pages for data file(s): {fileList}.",
                missingFiles);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Handle?.Dispose();

        Handle = null;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets page data for a page address from a backup file
    /// </summary>
    /// <remarks>
    /// The index map built during initialization is used to locate the offset of the page in the backup file. 
    ///
    /// Once the offset has been located, the page data can be read directly from the backup file from offset to offset + 8192 bytes (page
    /// size).
    /// </remarks>
    private void ReadBackupFileInto(PageAddress pageAddress, byte[] buffer)
    {
        if (Handle is null || Locator is null)
        {
            throw new InvalidOperationException("Initialize must be called before pages can be read.");
        }

        if (!Locator.TryGetOffset(pageAddress, out var offset))
        {
            throw new PageNotInBackupException(pageAddress);
        }

        ReadExactly(Handle, offset, buffer.AsSpan(0, PageData.Size));
    }

    private static void ReadExactly(SafeFileHandle handle, long offset, Span<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer[totalRead..], offset + totalRead);

            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of backup file reading at offset {offset + totalRead}.");
            }

            totalRead += read;
        }
    }
}
