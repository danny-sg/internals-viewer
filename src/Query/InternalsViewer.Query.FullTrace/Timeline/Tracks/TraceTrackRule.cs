using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed partial record TraceTrackRule(string Name,
                                            string Module,
                                            string Function,
                                            string Colour,
                                            double HeightFactor,
                                            double WidthFactor,
                                            IReadOnlyList<TraceCallCondition> Conditions)
{
    public const string CategoryColour = "Category";

    public const string OperatorNamePlaceholder = "OperatorName";

    public const string ParentOperatorNamePlaceholder = "ParentOperatorName";

    public const string FunctionPlaceholder = "Function";

    public const string PagePlaceholder = "Page";

    public const string SlotPlaceholder = "Slot";

    public const string HeaderFieldPlaceholder = "HeaderField";

    public const string ResultPlaceholder = "Result";

    public const string PartitionPlaceholder = "Partition";

    public const string BucketPlaceholder = "Bucket";

    public const string RowCallPlaceholder = "RowCall";

    public const string RowUnitPlaceholder = "RowUnit";

    public const string ExpressionPlaceholder = "Expression";

    public const string ExecutionPlanPrefix = "ExecutionPlan.";

    public const string CurrentRangePlaceholder = ExecutionPlanPrefix + "CurrentRange";

    public const string ListSeparator = ", ";

    private const string AnyModule = "*";

    private const char FunctionSeparator = '|';

    public string? Label { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Details { get; init; }

    public string? ReturnDescription { get; init; }

    public string? PlanPart { get; init; }

    public string? PlanPartColour { get; init; }

    public string? Background { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsCounter { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsPhase { get; init; }

    public bool IsPlanDefault { get; init; } = true;

    public TraceTickSourceType Source { get; init; }

    public byte Value { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Offset { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Length { get; init; }

    public string? DataType { get; init; }

    private GlobPattern[] Functions
        => field ??= [.. Function.Split(FunctionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                 .Select(GlobPattern.Parse)];

    public string GetLabel() => Label ?? Function;

    public string GetTitle() => Title ?? GetLabel();

    public string GetPlanStepKey() => $"{Name}/{GetLabel()}";

    public static string Format(string text, Func<string, string?> getValue)
        => PlaceholderPattern().Replace(text, m => GetPlaceholderText(m, getValue));

    public bool MatchesName(string name) => Functions.Length == 0 || Functions.Any(f => f.Matches(name));

    public bool IsFunction(CallTreeNode node)
        => node.Frame is { } frame
           && (Module == AnyModule || string.Equals(frame.Module.Name, Module, StringComparison.OrdinalIgnoreCase))
           && Functions.Any(f => f.Matches(node.Symbol));

    public bool MatchesCall(TraceArgumentCall call) => TraceCallCondition.MatchesAll(Conditions, call);

    public bool MatchesNode(CallTreeNode node) => Conditions.All(c => c.MatchesNode(node));

    public bool MatchesWithin(CallTreeNode node) => Conditions.All(c => c.MatchesNode(node, isWithin: true));

    public bool MatchesOperator(ExecutionOperatorEvent? operatorEvent) => Conditions.All(c => c.MatchesOperator(operatorEvent));

    private static string GetPlaceholderText(Match match, Func<string, string?> getValue)
    {
        var name = match.Groups["name"].Value;

        if (getValue(name) is not { } value)
        {
            return match.Value;
        }

        var quote = match.Groups["quote"].Value;

        var items = name.Equals(HeaderFieldPlaceholder, StringComparison.OrdinalIgnoreCase) ? value.Split(ListSeparator) : [value];

        return string.Join(ListSeparator, items.Select(i => $"{quote}{i}{quote}"));
    }

    [GeneratedRegex(@"(?<quote>`?)\{(?<name>[\w.]+)\}\k<quote>")]
    private static partial Regex PlaceholderPattern();
}
