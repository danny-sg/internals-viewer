using System.Text.RegularExpressions;

namespace InternalsViewer.Query;

/// <summary>
/// Parses a payload into a set of SQL commands
/// </summary>
internal static partial class QueryParser
{
    public static (string[] PreCommands, string[] Commands, string[] PostCommands) Parse(ExecuteSqlPayload payload)
    {
        if (payload.TrackedSelection == null)
        {
            return ([], SplitCommands(payload.SqlText), []);
        }

        var length = payload.SqlText.Length;
        
        var start = Math.Clamp(payload.TrackedSelection.Start, 0, length);
        
        var end = Math.Clamp(payload.TrackedSelection.End, start, length);

        var preCommands = payload.SqlText[..start];

        var commands = payload.SqlText[start..end];
        
        var postCommands = payload.SqlText[end..];

        return (SplitCommands(preCommands), SplitCommands(commands), SplitCommands(postCommands));
    }

    /// <summary>
    /// Split commands by GO
    /// </summary>
    public static string[] SplitCommands(string sql)
    {
        var result = GoRegEx().Split(sql)
                              .Where(value => !string.IsNullOrWhiteSpace(value))
                              .ToArray();

        return result;
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GoRegEx();
}