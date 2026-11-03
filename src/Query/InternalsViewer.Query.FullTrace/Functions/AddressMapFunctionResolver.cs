using InternalsViewer.Query.FullTrace.Memory.Address;
using System.Text.RegularExpressions;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.Helpers;

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
internal static partial class AddressMapFunctionResolver
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

    internal static int[] GetPointers(ArgumentLayout layout)
        => [.. layout.Slots.Where(s => s.Location is ArgumentLocation.Register or ArgumentLocation.Stack
                                       && s.Index < ArgumentLayout.CapturedSlots
                                       && s.Name != ArgumentLayout.ReturnBuffer
                                       && ArgumentValue.IsPointer(s.Type)
                                       && ArgumentValue.GetPointeeClass(s.Type) is null)
                           .Select(s => s.Index)];

    [GeneratedRegex("[PQA]E[A-D]")]
    private static partial Regex IsPointerRegex();

    /// <summary>
    /// Collects memory map functions during symbol enumeration
    /// </summary>
    internal sealed class Collector
    {
        private Dictionary<uint, AddressMapFunction> Found { get; } = [];

        private Dictionary<uint, string> Classes { get; } = [];

        private HashSet<uint> Constructors { get; } = [];

        public void Visit(FunctionSymbol symbol)
        {
            if (!symbol.IsFirst)
            {
                Revisit(symbol);

                return;
            }

            DeclaredClass[] declared = [];

            int[] pointers = [];

            if (IsPointerRegex().IsMatch(symbol.DecoratedName) && symbol.Parsed is { } signature && symbol.Layout is { } layout)
            {
                declared = GetDeclared(signature, layout);

                pointers = GetPointers(layout);
            }

            var operation = FunctionSignature.GetFunctionType(symbol.DecoratedName) == FunctionType.Member
                ? symbol.DecoratedName.StartsWith(ConstructorPrefix, StringComparison.Ordinal)
                    ? AddressMapOperation.Constructed
                    : AddressMapOperation.Visited
                : AddressMapOperation.Declared;

            if (operation != AddressMapOperation.Declared || declared.Length > 0 || pointers.Length > 0)
            {
                Found[symbol.Rva] = new AddressMapFunction(symbol.Rva, operation) { Declared = declared, Pointers = pointers };
            }

            if (operation != AddressMapOperation.Declared)
            {
                Classes[symbol.Rva] = GetClassName(symbol.Name);
            }

            if (operation == AddressMapOperation.Constructed)
            {
                Constructors.Add(symbol.Rva);
            }
        }

        public AddressMapFunction[] Build() => [.. Found.Values];

        private static string GetClassName(ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out _);

            return className.ToString();
        }

        private static bool IsMemberOf(ReadOnlySpan<char> name, string className)
        {
            SymbolsHelper.Split(name, out var memberClass, out _);

            return memberClass.Length > 0 && memberClass.SequenceEqual(className);
        }

        private void Revisit(FunctionSymbol symbol)
        {
            if (!Found.TryGetValue(symbol.Rva, out var function))
            {
                return;
            }

            if (!symbol.HasSameName && function.Operation == AddressMapOperation.Constructed)
            {
                function = function with { Operation = AddressMapOperation.Visited };
            }

            if (!symbol.HasSameName
                && function.Operation == AddressMapOperation.Visited
                && !Constructors.Contains(symbol.Rva)
                && Classes.GetValueOrDefault(symbol.Rva) is { } className
                && !IsMemberOf(symbol.Name, className))
            {
                function = function with
                {
                    Operation = AddressMapOperation.Declared,
                    Declared = [.. function.Declared.Where(d => d.ArgumentSlot != ArgumentLayout.ReturnRegister)]
                };
            }

            if (!symbol.HasSameParameters)
            {
                function = function with { Declared = [], Pointers = [] };
            }

            if (function is { Operation: AddressMapOperation.Declared, Declared.Length: 0, Pointers.Length: 0 })
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
