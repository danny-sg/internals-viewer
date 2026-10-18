using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output.SourceParsers;

internal static class OutputSourceParser
{
    private const int MaximumDepth = 4;

    public static TraceOutputSource Parse(ITraceReplay replay,
                                          IReadOnlyDictionary<ulong, OutputValueClass> classes,
                                          ulong value,
                                          ulong context,
                                          int depth = 0)
    {
        if (depth > MaximumDepth || !replay.TryRead(value, out ulong vtable) || !classes.TryGetValue(vtable, out var valueClass))
        {
            return TraceOutputSource.Unknown;
        }

        if (SwitchSourceParser.GetInner(replay, value, valueClass) is { } inner)
        {
            return Parse(replay, classes, inner, context, depth + 1) with { Value = value };
        }

        var source = valueClass switch
        {
            OutputValueClass.Hash => HashSourceParser.Parse(replay, value, context),
            OutputValueClass.Table => TableSourceParser.Parse(replay, value, context),
            OutputValueClass.BatchRedirect => BatchRedirectSourceParser.Parse(replay, value),
            OutputValueClass.VariantTable => VariantTableSourceParser.Parse(replay, value, context),
            OutputValueClass.Constant => ConstantSourceParser.Parse(replay, value),
            _ => null
        };

        return source ?? TraceOutputSource.Unknown;
    }
}
