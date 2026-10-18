using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Accessors;

internal sealed record AccessorFunctions(ulong[] Functions)
{
    public static AccessorFunctions Empty { get; } = new([]);

    public static AccessorFunctions Merge(IEnumerable<AccessorFunctions> functions) => new([.. functions.SelectMany(f => f.Functions)]);

    public AccessorFunctions Rebase(ulong address) => new([.. Functions.Select(f => f + address)]);

    internal sealed class Collector
    {
        private const string AccessorMethod = "CreateAccessor";

        private HashSet<uint> Accessors { get; } = [];

        private HashSet<uint> Others { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out _, out var methodName);

            if (methodName is AccessorMethod)
            {
                Accessors.Add(rva);
            }
            else
            {
                Others.Add(rva);
            }
        }

        public AccessorFunctions Build() => new([.. Accessors.Where(r => !Others.Contains(r)).Select(r => (ulong)r)]);
    }
}
