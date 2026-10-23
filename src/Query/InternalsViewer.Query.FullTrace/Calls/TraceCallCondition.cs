using System.Globalization;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceCallCondition(TraceConditionTarget Target,
                                        ArgumentSlot? Argument,
                                        string Type,
                                        TraceConditionOperator Operator,
                                        string Value)
{
    private const string HexPrefix = "0x";

    private static readonly char[] Indirections = ['*', '&'];

    public bool IsCallCondition => Target is TraceConditionTarget.Argument or TraceConditionTarget.Return;

    public static bool MatchesAll(IReadOnlyList<TraceCallCondition> conditions, TraceArgumentCall call)
    {
        foreach (var condition in conditions)
        {
            if (!condition.Matches(call))
            {
                return false;
            }
        }

        return true;
    }

    public bool Matches(TraceArgumentCall call)
    {
        if (!IsCallCondition)
        {
            return true;
        }

        if (GetRaw(call) is not { } raw)
        {
            return false;
        }

        var locator = call.GetLocator(Target == TraceConditionTarget.Return ? ArgumentLayout.ReturnRegister : Argument?.Index ?? -1);

        var comparison = TryParseNumber(Value, out var expected)
            ? GetNumber(raw, locator)?.CompareTo(expected)
            : string.Compare(GetText(raw, locator), Value.Trim(), StringComparison.OrdinalIgnoreCase);

        return comparison is { } result && Operator switch
        {
            TraceConditionOperator.Equal => result == 0,
            TraceConditionOperator.NotEqual => result != 0,
            TraceConditionOperator.LessThan => result < 0,
            TraceConditionOperator.LessThanOrEqual => result <= 0,
            TraceConditionOperator.GreaterThan => result > 0,
            TraceConditionOperator.GreaterThanOrEqual => result >= 0,
            _ => false
        };
    }

    public bool MatchesNode(CallStackNode node)
    {
        if (Target is not (TraceConditionTarget.CalledBy or TraceConditionTarget.CalledDirectlyBy))
        {
            return true;
        }

        var isMatch = Target == TraceConditionTarget.CalledDirectlyBy
            ? node.Parent is { } parent && IsFrameMatch(parent)
            : GetCallers(node).Any(IsFrameMatch);

        return Operator == TraceConditionOperator.NotEqual ? !isMatch : isMatch;
    }

    public bool MatchesOperator(ExecutionOperatorEvent? operatorEvent)
    {
        if (Target != TraceConditionTarget.Operator)
        {
            return true;
        }

        var isMatch = operatorEvent is not null && GetLabels(operatorEvent).Any(l => SymbolIndex.ModuleMatches(Value, l));

        return Operator == TraceConditionOperator.NotEqual ? !isMatch : isMatch;
    }

    private static IEnumerable<string> GetLabels(ExecutionOperatorEvent operatorEvent)
        => [operatorEvent.Name, operatorEvent.OperatorDescription, operatorEvent.Label];

    private static IEnumerable<CallStackNode> GetCallers(CallStackNode node)
    {
        for (var caller = node.Parent; caller is not null; caller = caller.Parent)
        {
            yield return caller;
        }
    }

    private static bool TryParseNumber(string text, out Int128 value)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var isHex = ulong.TryParse(trimmed[HexPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex);

            value = hex;

            return isHex;
        }

        return Int128.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string GetPointee(string type) => type.IndexOfAny(Indirections) is var index and >= 0 ? type[..index] : type;

    private bool IsFrameMatch(CallStackNode node)
    {
        if (node.Frame is not { } frame)
        {
            return false;
        }

        var (module, symbol) = SymbolIndex.SplitModule(Value);

        return (module is null || SymbolIndex.ModuleMatches(module, frame.Module))
               && symbol.Length > 0
               && SymbolIndex.Matches(symbol, SymbolSearchFields.All, frame.Resolved?.ClassName ?? string.Empty, node.Symbol, string.Empty);
    }

    private ulong? GetRaw(TraceArgumentCall call) => Target switch
    {
        TraceConditionTarget.Return => call.Returned ? call.ReturnValue : null,
        _ => Argument is { } slot ? call.GetValue(slot) : null
    };

    private Int128? GetNumber(ulong raw, Locator? locator) => locator switch
    {
        { ValueType: LocatorValueType.Variant } variant when new VariantLocator(variant).Status == VariantStatus.Value
            => (long)variant.Value,
        { ValueType: LocatorValueType.Variant } => null,
        { ValueType: LocatorValueType.Referenced } referenced => ArgumentValue.ToInteger(GetPointee(Type), referenced.Value),
        _ => ArgumentValue.ToInteger(Type, raw)
    };

    private string GetText(ulong raw, Locator? locator) => locator switch
    {
        { ValueType: LocatorValueType.Referenced } referenced => ArgumentValue.FormatReferenced(Type, referenced.Value),
        { ValueType: not LocatorValueType.Argument } typed => typed.ToString(),
        _ => ArgumentValue.Decode(Type, raw) ?? ArgumentValue.Format(Type, raw)
    };
}
