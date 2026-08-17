using System.Data;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.AccessPaths.Aggregation;

public sealed class AggregateAccumulator(AggregateColumn column)
{
    private readonly HashSet<AccessValue> _seen = [];

    private AccessValue _extreme = AccessValue.Null;

    private long _count;

    private long _sumInteger;

    private decimal _sumDecimal;

    private double _sumReal;

    private AccessValueType _sumType = AccessValueType.Null;

    private SqlDbType _argumentType = SqlDbType.Variant;

    private bool _hasValue;

    public AggregateColumn Column { get; } = column;

    public AccessValue Result => Column.Function switch
    {
        AggregateFunction.CountStar or AggregateFunction.CountBig
            => AccessValue.FromInteger(SqlDbType.BigInt, _count),
        AggregateFunction.Count
            => AccessValue.FromInteger(SqlDbType.Int, _count),
        AggregateFunction.Min or AggregateFunction.Max or AggregateFunction.Any
            => _hasValue ? _extreme : AccessValue.Null,
        AggregateFunction.Sum
            => _hasValue ? Total() : AccessValue.Null,
        AggregateFunction.Average
            => _hasValue && _count > 0 ? Mean() : AccessValue.Null,
        _ => AccessValue.Null
    };

    public AggregateValue Value => new(Column.Column, Column.ToText(), AccessValueFormatter.ToText(Result));

    private SqlDbType TotalType => _argumentType switch
    {
        SqlDbType.TinyInt or SqlDbType.SmallInt or SqlDbType.Int => SqlDbType.Int,
        _ => _argumentType
    };

    public void Reset()
    {
        _seen.Clear();

        _extreme = AccessValue.Null;

        _count = 0;
        _sumInteger = 0;
        _sumDecimal = 0;
        _sumReal = 0;

        _sumType = AccessValueType.Null;
        _argumentType = SqlDbType.Variant;

        _hasValue = false;
    }

    public void Add(AccessValue value)
    {
        if (Column.Function == AggregateFunction.CountStar)
        {
            _count++;

            return;
        }

        if (value.IsNull)
        {
            return;
        }

        if (Column.IsDistinct && !_seen.Add(value))
        {
            return;
        }

        _argumentType = value.DataType;

        _count++;

        switch (Column.Function)
        {
            case AggregateFunction.Min:
                if (!_hasValue || AccessValueComparer.Compare(value, _extreme) < 0)
                {
                    _extreme = value;
                }

                break;

            case AggregateFunction.Max:
                if (!_hasValue || AccessValueComparer.Compare(value, _extreme) > 0)
                {
                    _extreme = value;
                }

                break;

            case AggregateFunction.Any:
                if (!_hasValue)
                {
                    _extreme = value;
                }

                break;

            case AggregateFunction.Sum or AggregateFunction.Average:
                AddToTotal(value);

                break;
        }

        _hasValue = true;
    }

    private void AddToTotal(AccessValue value)
    {
        switch (value.Type)
        {
            case AccessValueType.Integer:
                _sumInteger += value.Numeric;

                break;

            case AccessValueType.Decimal:
                _sumDecimal += value.ToDecimal();

                break;

            case AccessValueType.Real:
                _sumReal += value.Real;

                break;

            default:
                return;
        }

        _sumType = value.Type;
    }

    private AccessValue Total()
        => _sumType switch
        {
            AccessValueType.Integer 
                => AccessValue.FromInteger(TotalType, _sumInteger),
            AccessValueType.Decimal 
                => AccessValue.FromDecimal(TotalType, _sumDecimal),
            AccessValueType.Real 
                => AccessValue.FromReal(SqlDbType.Float, _sumReal),
            _ => AccessValue.Null
        };

    private AccessValue Mean()
        => _sumType switch
        {
            AccessValueType.Integer 
                => AccessValue.FromInteger(TotalType, _sumInteger / _count),
            AccessValueType.Decimal 
                => AccessValue.FromDecimal(TotalType, _sumDecimal / _count),
            AccessValueType.Real 
                => AccessValue.FromReal(SqlDbType.Float, _sumReal / _count),
            _ => AccessValue.Null
        };
}
