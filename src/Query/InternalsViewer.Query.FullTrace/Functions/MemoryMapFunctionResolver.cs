using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Resolves functions that contribute to the memory map by declaring, constructing or visiting class instances
/// </summary>
/// <remarks>
/// A function contributes to the memory map in one of three ways, derived from its decorated name:
///
/// - Constructed
///     Member function whose decorated name starts with ??0 (a constructor). The address in RCX (this) is a new instance of the class.
/// 
/// - Visited
///     Any other member function. The address in RCX is an existing instance of the class.
///
/// - Declared
///     A non-member function that takes or returns class pointers. Each pointer argument/return associates an address with a class.
///     Member functions can also carry declared classes for their class pointer arguments in addition to their operation.
/// 
/// GetDeclared maps register passed class pointer arguments (and a class pointer return value in RAX) to their argument slot index and
/// class name, skipping the hidden return buffer slot.
/// </remarks>
internal static partial class MemoryMapFunctionResolver
{
    private const string ConstructorPrefix = "??0";

    internal static DeclaredClass[] GetDeclared(FunctionSignature signature) 
        => GetDeclared(signature, ArgumentLayout.GetLayout(signature));

    internal static DeclaredClass[] GetDeclared(FunctionSignature signature, ArgumentLayout layout)
    {
        var declared = new List<DeclaredClass>();

        foreach (var slot in layout.Slots)
        {
            if (slot.Location == ArgumentLocation.Register
                && slot.Name != ArgumentLayout.ReturnBuffer
                && ArgumentValue.GetPointeeClass(slot.Type) is { } className)
            {
                declared.Add(new DeclaredClass(slot.Index, className));
            }
        }

        if (signature.ReturnType is { } returnType && ArgumentValue.GetPointeeClass(returnType) is { } returned)
        {
            declared.Add(new DeclaredClass(ArgumentLayout.ReturnRegister, returned));
        }

        return [.. declared];
    }

    [GeneratedRegex("[PQA]E[A-D][UVT]")]
    private static partial Regex ClassPointer();

    /// <summary>
    /// Collects memory map functions during symbol enumeration
    /// </summary>
    internal sealed class Collector
    {
        private Dictionary<uint, MemoryMapFunction> Found { get; } = [];

        public void Visit(FunctionSymbol symbol)
        {
            if (!symbol.IsFirst)
            {
                Revisit(symbol);

                return;
            }

            var declared = ClassPointer().IsMatch(symbol.DecoratedName) && symbol.Parsed is { } signature && symbol.Layout is { } layout
                ? GetDeclared(signature, layout)
                : [];

            var operation = FunctionSignature.GetFunctionType(symbol.DecoratedName) == FunctionType.Member
                ? symbol.DecoratedName.StartsWith(ConstructorPrefix, StringComparison.Ordinal)
                    ? MemoryMapOperation.Constructed
                    : MemoryMapOperation.Visited
                : MemoryMapOperation.Declared;

            if (operation != MemoryMapOperation.Declared || declared.Length > 0)
            {
                Found[symbol.Rva] = new MemoryMapFunction(symbol.Rva, operation) { Declared = declared };
            }
        }

        public MemoryMapFunction[] Build() => [.. Found.Values];

        private void Revisit(FunctionSymbol symbol)
        {
            if (!Found.TryGetValue(symbol.Rva, out var function))
            {
                return;
            }

            if (!symbol.HasSameName && function.Operation == MemoryMapOperation.Constructed)
            {
                function = function with { Operation = MemoryMapOperation.Visited };
            }

            if (!symbol.HasSameParameters)
            {
                function = function with { Declared = [] };
            }

            if (function.Operation == MemoryMapOperation.Declared && function.Declared.Length == 0)
            {
                Found.Remove(symbol.Rva);
            }
            else
            {
                Found[symbol.Rva] = function;
            }
        }
    }
}
