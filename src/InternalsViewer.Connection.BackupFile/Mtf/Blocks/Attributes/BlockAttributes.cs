namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Attributes;

internal enum BlockAttributes : uint
{
    Continuation = 0x1,      
    Compression = 0x4,       
    EosAtEom = 0x8,          
    SetMapExists = 0x10000,  
    FddAllowed = 0x20000,    
    FddExists = 0x10000,     
    Encryption = 0x20000,    
    FddAborted = 0x10000,    
    EndOfFamily = 0x20000,   
    AbortedSet = 0x40000,    
    NoEsetPba = 0x10000,     
    InvalidEsetPba = 0x20000,
}