using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Query.FullTrace.Memory.History.Pages;

public static class WorkfilePageReader
{
    public const string ClassName = "Page";

    private const byte HeaderVersion = 1;

    private const int HeaderSize = 96;

    private static FixedVarDataRecordLoader Loader { get; } = new(NullLogger<FixedVarDataRecordLoader>.Instance);

    private static FixedVarIndexRecordLoader IndexLoader { get; } = new(NullLogger<FixedVarIndexRecordLoader>.Instance);

    public static WorkfilePage? Read(byte[] data, RowRecordLayout? layout)
    {
        if (data.Length != PageData.Size || data[0] != HeaderVersion)
        {
            return null;
        }

        var header = PageHeaderParser.Parse(data, true);

        if (header.PageType is not (PageType.WorkFile or PageType.Sort))
        {
            return null;
        }

        var page = new DataPage
        {
            Data = data,
            PageHeader = header,
            PageAddress = header.PageAddress,
            IsMarkEnabled = true
        };

        return new WorkfilePage(page, GetRecords(page, header.PageType == PageType.WorkFile ? layout : null), layout);
    }

    private static List<FixedVarRecord> GetRecords(DataPage page, RowRecordLayout? layout)
    {
        var records = new List<FixedVarRecord>();

        var end = PageData.Size - page.PageHeader.SlotCount * sizeof(ushort);

        for (var slot = 0; slot < page.OffsetTable.Length; slot++)
        {
            var offset = page.OffsetTable[slot];

            if (offset < HeaderSize || offset >= end)
            {
                continue;
            }

            try
            {
                records.Add(GetRecord(page, offset, slot, layout));
            }
            catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException)
            {
            }
        }

        return records;
    }

    private static FixedVarRecord GetRecord(DataPage page, ushort offset, int slot, RowRecordLayout? layout)
    {
        if (page.PageHeader.PageType == PageType.Sort)
        {
            return IndexLoader.LoadStructure(page, offset, slot);
        }

        var record = layout is not null && layout.Fits(page.Data, offset)
            ? Loader.Load(page, offset, layout.Structure)
            : Loader.LoadStructure(page, offset);

        record.Slot = slot;

        return record;
    }
}
