using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.Helpers;

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

        return new(AddressMap.Merge(all.Select(f => f.Functions)), AddressMap.Merge(all.Select(f => f.Classes)));
    }

    public OutputFunctions Rebase(ulong address)
        => new(AddressMap.Rebase(Functions, address), AddressMap.Rebase(Classes, address));

    private bool Has(OutputFunctionType type) => Functions.Values.Contains(type);

    internal sealed class Collector
    {
        private const string VTableName = "`vftable'";

        private static readonly Dictionary<string, OutputFunctionType> FunctionTypes = new(StringComparer.Ordinal)
        {
            ["CEsExec::GeneralEval"] = OutputFunctionType.Evaluation,
            ["CEsExec::GeneralEval4"] = OutputFunctionType.Evaluation,
            ["GetData"] = OutputFunctionType.ValueRead,
            ["GetDataWithBuffer"] = OutputFunctionType.ValueRead,
            ["GetDataFromXvar"] = OutputFunctionType.VariantRead,
            ["GetDataFromXvar8"] = OutputFunctionType.VariantRead,
            ["CValOdsRow::SetDataX"] = OutputFunctionType.Send,
            ["CQScanRowsetNew::CreateValSSTable"] = OutputFunctionType.Creator,
            ["CQScanNew::GetRowOrReQualifyHelper"] = OutputFunctionType.Iterator
        };

        private static readonly Dictionary<string, OutputValueClass> ValueClasses = new(StringComparer.Ordinal)
        {
            ["CValOdsRow"] = OutputValueClass.OutputRow,
            ["CValConstXVar"] = OutputValueClass.Constant,
            ["CValSSTable"] = OutputValueClass.Table,
            ["CValHash"] = OutputValueClass.Hash,
            ["CValHashSwitch"] = OutputValueClass.HashSwitch,
            ["CValHashCachedSwitch"] = OutputValueClass.HashCachedSwitch,
            ["CValBatchRedirect"] = OutputValueClass.BatchRedirect
        };

        private static Dictionary<string, OutputFunctionType>.AlternateLookup<ReadOnlySpan<char>> FunctionLookup { get; }
            = FunctionTypes.GetAlternateLookup<ReadOnlySpan<char>>();

        private static Dictionary<string, OutputValueClass>.AlternateLookup<ReadOnlySpan<char>> ClassLookup { get; }
            = ValueClasses.GetAlternateLookup<ReadOnlySpan<char>>();

        private Dictionary<ulong, OutputFunctionType> Functions { get; } = [];

        private Dictionary<ulong, OutputValueClass> Classes { get; } = [];

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

            if (memberName.StartsWith(VTableName, StringComparison.Ordinal) && ClassLookup.TryGetValue(className, out var valueClass))
            {
                Classes.TryAdd(rva, valueClass);
            }
        }

        public OutputFunctions Build() => new(Functions, Classes);
    }
}
