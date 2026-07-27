using System.Data;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.Tests.UnitTests.DataAccess.AccessPaths;

internal static class TestKey
{
    public static AccessKey Of(params int[] values)
    {
        return AccessKey.Create([.. values.Select(v => AccessValue.FromInteger(SqlDbType.Int, v))]);
    }
}
