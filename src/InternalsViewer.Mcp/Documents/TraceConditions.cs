using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Documents;

/// <summary>
/// Turns condition text such as <c>arg 3 = 0x1</c> or <c>return != 0</c> into the conditions the Trace Timeline filters calls with
/// </summary>
public static class TraceConditions
{
    private const string ThisType = "this";

    private const string VoidType = "void";

    private static readonly Regex ValueCondition = new(
        @"^\s*(?:arg(?:ument)?\s*(?<number>\d+)|(?<return>return(?:s)?|ret))\s*(?<operator>==|=|!=|<>|<=|>=|<|>)\s*(?<value>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CallerCondition = new(
        @"^\s*(?<not>not\s+)?(?<kind>calledby|calleddirectlyby|operator)\s+(?<value>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool NeedsLayout(IReadOnlyList<string>? specs) => specs?.Any(s => ValueCondition.IsMatch(s)) == true;

    public static IReadOnlyList<TraceCallCondition> Parse(IReadOnlyList<string>? specs, ArgumentLayout? layout)
    {
        var conditions = new List<TraceCallCondition>();

        foreach (var spec in specs ?? [])
        {
            conditions.Add(ParseOne(spec, layout));
        }

        return conditions;
    }

    public static string Describe(CallStackNode node) => $"{node.Frame?.Module}!{node.Symbol}";

    private static TraceCallCondition ParseOne(string spec, ArgumentLayout? layout)
    {
        if (CallerCondition.Match(spec) is { Success: true } caller)
        {
            var target = caller.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "calledby" => TraceConditionTarget.CalledBy,
                "calleddirectlyby" => TraceConditionTarget.CalledDirectlyBy,
                _ => TraceConditionTarget.Operator
            };

            var comparison = caller.Groups["not"].Success ? TraceConditionOperator.NotEqual : TraceConditionOperator.Equal;

            return new TraceCallCondition(target, null, string.Empty, comparison, caller.Groups["value"].Value);
        }

        if (ValueCondition.Match(spec) is not { Success: true } match)
        {
            throw new McpException(
                $"Cannot read the condition '{spec}'. Use 'arg N = value', 'return != 0', 'calledby Function', " +
                "'calleddirectlyby Function' or 'operator Name'. The operators are =, !=, <, <=, > and >=.");
        }

        var comparisonOperator = match.Groups["operator"].Value switch
        {
            "=" or "==" => TraceConditionOperator.Equal,
            "!=" or "<>" => TraceConditionOperator.NotEqual,
            "<" => TraceConditionOperator.LessThan,
            "<=" => TraceConditionOperator.LessThanOrEqual,
            ">" => TraceConditionOperator.GreaterThan,
            _ => TraceConditionOperator.GreaterThanOrEqual
        };

        var value = match.Groups["value"].Value;

        if (layout is null)
        {
            throw new McpException($"Cannot use '{spec}' as the function's signature could not be resolved.");
        }

        if (match.Groups["return"].Success)
        {
            if (layout.ReturnType is not { } returnType || returnType == VoidType)
            {
                throw new McpException($"Cannot use '{spec}' as the function returns nothing.");
            }

            return new TraceCallCondition(TraceConditionTarget.Return, null, returnType, comparisonOperator, value);
        }

        var number = int.Parse(match.Groups["number"].Value);

        var slots = layout.Slots.Where(s => TraceArgumentCall.IsCaptured(s) && s.Type != ThisType && s.Name != ArgumentLayout.ReturnBuffer)
                          .ToList();

        if (number < 1 || number > slots.Count)
        {
            throw new McpException($"Cannot use '{spec}' as the function has {slots.Count} captured arguments, numbered from 1.");
        }

        var slot = slots[number - 1];

        return new TraceCallCondition(TraceConditionTarget.Argument, slot, slot.Type, comparisonOperator, value);
    }
}
