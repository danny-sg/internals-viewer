using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Connection.BackupFile.Reader;

public sealed class PageNotInBackupException(PageAddress pageAddress)
    : Exception($"Page {pageAddress} is not in the backup - it was not allocated when the backup was taken.")
{
    public PageAddress PageAddress { get; } = pageAddress;
}
