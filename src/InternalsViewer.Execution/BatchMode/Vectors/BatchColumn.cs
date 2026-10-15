using System.Data;
using InternalsViewer.Internals.BatchMode.Normalization;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public sealed record BatchColumn
{
    public string Name { get; set; } = string.Empty;

    public SqlDbType DataType { get; set; }

    public byte Precision { get; set; }

    public byte Scale { get; set; }

    public short DataLength { get; set; }

    public DataIdSpace? IdSpace { get; set; }

    public BatchValueDomain Domain => BatchValueNormalizer.GetDomain(DataType);

    /// <summary>
    /// Whether this column's data ids can be compared with another's without reading either value
    /// </summary>
    public bool SharesDataIdsWith(BatchColumn other) => IdSpace is { } space && space == other.IdSpace;
}
