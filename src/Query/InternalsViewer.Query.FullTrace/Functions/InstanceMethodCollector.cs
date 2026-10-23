using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class InstanceMethodCollector
{
    public static int? InstanceRegister(string? signature, string? decoratedName)
    {
        if (signature is null || decoratedName is null)
        {
            return 0;
        }

        return FunctionSignature.Parse(signature, decoratedName) switch
        {
            { Type: FunctionType.Member } => 0,
            { Type: FunctionType.Static, Parameters: var parameters } => KeyedParameter(parameters),
            _ => null
        };
    }

    public static bool IsPlanObjectMethod(ReadOnlySpan<char> symbol)
    {
        SymbolsHelper.Split(symbol, out var className, out _);

        return className.Length > 0 && IteratorSymbols.IsPlanObject(className);
    }

    public static bool IsInstanceMethod(ReadOnlySpan<char> symbol)
    {
        SymbolsHelper.Split(symbol, out var className, out var methodName);

        if (className.Length == 0)
        {
            return false;
        }

        return IteratorSymbols.IsPlanObject(className)
               || ((IteratorSymbols.IsIterator(className) || IteratorSymbols.IsQueryScan(className))
                   && IteratorSymbols.IsIteratorMethod(methodName));
    }

    public static bool IsInstanceClass(ReadOnlySpan<char> symbol) => IteratorSymbols.IsInstanceClass(symbol);

    public static List<InstanceMethod> FindInstanceMethods(IEnumerable<uint> addresses, DiaResolver resolver)
    {
        var methods = new List<InstanceMethod>();

        foreach (var rva in addresses)
        {
            if (InstanceRegister(resolver.GetSignature(rva), resolver.GetDecoratedName(rva)) is { } register)
            {
                methods.Add(new InstanceMethod(rva, register));
            }
        }

        return methods;
    }

    private static int? KeyedParameter(IReadOnlyList<string> parameters)
    {
        for (var index = 0; index < Math.Min(parameters.Count, ArgumentLayout.RegisterCount); index++)
        {
            var parameter = parameters[index].TrimEnd();

            if (IteratorSymbols.IsInstanceClass(parameter) && parameter.EndsWith('*'))
            {
                return index;
            }
        }

        return null;
    }
}
