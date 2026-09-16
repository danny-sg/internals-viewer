using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Readers.Pages;

public sealed class MissingDataFileException(string message, IReadOnlyList<DatabaseFile> missingFiles)
    : Exception(message)
{
    public IReadOnlyList<DatabaseFile> MissingFiles { get; } = missingFiles;
}
