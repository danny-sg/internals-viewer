using System.Data;
using InternalsViewer.Internals.Converters;

namespace InternalsViewer.Internals.Metadata.Internals;

public static class TypeInfoExtensions
{
    /// <remarks>
    ///     Adapted from
    ///     https://improve.dk/creating-a-type-aware-parser-for-the-sys-system_internals_partition_columns-ti-field/
    /// </remarks>
    public static TypeInfo ToTypeInfo(this int value)
    {
        var sqlType = SqlTypeConverter.ToSqlType((byte) (value & 0xFF));

        return sqlType switch
        {
            SqlDbType.Bit => GetBit(),
            // Number types
            SqlDbType.TinyInt => GetTinyInt(),
            SqlDbType.SmallInt => GetSmallInt(),
            SqlDbType.Int => GetInt(),
            SqlDbType.BigInt => GetBigInt(),
            SqlDbType.Decimal => GetDecimalType(value),
            SqlDbType.Money => GetMoneyType(),
            SqlDbType.SmallMoney => GetSmallMoney(),
            SqlDbType.Float => GetFloat(),
            SqlDbType.Real => GetReal(),

            // General types
            SqlDbType.VarBinary => Get(SqlDbType.VarBinary, value),

            SqlDbType.Char => Get(SqlDbType.Char, value),
            SqlDbType.NChar => Get(SqlDbType.NChar, value),
            SqlDbType.VarChar => Get(SqlDbType.VarChar, value),
            SqlDbType.NVarChar => Get(SqlDbType.NVarChar, value),

            SqlDbType.Binary => Get(SqlDbType.Binary, value),

            SqlDbType.Image => Get(SqlDbType.Image, value),
            SqlDbType.NText => Get(SqlDbType.NText, value),
            SqlDbType.Text => Get(SqlDbType.Text, value),

            SqlDbType.Xml => Get(SqlDbType.Xml, value),

            // Date/Time Types
            SqlDbType.Date => GetDate(),
            SqlDbType.Time => GetTime(value),
            SqlDbType.SmallDateTime => GetSmallDateTime(),
            SqlDbType.DateTime => GetDateTime(),
            SqlDbType.DateTime2 => GetDateTime2(value),
            SqlDbType.DateTimeOffset => GetDateTimeOffset(value),
            SqlDbType.Timestamp => GetTimestamp(),

            SqlDbType.Variant => GetSqlVariant(),

            SqlDbType.UniqueIdentifier => GetUniqueIdentifier(),
            _ => throw new ArgumentException("Unimplemented type - " + sqlType)
        };
    }

