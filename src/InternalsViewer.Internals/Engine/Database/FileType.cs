namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// File Types
/// </summary>
/// <remarks>
/// Source: SELECT * FROM sys.syspalvalues ft WHERE ft.class = 'DBFT'
/// </remarks>
public enum FileType : byte
{
    Rows = 0,
    Log = 1,
    Filestream = 2,
    FilestreamLog = 3,
    FullText = 4
}