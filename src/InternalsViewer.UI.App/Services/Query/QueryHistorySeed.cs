using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InternalsViewer.UI.App.Services.Query;

/// <summary>
/// The queries a database starts its history with
/// </summary>
/// <remarks>
/// One embedded file per database under Assets\Samples, named after the database it belongs to, so a database is
/// recognised by its name alone and a new set needs no code. The file is a plain script split on its GO separators,
/// which makes each batch one history entry and leaves the file runnable as it stands. Anything before the first GO
/// is an entry too, so a file opens on a query rather than on a banner.
/// </remarks>
public static class QueryHistorySeed
{
    public static IReadOnlyList<string> Read(string databaseName)
    {
        var assembly = typeof(QueryHistorySeed).Assembly;

        var resource = assembly.GetManifestResourceNames()
                               .FirstOrDefault(n => n.EndsWith($".{databaseName}.sql", StringComparison.OrdinalIgnoreCase));

        if (resource is null)
        {
            return [];
        }

        using var stream = assembly.GetManifestResourceStream(resource);

        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);

        return Split(reader.ReadToEnd());
    }

    private static List<string> Split(string script)
    {
        var queries = new List<string>();

        var batch = new List<string>();

        foreach (var line in script.Split('\n'))
        {
            var text = line.TrimEnd('\r');

            if (text.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                Take(queries, batch);

                continue;
            }

            batch.Add(text);
        }

        Take(queries, batch);

        return queries;
    }

    private static void Take(List<string> queries, List<string> batch)
    {
        var query = string.Join(Environment.NewLine, batch).Trim();

        batch.Clear();

        if (query.Length > 0)
        {
            queries.Add(query);
        }
    }
}
