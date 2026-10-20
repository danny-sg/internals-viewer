using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Evaluations;

internal sealed record EvaluationFunctions(ulong[] Functions)
{
    public static EvaluationFunctions Empty { get; } = new([]);

    public static EvaluationFunctions Merge(IEnumerable<EvaluationFunctions> functions) => new([.. functions.SelectMany(f => f.Functions)]);

    public EvaluationFunctions Rebase(ulong address) => new([.. Functions.Select(f => f + address)]);

    internal sealed class Collector
    {
        private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
        {
            "CEsExec::FastCompare",
            "CEsExec::FastCompare1",
            "CEsExec::GeneralEval",
            "CEsExec::GeneralEval4"
        };

        private static HashSet<string>.AlternateLookup<ReadOnlySpan<char>> NameLookup { get; }
            = Names.GetAlternateLookup<ReadOnlySpan<char>>();

        private HashSet<ulong> Functions { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            if (NameLookup.Contains(name))
            {
                Functions.Add(rva);
            }
        }

        public EvaluationFunctions Build() => new([.. Functions]);
    }
}
