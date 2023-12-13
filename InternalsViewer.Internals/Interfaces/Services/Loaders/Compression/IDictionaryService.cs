using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders;

public interface IDictionaryService
{
    Dictionary Load(byte[] data, int offset);
}