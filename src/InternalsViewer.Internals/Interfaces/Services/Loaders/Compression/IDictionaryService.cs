using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface IDictionaryService
{
    Dictionary Load(byte[] data, int offset);
}