using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Batches;

internal sealed record BatchFunctions(ulong[] Functions)
{
    public static BatchFunctions Empty { get; } = new([]);

    public static BatchFunctions Merge(IEnumerable<BatchFunctions> functions) => new([.. functions.SelectMany(f => f.Functions)]);

    public BatchFunctions Rebase(ulong address) => new([.. Functions.Select(f => f + address)]);

    internal sealed class Collector
    {
        private const string BatchClass = "CBpQScan";

        private const string BatchMethod = "BpGetBatchData";

        private List<(string ClassName, uint Rva)> Methods { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (className.Length > 0 && methodName is BatchMethod)
            {
                Methods.Add((className.ToString(), rva));
            }
        }

        public BatchFunctions Build(IReadOnlyDictionary<string, string[]> bases)
        {
            HashSet<uint> shared = [.. Methods.Where(m => !IsBatchClass(bases, m.ClassName)).Select(m => m.Rva)];

            ulong[] functions = [.. Methods.Where(m => IsBatchClass(bases, m.ClassName) && !shared.Contains(m.Rva))
                                           .Select(m => (ulong)m.Rva)
                                           .Distinct()];

            return functions.Length == 0 ? Empty : new(functions);
        }

        private static bool IsBatchClass(IReadOnlyDictionary<string, string[]> bases, string className)
            => bases.TryGetValue(className, out var classBases) && classBases.Contains(BatchClass);
    }
}
