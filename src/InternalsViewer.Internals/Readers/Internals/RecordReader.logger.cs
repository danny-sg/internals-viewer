using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Readers.Internals;

public partial class RecordReader
{
    [LoggerMessage(LogLevel.Trace, "Reading records from {StartPage} - {@Structure}")]
    static partial void LogReadingRecords(ILogger<RecordReader> logger, PageAddress startPage, TableStructure @Structure);

    [LoggerMessage(LogLevel.Trace, "Loading record {FileId}:{PageId}:{Offset}")]
    static partial void LogLoadingRecord(ILogger<RecordReader> logger, short fileId, int pageId, ushort offset);

    [LoggerMessage(LogLevel.Trace, "Next page: {NextPage}")]
    static partial void LogNextPageNextPage(ILogger<RecordReader> logger, PageAddress nextPage);
}