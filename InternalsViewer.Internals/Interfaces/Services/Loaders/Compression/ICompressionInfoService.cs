using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface ICompressionInfoService
{
    CompressionInfo Load(Page page);
}