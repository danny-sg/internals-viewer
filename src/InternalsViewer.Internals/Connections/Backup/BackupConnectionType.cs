using InternalsViewer.Internals.Interfaces.Readers;

namespace InternalsViewer.Internals.Connections.Backup;

public class BackupConnectionType(IPageReader pageReader) : ConnectionType(pageReader)
{

}