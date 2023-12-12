using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Extensions;

internal static class DataReaderExtensions
{
    internal static T? GetNullableValue<T>(this SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);

        if(reader.IsDBNull(ordinal))
        {
            return default;
        }

        return reader.GetFieldValue<T>(ordinal);
    }
}
