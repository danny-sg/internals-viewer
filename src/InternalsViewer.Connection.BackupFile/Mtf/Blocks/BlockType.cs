namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks;

/// <summary>
/// MTF Block Types
/// </summary>
internal enum BlockType : uint
{
    None = 0,

    /// <summary>
    /// TAPE descriptor block - TAPE
    /// </summary>
    Tape = 0x45504154,
    
    /// <summary>
    /// Start of Data Set - SSET
    /// </summary>
    StartOfDataSet = 0x54455353,
    
    /// <summary>
    /// Volume - VOLB
    /// </summary>
    Volume = 0x424C4F56,
    
    /// <summary>
    /// End of Set Pad - EPSB
    /// </summary>
    EndOfSetPad = 0x42505345,
    
    /// <summary>
    /// End of Set - ESET
    /// </summary>
    EndOfSet = 0x54455345,
    
    /// <summary>
    /// End Of Tape Marker - EOTM
    /// </summary>
    EndOfTape = 0x4D544F45,

    /// <summary>
    /// Soft File mark - SFMB
    /// </summary>
    SoftFileMark = 0x424D4653,
    
    /// <summary>
    /// Configuration Information
    /// </summary>
    MSCI =0x4943534d, 

    /// <summary>
    /// Data file stream
    /// </summary>
    MSDA = 0x4144534d,

    /// <summary>
    /// Transaction Log stream
    /// </summary>
    MSTL = 0x4c54534d,

    /// <summary>
    /// unknown expansion - observed to contain a copy of the MQCI configuration stream
    /// </summary>
    MSLS = 0x534c534d,
}