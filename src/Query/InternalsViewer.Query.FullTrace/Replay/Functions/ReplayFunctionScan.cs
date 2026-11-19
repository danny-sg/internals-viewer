using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.Events.Parsing;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Replay.Functions;

internal static class ReplayFunctionScan
{
    private const string IteratorModule = "sqlmin";

    public static ReplayFunctionTable Scan(SymbolTable table, string moduleName)
    {
        var vtables = new VtableResolver.Collector();

        var functions = ClassifyFunctions(table, moduleName, vtables);

        var iterates = string.Equals(moduleName, IteratorModule, StringComparison.OrdinalIgnoreCase);

        var instanceMethods = new List<InstanceMethod>();

        var reads = new List<LocatorRead>();

        var stackArguments = new Dictionary<ulong, int>();

        var addressMapFunctions = new List<AddressMapFunction>(table.FunctionCount);

        var classes = new StringInternPool();

        var layouts = new Dictionary<FunctionSignature, ArgumentLayout>(SignatureComparer.Instance);

        var symbol = table.Read();

        while (symbol.MoveNext())
        {
            if (!symbol.IsFirst || !functions.TryGetValue(symbol.Rva, out var classification))
            {
                continue;
            }

            var signature = symbol.Parsed;

            if (iterates && classification.IsInstanceMethod && InstanceMethodLookup.GetInstanceRegister(signature) is { } register)
            {
                instanceMethods.Add(new InstanceMethod(symbol.Rva, register));
            }

            if (classification.IsExcluded)
            {
                continue;
            }

            var layout = signature is null ? null : GetLayout(layouts, signature);

            if (signature is not null && layout is not null && !symbol.HasFoldedParameters)
            {
                LocatorReadLookup.AddReads(reads, symbol.Rva, signature, layout);

                if (LocatorReadLookup.GetStackArguments(symbol.ParameterCount, layout) is > 0 and var count)
                {
                    stackArguments[symbol.Rva] = count;
                }

                if (classification.AllocationFunction is { IsAllocation: true } allocation
                    && AllocationSiteLookup.GetSiteSlots(layout) is { } site)
                {
                    functions[symbol.Rva] = classification with
                    {
                        AllocationFunction = allocation with { FileSlot = site.File, LineSlot = site.Line }
                    };
                }
            }

            if (AddressMapFunctionLookup.GetFunction(symbol, layout, classes) is { } function)
            {
                addressMapFunctions.Add(function);
            }
        }

        var vtableClasses = vtables.Build();

        return new ReplayFunctionTable([.. functions.Where(f => f.Value.IsExcluded).Select(f => (ulong)f.Key)],
                                       [.. instanceMethods],
                                       [.. reads],
                                       [.. functions.Values.Select(f => f.AllocationFunction).OfType<AllocationFunction>()],
                                       [.. addressMapFunctions])
        {
            PlanNodeFunctions = GetFunctionTable(functions, f => f.PlanNodeFunction),
            PlanObjects = PlanNodeFunctionLookup.GetPlanObjects(vtableClasses),
            BufferPoolFunctions = GetFunctionTable(functions, f => f.BufferPoolFunction),
            Vtables = vtableClasses,
            StackArguments = new AddressTable<int>(stackArguments)
        };
    }

    private static Dictionary<uint, FunctionClassification> ClassifyFunctions(SymbolTable table,
                                                                             string moduleName,
                                                                             VtableResolver.Collector vtables)
    {
        var mappings = CategoryRuleTable.Default;

        var functions = new Dictionary<uint, FunctionClassification>(table.FunctionCount);

        var symbol = table.Read();

        while (symbol.MoveNext())
        {
            var rva = symbol.Rva;

            var name = symbol.Name;

            if (!symbol.IsFunction)
            {
                vtables.VisitData(rva, name);

                continue;
            }

            FunctionClassification.Add(functions, rva, ClassifyFunction(mappings, moduleName, rva, name));
        }

        return functions;
    }

    private static FunctionClassification ClassifyFunction(CategoryRuleTable mappings, string module, uint rva, ReadOnlySpan<char> symbol)
        => new(IsExcluded(mappings, module, symbol),
               InstanceMethodLookup.IsInstanceMethod(symbol),
               IteratorSymbolLookup.IsInstanceClass(symbol),
               AllocationFunction.Classify(rva, symbol))
        {
            PlanNodeFunction = PlanNodeFunctionLookup.GetFunctionType(symbol),
            BufferPoolFunction = BufferPoolFunctionLookup.GetFunctionType(symbol)
        };

    private static AddressTable<T> GetFunctionTable<T>(Dictionary<uint, FunctionClassification> functions,
                                                       Func<FunctionClassification, T?> select)
        where T : struct
    {
        var table = new Dictionary<ulong, T>();

        foreach (var (rva, function) in functions)
        {
            if (select(function) is { } value)
            {
                table[rva] = value;
            }
        }

        return new(table);
    }

    private static bool IsExcluded(CategoryRuleTable mappings, string module, ReadOnlySpan<char> symbol)
    {
        SymbolHelper.Split(symbol, out var className, out var methodName);

        return mappings.Classify(module, className, methodName)
                       .IsInstrumentation(mappings.ClassifyOperator(module, className, methodName).OperatorName is not null);
    }

    private static ArgumentLayout GetLayout(Dictionary<FunctionSignature, ArgumentLayout> layouts, FunctionSignature signature)
    {
        ref var layout = ref CollectionsMarshal.GetValueRefOrAddDefault(layouts, signature, out var exists);

        if (!exists)
        {
            layout = ArgumentLayout.GetLayout(signature);
        }

        return layout!;
    }

    private sealed class SignatureComparer : IEqualityComparer<FunctionSignature>
    {
        public static SignatureComparer Instance { get; } = new();

        public bool Equals(FunctionSignature? left, FunctionSignature? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null
                || right is null
                || left.Type != right.Type
                || left.HasReturnBuffer != right.HasReturnBuffer
                || left.ReturnType != right.ReturnType
                || left.Parameters.Count != right.Parameters.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Parameters.Count; index++)
            {
                if (left.Parameters[index] != right.Parameters[index])
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(FunctionSignature signature)
        {
            var hash = new HashCode();

            hash.Add(signature.Type);

            hash.Add(signature.HasReturnBuffer);

            hash.Add(signature.ReturnType);

            for (var index = 0; index < signature.Parameters.Count; index++)
            {
                hash.Add(signature.Parameters[index]);
            }

            return hash.ToHashCode();
        }
    }
}
