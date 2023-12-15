using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Readers.Pages;

namespace InternalsViewer.Tests.Internals.UnitTests.TestHelpers.TestReaders;

public class FilePageReader(string path) : PageReader, IPageReader
{
    private byte[] LoadTextPage(string pageText)
    {
        var offset = 0;

        var dumpFound = false;

        var data = new byte[Page.Size];

        foreach (var line in pageText.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
        {
            if (line.StartsWith("OFFSET TABLE"))
            {
                break;
            }
            if (line.StartsWith(@"Memory Dump @"))
            {
                dumpFound = true;
                continue;
            }
            if (dumpFound && !string.IsNullOrEmpty(line))
            {
                offset = ReadData(line, offset, data);
            }
        }

        return data;
    }

    public async Task<PageData> Read(string databaseName, PageAddress pageAddress)
    {
        var pattern = $"{databaseName}_{pageAddress.FileId}_{pageAddress.PageId}_*.page";

        var files = Directory.GetFiles(path, pattern);

        if (files.Length == 0)
        {
            throw new FileNotFoundException($"File not found matching pattern: {pattern}");
        }

        if (files.Length > 1)
        {
            throw new ArgumentException($"Multiple files found matching pattern: {pattern}");
        }

        var pageText = await File.ReadAllTextAsync(files[0]);

        var page = new PageData
        {
            Data = LoadTextPage(pageText),
            HeaderValues =GetHeaderValues(pageText)
        };

        return page;
    }

    private Dictionary<string, string> GetHeaderValues(string s)
    {
        var header = new Dictionary<string, string>
        {
            { "m_slotCnt", GetHeaderValue(s, "m_slotCnt") },
            { "m_freeCnt", GetHeaderValue(s, "m_freeCnt") },
            { "m_freeData", GetHeaderValue(s, "m_freeData") },
            { "m_type", GetHeaderValue(s, "m_type") },
            { "m_level", GetHeaderValue(s, "m_level") },
            { "pminlen", GetHeaderValue(s, "pminlen") },
            { "Metadata: IndexId", GetHeaderValue(s, "Metadata: IndexId") },
            { "Metadata: AllocUnitId", GetHeaderValue(s, "Metadata: AllocUnitId") },
            { "Metadata: ObjectId", GetHeaderValue(s, "Metadata: ObjectId") },
            { "Metadata: PartitionId", GetHeaderValue(s, "Metadata: PartitionId") },
            { "m_reservedCnt", GetHeaderValue(s, "m_reservedCnt") },
            { "m_xactReserved", GetHeaderValue(s, "m_xactReserved") },
            { "m_tornBits", GetHeaderValue(s, "m_tornBits") },
            { "m_pageId", GetHeaderValue(s, "m_pageId") },
            { "m_lsn", GetHeaderValue(s, "m_lsn") },
            { "m_flagBits", GetHeaderValue(s, "m_flagBits") },
            { "m_prevPage", GetHeaderValue(s, "m_prevPage") },
            { "m_nextPage", GetHeaderValue(s, "m_nextPage") }
        };

        return header;
    }

    public string GetHeaderValue(string pageText, string key)
    {
        var searchText = $"{key} = ";

        var keyStartPosition = pageText.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) + searchText.Length;
        var keyEndPosition = pageText.IndexOfAny(new[] { ' ', '\r', '\n' }, keyStartPosition);

        return pageText.Substring(keyStartPosition, keyEndPosition - keyStartPosition);
    }
}
