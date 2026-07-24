namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;

internal enum StreamFileSystemAttributes : ushort
{
    ModifiedByRead = 1,  
    ContainsSecurity = 2,
    IsNonPortable = 4,   
    IsSparse = 8
}