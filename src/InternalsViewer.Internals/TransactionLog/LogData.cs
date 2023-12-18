using System;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.TransactionLog;

/// <summary>
/// Log Data containing fragment of a page at a particular offset
/// </summary>
public class LogData
{
    public ushort Offset { get; set; }

    public ushort Slot { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public LogSequenceNumber LogSequenceNumber { get; set; }

    public override string ToString()
    {
        return string.Format("LSN: {0} Slot: {1} Offset: {2} Data: {3}",
                             LogSequenceNumber,
                             Slot,
                             Offset,
                             DataConverter.ToHexString(Data));
    }

    /// <summary>
    /// Merges the data into a page
    /// </summary>
    /// <param name="page">The target page.</param>
    public Page MergeData(Page page)
    {
        var dataOffset = page.OffsetTable[Slot] + Offset;

        var pageData = new byte[Page.Size];

        Array.Copy(page.PageData, pageData, Page.Size);

        Array.Copy(Data, 0, pageData, dataOffset, Data.Length);

        page.PageData = pageData;

        return page;
    }

}