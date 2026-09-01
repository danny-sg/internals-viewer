using System.Data;
using InternalsViewer.Execution.BatchMode.Normalization;

namespace InternalsViewer.Execution.BatchMode.Vectors;

public sealed record BatchColumn
{
    public string Name { get; set; } = string.Empty;

    public SqlDbType DataType { get; set; }

    public byte Precision { get; set; }

    public byte Scale { get; set; }

    public short DataLength { get; set; }

    public DataIdSpace? IdSpace { get; set; }

    public BatchValueDomain Domain => DataType switch
    {
        SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt or SqlDbType.Bit
            or SqlDbType.Money or SqlDbType.SmallMoney
            or SqlDbType.Date or SqlDbType.DateTime or SqlDbType.SmallDateTime => BatchValueDomain.Integer,

        SqlDbType.Float or SqlDbType.Real => BatchValueDomain.Real,

        SqlDbType.Decimal => BatchValueDomain.Numeric,

        SqlDbType.DateTime2 or SqlDbType.Time or SqlDbType.DateTimeOffset => BatchValueDomain.Temporal,

        SqlDbType.Char or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.NVarChar
            => BatchValueDomain.Dictionary,

        _ => BatchValueDomain.Deep
    };

    /// <summary>
    /// Whether this column's data ids can be compared with another's without reading either value
    /// </summary>
    public bool SharesDataIdsWith(BatchColumn other) => IdSpace is { } space && space == other.IdSpace;
}
