using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Content;
using InternalsViewer.Connection.BackupFile.Mtf.Configuration;
using InternalsViewer.Connection.BackupFile.Interfaces;
using InternalsViewer.Connection.BackupFile.Mapping;
using InternalsViewer.Connection.BackupFile.Media;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Readers.Pages;
using Microsoft.Extensions.Logging.Abstractions;
using InternalsViewer.Internals.Engine.Loading;

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

    private List<IContentSource> Sources { get; } = [];

    private IContentSource[] StripeContents { get; set; } = [];

    private PageMap? PageMap { get; set; }

    public BackupConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Initializes the backup page reader by reading the media set and building the page index
    /// </summary>
    /// <remarks>
    /// The media set (files) is parsed and validated as complete and consistent, and the backup configuration is captured.
    ///
    /// A content source is opened per file, then ordered by family sequence into stripes. The stripe index must match the StripeContents
    /// array - page runs resolve reads via StripeContents[StripeIndex].
    ///
    /// The page index is then built by scanning the data streams of every stripe, mapping page address to stripe and offset.
    /// </remarks>
    public async Task Initialize(CancellationToken cancellationToken, IProgress<ProgressDetail>? progress = null)
    {
        await Task.Run(() =>
        {
            var mediaSet = MediaSetReader.Read(OpenSources(cancellationToken, progress));

            progress?.Report(new ProgressDetail($"Read media set - {mediaSet.Families.Count} file(s)"));

            Configuration = mediaSet.Configuration;

            var stripes = new List<Stripe>();

            try
            {
                foreach (var family in mediaSet.Families)
                {
                    stripes.Add(new Stripe(stripes.Count, family.Content, family.Blocks));
                }

                StripeContents = [.. stripes.Select(s => s.Content)];

                PageMap = PageMapper.Build(stripes, cancellationToken, progress);

                progress?.Report(new ProgressDetail($"Mapped pages for {PageMap.Runs.Count} data file(s)"));
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
        var map = PageMap
                      ?? throw new InvalidOperationException("Initialize must be called before files can be registered.");

        var missingFiles = files.Where(f => f.FileType == FileType.Rows && !map.HasFile(f.FileId))
                                .ToList();

        if (missingFiles.Count > 0)
        {
            var fileList = string.Join(", ", missingFiles.Select(f => $"{f.Name} ({f.PhysicalName})"));

            throw new MissingDataFileException($"The backup does not contain any pages for data file(s): {fileList}.",
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
    private IReadOnlyList<MediaSource> OpenSources(CancellationToken cancellationToken, IProgress<ProgressDetail>? progress)
    {
        foreach (var filename in Filenames)
        {
            progress?.Report(new ProgressDetail($"Opening {Path.GetFileName(filename)}"));

            IContentSource content = CompressedBackupFormat.IsCompressed(filename)
                                           ? new CompressedContentSource(filename, NullLogger.Instance, cancellationToken, progress)
                                           : new FileContentSource(filename);

            Sources.Add(content);
        }

        return [.. Filenames.Select((f, i) => new MediaSource(f, Sources[i]))];
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
        if (StripeContents.Length == 0 || PageMap is null)
        {
            throw new InvalidOperationException("Initialize must be called before pages can be read.");
        }

        if (!PageMap.TryGetLocation(pageAddress, out var location))
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
