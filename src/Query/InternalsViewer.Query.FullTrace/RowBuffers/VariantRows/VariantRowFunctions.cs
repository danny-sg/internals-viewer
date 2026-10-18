using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.VariantRows;

internal sealed record VariantRowFunctions(IReadOnlyDictionary<ulong, VariantRowFunctionType> Functions,
                                           IReadOnlyDictionary<ulong, VariantTableClass> Classes)
{
    public static VariantRowFunctions Empty { get; } = new(new Dictionary<ulong, VariantRowFunctionType>(),
                                                           new Dictionary<ulong, VariantTableClass>());

    public static VariantRowFunctions Merge(IEnumerable<VariantRowFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMap.Merge(all.Select(f => f.Functions)), AddressMap.Merge(all.Select(f => f.Classes)));
    }

    public VariantRowFunctions Rebase(ulong address) => new(AddressMap.Rebase(Functions, address), AddressMap.Rebase(Classes, address));

    internal sealed class Collector
    {
        private const string ColumnClass = "CValXVarTable";

        private const string RowClass = "CValXVarTableRow";

        private const string VTableName = "`vftable'";

        private static readonly Dictionary<string, VariantRowFunctionType> FunctionTypes = new(StringComparer.Ordinal)
        {
            ["CQScanNew::GetRowOrReQualifyHelper"] = VariantRowFunctionType.Iterator,
            ["CQScanBatchHelper::GetRow"] = VariantRowFunctionType.BatchHelper,
            ["CValXVarTable::CValXVarTable"] = VariantRowFunctionType.ColumnCreator,
            ["CValXVarTableRow::CValXVarTableRow"] = VariantRowFunctionType.RowCreator
        };

        private static Dictionary<string, VariantRowFunctionType>.AlternateLookup<ReadOnlySpan<char>> FunctionLookup { get; }
            = FunctionTypes.GetAlternateLookup<ReadOnlySpan<char>>();

        private Dictionary<ulong, VariantRowFunctionType> Functions { get; } = [];

        private Dictionary<ulong, VariantTableClass> Classes { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            if (FunctionLookup.TryGetValue(name, out var type))
            {
                Functions.TryAdd(rva, type);
            }
        }

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var memberName);

            if (!memberName.StartsWith(VTableName, StringComparison.Ordinal))
            {
                return;
            }

            if (className is ColumnClass)
            {
                Classes.TryAdd(rva, VariantTableClass.Column);
            }
            else if (className is RowClass)
            {
                Classes.TryAdd(rva, VariantTableClass.Row);
            }
        }

        public VariantRowFunctions Build() => Functions.Count == 0 ? Empty : new(Functions, Classes);
    }
}
