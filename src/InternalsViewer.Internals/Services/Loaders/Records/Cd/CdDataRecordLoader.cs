using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Services.Loaders.Records.Cd;

public sealed class CdDataRecordLoader(ILogger<CdDataRecordLoader> logger)
    : CdRecordLoader<ColumnStructure>(logger);