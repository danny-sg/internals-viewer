using System.Data;

namespace InternalsViewer.Internals.BatchMode.Normalization;

/// <summary>
/// Normalizes a value into a Vector Batch Slot value
/// </summary>
/// <remarks>
/// SQL Server converts values of different type into a 64 bit value via normalization.
///
/// This allows batch mode to take advantage of SIMD processing and vectorization, CPU cache optimizations, and means non-dictionary values
/// can be compared, sorted, and grouped directly without decoding/transformation.
///
/// Types and values that are not representable in a normalized space have a low bit = 1 and are instead represented by a reference to the
/// deep data store, where the value is stored in its original form - see <see cref="BatchDeepDataStore"/>.
///
/// NULL is represented by a special value of 1 (tag set, payload zero).
///
/// See <see href="https://sqlperformance.com/2019/09/sql-performance/batch-mode-normalization-performance"/>
/// </remarks>
public static class BatchValueNormalizer
{
    public static BatchValueDomain GetDomain(SqlDbType dataType) => dataType switch
    {
        SqlDbType.BigInt or SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt or SqlDbType.Bit
            or SqlDbType.Money or SqlDbType.SmallMoney
            or SqlDbType.DateTime or SqlDbType.SmallDateTime => BatchValueDomain.Integer,

        SqlDbType.Float or SqlDbType.Real => BatchValueDomain.Real,

        SqlDbType.Decimal => BatchValueDomain.Numeric,

        SqlDbType.Date or SqlDbType.DateTime2 or SqlDbType.Time or SqlDbType.DateTimeOffset => BatchValueDomain.Temporal,

        SqlDbType.Char or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.NVarChar
            => BatchValueDomain.Dictionary,

        _ => BatchValueDomain.Deep
    };
}
