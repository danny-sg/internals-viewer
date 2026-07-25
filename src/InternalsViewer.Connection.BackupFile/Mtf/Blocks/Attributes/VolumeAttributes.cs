namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Attributes;

internal enum VolumeAttributes : uint
{
    NoRedirectRestoreBit = 0x1,
    NonVolumeBit = 0x2,
    DevDriveBit = 0x4,
    DevUncBit = 0x8,
    DevOsSpecBit = 0x10,
    DevVendSpecBit = 0x20
}