    private static TypeInfo GetBigInt()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.BigInt,
            MaxLength = 8,
            MaxInRowLength = 8,
            Precision = 19
        };

        return typeInfo;
    }

    private static TypeInfo GetBit()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Bit,
            MaxLength = 1,
            MaxInRowLength = 1,
            Precision = 1
        };

        return typeInfo;
    }

    private static TypeInfo GetDate()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Date,
            MaxLength = 3,
            MaxInRowLength = 3,
            Precision = 10
        };

        return typeInfo;
    }

    private static TypeInfo GetDateTime()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.DateTime,
            MaxLength = 8,
            MaxInRowLength = 8,
            Precision = 23,
            Scale = 3
        };

        return typeInfo;
    }

    private static TypeInfo GetDateTime2(int value)
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.DateTime2
        };

        typeInfo.Scale = (byte) ((value & 0xFF00) >> 8);
        typeInfo.Precision = (byte) (20 + typeInfo.Scale);

        switch (typeInfo.Scale)
        {
            case < 3:
                typeInfo.MaxLength = 6;
                typeInfo.MaxInRowLength = 6;
                break;
            case < 5:
                typeInfo.MaxLength = 7;
                typeInfo.MaxInRowLength = 7;
                break;
            default:
                typeInfo.MaxLength = 8;
                typeInfo.MaxInRowLength = 8;
                break;
        }

        return typeInfo;
    }

    private static TypeInfo GetDateTimeOffset(int value)
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.DateTimeOffset
        };

        typeInfo.Scale = (byte) ((value & 0xFF00) >> 8);
        typeInfo.Precision = (byte) (26 + (typeInfo.Scale > 0 ? typeInfo.Scale + 1 : typeInfo.Scale));

        switch (typeInfo.Scale)
        {
            case < 3:
                typeInfo.MaxLength = 8;
                typeInfo.MaxInRowLength = 8;
                break;
            case < 5:
                typeInfo.MaxLength = 9;
                typeInfo.MaxInRowLength = 9;
                break;
            default:
                typeInfo.MaxLength = 10;
                typeInfo.MaxInRowLength = 10;
                break;
        }

        return typeInfo;
    }

    private static TypeInfo GetDecimalType(int value)
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Decimal
        };

        typeInfo.Precision = (byte) ((value & 0xFF00) >> 8);
        typeInfo.Scale = (byte) ((value & 0xFF0000) >> 16);

        switch (typeInfo.Precision)
        {
            case < 10:
                typeInfo.MaxLength = 5;
                typeInfo.MaxInRowLength = 5;
                break;
            case < 20:
                typeInfo.MaxLength = 9;
                typeInfo.MaxInRowLength = 9;
                break;
            case < 29:
                typeInfo.MaxLength = 13;
                typeInfo.MaxInRowLength = 13;
                break;
            default:
                typeInfo.MaxLength = 17;
                typeInfo.MaxInRowLength = 17;
                break;
        }

        return typeInfo;
    }

    private static TypeInfo GetFloat()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Float,
            MaxLength = 8,
            MaxInRowLength = 8,
            Precision = 53
        };

        return typeInfo;
    }

    private static TypeInfo GetInt()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Int,
            MaxLength = 4,
            MaxInRowLength = 4,
            Precision = 10
        };

        return typeInfo;
    }

    private static TypeInfo GetMoneyType()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Money,
            MaxLength = 8,
            MaxInRowLength = 8,
            Precision = 19,
            Scale = 4
        };

        return typeInfo;
    }

    private static TypeInfo GetReal()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Real,
            MaxLength = 4,
            MaxInRowLength = 4,
            Precision = 24
        };

        return typeInfo;
    }

    private static TypeInfo GetSmallDateTime()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.SmallDateTime,
            MaxLength = 4,
            MaxInRowLength = 4,
            Precision = 16
        };

        return typeInfo;
    }

    private static TypeInfo GetSmallInt()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.SmallInt,
            MaxLength = 2,
            MaxInRowLength = 2,
            Precision = 5
        };

        return typeInfo;
    }

    private static TypeInfo GetSmallMoney()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.SmallMoney,
            MaxLength = 4,
            MaxInRowLength = 4,
            Precision = 10,
            Scale = 4
        };

        return typeInfo;
    }

    private static TypeInfo GetSqlVariant()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Variant,
            MaxLength = 8016,
            MaxInRowLength = 8016
        };

        return typeInfo;
    }

    private static TypeInfo GetTime(int value)
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Time
        };

        typeInfo.Scale = (byte) ((value & 0xFF00) >> 8);
        typeInfo.Precision = (byte) (8 + (typeInfo.Scale > 0 ? typeInfo.Scale + 1 : typeInfo.Scale));

        switch (typeInfo.Scale)
        {
            case < 3:
                typeInfo.MaxLength = 3;
                typeInfo.MaxInRowLength = 3;
                break;
            case < 5:
                typeInfo.MaxLength = 4;
                typeInfo.MaxInRowLength = 4;
                break;
            default:
                typeInfo.MaxLength = 5;
                typeInfo.MaxInRowLength = 5;
                break;
        }

        return typeInfo;
    }

    private static TypeInfo GetTimestamp()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.Timestamp,
            MaxLength = 8,
            MaxInRowLength = 8
        };

        return typeInfo;
    }

    private static TypeInfo GetTinyInt()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.TinyInt,
            MaxLength = 1,
            MaxInRowLength = 1,
            Precision = 3
        };

        return typeInfo;
    }

    private static TypeInfo GetUniqueIdentifier()
    {
        var typeInfo = new TypeInfo
        {
            DataType = SqlDbType.UniqueIdentifier,
            MaxLength = 16,
            MaxInRowLength = 16
        };

        return typeInfo;
    }

    private static TypeInfo Get(SqlDbType type, int value)
    {
        var typeInfo = new TypeInfo
        {
            DataType = type
        };

        typeInfo.MaxLength = (short) ((value & 0xFFFF00) >> 8);

        if (typeInfo.MaxLength == 0)
        {
            typeInfo.MaxLength = -1;
            typeInfo.MaxInRowLength = 8000;
        }
        else
        {
            typeInfo.MaxInRowLength = typeInfo.MaxLength;
        }

        return typeInfo;
    }
}