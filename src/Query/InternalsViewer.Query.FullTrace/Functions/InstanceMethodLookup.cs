using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Identifies functions that operate on query plan instances and which argument slot carries the instance pointer
/// </summary>
/// <remarks>
/// An instance method is a function whose calls can be tied to a specific plan object instance - plan object methods, and iterator/query
/// scan methods that are part of the iterator interface.
///
/// The instance pointer is located per function type:
///
///     - Member functions - the instance is 'this' in slot 0 (RCX)
///     - Static functions - the instance is the first register passed parameter that is a pointer to an instance class
///     - Unknown signatures - slot 0 is assumed
///
/// Free functions, and static functions with no instance class pointer in a register, are not instance methods.
/// </remarks>
internal static class InstanceMethodLookup
{
    public static int? InstanceRegister(FunctionSignature? signature) => signature switch
    {
        { Type: FunctionType.Member } => 0,
        { Type: FunctionType.Static, Parameters: var parameters } => KeyedParameter(parameters),
        _ => null
    };

    public static bool IsInstanceMethod(ReadOnlySpan<char> symbol)
    {
        SymbolsHelper.Split(symbol, out var className, out var methodName);

        if (className.Length == 0)
        {
            return false;
        }

        return IteratorSymbolLookup.IsPlanObject(className)
               || ((IteratorSymbolLookup.IsIterator(className) || IteratorSymbolLookup.IsQueryScan(className))
                   && IteratorSymbolLookup.IsIteratorMethod(methodName));
    }

    private static int? KeyedParameter(IReadOnlyList<string> parameters)
    {
        for (var index = 0; index < Math.Min(parameters.Count, ArgumentLayout.RegisterCount); index++)
        {
            var parameter = parameters[index].TrimEnd();

            if (IteratorSymbolLookup.IsInstanceClass(parameter) && parameter.EndsWith('*'))
            {
                return index;
            }
        }

        return null;
    }
}
