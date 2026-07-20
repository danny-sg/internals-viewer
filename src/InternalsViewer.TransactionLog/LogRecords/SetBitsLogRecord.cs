namespace InternalsViewer.TransactionLog.LogRecords;

/// <summary>
/// LOP_SET_BITS log record
/// </summary>
/// <remarks>
/// Sets/clears bits in an allocation page bitmap
/// </remarks>
public sealed record SetBitsLogRecord : PageLogRecord
{
    /// <summary>
    /// First updated bit offset in raw bitmap record data
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 48. The raw index counts from the start of the bitmap record's data, which begins with a 32 bit prefix before the
    /// bitmap proper - so the logical extent index is FirstBit - 32 (e.g. GAM bit 1321 = extent 1289 = page 10312).
    /// </remarks>
    public int FirstBit { get; set; }

    /// <summary>
    /// Number of consecutive bits written
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 50. The run [FirstBit, FirstBit + BitCount) is filled with BitValue. Single-extent operations log a count of 1.
    /// </remarks>
    public int BitCount { get; set; }

    /// <summary>
    /// Value the bits in the run are set to
    /// </summary>
    /// <remarks>
    /// 2 bytes at offset 52, always 0 or 1 - a single value applied to the whole run, not a bit pattern.
    ///
    /// - GAM allocate writes 0 and deallocate writes 1
    /// - IAM add writes 1 and remove writes 0
    /// - DIFF map first-change writes 1
    /// </remarks>
    public int BitValue { get; set; }
}
