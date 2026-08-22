using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// What a dictionary's header says without reading the whole of it
/// </summary>
public sealed record DictionaryHeaderInfo(SubLobType? Coding, int PageCount);
