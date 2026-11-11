using System.Data;
using InternalsViewer.Internals.Helpers;
using SqlTypeInfo = InternalsViewer.Query.FullTrace.Locators.SqlTypeInfo;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

namespace InternalsViewer.UI.App.Helpers;

public static class ColumnBindingText
{
    public static string GetType(ColumnBinding binding) => GetType(binding.Type, 0, 0, binding.MaximumLength);

    public static string GetType(SqlTypeInfo type) => GetType(type.TypeId, type.Precision, type.Scale, type.MaxLength);

    private static string GetType(byte typeId, int precision, int scale, int length)
    {
        if (!SqlTypeHelpers.TryGetSqlType(typeId, out var type))
        {
            return $"0x{typeId:X2}";
        }

        return type switch
        {
            SqlDbType.Char or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.NVarChar or SqlDbType.Binary or SqlDbType.VarBinary
                => SqlDataTypeFormat.Format(type, 0, 0, length),
            SqlDbType.Decimal when precision > 0 => SqlDataTypeFormat.Format(type, precision, scale, length),
            _ => SqlDataTypeFormat.GetName(type)
        };
    }

    public static string GetOffset(ColumnBinding binding)
        => binding.IsVariableLength ? $"Variable {binding.VariableIndex}" : $"0x{binding.RecordOffset:X}";
}
