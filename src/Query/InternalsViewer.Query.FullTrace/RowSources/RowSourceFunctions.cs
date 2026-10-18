using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.RowSources.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowSources;

internal sealed record RowSourceFunctions(IReadOnlyDictionary<ulong, RowSourceFunctionType> Functions)
{
    public static RowSourceFunctions Empty { get; } = new(new Dictionary<ulong, RowSourceFunctionType>());

    public static RowSourceFunctions Merge(IEnumerable<RowSourceFunctions> functions) => new(AddressMap.Merge(functions.Select(f => f.Functions)));

    public RowSourceFunctions Rebase(ulong address) => new(AddressMap.Rebase(Functions, address));

    internal sealed class Collector
    {
        private const string IteratorPrefix = "CQScan";

        private const string CreatorPrefix = "CreateVal";

        private Dictionary<uint, RowSourceFunctionType> Candidates { get; } = [];

        private HashSet<uint> Others { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (!className.StartsWith(IteratorPrefix, StringComparison.Ordinal))
            {
                Others.Add(rva);
            }
            else if (methodName is "GetRow" or "GetRowOrReQualifyHelper" or "Open")
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Iterator);
            }
            else if (methodName.StartsWith(CreatorPrefix, StringComparison.Ordinal))
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Creator);
            }
            else if (methodName.SequenceEqual(className))
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Constructor);
            }
            else
            {
                Others.Add(rva);
            }
        }

        public RowSourceFunctions Build()
            => new(Candidates.Where(c => !Others.Contains(c.Key)).ToDictionary(c => (ulong)c.Key, c => c.Value));
    }
}
