using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Services.Loaders.Records.Cd;

public class CdIndexRecordLoader(ILogger<CdIndexRecordLoader> logger) 
    : CdRecordLoader<IndexColumnStructure>(logger);