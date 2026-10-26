using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output;

internal sealed record OutputFunctions(IReadOnlyDictionary<ulong, OutputFunctionType> Functions,
                                       IReadOnlyDictionary<ulong, OutputValueClass> Classes)
{
    public static OutputFunctions Empty { get; } = new(new Dictionary<ulong, OutputFunctionType>(),
                                                       new Dictionary<ulong, OutputValueClass>());

    public bool IsComplete => Classes.Values.Contains(OutputValueClass.OutputRow)
                              && (Has(OutputFunctionType.Send)
                                  || (Has(OutputFunctionType.Evaluation)
                                      && (Has(OutputFunctionType.ValueRead) || Has(OutputFunctionType.VariantRead))));

    public static OutputFunctions Merge(IEnumerable<OutputFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMapHelper.Merge(all.Select(f => f.Functions)), AddressMapHelper.Merge(all.Select(f => f.Classes)));
    }

    public OutputFunctions Rebase(ulong address)
        => new(AddressMapHelper.Rebase(Functions, address), AddressMapHelper.Rebase(Classes, address));

    private bool Has(OutputFunctionType type) => Functions.Values.Contains(type);

    internal sealed class Collector
    {
        private static readonly Dictionary<string, OutputFunctionType> FunctionTypes = new(StringComparer.Ordinal)
        {
            ["CEsExec::GeneralEval"] = OutputFunctionType.Evaluation,
            ["CEsExec::GeneralEval4"] = OutputFunctionType.Evaluation,
            ["GetData"] = OutputFunctionType.ValueRead,
            ["GetDataWithBuffer"] = OutputFunctionType.ValueRead,
            ["GetDataFromXvar"] = OutputFunctionType.VariantRead,
            ["GetDataFromXvar8"] = OutputFunctionType.VariantRead,
            ["CValOdsRow::SetDataX"] = OutputFunctionType.Send
        };

        private static readonly Dictionary<string, OutputValueClass> ValueClasses = new(StringComparer.Ordinal)
        {
            ["CValOdsRow"] = OutputValueClass.OutputRow,
            ["CValConstXVar"] = OutputValueClass.RowBuffer,
            ["CValSSTable"] = OutputValueClass.Table,
            ["CValHash"] = OutputValueClass.Hash,
            ["CValHashSwitch"] = OutputValueClass.HashSwitch,
            ["CValHashCachedSwitch"] = OutputValueClass.HashCachedSwitch,
            ["CValBatchRedirect"] = OutputValueClass.BatchRedirect,
            ["CValXVarTable"] = OutputValueClass.VariantTable
        };

        private static Dictionary<string, OutputFunctionType>.AlternateLookup<ReadOnlySpan<char>> FunctionLookup { get; }
            = FunctionTypes.GetAlternateLookup<ReadOnlySpan<char>>();

        private Dictionary<ulong, OutputFunctionType> Functions { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            if (FunctionLookup.TryGetValue(name, out var type))
            {
                Functions.TryAdd(rva, type);
            }
        }

        public OutputFunctions Build(IReadOnlyDictionary<ulong, string[]> vtables)
            => new(Functions, VtableResolver.GetFirstMatches(vtables, GetValueClass));

        private static OutputValueClass? GetValueClass(string className)
            => ValueClasses.TryGetValue(className, out var valueClass) ? valueClass : null;
    }
}
