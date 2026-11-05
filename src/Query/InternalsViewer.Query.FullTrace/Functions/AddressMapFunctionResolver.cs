using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.Query.Events.Parsing;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

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
internal static class AddressMapFunctionResolver
{
    internal static DeclaredClass[] GetDeclared(ArgumentLayout layout, StringInternPool? strings = null)
    {
        List<DeclaredClass>? declared = null;

        var slots = layout.Slots;

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];

            if (slot.Location == ArgumentLocation.Register
                && slot.Name != ArgumentLayout.ReturnBuffer
                && ArgumentValue.GetPointeeClass(slot.Type, strings) is { } className)
            {
                (declared ??= []).Add(new DeclaredClass(slot.Index, className));
            }
        }

        if (layout.ReturnType is { } returnType && ArgumentValue.GetPointeeClass(returnType, strings) is { } returned)
        {
            (declared ??= []).Add(new DeclaredClass(ArgumentLayout.ReturnRegister, returned));
        }

        return declared is null ? [] : [.. declared];
    }

    internal static int[] GetPointers(ArgumentLayout layout)
    {
        List<int>? pointers = null;

        var slots = layout.Slots;

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];

            if (slot.Location is ArgumentLocation.Register or ArgumentLocation.Stack
                && slot.Index < ArgumentLayout.CapturedSlots
                && slot.Name != ArgumentLayout.ReturnBuffer
                && ArgumentValue.IsPointer(slot.Type)
                && !ArgumentValue.IsClassPointer(slot.Type))
            {
                (pointers ??= []).Add(slot.Index);
            }
        }

        return pointers is null ? [] : [.. pointers];
    }

    /// <summary>
    /// Collects memory map functions during symbol enumeration
    /// </summary>
    internal sealed class Collector
    {
        private Dictionary<uint, AddressMapFunction> Found { get; } = [];

        private StringInternPool Classes { get; } = new();

        public void Visit(SymbolTable.Reader symbol, ArgumentLayout? layout)
        {
            if (!symbol.IsFirst)
            {
                Revisit(symbol);

                return;
            }

            DeclaredClass[] declared = [];

            int[] pointers = [];

            if (layout is not null)
            {
                declared = GetDeclared(layout, Classes);

                pointers = GetPointers(layout);
            }

            var operation = symbol.FunctionType == FunctionType.Member
                             ? symbol.IsConstructor
                                 ? AddressMapOperation.Constructed
                                 : AddressMapOperation.Visited
                             : AddressMapOperation.Declared;

            if (operation != AddressMapOperation.Declared || declared.Length > 0 || pointers.Length > 0)
            {
                Found[symbol.Rva] = new AddressMapFunction(symbol.Rva, operation) { DeclaredClasses = declared, Pointers = pointers };
            }
        }

        public AddressMapFunction[] Build() => [.. Found.Values];

        private void Revisit(SymbolTable.Reader symbol)
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
                && !symbol.First.IsConstructor
                && !symbol.HasSameClass)
            {
                function = function with
                {
                    Operation = AddressMapOperation.Declared,
                    DeclaredClasses = [.. function.DeclaredClasses.Where(d => d.ArgumentSlot != ArgumentLayout.ReturnRegister)]
                };
            }

            if (!symbol.HasSameParameters)
            {
                function = function with { DeclaredClasses = [], Pointers = [] };
            }

            if (function is { Operation: AddressMapOperation.Declared, DeclaredClasses.Length: 0, Pointers.Length: 0 })
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
