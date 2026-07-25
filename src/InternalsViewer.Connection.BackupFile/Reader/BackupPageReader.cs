using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Content;
using InternalsViewer.Connection.BackupFile.Format.Configuration;
using InternalsViewer.Connection.BackupFile.Index;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Extensions.Logging.Abstractions;

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

    private List<IBackupContentSource> Sources { get; } = [];

    private IBackupContentSource[] StripeContents { get; set; } = [];

    private BackupPageLocator? Locator { get; set; }

    public BackupConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Initializes the backup page reader by reading the media set and building the page index
    /// </summary>
    /// <remarks>
    /// The media set (one or more .bak files) is parsed and validated as complete and consistent, and the backup
    /// configuration is captured.
    ///
    /// A content source is opened per file, then ordered by family sequence into stripes. The stripe index must
    /// match the StripeContents array - page runs resolve reads via StripeContents[StripeIndex].
    ///
    /// The page index is then built by scanning the data streams of every stripe, mapping page address to stripe and
    /// offset.
    /// </remarks>
    public async Task Initialize(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var mediaSet = BackupMediaSetReader.Read(OpenSources(cancellationToken));

            Configuration = mediaSet.Configuration;

            var stripes = new List<BackupStripe>();

            try
            {
                foreach (var family in mediaSet.Families)
                {
                    stripes.Add(new BackupStripe(stripes.Count, family.Content, family.Blocks));
                }

                StripeContents = [.. stripes.Select(s => s.Content)];

                Locator = BackupPageIndexer.Build(stripes, cancellationToken);
            }
            catch
            {
                DisposeSources();

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
        DisposeSources();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Opens a content source for every file in the media set
    /// </summary>
    private IReadOnlyList<BackupMediaSource> OpenSources(CancellationToken cancellationToken)
    {
        foreach (var filename in Filenames)
        {
            IBackupContentSource content = CompressedBackupFormat.IsCompressed(filename)
                                           ? new CompressedBackupContentSource(filename, NullLogger.Instance, cancellationToken)
                                           : new FileBackupContentSource(filename);

            Sources.Add(content);
        }

        return [.. Filenames.Select((f, i) => new BackupMediaSource(f, Sources[i]))];
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
        if (StripeContents.Length == 0 || Locator is null)
        {
            throw new InvalidOperationException("Initialize must be called before pages can be read.");
        }

        if (!Locator.TryGetLocation(pageAddress, out var location))
        {
            throw new PageNotInBackupException(pageAddress);
        }

        StripeContents[location.StripeIndex].Read(location.Offset, buffer.AsSpan(0, PageData.Size));
    }

    private void DisposeSources()
    {
        foreach (var source in Sources)
        {
            source.Dispose();
        }

        Sources.Clear();
    }
}
