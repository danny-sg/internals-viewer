using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseGroup(string title, IReadOnlyList<ValueUseRow> rows)
{
    public const string CallsOn = "Calls On It";

    public const string Returned = "Returned It";

    public const string Passed = "Passed It";

    public const string Pointed = "Held It Behind A Pointer";

    private static readonly string[] Order = [CallsOn, Returned, Passed, Pointed];

    public string Title { get; } = title;

    public IReadOnlyList<ValueUseRow> Rows { get; } = rows;

    public static IReadOnlyList<ValueUseGroup> Build(IReadOnlyList<TimeTravelValueUse> uses,
                                                     Func<TimeTravelValueUse, FunctionKind?> kindOf)
    {
        var grouped = uses.GroupBy(u => GroupOf(u, kindOf)).ToDictionary(g => g.Key, g => g.ToList());

        return [.. Order.Where(grouped.ContainsKey)
                        .Select(title => new ValueUseGroup(title, [.. grouped[title].Select(u => new ValueUseRow(u))]))];
    }

    private static string GroupOf(TimeTravelValueUse use, Func<TimeTravelValueUse, FunctionKind?> kindOf) => use.Location switch
    {
        "RCX" when kindOf(use) == FunctionKind.Member => CallsOn,
        "RAX" => Returned,
        ['*', ..] => Pointed,
        _ => Passed
    };
}
