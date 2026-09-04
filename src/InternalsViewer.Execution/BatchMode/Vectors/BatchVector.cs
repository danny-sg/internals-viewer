using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Internals.Columnstore.Decoding;

namespace InternalsViewer.Execution.BatchMode.Vectors;

/// <summary>
/// Batch mode vector representation of a column/segment
/// </summary>
/// <remarks>
/// Batch Mode represents a column as a vector of normalized 64-bit values.
/// </remarks>
public sealed class BatchVector(BatchColumn column, int size)
{
    /// <summary>
    /// Column metadata for this vector
    /// </summary>
    /// <remarks>
    /// Required to decode the normalized values in the vector into their actual values as the decode depends on the data type and domain.
    /// </remarks>
    public BatchColumn Column { get; set; } = column;

    /// <summary>
    /// Normalized values of the vector
    /// </summary>
    public BatchValue[] Values { get; } = new BatchValue[size];

    /// <summary>
    /// Source segment reader
    /// </summary>
    /// <remarks>
    /// Required for reference back to the dictionaries linked to the Segment to look up values if the column is dictionary encoded
    /// </remarks>
    public SegmentReader? Source { get; set; }

    public bool IsPure { get; private set; }

    public BatchValue PureValue { get; private set; }

    public BatchValue this[int row] => IsPure ? PureValue : Values[row];

    public void SetPureValue(BatchValue value)
    {
        IsPure = true;

        PureValue = value;
    }

    public void SetValue(int row, BatchValue value)
    {
        if (IsPure)
        {
            Values.AsSpan().Fill(PureValue);

            IsPure = false;
        }

        Values[row] = value;
    }

    public void ClearPure() => IsPure = false;
}
