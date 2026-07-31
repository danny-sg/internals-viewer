using System.Data;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Tests.Helpers;

internal static class TestKey
{
    public static AccessKey Of(params int[] values)
    {
        return AccessKey.Create([.. values.Select(v => AccessValue.FromInteger(SqlDbType.Int, v))]);
    }

    public static AccessKey Of(int[] values, params string[] columnNames)
    {
        return AccessKey.Create(
        [
            .. values.Select((v, i) => AccessValue.FromInteger(SqlDbType.Int, v)
                                                   .WithColumnName(i < columnNames.Length ? columnNames[i] : null))
        ]);
    }
}
