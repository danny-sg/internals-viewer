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

    public BatchSlotDomain Domain => DataType switch
    {
        SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt or SqlDbType.Bit
            or SqlDbType.Money or SqlDbType.SmallMoney
            or SqlDbType.Date or SqlDbType.DateTime or SqlDbType.SmallDateTime => BatchSlotDomain.Integer,

        SqlDbType.Float or SqlDbType.Real => BatchSlotDomain.Real,

        SqlDbType.Decimal => BatchSlotDomain.Numeric,

        SqlDbType.DateTime2 or SqlDbType.Time or SqlDbType.DateTimeOffset => BatchSlotDomain.Temporal,

        SqlDbType.Char or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.NVarChar
            => BatchSlotDomain.Dictionary,

        _ => BatchSlotDomain.Deep
    };

    /// <summary>
    /// Whether this column's data ids can be compared with another's without reading either value
    /// </summary>
    public bool SharesDataIdsWith(BatchColumn other) => IdSpace is { } space && space == other.IdSpace;
}
