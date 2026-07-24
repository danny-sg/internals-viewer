namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;

internal enum StreamMediaFormatAttributes : ushort
{
    Continue = 1,
    Variable = 2,
    VarEnd = 4,
    Encrypted = 8,
    Compressed = 16,
    Checksum = 32,
    EmbeddedLength = 64,
}