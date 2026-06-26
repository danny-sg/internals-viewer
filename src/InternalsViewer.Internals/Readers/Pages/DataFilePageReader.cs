using System.IO;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for SQL Server data files
/// </summary>
/// <param name="path"></param>
public sealed class DataFilePageReader(string path) : PageReader, IPageReader
{
    private string FilePath { get; } = path;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads a page from a SQL Server data file
    /// </summary>
    /// <remarks>
    /// SQL Server data files (MDF/LDF) are stored in 8 KB (8192 bytes) pages, so a page is located in the file at
    /// location (Page Id * 8192)
    /// 
    /// The file has to be detached/not attached to SQL Server to be read as it will be locked by the SQL Server
    /// process.
    /// </remarks>
    public async Task<byte[]> Read(string name, PageAddress pageAddress)
    {
        var data = new byte[PageData.Size];

        await ReadInto(name, pageAddress, data);

        return data;
    }

    public async Task ReadInto(string name, PageAddress pageAddress, byte[] buffer)
    {
        var offset = (long)pageAddress.PageId * PageData.Size;

        await using var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (offset < 0 || offset + PageData.Size > file.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageAddress),
                $"Page {pageAddress} (offset {offset}) is outside file '{FilePath}' (length {file.Length}).");
        }

        file.Seek(offset, SeekOrigin.Begin);

        await file.ReadExactlyAsync(buffer, 0, PageData.Size);
    }
}