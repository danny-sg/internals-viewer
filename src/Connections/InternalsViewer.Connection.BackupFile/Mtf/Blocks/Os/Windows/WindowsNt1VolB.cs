namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Os.Windows;

internal sealed class WindowsNt1VolB(BinaryReader reader) : OsSpecificData
{
    public uint FileSystemFlags { get; } = reader.ReadUInt32();

    public uint NtBackupSetAttributes { get; } = reader.ReadUInt32();
}