namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Attributes;

internal enum StartOfDataSetAttributes : uint
{
    TransferBit = 0x1,     
    CopyBit = 0x2,         
    NormalBit = 0x4,       
    DifferentialBit = 0x8, 
    IncrementalBit = 0x10, 
    DailyBit = 0x20,       
}