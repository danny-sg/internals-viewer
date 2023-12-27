using System.IO;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for SQL Server data files
/// </summary>
/// <param name="path"></param>
public class DataFilePageReader(string path) : PageReader, IPageReader
{
    public string FilePath { get; } = path;

    /// <summary>
    /// Reads a page from a SQL Server data file
    /// </summary>
    /// <remarks>
    /// SQL Server data files (MDF/LDF) are stored in 8 KB (8192 bytes) pages, so a page is located in the file at location 
    /// (Page Id * 8192)
    /// 
    /// The file has to be detached/not attached to SQL Server to be read as it will be locked by the SQL Server process.
    /// </remarks>
    public async Task<byte[]> Read(string name, PageAddress pageAddress)
    {
        var offset = pageAddress.PageId * PageData.Size;

        var data = new byte[PageData.Size];

        await using var file = File.OpenRead(Path.Combine(FilePath, $"{name}.mdf"));

        file.Seek(offset, SeekOrigin.Begin);

        _ = await file.ReadAsync(data, 0, PageData.Size);

        return data;
    }
}
