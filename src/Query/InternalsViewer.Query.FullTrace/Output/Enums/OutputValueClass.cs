namespace InternalsViewer.Query.FullTrace.Output.Enums;

internal enum OutputValueClass : byte
{
    /// <summary>
    /// The query's output row (CValOdsRow), which sends each row to the client
    /// </summary>
    /// <remarks>
    /// First entry in the output expression's value table, followed by the output columns.
    ///
    /// It is not a column so it has no source address.
    /// </remarks>
    OutputRow,

    /// <summary>
    /// A column copied into the producing iterator's output row buffer (CValConstXVar)
    /// </summary>
    /// <remarks>
    /// +0x08 is a fixed pointer to a 32-byte CXVariant in the buffer, such as a scan's [this+0x80].
    ///
    /// FetchNextRow overwrites it through the column bindings on every row. For a column bound by reference the variant holds a pointer
    /// into the page.
    /// </remarks>
    RowBuffer,

    /// <summary>
    /// A column read straight from a storage engine rowset (CValSSTable)
    /// </summary>
    /// <remarks>
    /// +0x08 is the RowsetNewSS the scan opened. Nothing is copied. The value is fetched from the rowset's current record, on a buffer pool
    /// page, when it is read.
    /// </remarks>
    Table,

    /// <summary>
    /// A column stored in a Hash Match's hash table (CValHash)
    /// </summary>
    /// <remarks>
    /// The context slot at +0x2C holds the current hash record, which the Hash Match moves as it matches rows. +0x10 is the column's offset
    /// in the record and +0x14 its ordinal.
    /// </remarks>
    Hash,

    /// <summary>
    /// A Hash Match output column that is either its primary value or NULL (CValHashSwitch)
    /// </summary>
    /// <remarks>
    /// +0x08 points at the Hash Match's state for the column's input, one dword per input from +0x110.
    ///
    /// While it is 0 the value is read through the primary at +0x10, a CValHash for a build column or the probe input's own value for a
    /// column passed through. When it is non-zero the input has no row, as for an unmatched row in an outer join, and the value is NULL.
    /// </remarks>
    HashSwitch,

    /// <summary>
    /// A wrapper with a single inner value used by the hash build (CValHashCachedSwitch)
    /// </summary>
    /// <remarks>
    /// +0x08 is the inner value. The build's insert expression has one per stored column, reading the build input's row buffer.
    /// </remarks>
    HashCachedSwitch,

    /// <summary>
    /// A column a Batch To Row adapter passes up from a batch mode operator (CValBatchRedirect)
    /// </summary>
    /// <remarks>
    /// +0x08 is the column index and +0x10 the CQScanBatchHelper. The value is the variant at that index in the helper's value row at
    /// [helper+0x78].
    /// </remarks>
    BatchRedirect,

    /// <summary>
    /// A column in a row of variants held in a context slot (CValXVarTable)
    /// </summary>
    /// <remarks>
    /// Used for computed values, e.g. a Stream Aggregate's results.
    ///
    /// The context slot at +0x08 holds the current row and the ordinal at +0x0C picks its 32-byte variant. CValXVarTable::SetDataX writes
    /// it.
    /// </remarks>
    VariantTable
}
