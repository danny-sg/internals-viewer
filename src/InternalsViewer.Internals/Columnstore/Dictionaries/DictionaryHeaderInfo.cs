using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

public sealed record DictionaryHeaderInfo(SubLobType? Coding, int PageCount);
