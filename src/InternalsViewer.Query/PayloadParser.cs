using System.Text.RegularExpressions;

namespace InternalsViewer.Query;

internal static partial class PayloadParser
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