namespace InternalsViewer.Query.Plans.Predicates;

public static class IntrinsicFunctions
{
    private static readonly Dictionary<string, (int MinArguments, int MaxArguments)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ABS"] = (1, 1),
        ["SIGN"] = (1, 1),
        ["FLOOR"] = (1, 1),
        ["CEILING"] = (1, 1),
        ["ROUND"] = (2, 2),
        ["POWER"] = (2, 2),
        ["LEN"] = (1, 1),
        ["UPPER"] = (1, 1),
        ["LOWER"] = (1, 1),
        ["LTRIM"] = (1, 1),
        ["RTRIM"] = (1, 1),
        ["LEFT"] = (2, 2),
        ["RIGHT"] = (2, 2),
        ["SUBSTRING"] = (3, 3),
        ["CHARINDEX"] = (2, 3),
        ["REPLACE"] = (3, 3),
        ["CONCAT"] = (2, 254),
        ["CONCAT_WS"] = (3, 254),
        ["ISNULL"] = (2, 2),
        ["GETDATE"] = (0, 0),
        ["GETUTCDATE"] = (0, 0),
        ["SYSDATETIME"] = (0, 0),
        ["SYSUTCDATETIME"] = (0, 0)
    };

    public static bool IsSupported(string name, int argumentCount)
    {
        return Supported.TryGetValue(name, out var arity)
               && argumentCount >= arity.MinArguments
               && argumentCount <= arity.MaxArguments;
    }
}
