using InternalsViewer.Connection.BackupFile.Format.Configuration;
using InternalsViewer.Connection.BackupFile.Index;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Connection.BackupFile.Reader;

/// <summary>
/// Page reader for SQL Server backup files
/// </summary>
internal sealed class BackupPageReader(IReadOnlyList<string> filenames)
    : PageReader, IPageReader, IMultiFilePageReader
{
    public BackupPageReader(string filename) : this([filename])
    {
    }

    private IReadOnlyList<string> Filenames { get; } = filenames;

    private List<SafeFileHandle> Handles { get; } = [];

    private BackupPageLocator? Locator { get; set; }

    public BackupConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Initializes the backup page reader by reading the media set and building the page index
    /// </summary>
    /// <remarks>
    /// The media set (one or more .bak files) is parsed and validated as complete and consistent, and the backup
    /// configuration is captured.
    ///
    /// A read handle is then opened per stripe in family sequence order. The stripe index assigned here must match
    /// the Handles list - page runs resolve reads via Handles[StripeIndex].
    ///
    /// The page index is then built by scanning the data streams of every stripe, mapping page address to stripe and
    /// offset.
    /// </remarks>
    public async Task Initialize(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var mediaSet = BackupMediaSetReader.Read(Filenames);

            Configuration = mediaSet.Configuration;

            var stripes = new List<BackupStripe>();

            try
            {
                foreach (var family in mediaSet.Families)
                {
                    var handle = File.OpenHandle(family.Filename, FileMode.Open, FileAccess.Read, FileShare.Read);

                    Handles.Add(handle);

                    stripes.Add(new BackupStripe(stripes.Count, handle, family.Blocks));
                }

                Locator = BackupPageIndexer.Build(stripes, cancellationToken);
            }
            catch
            {
                DisposeHandles();

                throw;
            }
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
        DisposeHandles();

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
        if (Handles.Count == 0 || Locator is null)
        {
            throw new InvalidOperationException("Initialize must be called before pages can be read.");
        }

        if (!Locator.TryGetLocation(pageAddress, out var location))
        {
            throw new PageNotInBackupException(pageAddress);
        }

        ReadExactly(Handles[location.StripeIndex], location.Offset, buffer.AsSpan(0, PageData.Size));
    }

    private void DisposeHandles()
    {
        foreach (var handle in Handles)
        {
            handle.Dispose();
        }

        Handles.Clear();
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
