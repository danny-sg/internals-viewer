using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantTables.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.VariantTables;

internal sealed record VariantTableFunctions(IReadOnlyDictionary<ulong, VariantTableFunctionType> Functions,
                                             IReadOnlyDictionary<ulong, VariantTableClass> Classes)
{
    public static VariantTableFunctions Empty { get; } = new(new Dictionary<ulong, VariantTableFunctionType>(),
                                                             new Dictionary<ulong, VariantTableClass>());

    public static VariantTableFunctions Merge(IEnumerable<VariantTableFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMap.Merge(all.Select(f => f.Functions)), AddressMap.Merge(all.Select(f => f.Classes)));
    }

    public VariantTableFunctions Rebase(ulong address) => new(AddressMap.Rebase(Functions, address), AddressMap.Rebase(Classes, address));

    internal sealed class Collector
    {
        private const string ColumnClass = "CValXVarTable";

        private const string RowClass = "CValXVarTableRow";

        private const string VTableName = "`vftable'";

        private static readonly Dictionary<string, VariantTableFunctionType> FunctionTypes = new(StringComparer.Ordinal)
        {
            ["CQScanNew::GetRowOrReQualifyHelper"] = VariantTableFunctionType.Iterator,
            ["CValXVarTable::CValXVarTable"] = VariantTableFunctionType.ColumnCreator,
            ["CValXVarTableRow::CValXVarTableRow"] = VariantTableFunctionType.RowCreator,
            ["CValXVarTable::SetDataX"] = VariantTableFunctionType.Writer,
            ["CValXVarTableRow::SetDataX"] = VariantTableFunctionType.Writer
        };

        private static Dictionary<string, VariantTableFunctionType>.AlternateLookup<ReadOnlySpan<char>> FunctionLookup { get; }
            = FunctionTypes.GetAlternateLookup<ReadOnlySpan<char>>();

        private Dictionary<ulong, VariantTableFunctionType> Functions { get; } = [];

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

        public VariantTableFunctions Build()
            => Functions.ContainsValue(VariantTableFunctionType.Iterator) && Functions.ContainsValue(VariantTableFunctionType.Writer)
                ? new(Functions, Classes)
                : Empty;
    }
}
